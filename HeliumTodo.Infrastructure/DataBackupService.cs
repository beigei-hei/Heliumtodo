// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using HeliumTodo.Domain;
using HeliumTodo.Domain.Backup;
using HeliumTodo.Application.Configuration;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 备份 / 恢复（统一用 .zip）。
///
/// 为什么要有这么一个服务，而不是在界面里直接拷文件？
///   ① 一次备份要包含三样东西：数据库、配置、壁纸。少一样恢复到新电脑上就不完整。
///   ② 导入必须"先验后动"：确认是合法备份、校验和都对，再动用户的真实数据。
///   ③ 导入前要自动存一份当前数据 —— 万一导错了还能退回去。
///   这三件事都不该散落在界面代码里，所以集中在这里。
///
/// 导入为什么用 ATTACH 而不是"关程序、换文件"？
///   换文件要求那一刻没有任何连接打开着数据库，Windows 上很容易失败
///   （某个后台计时器还开着连接，用户就会看到"文件被占用"）。
///   ATTACH 之后用 SQL 把数据搬进来，连接一直是程序自己的，
///   不需要关文件、也不会被杀毒软件拦住。
/// </summary>
public sealed class DataBackupService
{
    private readonly IDbContextFactory<HeliumDbContext> _factory;
    private readonly ConfigService _config;

