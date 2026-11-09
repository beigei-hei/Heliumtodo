// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.IO.Compression;
using System.Text;
using HeliumTodo.Application.Configuration;
using HeliumTodo.Domain.Backup;
using HeliumTodo.Domain.Models;
using HeliumTodo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 备份导入导出的测试。
///
/// 这些测试连一个**真的 PostgreSQL 库**（每个测试类一个临时库），然后真的导出成 zip、
/// 真的再导回来，不是打桩。因为备份最容易出错的地方恰好是数据本身：
/// 时间列的时区、自增序列、外键指向、zip 里的路径和 SHA256。
/// 没有设置 `HELIUM_TEST_POSTGRES` 时整类跳过（返回而不是失败）。
/// </summary>
public sealed class BackupTests : IDisposable
{
    private readonly PostgresTestDatabase _pg;
    private readonly string? _skip;

    private readonly string _root;
    private readonly ServiceProvider _provider;
    private readonly IDbContextFactory<HeliumDbContext> _factory;
    private readonly ConfigService _config;
    private readonly DataBackupService _backup;
    private readonly TodoRepository _todos;
    private readonly FocusRepository _focus;

    public BackupTests()
    {
        _pg = new PostgresTestDatabase("backup");
        _skip = _pg.SkipReason;
        if (_skip is not null)
        {
            _root = string.Empty;
            _provider = null!;
            _factory = null!;
            _config = null!;
            _backup = null!;
            _todos = null!;
            _focus = null!;
            return;
        }

        // 每个测试一个独立目录，避免配置/备份文件互相看到
        _root = TestWorkspace.Create("backup");

        _config = new ConfigService(_root);
        _config.Load();

        var services = new ServiceCollection();
        services.AddHeliumData(_pg.ConnectionString);
        _provider = services.BuildServiceProvider();

        _factory = _provider.GetRequiredService<IDbContextFactory<HeliumDbContext>>();

        using (var db = _factory.CreateDbContext())
        {
            // 不灌示例数据：测试要自己控制"库里到底有什么"
            DatabaseInitializer.Initialize(db, seedIfEmpty: false);
        }

        _backup = new DataBackupService(_factory, _config);
        _todos = new TodoRepository(_factory);
        _focus = new FocusRepository(_factory);
    }

    public void Dispose()
    {
        if (_skip is null)
        {
            _provider.Dispose();
            TestWorkspace.Delete(_root);
        }

        _pg.Dispose();
    }

    // ────────────── 导出 ──────────────

    [Fact]
    public void 导出的压缩包带清单和数据()
    {
        if (_skip is not null) return;

        AddTodo("写实验报告");
        AddTodo("复习线代");

        var result = _backup.Export(ZipPath("basic"));

        Assert.True(File.Exists(result.Path));
        Assert.Equal(2, result.Manifest.Counts.Todos);
        Assert.Equal(BackupFormat.FormatId, result.Manifest.Format);
        Assert.Equal(BackupFormat.CurrentVersion, result.Manifest.FormatVersion);
        Assert.Equal("postgres", result.Manifest.Provider);

        // 包里的清单能再读出来，且内容一致
        using var reader = BackupPackageReader.Open(result.Path);
        Assert.Equal(2, reader.Manifest.Counts.Todos);
        Assert.Contains(BackupFormat.TableEntry("Todos"), reader.EntryNames);
        Assert.Contains(BackupFormat.ConfigEntry, reader.EntryNames);
    }

    [Fact]
    public void 导出包的校验和能通过复核()
    {
        if (_skip is not null) return;

        AddTodo("随便一条");

        var result = _backup.Export(ZipPath("checksum"));

        using var reader = BackupPackageReader.Open(result.Path);
        Assert.True(reader.TryVerifyChecksums(out var error), error);
        Assert.Null(error);
    }

