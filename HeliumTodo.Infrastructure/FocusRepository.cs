// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 专注记录的数据操作（含「预订专注时长」）。
/// </summary>
public sealed class FocusRepository
{
    private readonly IDbContextFactory<HeliumDbContext> _factory;

    public FocusRepository(IDbContextFactory<HeliumDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>取全部专注记录，按计划开始时间倒序（最近的在前）。</summary>
    public async Task<List<FocusSession>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.FocusSessions
                       .AsNoTracking()
                       .OrderByDescending(f => f.PlannedStart)
                       .ToListAsync(ct);
    }

    /// <summary>取某一天的专注记录。</summary>
    public async Task<List<FocusSession>> GetForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);

        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.FocusSessions
                       .AsNoTracking()
                       .Where(f => f.PlannedStart >= start && f.PlannedStart < end)
                       .OrderBy(f => f.PlannedStart)
                       .ToListAsync(ct);
    }

    /// <summary>取所有「已预订但还没开始」的专注（界面上显示在「预订的专注时长」列表里）。</summary>
    public async Task<List<FocusSession>> GetReservedAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.FocusSessions
                       .AsNoTracking()
                       .Where(f => f.Status == FocusSessionStatus.Reserved)
                       .OrderBy(f => f.PlannedStart)
                       .ToListAsync(ct);
    }

    /// <summary>新增一条专注记录（预订或直接完成都走这里）。</summary>
    public async Task<FocusSession> AddAsync(FocusSession session, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.FocusSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    /// <summary>修改。</summary>
    public async Task UpdateAsync(FocusSession session, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.FocusSessions.Update(session);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>删除（取消预订时用）。</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var found = await db.FocusSessions.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (found is null) return;

        db.FocusSessions.Remove(found);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 今天已经完成的专注总时长（只算真正完成的）。
    /// </summary>
    public async Task<TimeSpan> GetTodayTotalAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var sessions = await db.FocusSessions
            .AsNoTracking()
            .Where(f => f.Status == FocusSessionStatus.Completed &&
                        f.StartedAt >= today && f.StartedAt < tomorrow)
            .ToListAsync(ct);

        // 时长在内存里加起来：因为 ActualDuration 是计算属性，数据库里没有这个字段
        var total = TimeSpan.Zero;
        foreach (var s in sessions)
        {
            total += s.ActualDuration ?? TimeSpan.Zero;
        }
        return total;
    }

    /// <summary>今天的专注次数。</summary>
    public async Task<int> GetTodayCountAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.FocusSessions.CountAsync(
            f => f.Status == FocusSessionStatus.Completed &&
                 f.StartedAt >= today && f.StartedAt < tomorrow, ct);
    }

    /// <summary>统计信息：今天总时长 / 今天次数 / 累计总时长。</summary>
    public async Task<(TimeSpan Today, int TodayCount, TimeSpan AllTime)> GetStatsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var completed = await db.FocusSessions
            .AsNoTracking()
            .Where(f => f.Status == FocusSessionStatus.Completed)
            .ToListAsync(ct);

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var allTime = TimeSpan.Zero;
        var todayTotal = TimeSpan.Zero;
        var todayCount = 0;

        foreach (var s in completed)
        {
            var duration = s.ActualDuration ?? TimeSpan.Zero;
            allTime += duration;

            if (s.StartedAt >= today && s.StartedAt < tomorrow)
            {
                todayTotal += duration;
                todayCount++;
            }
        }

        return (todayTotal, todayCount, allTime);
    }
}
