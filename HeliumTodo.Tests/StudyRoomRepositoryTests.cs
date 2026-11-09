// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using HeliumTodo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 自习室「离开 / 重进」的测试。
///
/// 背景：用户报告"退出后重进仍显示在房间内部"。
/// 原因不在界面绑定，而在数据层 —— 房间存在库里，
/// 离开时只清了内存里的 CurrentRoom，数据库那行还在，
/// 于是下次打开页面「载入最近的房间」又把它拉回来了。
///
/// 所以这里测的就是数据层的约定：
///   · 离开 = 记下 ClosedAt，之后 GetOpenAsync 不再返回它；
///   · 主动回到这个房间 = 清掉 ClosedAt。
///
/// 0.5.0 起数据层换成 PostgreSQL，「老库自动补列」那条 SQLite 时代的测试
/// 随 PRAGMA 逻辑一起删除：结构变更只走 EF Migration，不再有运行期补列。
/// </summary>
public sealed class StudyRoomRepositoryTests : IDisposable
{
    private readonly PostgresTestDatabase _pg;
    private readonly string? _skip;
    private readonly ServiceProvider _provider;
    private readonly IDbContextFactory<HeliumDbContext> _factory;
    private readonly StudyRoomRepository _rooms;

    public StudyRoomRepositoryTests()
    {
        _pg = new PostgresTestDatabase("room");
        _skip = _pg.SkipReason;

        if (_skip is not null)
        {
            _provider = null!;
            _factory = null!;
            _rooms = null!;
            return;
        }

        var services = new ServiceCollection();
        services.AddHeliumData(_pg.ConnectionString);
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
        if (_skip is null)
        {
            _provider.Dispose();
        }

        _pg.Dispose();
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
        if (_skip is not null) return;

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
        if (_skip is not null) return;

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

    // 成员要跟着房间一起存：外键和级联在 PostgreSQL 上也得对
    [Fact]
    public async Task 保存房间时成员一起落库()
    {
        if (_skip is not null) return;

        await AddRoomAsync("ROOM03");

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(1, await db.StudyRoomMembers.CountAsync());
    }
}