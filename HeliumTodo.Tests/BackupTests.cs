// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.IO.Compression;
using System.Text;
using HeliumTodo.Domain.Backup;
using HeliumTodo.Domain.Configuration;
using HeliumTodo.Domain.Models;
using HeliumTodo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 备份导入导出的测试。
///
/// 这些测试都在临时目录里建一个**真的 SQLite 库**，然后真的导出成 zip、真的再导回来，
/// 不是打桩。因为备份这件事最容易出错的地方恰好是"文件层面"：
/// WAL 里还没落盘的数据、zip 里的路径、SHA256、ATTACH 之后的表结构。
/// 只有真跑一遍才知道对不对。
/// </summary>
public sealed class BackupTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _provider;
    private readonly IDbContextFactory<HeliumDbContext> _factory;
    private readonly ConfigService _config;
    private readonly DataBackupService _backup;
    private readonly TodoRepository _todos;
    private readonly FocusRepository _focus;

    public BackupTests()
    {
        // 每个测试一个独立目录，避免互相看到对方的库
        _root = TestWorkspace.Create("backup");

        _config = new ConfigService(_root);
        _config.Load();

        var services = new ServiceCollection();
        services.AddHeliumData(_config.ResolveDatabasePath());
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
        _provider.Dispose();
        TestWorkspace.Delete(_root);
    }

    // ────────────── 导出 ──────────────

    [Fact]
    public void 导出的压缩包带清单和数据()
    {
        AddTodo("写实验报告");
        AddTodo("复习线代");

        var result = _backup.Export(ZipPath("basic"));

        Assert.True(File.Exists(result.Path));
        Assert.Equal(2, result.Manifest.Counts.Todos);
        Assert.Equal(BackupFormat.FormatId, result.Manifest.Format);
        Assert.Equal(BackupFormat.CurrentVersion, result.Manifest.FormatVersion);

        // 包里的清单能再读出来，且内容一致
        using var reader = BackupPackageReader.Open(result.Path);
        Assert.Equal(2, reader.Manifest.Counts.Todos);
        Assert.Contains(BackupFormat.DatabaseEntry, reader.EntryNames);
        Assert.Contains(BackupFormat.ConfigEntry, reader.EntryNames);
    }

    [Fact]
    public void 导出包的校验和能通过复核()
    {
        AddTodo("随便一条");

        var result = _backup.Export(ZipPath("checksum"));

        using var reader = BackupPackageReader.Open(result.Path);
        Assert.True(reader.TryVerifyChecksums(out var error), error);
        Assert.Null(error);
    }

    [Fact]
    public void 备份里能看到条数摘要()
    {
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
        var path = ZipPath("not-a-zip");
        File.WriteAllText(path, "我其实是一个文本文件");

        var inspection = _backup.Inspect(path);

        Assert.False(inspection.IsUsable);
        Assert.Contains("zip", inspection.Error!);
    }

    [Fact]
    public void 格式版本更高的备份会被拒绝()
    {
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
        AddTodo("原始数据");
        var zip = _backup.Export(ZipPath("tampered")).Path;

        // 用垃圾字节替换包里的数据库：清单里的 SHA256 立刻对不上
        RewriteEntry(zip, BackupFormat.DatabaseEntry, Encoding.UTF8.GetBytes("坏掉的数据库"));

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
    public void 合并导入会追加数据并重新编号()
    {
        var first = AddTodo("甲");
        AddFocus(first.Id, "甲 的专注");

        var zip = _backup.Export(ZipPath("merge")).Path;

        var second = AddTodo("乙");
        AddFocus(second.Id, "乙 的专注");

        var result = _backup.Import(zip, BackupImportMode.Merge);

        Assert.True(result.Success);
        Assert.Equal(1, result.Counts.Todos);
        Assert.Equal(1, result.Counts.FocusSessions);

        // 原有的两条 + 备份里那一条
        var todos = AllTodos();
        Assert.Equal(3, todos.Count);

        var sessions = AllSessions();
        Assert.Equal(3, sessions.Count);

        // 关键：导入进来的专注记录指向的是**新编号**的待办，而且这个待办真的存在
        var todoIds = todos.Select(t => t.Id).ToHashSet();
        foreach (var session in sessions.Where(s => s.TodoId.HasValue))
        {
            Assert.Contains(session.TodoId!.Value, todoIds);
        }

        // 合并模式不动现有数据，所以不该有安全备份
        Assert.Null(result.SafetyBackupPath);
    }

    [Fact]
    public void 替换导入前会自动备份当前数据()
    {
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
    public void 导入不会覆盖本机的数据库路径()
    {
        AddTodo("甲");
        var zip = _backup.Export(ZipPath("config")).Path;

        // 模拟"别人的备份"：配置里带着一台不存在的电脑上的数据库路径，主题也换一个
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

        // 主题跟着备份走，数据库路径必须还是本机的
        Assert.Equal("imported-theme", _config.Current.ThemeId);
        Assert.NotEqual(@"Z:\别人的电脑\helium.db", _config.Current.DatabasePath);
        Assert.Equal(_config.ConfigDirectory, Path.GetDirectoryName(_backup.DatabasePath));
    }

    [Fact]
    public void 合并导入不会因为房间码重复而失败()
    {
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

        // 房间还有成员之类都只应该有一份，不许叠起来
        using var verify = _factory.CreateDbContext();
        Assert.Equal(1, verify.StudyRooms.Count(r => r.Code == "ROOM01"));

        // 待办是"追加"语义，合并两次就该有两份（没有唯一约束，这是预期行为）
        Assert.Equal(3, verify.Todos.Count());
    }

    // ────────────── 辅助 ──────────────

    [Fact(DisplayName = "原位置写不进去时改存到备份文件夹")]
    public void ExportFallsBackWhenTargetIsNotWritable()
    {
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