    public DataBackupService(IDbContextFactory<HeliumDbContext> factory, ConfigService config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>当前数据库文件路径。</summary>
    public string DatabasePath => _config.ResolveDatabasePath();

    /// <summary>备份目录（数据库旁边的 backups 文件夹）。</summary>
    public string BackupDirectory
    {
        get
        {
            var directory = Path.GetDirectoryName(DatabasePath);
            return Path.Combine(string.IsNullOrEmpty(directory) ? _config.ConfigDirectory : directory,
                BackupFormat.BackupDirectoryName);
        }
    }

    // ════════════════════════════ 导出 ════════════════════════════

    /// <summary>把当前数据导出成一个 .zip（同步版，启动时的自动备份用它）。</summary>
    /// <param name="targetPath">目标 zip 路径。</param>
    public BackupExportResult Export(string targetPath)
    {
        using var db = _factory.CreateDbContext();

        var counts = new BackupCounts
        {
            Schedules = db.Schedules.Count(),
            Todos = db.Todos.Count(),
            FocusSessions = db.FocusSessions.Count(),
            StudyRooms = db.StudyRooms.Count(),
            StudyRoomMembers = db.StudyRoomMembers.Count(),
            StudyRoomMessages = db.StudyRoomMessages.Count(),
        };

        var databaseBytes = TakeDatabaseSnapshot(db);
        var configBytes = _config.ExportJsonBytes();

        var wallpaper = ReadWallpaper();

        var content = new BackupPackageContent
        {
            Database = databaseBytes,
            Config = configBytes,
            Wallpaper = wallpaper.Bytes,
            WallpaperFileName = wallpaper.FileName,
            WallpaperSkippedReason = wallpaper.SkippedReason,
            Counts = counts,
            AppVersion = VersionInfo.Current,
            ExportedAt = DateTime.Now,
        };

        try
        {
            return new BackupExportResult(targetPath, BackupPackageWriter.Write(targetPath, content));
        }
        catch (BackupWriteDeniedException denied)
        {
            // 用户挑的地方写不进去（受控文件夹访问 / 只读盘 / 权限不够）。
            // 与其弹一个红叉让他自己想办法，不如把文件放到我们确定能写的地方，
            // 再把"放哪了"说清楚 —— 备份是保命的东西，不能因为路径问题就不导了。
            var fallbackPath = BuildFallbackPath(targetPath);

            System.Diagnostics.Debug.WriteLine(
                $"[DataBackupService] 目标位置不可写，改存到 {fallbackPath}；原因：{denied.Message}");

            var manifest = BackupPackageWriter.Write(fallbackPath, content);
            return new BackupExportResult(fallbackPath, manifest) { RequestedPath = targetPath };
        }
    }

    /// <summary>
    /// 原位置写不进去时的落脚点：应用自己的 backups 文件夹。
    /// 文件名沿用用户挑的那个，重名就加序号，免得覆盖掉同名的旧备份。
    /// </summary>
    private string BuildFallbackPath(string targetPath)
    {
        Directory.CreateDirectory(BackupDirectory);

        var name = Path.GetFileName(targetPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = BackupFormat.BuildFileName(DateTime.Now);
        }

        var candidate = Path.Combine(BackupDirectory, name);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);

        for (var index = 2; index < 1000; index++)
        {
            candidate = Path.Combine(BackupDirectory, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(BackupDirectory, BackupFormat.BuildFileName(DateTime.Now));
    }

    /// <summary>异步导出（界面上点"导出"用它）。</summary>
    public Task<BackupExportResult> ExportAsync(string targetPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Export(targetPath), cancellationToken);

    /// <summary>
    /// 启动时的自动备份：导出到 backups 文件夹，并只保留最近几份。
    /// 失败不抛异常（备份失败不能影响程序启动），返回 null。
    /// </summary>
    /// <param name="keepCount">最多保留几份。</param>
    /// <param name="isPreImport">是否是"导入前的安全备份"（文件名前缀不同）。</param>
    public string? BackupAutomatically(int keepCount = 7, bool isPreImport = false)
    {
        try
        {
            var now = DateTime.Now;
            var name = isPreImport
                ? BackupFormat.BuildPreImportFileName(now)
                : BackupFormat.BuildFileName(now);

            var target = Path.Combine(BackupDirectory, name);

            // 同一秒内重复触发（比如快速连点两次导入）就直接复用已有文件
            if (File.Exists(target))
            {
                return target;
            }

            Export(target);
            PruneOldBackups(keepCount);
            return target;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataBackupService] 自动备份失败：{ex.Message}");
            return null;
        }
    }

    // ════════════════════════════ 检查 ════════════════════════════

    /// <summary>
    /// 只看一眼备份里有什么，不动任何数据。界面在用户确认前先展示这个。
    /// </summary>
    public BackupInspection Inspect(string zipPath)
    {
        try
        {
            using var reader = BackupPackageReader.Open(zipPath);

            if (!reader.TryVerifyChecksums(out var checksumError))
            {
                return BackupInspection.Failed(zipPath, checksumError ?? "备份文件校验失败。");
            }

            var manifest = reader.Manifest;
            var warning = BuildWarning(manifest);

            return new BackupInspection
            {
                IsUsable = true,
                Warning = warning,
                FilePath = zipPath,
                FormatVersion = manifest.FormatVersion,
                AppVersion = manifest.AppVersion,
                ExportedAt = manifest.ExportedAt,
                Counts = manifest.Counts,
                DatabaseBytes = manifest.Database?.SizeBytes ?? 0,
                HasConfig = manifest.Config is not null,
                HasWallpaper = manifest.Wallpaper is not null,
                WallpaperFileName = manifest.WallpaperFileName,
            };
        }
        catch (BackupPackageException ex)
        {
            return BackupInspection.Failed(zipPath, ex.Message);
        }
        catch (Exception ex)
        {
            return BackupInspection.Failed(zipPath, $"读取备份时出错：{ex.Message}");
        }
    }

    /// <summary>异步版检查。</summary>
    public Task<BackupInspection> InspectAsync(string zipPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Inspect(zipPath), cancellationToken);

    // ════════════════════════════ 导入 ════════════════════════════

    /// <summary>
    /// 从 .zip 备份导入数据。
    ///
    /// 流程（每一步失败都不会把库改坏）：
    ///   ① 打开并校验备份（格式、版本、SHA256）
    ///   ② 替换模式下，先把当前数据自动备份一份
    ///   ③ 把备份里的数据库解压到临时文件并 ATTACH 进来
    ///   ④ 在一个事务里搬数据：替换 = 清空后照搬；合并 = 追加并重编号
    ///   ⑤ 应用配置（本机路径类设置不覆盖）
    ///   ⑥ 恢复壁纸
    /// </summary>
    public BackupImportResult Import(string zipPath, BackupImportMode mode)
    {
        using var reader = BackupPackageReader.Open(zipPath);

        if (!reader.TryVerifyChecksums(out var checksumError))
        {
            throw new BackupPackageException(checksumError ?? "备份文件校验失败。");
        }

        var manifest = reader.Manifest;

        // ② 替换模式是有破坏性的，先给当前数据留一份
        string? safetyBackup = mode == BackupImportMode.Replace
            ? BackupAutomatically(isPreImport: true)
            : null;

        // ③ 解压到临时文件：绝不让 SQLite 直接打开 zip 里的东西。
        //    放数据库旁边而不是 %TEMP% —— 那才是这台机器上确定可写的目录
        //    （有些环境里 .NET 进程建不了 %TEMP% 下的文件，解压会直接 Access denied）。
        var scratchDirectory = Path.GetDirectoryName(Path.GetFullPath(DatabasePath)) ?? ".";
        var tempDatabase = Path.Combine(scratchDirectory, $"helium-import-{Guid.NewGuid():N}.db");

        BackupCounts imported;
        try
        {
            reader.ExtractEntryTo(manifest.Database!.Entry, tempDatabase);
            imported = CopyFromDatabase(tempDatabase, mode);
        }
        catch (BackupPackageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BackupPackageException($"导入失败：{ex.Message}");
        }
        finally
        {
            TryDelete(tempDatabase);
        }

        // ⑤ 配置
        var configApplied = false;
        if (manifest.Config is not null)
        {
            var json = Encoding.UTF8.GetString(reader.ReadEntry(manifest.Config.Entry));
            _config.ApplyImportedConfig(json);
            configApplied = true;
        }

        // ⑥ 壁纸
        var wallpaperRestored = false;
        if (manifest.Wallpaper is not null)
        {
            wallpaperRestored = RestoreWallpaper(reader, manifest);
        }

        // 配置里可能带着"别人电脑上的壁纸路径"，本机不存在就清掉，免得界面上留一个空图
        if (!string.IsNullOrWhiteSpace(_config.Current.WallpaperPath)
            && !File.Exists(_config.Current.WallpaperPath))
        {
            _config.Current.WallpaperPath = null;
            _config.Save();
        }

        return new BackupImportResult
        {
            Success = true,
            Mode = mode,
            SourcePath = zipPath,
            Counts = imported,
            SafetyBackupPath = safetyBackup,
            ConfigApplied = configApplied,
            WallpaperRestored = wallpaperRestored,
        };
    }

    /// <summary>异步版导入。</summary>
    public Task<BackupImportResult> ImportAsync(
        string zipPath,
        BackupImportMode mode,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Import(zipPath, mode), cancellationToken);

    // ════════════════════════════ 内部：快照与壁纸 ════════════════════════════

    /// <summary>
    /// 取一份"干净的"数据库快照。
    ///
    /// 为什么不用 File.Copy？
    ///   开了 WAL 之后，最新的数据可能还在 helium.db-wal 里，
    ///   只拷主文件会丢掉最近的改动。`VACUUM INTO` 由 SQLite 自己写出一份整理好的完整副本，
    ///   天然一致，不需要先 checkpoint 再赌一把。
    /// </summary>
    private byte[] TakeDatabaseSnapshot(HeliumDbContext db)
    {
        // 快照先写到数据库所在目录 —— 那里一定是可写的（库本身就在那儿），
        // 而 %TEMP% 未必：有些机器上 .NET 进程建不了临时文件，VACUUM INTO 会直接失败。
        var databasePath = db.Database.GetDbConnection().DataSource;
        var snapshotDirectory = Path.GetDirectoryName(Path.GetFullPath(databasePath)) ?? ".";
        var snapshotPath = Path.Combine(snapshotDirectory, $"helium-snapshot-{Guid.NewGuid():N}.db");

        try
        {
            db.Database.ExecuteSqlRaw("VACUUM INTO {0}", snapshotPath);
            return ReadFileShared(snapshotPath);
        }
        catch (Exception ex)
        {
            // VACUUM INTO 要 SQLite 3.27+；万一失败就退回"先 checkpoint 再读文件"
            System.Diagnostics.Debug.WriteLine($"[DataBackupService] VACUUM INTO 失败，改用文件读取：{ex.Message}");

            try
            {
                db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE)");
            }
            catch
            {
                // checkpoint 也失败就按现状读，至少比导出失败强
            }

            return ReadFileShared(databasePath);
        }
        finally
        {
            TryDelete(snapshotPath);
        }
    }

