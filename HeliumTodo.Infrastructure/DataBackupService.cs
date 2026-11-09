// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeliumTodo.Application.Configuration;
using HeliumTodo.Domain;
using HeliumTodo.Domain.Backup;
using HeliumTodo.Domain.Models;
using HeliumTodo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 备份 / 恢复（统一用 .zip，格式 v2）。
///
/// 为什么要有这么一个服务，而不是在界面里直接拷文件？
///   ① 一次备份要包含三样东西：数据、配置、壁纸。少一样恢复到新电脑上就不完整。
///   ② 导入必须"先验后动"：确认是合法备份、校验和都对，再动用户的真实数据。
///   ③ 导入前要自动存一份当前数据 —— 万一导错了还能退回去。
///
/// 0.5.0 起数据库换成 PostgreSQL，备份不再是"拷一个数据库文件"：
/// 每张表序列化成 `data/&lt;Table&gt;.jsonl`，一行一条记录（规格 §7.5）。
/// 这样跨机器、跨 PostgreSQL 小版本都能导，也不用关心数据目录权限。
/// </summary>
public sealed class DataBackupService
{
    /// <summary>导入顺序：父表在前，子表在后。</summary>
    private static readonly string[] TableOrder =
    {
        "Schedules",
        "Todos",
        "FocusSessions",
        "StudyRooms",
        "StudyRoomMembers",
        "StudyRoomMessages",
    };

    private static readonly JsonSerializerOptions RowJson = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly IDbContextFactory<HeliumDbContext> _factory;
    private readonly ConfigService _config;

    public DataBackupService(IDbContextFactory<HeliumDbContext> factory, ConfigService config)
    {
        _factory = factory;
        _config = config;
    }

    /// <summary>当前数据库连接（已隐去密码），界面和日志用它说明"数据在哪"。</summary>
    public string DatabasePath => PostgresConnection.Mask(
        PostgresConnection.Resolve(_config.GetStorageSettings()));

