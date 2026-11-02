using HeliumTodo.Core.Configuration;
using HeliumTodo.Core.Models;
using HeliumTodo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 自习室「离开 / 重进」的测试。
///
/// 背景：用户报告"退出后重进仍显示在房间内部"。
/// 原因不在界面绑定，而在数据层 —— 房间存在 SQLite 里，
/// 离开时只清了内存里的 CurrentRoom，数据库那行还在，
/// 于是下次打开页面「载入最近的房间」又把它拉回来了。
///
/// 所以这里测的就是数据层的约定：
///   · 离开 = 记下 ClosedAt，之后 GetOpenAsync 不再返回它；
///   · 主动回到这个房间 = 清掉 ClosedAt；
///   · 老数据库（没有 ClosedAt 这一列）要能自动补列，不能直接崩。
/// </summary>
public sealed class StudyRoomRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _provider;
    private readonly IDbContextFactory<HeliumDbContext> _factory;
    private readonly StudyRoomRepository _rooms;

    public StudyRoomRepositoryTests()
    {
        _root = TestWorkspace.Create("room");

        var config = new ConfigService(_root);
        config.Load();

        var services = new ServiceCollection();
        services.AddHeliumData(config.ResolveDatabasePath());
        _provider = services.BuildServiceProvider();

        _factory = _provider.GetRequiredService<IDbContextFactory<HeliumDbContext>>();

        using (var db = _factory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(db, seedIfEmpty: false);
        }

        _rooms = new StudyRoomRepository(_factory);
    }

    public void Dispose()
    {
        _provider.Dispose();
        TestWorkspace.Delete(_root);
    }

    // 辅助：造一个房间并存进库
    private async Task<StudyRoom> AddRoomAsync(string code)
    {
        var room = new StudyRoom
        {
            Code = code,
            Name = "测试自习室",
            HostName = "我",
            DefaultFocusDuration = TimeSpan.FromMinutes(45),
            CreatedAt = DateTime.Now,
        };

        room.Members.Add(new StudyRoomMember
        {
            DisplayName = "我",
            PeerId = "local",
            IsOnline = true,
            JoinedAt = DateTime.Now,
        });

        return await _rooms.SaveAsync(room);
    }

    // 离开房间应该记下时间，之后不该再被当成"还开着"
    [Fact]
    public async Task 离开房间之后重新载入不会回到房间里()
    {
        var room = await AddRoomAsync("ROOM01");

        // 打开页面会载入它
        var opened = await _rooms.GetOpenAsync();
        Assert.Single(opened);

        // 用户点「离开」
        room.ClosedAt = DateTime.Now;
        await _rooms.SaveAsync(room);

        // 再打开页面：不该再回到房间里
        var afterLeave = await _rooms.GetOpenAsync();
        Assert.Empty(afterLeave);

        // 但数据没丢，历史还在
        var all = await _rooms.GetAllAsync();
        Assert.Single(all);
        Assert.NotNull(all[0].ClosedAt);
    }

    // 主动回到本地房间要能把离开时间清掉
    [Fact]
    public async Task 离开过的房间可以重新回去()
    {
        var room = await AddRoomAsync("ROOM02");
        room.ClosedAt = DateTime.Now;
        await _rooms.SaveAsync(room);

        Assert.Empty(await _rooms.GetOpenAsync());

        // 「输入房间码加入」在本地找到它 —— 这时要把离开时间清掉
        var local = await _rooms.GetByCodeAsync("ROOM02");
        Assert.NotNull(local);

        local!.ClosedAt = null;
        await _rooms.SaveAsync(local);

        var reopened = await _rooms.GetOpenAsync();
        Assert.Single(reopened);
        Assert.Equal("ROOM02", reopened[0].Code);
    }

    // 老数据库（没有 ClosedAt 这一列）要能自动补列，
    // 而且补完之后旧房间一律算「已经离开」——
    // 升级前根本没有离开的概念，那些房间都是点了离开没能记住的残留。
    [Fact]
    public async Task 老数据库会自动补上离开时间这一列()
    {
        await AddRoomAsync("ROOM03");

        // 模拟"升级前建的库"：把新列删掉
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"StudyRooms\" DROP COLUMN \"ClosedAt\"");
        }

        // 再初始化一次应当把列补回来，而不是抛"没有这一列"
        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.EnsureDatabaseReady();
        }

        Assert.True(await ColumnExistsAsync("StudyRooms", "ClosedAt"));

        // 补完之后照常能读能写，而且旧房间被标记成已离开
        var rooms = await _rooms.GetAllAsync();
        Assert.Single(rooms);
        Assert.NotNull(rooms[0].ClosedAt);

        // 标成已离开之后，下次进页面就不会被载回房间里
        Assert.Empty(await _rooms.GetOpenAsync());
    }

    private async Task<bool> ColumnExistsAsync(string table, string column)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var connection = db.Database.GetDbConnection();

        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