    /// <summary>
    /// 读一个数据库文件，允许别人同时开着它。
    ///
    /// 为什么不用 File.ReadAllBytes：它按 FileShare.Read 打开，等于告诉系统
    /// "别人只能读"。可数据库连接自己拿着写权限，两边一撞就是
    /// IOException：文件正由另一进程使用。导入导出又恰恰总在"库还开着"的时候做，
    /// 所以必须显式声明 ReadWrite + Delete 共享。
    /// </summary>
    private static byte[] ReadFileShared(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record WallpaperPayload(byte[]? Bytes, string? FileName, string? SkippedReason);

    /// <summary>读当前壁纸准备打进包里；太大就跳过并记下原因。</summary>
    private WallpaperPayload ReadWallpaper()
    {
        var path = _config.Current.WallpaperPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new WallpaperPayload(null, null, null);
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > BackupFormat.MaxWallpaperBytes)
            {
                var limit = BackupFormat.MaxWallpaperBytes / (1024 * 1024);
                return new WallpaperPayload(null, info.Name, $"壁纸超过 {limit} MB，没有打进备份");
            }

            return new WallpaperPayload(File.ReadAllBytes(path), info.Name, null);
        }
        catch (Exception ex)
        {
            return new WallpaperPayload(null, Path.GetFileName(path), $"壁纸读取失败：{ex.Message}");
        }
    }

    /// <summary>把备份里的壁纸落到本机的 wallpapers 目录，并改配置指向它。</summary>
    private bool RestoreWallpaper(BackupPackageReader reader, BackupManifest manifest)
    {
        try
        {
            var entry = manifest.Wallpaper!;

            // 只用文件名，防止备份里塞 "..\..\windows\system32\..." 这种路径穿越
            var name = Path.GetFileName(
                string.IsNullOrWhiteSpace(manifest.WallpaperFileName)
                    ? "wallpaper" + Path.GetExtension(entry.Entry)
                    : manifest.WallpaperFileName);

            if (string.IsNullOrWhiteSpace(name))
            {
                name = "wallpaper" + Path.GetExtension(entry.Entry);
            }

            var directory = Path.Combine(_config.ConfigDirectory, BackupFormat.WallpaperDirectoryName);
            Directory.CreateDirectory(directory);

            var target = Path.Combine(directory, name);
            reader.ExtractEntryTo(entry.Entry, target);

            _config.Current.WallpaperPath = target;
            _config.Current.WallpaperEnabled = true;
            _config.Save();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataBackupService] 壁纸恢复失败：{ex.Message}");
            return false;
        }
    }

    // ════════════════════════════ 内部：把数据搬进来 ════════════════════════════

    /// <summary>一张表的搬运规则：外键列指向哪张父表的哪个列。</summary>
    /// <param name="UniqueColumn">
    /// 这张表上的唯一列（目前只有自习室的房间码）。
    /// 合并导入时碰到同一个值就不再重复搬 —— 同一个房间码就是同一个房间。
    /// </param>
    private sealed record TableSpec(
        string Name,
        string? ParentTable = null,
        string? ForeignKey = null,
        string? UniqueColumn = null);

    /// <summary>
    /// 搬运顺序很重要：父表在前、子表在后。
    /// 合并模式下要给子表的外键换成新编号，所以必须等父表搬完。
    /// </summary>
    private static readonly TableSpec[] Tables =
    {
        new("Schedules"),
        new("Todos"),
        new("FocusSessions", "Todos", "TodoId"),
        new("StudyRooms", UniqueColumn: "Code"),
        new("StudyRoomMembers", "StudyRooms", "StudyRoomId"),
        new("StudyRoomMessages", "StudyRooms", "StudyRoomId"),
    };

    /// <summary>
    /// 把备份库（临时文件）里的数据搬进当前库。
    /// </summary>
    /// <param name="sourceDatabasePath">解压出来的备份数据库。</param>
    /// <param name="mode">替换还是合并。</param>
    private BackupCounts CopyFromDatabase(string sourceDatabasePath, BackupImportMode mode)
    {
        using var db = _factory.CreateDbContext();

        // 让主库的 WAL 落盘，避免"搬完了但文件里还是旧的"
        try
        {
            db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE)");
        }
        catch
        {
            // 有别的连接在读时 checkpoint 会失败，不影响后面的 ATTACH + 事务
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        Attach(connection, sourceDatabasePath);

        try
        {
            EnsureSourceSchema(connection);

            using var transaction = connection.BeginTransaction();

            if (mode == BackupImportMode.Replace)
            {
                ClearTables(connection, transaction);
            }

            var counts = new BackupCounts();
            var idMaps = new Dictionary<string, Dictionary<long, long>>(StringComparer.Ordinal);

            // 合并时"库里已经有同一条"而被跳过的父记录。
            // 子表碰到这些父记录的子行也要跳过 —— 否则它们会挂到一个 null 外键上，
            // 变成一堆"不属于任何房间的成员"。
            var skippedIds = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);

            foreach (var spec in Tables)
            {
                var imported = CopyTable(connection, transaction, spec, mode, idMaps, skippedIds);
                AssignCount(counts, spec.Name, imported);
            }

            transaction.Commit();
            return counts;
        }
        finally
        {
            Detach(connection);
        }
    }

    private static void Attach(DbConnection connection, string sourcePath)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ATTACH DATABASE $path AS src";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$path";
            parameter.Value = sourcePath;
            command.Parameters.Add(parameter);
            command.ExecuteNonQuery();
        }
        catch (DbException ex)
        {
            throw new BackupPackageException(
                $"备份里的数据库打不开（可能不是有效的 SQLite 文件）：{ex.Message}");
        }
    }

    private static void Detach(DbConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DETACH DATABASE src";
            command.ExecuteNonQuery();
        }
        catch (DbException)
        {
            // 连接关掉时 SQLite 也会自动解开，这里失败不用管
        }
    }

    /// <summary>确认备份库里确实有我们认识的表，否则给一句人能看懂的话。</summary>
    private static void EnsureSourceSchema(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM src.sqlite_master WHERE type = 'table' AND name IN " +
            "('Schedules', 'Todos', 'FocusSessions', 'StudyRooms')";

        var count = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (count == 0)
        {
            throw new BackupPackageException("备份里的数据库结构不认识（找不到日程/待办这些表）。");
        }
    }

    /// <summary>清空现有数据。子表先删，否则外键会拦着。</summary>
    private static void ClearTables(DbConnection connection, DbTransaction transaction)
    {
        // 倒序：先删依赖别人的表
        for (var i = Tables.Length - 1; i >= 0; i--)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM main.\"{Tables[i].Name}\"";
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// 搬一张表。
    ///   替换模式：连 Id 一起照搬（外键天然对得上）。
    ///   合并模式：不带 Id，让 SQLite 发新号；子表的外键换成新号。
    /// </summary>
    private static int CopyTable(
        DbConnection connection,
        DbTransaction transaction,
        TableSpec spec,
        BackupImportMode mode,
        Dictionary<string, Dictionary<long, long>> idMaps,
        Dictionary<string, HashSet<long>> skippedIds)
    {
        var targetColumns = GetColumns(connection, transaction, "main", spec.Name);
        var sourceColumns = GetColumns(connection, transaction, "src", spec.Name);

        // 两边都有的列才搬 —— 这样旧版本备份少几个列也能导进来
        var common = targetColumns.Where(c => sourceColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (common.Count == 0)
        {
            return 0;
        }

        if (mode == BackupImportMode.Replace)
        {
            return CopyTableKeepingIds(connection, transaction, spec.Name, common);
        }

        return CopyTableRemappingIds(connection, transaction, spec, common, idMaps, skippedIds);
    }

    /// <summary>替换模式：一条 INSERT ... SELECT，Id 原样保留。</summary>
    private static int CopyTableKeepingIds(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        List<string> columns)
    {
        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO main.\"{table}\" ({columnList}) SELECT {columnList} FROM src.\"{table}\"";
        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// 合并模式：逐行插入并重新编号，同时记录 旧Id → 新Id 供子表改外键。
    /// </summary>
    private static int CopyTableRemappingIds(
        DbConnection connection,
        DbTransaction transaction,
        TableSpec spec,
        List<string> columns,
        Dictionary<string, Dictionary<long, long>> idMaps,
        Dictionary<string, HashSet<long>> skippedIds)
    {
        var rowMap = new Dictionary<long, long>();
        idMaps[spec.Name] = rowMap;

        var skipped = new HashSet<long>();
        skippedIds[spec.Name] = skipped;

        // 父表的映射（TodoId / StudyRoomId 要用）
        Dictionary<long, long>? parentMap = null;
        HashSet<long>? skippedParentIds = null;
        if (spec.ParentTable is not null)
        {
            idMaps.TryGetValue(spec.ParentTable, out parentMap);
            skippedIds.TryGetValue(spec.ParentTable, out skippedParentIds);
        }

        // 唯一列在哪一列，查重用
        var uniqueIndex = spec.UniqueColumn is null
            ? -1
            : columns.FindIndex(c => string.Equals(c, spec.UniqueColumn, StringComparison.OrdinalIgnoreCase));

        var rows = ReadRows(connection, transaction, spec.Name, columns);
        var inserted = 0;

        foreach (var row in rows)
        {
            var oldId = row.TryGetValue("Id", out var idValue) && idValue is not null
                ? Convert.ToInt64(idValue, CultureInfo.InvariantCulture)
                : 0L;

            // 父记录已经在库里（同一个房间码），这一行就没必要再搬一遍
            if (spec.ForeignKey is not null
                && skippedParentIds is { Count: > 0 }
                && row.TryGetValue(spec.ForeignKey, out var parentValue)
                && parentValue is not null
                && skippedParentIds.Contains(Convert.ToInt64(parentValue, CultureInfo.InvariantCulture)))
            {
                continue;
            }

            // 唯一列上已经有同一条了就跳过 —— 合并不能把唯一约束撞崩
            if (uniqueIndex >= 0)
            {
                var uniqueValue = row.TryGetValue(columns[uniqueIndex], out var raw) ? raw : null;

                if (uniqueValue is not null
                    && UniqueValueExists(connection, transaction, spec.Name, columns[uniqueIndex], uniqueValue))
                {
                    if (oldId != 0) skipped.Add(oldId);
                    continue;
                }
            }

            var names = new List<string>();
            var values = new List<object?>();

            foreach (var column in columns)
            {
                // Id 交给 SQLite 自己发号
                if (string.Equals(column, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = row.TryGetValue(column, out var raw) ? raw : null;

                if (spec.ForeignKey is not null
                    && string.Equals(column, spec.ForeignKey, StringComparison.OrdinalIgnoreCase)
                    && value is not null)
                {
                    var oldReference = Convert.ToInt64(value, CultureInfo.InvariantCulture);

                    // 父记录搬过来了就换成新号；没搬过来就断开引用（别指向别人家的记录）
                    value = parentMap is not null && parentMap.TryGetValue(oldReference, out var newReference)
                        ? newReference
                        : null;
                }

                names.Add(column);
                values.Add(value);
            }

            var newId = InsertRow(connection, transaction, spec.Name, names, values);
            inserted++;

            if (oldId != 0)
            {
                rowMap[oldId] = newId;
            }
        }

        return inserted;
    }

    /// <summary>这张表里是不是已经有这个值了（只用于唯一列）。</summary>
    private static bool UniqueValueExists(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        string column,
        object value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT 1 FROM main.\"{table}\" WHERE \"{column}\" = $v LIMIT 1";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$v";
        parameter.Value = value;
        command.Parameters.Add(parameter);

        return command.ExecuteScalar() is not null;
    }

    private static long InsertRow(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        List<string> names,
        List<object?> values)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        if (names.Count == 0)
        {
            // 极端情况：表里只有 Id 一列
            command.CommandText = $"INSERT INTO main.\"{table}\" DEFAULT VALUES; SELECT last_insert_rowid();";
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        var parameters = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            var name = "$v" + i.ToString(CultureInfo.InvariantCulture);
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = values[i] ?? DBNull.Value;
            command.Parameters.Add(parameter);
            parameters.Add(name);
        }

        command.CommandText =
            $"INSERT INTO main.\"{table}\" ({string.Join(", ", names.Select(n => $"\"{n}\""))}) " +
            $"VALUES ({string.Join(", ", parameters)}); SELECT last_insert_rowid();";

        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>读出一张表的所有行（列名 → 值）。</summary>
    private static List<Dictionary<string, object?>> ReadRows(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        List<string> columns)
    {
        var rows = new List<Dictionary<string, object?>>();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT * FROM src.\"{table}\"";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = value;
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>用 PRAGMA table_info 拿列名（顺带拿到顺序）。</summary>
    private static List<string> GetColumns(
        DbConnection connection,
        DbTransaction? transaction,
        string schema,
        string table)
    {
        var columns = new List<string>();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA {schema}.table_info(\"{table}\")";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // cid, name, type, notnull, dflt_value, pk
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static void AssignCount(BackupCounts counts, string table, int value)
    {
        switch (table)
        {
            case "Schedules": counts.Schedules = value; break;
            case "Todos": counts.Todos = value; break;
            case "FocusSessions": counts.FocusSessions = value; break;
            case "StudyRooms": counts.StudyRooms = value; break;
            case "StudyRoomMembers": counts.StudyRoomMembers = value; break;
            case "StudyRoomMessages": counts.StudyRoomMessages = value; break;
        }
    }

    // ════════════════════════════ 内部：杂项 ════════════════════════════

    private string? BuildWarning(BackupManifest manifest)
    {
        var warnings = new List<string>();

        if (manifest.Config is null)
        {
            warnings.Add("这份备份没带配置，导入后界面设置保持本机的");
        }

        // 只有"本来有壁纸但没打进来"才值得提醒；本来就没设壁纸是常态，不该提示
        if (manifest.Wallpaper is null && !string.IsNullOrWhiteSpace(manifest.WallpaperSkippedReason))
        {
            warnings.Add(manifest.WallpaperSkippedReason!);
        }

        if (manifest.Counts.Total == 0)
        {
            warnings.Add("这份备份里没有日程/待办/专注记录，导入后本地数据会被清空");
        }

        return warnings.Count == 0 ? null : string.Join("；", warnings) + "。";
    }

    /// <summary>只保留最近 <paramref name="keepCount"/> 份备份。删不掉就算了，下次再说。</summary>
    private void PruneOldBackups(int keepCount)
    {
        try
        {
            if (!Directory.Exists(BackupDirectory)) return;

            var files = new DirectoryInfo(BackupDirectory)
                .GetFiles("*" + BackupFormat.FileExtension)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(keepCount)
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // 可能正被资源管理器打开着
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataBackupService] 清理旧备份失败：{ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 临时文件删不掉不影响正确性，系统重启也会清
        }
    }
}