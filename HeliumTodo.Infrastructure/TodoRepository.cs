// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 待办的数据操作。
/// </summary>
public sealed class TodoRepository
{
    private readonly IDbContextFactory<HeliumDbContext> _factory;

    public TodoRepository(IDbContextFactory<HeliumDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 取待办列表。
    /// 排序规则：未完成在前 → 优先级高的在前 → 截止时间早的在前 → 创建时间早的在前。
    /// 这个顺序就是「任务优先级」在界面上的体现。
    /// </summary>
    public async Task<List<TodoItem>> GetAllAsync(bool includeCompleted = true, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var query = db.Todos.AsNoTracking();
        if (!includeCompleted)
        {
            query = query.Where(t => !t.IsCompleted);
        }

        return await query
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueAt == null)      // 有截止时间的排前面
            .ThenBy(t => t.DueAt)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>只取未完成的待办。</summary>
    public Task<List<TodoItem>> GetActiveAsync(CancellationToken ct = default) =>
        GetAllAsync(includeCompleted: false, ct);

    /// <summary>取今天要处理的待办：今天截止的，或者今天安排了时间的。</summary>
    public async Task<List<TodoItem>> GetForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var all = await GetAllAsync(includeCompleted: false, ct);

        return all.Where(t =>
                (t.DueAt.HasValue && DateOnly.FromDateTime(t.DueAt.Value) == date) ||
                (t.PlannedStart.HasValue && DateOnly.FromDateTime(t.PlannedStart.Value) == date))
            .ToList();
    }

    /// <summary>按 Id 取一条。</summary>
    public async Task<TodoItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    /// <summary>新增。</summary>
    public async Task<TodoItem> AddAsync(TodoItem item, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Todos.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    /// <summary>修改。</summary>
    public async Task UpdateAsync(TodoItem item, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Todos.Update(item);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 切换完成状态。
    /// 顺便记录完成时间，并返回修改后的对象（界面要用来更新列表）。
    /// </summary>
    public async Task<TodoItem?> ToggleCompletedAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var found = await db.Todos.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (found is null) return null;

        found.IsCompleted = !found.IsCompleted;
        found.CompletedAt = found.IsCompleted ? DateTime.Now : null;

        await db.SaveChangesAsync(ct);
        return found;
    }

    /// <summary>删除。</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var found = await db.Todos.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (found is null) return;

        db.Todos.Remove(found);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>给待办累计专注时长（专注结束时调用，实现「待办 ↔ 专注」的联动）。</summary>
    public async Task AddFocusedTimeAsync(int todoId, TimeSpan duration, CancellationToken ct = default)
    {
        if (duration <= TimeSpan.Zero) return;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var found = await db.Todos.FirstOrDefaultAsync(t => t.Id == todoId, ct);
        if (found is null) return;

        found.FocusedDuration += duration;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>统计信息：总数 / 未完成 / 今天完成。</summary>
    public async Task<(int Total, int Active, int CompletedToday)> GetStatsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var total = await db.Todos.CountAsync(ct);
        var active = await db.Todos.CountAsync(t => !t.IsCompleted, ct);

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var completedToday = await db.Todos.CountAsync(
            t => t.IsCompleted && t.CompletedAt >= today && t.CompletedAt < tomorrow, ct);

        return (total, active, completedToday);
    }
}