    [Fact]
    public void 备份里能看到条数摘要()
    {
        if (_skip is not null) return;

        AddTodo("一");
        AddTodo("二");
        AddTodo("三");

        var zip = _backup.Export(ZipPath("summary")).Path;
        var inspection = _backup.Inspect(zip);

        Assert.True(inspection.IsUsable, inspection.Error);
        Assert.Equal(3, inspection.Counts.Todos);
        Assert.Contains("3 条待办", inspection.DescribeText);
        Assert.Null(inspection.Warning);
    }

    // ────────────── 拒绝不合法的包 ──────────────

    [Fact]
    public void 缺少清单的zip会被拒绝()
    {
        if (_skip is not null) return;

        var path = ZipPath("no-manifest");
        using (var stream = File.Create(path))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "readme.txt", Encoding.UTF8.GetBytes("这不是备份"));
        }

        var error = Assert.Throws<BackupPackageException>(() => BackupPackageReader.Open(path));
        Assert.Contains("manifest.json", error.Message);

        // 走界面的那条路（Inspect）也应该给出人能看懂的结果，而不是抛出去
        var inspection = _backup.Inspect(path);
        Assert.False(inspection.IsUsable);
        Assert.Contains("manifest.json", inspection.Error!);
    }

    [Fact]
    public void 不是zip的文件会被拒绝()
    {
        if (_skip is not null) return;

        var path = ZipPath("not-a-zip");
        File.WriteAllText(path, "我其实是一个文本文件");

        var inspection = _backup.Inspect(path);

        Assert.False(inspection.IsUsable);
        Assert.Contains("zip", inspection.Error!);
    }

    [Fact]
    public void 格式版本更高的备份会被拒绝()
    {
        if (_skip is not null) return;

        AddTodo("未来的数据");
        var zip = _backup.Export(ZipPath("future")).Path;

        // 把清单里的格式版本改成 99，装作是一份"来自未来版本"的备份
        var manifest = ReadManifest(zip);
        manifest.FormatVersion = BackupFormat.CurrentVersion + 98;
        RewriteEntry(zip, BackupFormat.ManifestEntry, manifest.ToJsonBytes());

        var error = Assert.Throws<BackupPackageException>(() => BackupPackageReader.Open(zip));
        Assert.Contains("更新版本", error.Message);
    }

    [Fact]
    public void 数据库被篡改后校验不通过()
    {
        if (_skip is not null) return;

        AddTodo("原始数据");
        var zip = _backup.Export(ZipPath("tampered")).Path;

        // 用垃圾字节替换包里的表数据：清单里的 SHA256 立刻对不上
        RewriteEntry(zip, BackupFormat.TableEntry("Todos"), Encoding.UTF8.GetBytes("坏掉的数据"));

        using var reader = BackupPackageReader.Open(zip);
        Assert.False(reader.TryVerifyChecksums(out var error));
        Assert.Contains("校验失败", error!);

        var inspection = _backup.Inspect(zip);
        Assert.False(inspection.IsUsable);
    }

    // ────────────── 导入 ──────────────

    [Fact]
    public void 替换导入后只剩备份里的数据()
    {
        if (_skip is not null) return;

        AddTodo("甲");
        AddTodo("乙");
        var zip = _backup.Export(ZipPath("replace")).Path;

        // 导出之后又加了一条：替换导入应该把它冲掉
        AddTodo("丙");
        Assert.Equal(3, AllTodos().Count);

        var result = _backup.Import(zip, BackupImportMode.Replace);

        Assert.True(result.Success);
        Assert.Equal(2, result.Counts.Todos);

        var titles = AllTodos().Select(t => t.Title).OrderBy(t => t).ToList();
        Assert.Equal(new[] { "乙", "甲" }.OrderBy(t => t).ToList(), titles);
    }

    [Fact]
    public void 合并导入会追加不存在的记录并更新同Id记录()
    {
        if (_skip is not null) return;

        var first = AddTodo("甲");
        AddFocus(first.Id, "甲 的专注");

        var zip = _backup.Export(ZipPath("merge")).Path;

        // 清空当前库：备份里的 Id 在库里都不存在，合并应该追加
        using (var db = _factory.CreateDbContext())
        {
            db.FocusSessions.ExecuteDelete();
            db.Todos.ExecuteDelete();
        }

        var result = _backup.Import(zip, BackupImportMode.Merge);

        Assert.True(result.Success);
        Assert.Equal(1, result.Counts.Todos);
        Assert.Equal(1, result.Counts.FocusSessions);
        Assert.Single(AllTodos());
        Assert.Single(AllSessions());

        // 再合并一次：同一个 Id 已存在，应该"更新"而不是再叠一份
        var again = _backup.Import(zip, BackupImportMode.Merge);
        Assert.True(again.Success);
        Assert.Single(AllTodos());

        // 合并模式不动配置之外的东西，也不该有安全备份
        Assert.Null(again.SafetyBackupPath);
    }

    [Fact]
    public void 替换导入前会自动备份当前数据()
    {
        if (_skip is not null) return;

        AddTodo("旧的甲");
        var zip = _backup.Export(ZipPath("safety")).Path;

        AddTodo("导出后新增的乙");
        Assert.Equal(2, AllTodos().Count);

        var result = _backup.Import(zip, BackupImportMode.Replace);

        Assert.NotNull(result.SafetyBackupPath);
        Assert.True(File.Exists(result.SafetyBackupPath));
        Assert.StartsWith("pre-import-", Path.GetFileName(result.SafetyBackupPath!));

        // 那份安全备份里应该装着导入前的两条数据
        using var safety = BackupPackageReader.Open(result.SafetyBackupPath!);
        Assert.Equal(2, safety.Manifest.Counts.Todos);
    }

    [Fact]
    public void 导入不会覆盖本机的数据库连接()
    {
        if (_skip is not null) return;

        AddTodo("甲");
        var zip = _backup.Export(ZipPath("config")).Path;

        // 模拟"别人的备份"：配置里带着别人电脑上的数据库路径，主题也换一个
        var importedConfig = new AppConfig
        {
            ThemeId = "imported-theme",
            DatabasePath = @"Z:\别人的电脑\helium.db",
        };

        var configBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            importedConfig,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

        // 先把原清单读出来（reader 关掉之后才能改这个 zip）
        BackupManifest manifest;
        using (var reader = BackupPackageReader.Open(zip))
        {
            manifest = reader.Manifest;
        }

        RewriteEntry(zip, BackupFormat.ConfigEntry, configBytes);

        // 换了内容就要同步改清单里的校验和，否则会被"校验失败"挡在门外
        manifest.Config!.Sha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(configBytes)).ToLowerInvariant();
        manifest.Config.SizeBytes = configBytes.Length;
        RewriteEntry(zip, BackupFormat.ManifestEntry, manifest.ToJsonBytes());

        var result = _backup.Import(zip, BackupImportMode.Replace);

        Assert.True(result.ConfigApplied);

        // 主题跟着备份走；数据库连接必须还是本机的（PostgreSQL 不走 DatabasePath）
        Assert.Equal("imported-theme", _config.Current.ThemeId);
        Assert.NotEqual(@"Z:\别人的电脑\helium.db", _config.Current.DatabasePath);
        Assert.Contains("Database=", _backup.DatabasePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 合并导入不会因为房间码重复而失败()
    {
        if (_skip is not null) return;

        // 自习室的房间码是唯一列。把自己的备份合并回自己时，
        // 同一个房间码会撞唯一约束 —— 这种情况应该"跳过"，而不是让整次导入失败。
        using (var db = _factory.CreateDbContext())
        {
            db.StudyRooms.Add(new StudyRoom
            {
                Code = "ROOM01",
                Name = "二教 305",
                HostName = "我",
                CreatedAt = DateTime.Now,
            });
            db.SaveChanges();
        }

        AddTodo("甲");
        var zip = _backup.Export(ZipPath("room")).Path;

        // 合并两次，两次都不该抛异常
        var first = _backup.Import(zip, BackupImportMode.Merge);
        var second = _backup.Import(zip, BackupImportMode.Merge);

        Assert.True(first.Success);
        Assert.True(second.Success);

        // 房间还是只有一份
        using var verify = _factory.CreateDbContext();
        Assert.Equal(1, verify.StudyRooms.Count(r => r.Code == "ROOM01"));
    }

    // ────────────── 辅助 ──────────────

    [Fact(DisplayName = "原位置写不进去时改存到备份文件夹")]
    public void ExportFallsBackWhenTargetIsNotWritable()
    {
        if (_skip is not null) return;

        AddTodo("导出目标不可写");

        // 拿一个"目录"当目标路径：FileStream 对已存在的目录会抛 UnauthorizedAccessException，
        // 和"用户挑的文件夹被系统拦住"是同一种异常类型，正好拿来模拟。
        var target = Path.Combine(_root, "占位目录");
        Directory.CreateDirectory(target);

        var result = _backup.Export(target);

        Assert.True(result.FellBack);
        Assert.Equal(target, result.RequestedPath);
        Assert.True(File.Exists(result.Path));
        Assert.Equal(
            Path.GetFullPath(_backup.BackupDirectory),
            Path.GetFullPath(Path.GetDirectoryName(result.Path)!));
    }

    [Fact(DisplayName = "降级保存重名时不会覆盖旧文件")]
    public void FallbackExportDoesNotOverwriteExistingFile()
    {
        if (_skip is not null) return;

        AddTodo("重名测试");

        var target = Path.Combine(_root, "占位目录2");
        Directory.CreateDirectory(target);

        var first = _backup.Export(target);
        var second = _backup.Export(target);

        Assert.True(first.FellBack);
        Assert.True(second.FellBack);
        Assert.NotEqual(first.Path, second.Path);
        Assert.True(File.Exists(first.Path));
        Assert.True(File.Exists(second.Path));
    }

    private string ZipPath(string name) => Path.Combine(_root, name + BackupFormat.FileExtension);

    private TodoItem AddTodo(string title)
    {
        var todo = new TodoItem
        {
            Title = title,
            DueAt = DateTime.Today.AddDays(1),
            EstimatedDuration = TimeSpan.FromMinutes(30),
        };

        return _todos.AddAsync(todo).GetAwaiter().GetResult();
    }

    private void AddFocus(int todoId, string title)
    {
        var session = new FocusSession
        {
            Title = title,
            TodoId = todoId,
            PlannedStart = DateTime.Today.AddHours(9),
            PlannedDuration = TimeSpan.FromMinutes(25),
            Status = FocusSessionStatus.Completed,
            StartedAt = DateTime.Today.AddHours(9),
            EndedAt = DateTime.Today.AddHours(9).AddMinutes(25),
        };

        _focus.AddAsync(session).GetAwaiter().GetResult();
    }

    private List<TodoItem> AllTodos() => _todos.GetAllAsync().GetAwaiter().GetResult();

    private List<FocusSession> AllSessions() => _focus.GetAllAsync().GetAwaiter().GetResult();

    private static BackupManifest ReadManifest(string zipPath)
    {
        using var reader = BackupPackageReader.Open(zipPath);
        return reader.Manifest;
    }

    /// <summary>把一个 zip 条目换掉（用来伪造"坏备份"）。</summary>
    private static void RewriteEntry(string zipPath, string entryName, byte[] content)
    {
        using var stream = new FileStream(zipPath, FileMode.Open, FileAccess.ReadWrite);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Update);

        zip.GetEntry(entryName)?.Delete();
        WriteEntry(zip, entryName, content);
    }

    private static void WriteEntry(ZipArchive zip, string entryName, byte[] content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }
}