    /// <summary>备份目录（配置目录下的 backups 文件夹）。</summary>
    public string BackupDirectory => Path.Combine(_config.ConfigDirectory, BackupFormat.BackupDirectoryName);

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

        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["Schedules"] = SerializeRows(db.Schedules.AsNoTracking().OrderBy(e => e.Id)),
            ["Todos"] = SerializeRows(db.Todos.AsNoTracking().OrderBy(e => e.Id)),
            ["FocusSessions"] = SerializeRows(db.FocusSessions.AsNoTracking().OrderBy(e => e.Id)),
            ["StudyRooms"] = SerializeRows(db.StudyRooms.AsNoTracking().OrderBy(e => e.Id)),
            ["StudyRoomMembers"] = SerializeRows(db.StudyRoomMembers.AsNoTracking().OrderBy(e => e.Id)),
            ["StudyRoomMessages"] = SerializeRows(db.StudyRoomMessages.AsNoTracking().OrderBy(e => e.Id)),
        };

        var wallpaper = ReadWallpaper();

        var content = new BackupPackageContent
        {
            Tables = tables,
            Provider = "postgres",
            Config = _config.ExportJsonBytes(),
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
                DatabaseBytes = manifest.Tables.Sum(t => t.SizeBytes) + (manifest.Database?.SizeBytes ?? 0),
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
    ///   ③ 在一个事务里写数据：替换 = 清空后照搬；合并 = 同 Id 更新、不同 Id 追加
    ///   ④ 修好自增序列，否则下一次新建记录会撞主键
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

        BackupCounts imported;
        try
        {
            imported = WriteRows(reader, manifest, mode);
        }
        catch (BackupPackageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BackupPackageException($"导入失败：{ex.Message}");
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

    // ════════════════════════════ 内部：把数据搬进来 ════════════════════════════

    private BackupCounts WriteRows(BackupPackageReader reader, BackupManifest manifest, BackupImportMode mode)
    {
        var schedules = ReadTable<ScheduleItem>(reader, manifest, "Schedules");
        var todos = ReadTable<TodoItem>(reader, manifest, "Todos");
        var sessions = ReadTable<FocusSession>(reader, manifest, "FocusSessions");
        var rooms = ReadTable<StudyRoom>(reader, manifest, "StudyRooms");
        var members = ReadTable<StudyRoomMember>(reader, manifest, "StudyRoomMembers");
        var messages = ReadTable<StudyRoomMessage>(reader, manifest, "StudyRoomMessages");

        // 连接上开了 EnableRetryOnFailure（规格 §7.1），这种情况下不能自己 BeginTransaction：
        // 重试执行器会拒绝"用户发起的事务"，因为失败重放时事务状态已经不可信。
        // 正确做法是把整段搬数据的操作交给执行策略，由它决定什么时候重跑。
        using var probe = _factory.CreateDbContext();
        var strategy = probe.Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var db = _factory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();

            if (mode == BackupImportMode.Replace)
            {
                // 倒序清：先删依赖别人的表
                db.StudyRoomMessages.ExecuteDelete();
                db.StudyRoomMembers.ExecuteDelete();
                db.FocusSessions.ExecuteDelete();
                db.Todos.ExecuteDelete();
                db.StudyRooms.ExecuteDelete();
                db.Schedules.ExecuteDelete();

                db.Schedules.AddRange(schedules);
                db.Todos.AddRange(todos);
                db.FocusSessions.AddRange(sessions);
                db.StudyRooms.AddRange(rooms);
                db.StudyRoomMembers.AddRange(members);
                db.StudyRoomMessages.AddRange(messages);
            }
            else
            {
                MergeInto(db, schedules, todos, sessions, rooms, members, messages);
            }

            db.SaveChanges();
            FixSequences(db);
            transaction.Commit();

            return 0;
        });

        return new BackupCounts
        {
            Schedules = schedules.Count,
            Todos = todos.Count,
            FocusSessions = sessions.Count,
            StudyRooms = rooms.Count,
            StudyRoomMembers = members.Count,
            StudyRoomMessages = messages.Count,
        };
    }

    /// <summary>
    /// 合并：同 Id 覆盖（规格 §7.5 的 ON CONFLICT DO UPDATE），新 Id 追加。
    /// 外键指向不存在的父记录时断开，别把别人的数据挂成孤儿。
    /// </summary>
    private static void MergeInto(
        HeliumDbContext db,
        List<ScheduleItem> schedules,
        List<TodoItem> todos,
        List<FocusSession> sessions,
        List<StudyRoom> rooms,
        List<StudyRoomMember> members,
        List<StudyRoomMessage> messages)
    {
        var scheduleIds = db.Schedules.Select(e => e.Id).ToHashSet();
        var todoIds = db.Todos.Select(e => e.Id).ToHashSet();
        var sessionIds = db.FocusSessions.Select(e => e.Id).ToHashSet();
        var roomIds = db.StudyRooms.Select(e => e.Id).ToHashSet();
        var roomIdByCode = db.StudyRooms.AsNoTracking()
            .ToDictionary(e => e.Code, e => e.Id, StringComparer.Ordinal);
        var memberIds = db.StudyRoomMembers.Select(e => e.Id).ToHashSet();
        var messageIds = db.StudyRoomMessages.Select(e => e.Id).ToHashSet();

        foreach (var row in schedules)
        {
            if (scheduleIds.Contains(row.Id)) db.Schedules.Update(row);
            else db.Schedules.Add(row);
        }

        foreach (var row in todos)
        {
            if (todoIds.Contains(row.Id)) db.Todos.Update(row);
            else db.Todos.Add(row);
        }

        foreach (var row in sessions)
        {
            if (row.TodoId is { } todoId && !todoIds.Contains(todoId))
            {
                row.TodoId = null;
            }

            if (sessionIds.Contains(row.Id)) db.FocusSessions.Update(row);
            else db.FocusSessions.Add(row);
        }

        var roomIdMap = new Dictionary<int, int>();
        foreach (var row in rooms)
        {
            if (roomIds.Contains(row.Id))
            {
                db.StudyRooms.Update(row);
                roomIdMap[row.Id] = row.Id;
                continue;
            }

            // 同一个房间码就是同一个房间：别硬塞第二份，把子记录挪过去
            if (roomIdByCode.TryGetValue(row.Code, out var existingId))
            {
                roomIdMap[row.Id] = existingId;
                continue;
            }

            db.StudyRooms.Add(row);
        }

        foreach (var row in members)
        {
            if (!roomIdMap.TryGetValue(row.StudyRoomId, out var roomId)
                && !roomIds.Contains(row.StudyRoomId))
            {
                continue;
            }

            row.StudyRoomId = roomIdMap.TryGetValue(row.StudyRoomId, out var mapped) ? mapped : row.StudyRoomId;

            if (memberIds.Contains(row.Id)) db.StudyRoomMembers.Update(row);
            else db.StudyRoomMembers.Add(row);
        }

        foreach (var row in messages)
        {
            if (!roomIdMap.TryGetValue(row.StudyRoomId, out var roomId)
                && !roomIds.Contains(row.StudyRoomId))
            {
                continue;
            }

            row.StudyRoomId = roomIdMap.TryGetValue(row.StudyRoomId, out var mapped) ? mapped : row.StudyRoomId;

            if (messageIds.Contains(row.Id)) db.StudyRoomMessages.Update(row);
            else db.StudyRoomMessages.Add(row);
        }
    }

    /// <summary>
    /// 把每张表的自增序列拨到当前最大 Id。
    /// 不修的话，导入的 Id 是 1..100，序列却还停在 3，下一次新建就撞主键。
    /// </summary>
    private static void FixSequences(HeliumDbContext db)
    {
        foreach (var table in TableOrder)
        {
            // 表名来自上面的编译期常量数组（六张表写死），不是用户输入；
            // 这里也不能用参数占位符 —— PostgreSQL 的标识符不能当参数传。
            // EF1003 在这条语句上是误报，明确压掉并写明理由。
#pragma warning disable EF1003
            db.Database.ExecuteSqlRaw(
                $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), " +
                $"COALESCE((SELECT MAX(\"Id\") FROM \"{table}\"), 1), " +
                $"EXISTS (SELECT 1 FROM \"{table}\"));");
#pragma warning restore EF1003
        }
    }

    /// <summary>读一张表的 JSONL。缺文件按空表处理（旧备份/手工删过）。</summary>
    private static List<T> ReadTable<T>(BackupPackageReader reader, BackupManifest manifest, string table)
    {
        var entry = manifest.Tables.FirstOrDefault(
            e => string.Equals(Path.GetFileNameWithoutExtension(e.Entry), table, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return new List<T>();
        }

        var text = Encoding.UTF8.GetString(reader.ReadEntry(entry.Entry));
        var rows = new List<T>();

        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var row = JsonSerializer.Deserialize<T>(line, RowJson);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static byte[] SerializeRows<T>(IEnumerable<T> rows)
    {
        var builder = new StringBuilder();

        foreach (var row in rows)
        {
            builder.Append(JsonSerializer.Serialize(row, RowJson));
            builder.Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
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

    // ════════════════════════════ 内部：杂项 ════════════════════════════

    private static string? BuildWarning(BackupManifest manifest)
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
}