using HeliumTodo.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Data;

/// <summary>
/// 日程的数据操作。
/// 每个方法都开了自己的 DbContext，用完就释放 —— 这样在界面线程和后台任务里都能安全调用。
/// </summary>
public sealed class ScheduleRepository
{
    private readonly IDbContextFactory<HeliumDbContext> _factory;

    public ScheduleRepository(IDbContextFactory<HeliumDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>取出全部日程，按开始时间排序。</summary>
    public async Task<List<ScheduleItem>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Schedules
                       .AsNoTracking()
                       .OrderBy(s => s.Start)
                       .ToListAsync(ct);
    }

    /// <summary>取出某一天会出现的日程（已按重复规则展开筛选）。</summary>
    public async Task<List<ScheduleItem>> GetForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        // 先全部取出来再在内存里过滤，因为重复规则是自定义格式，
        // 数据库没法直接理解「weekly:1,3,5」。
        // 日程数量对个人使用来说很少（几百条量级），这样做完全够快。
        var all = await GetAllAsync(ct);
        return all.Where(s => Core.Services.RecurrenceExpander.OccursOn(s.Start, s.RecurrenceRule, date))
                  .ToList();
    }

    /// <summary>按 Id 取一条。</summary>
    public async Task<ScheduleItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Schedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    /// <summary>新增一条日程，返回带 Id 的对象。</summary>
    public async Task<ScheduleItem> AddAsync(ScheduleItem item, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Schedules.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    /// <summary>修改一条日程。</summary>
    public async Task UpdateAsync(ScheduleItem item, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Schedules.Update(item);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>删除一条日程。</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var found = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (found is null) return;

        db.Schedules.Remove(found);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>某一天有几条日程（给首页统计用）。</summary>
    public async Task<int> CountForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var items = await GetForDateAsync(date, ct);
        return items.Count(s => s.IsActive);
    }
}
