namespace HeliumTodo.Core.Notifications;

/// <summary>提醒是从哪一类东西来的。界面要据此决定点通知时跳哪一页。</summary>
public enum ReminderSourceKind
{
    /// <summary>日程。</summary>
    Schedule = 0,

    /// <summary>待办。</summary>
    Todo = 1,
}

/// <summary>
/// 一条"该弹出来了"的提醒。
///
/// 注意它不是数据库里的东西 —— 它是一次计算的结果：
/// 「在 now 这个时刻，哪些日程/待办正好到了该提醒的点」。
/// 这样 <see cref="ReminderPlanner"/> 可以脱离界面、脱离平台单独测。
/// </summary>
public sealed record ReminderItem
{
    /// <summary>来源类型。</summary>
    public required ReminderSourceKind Kind { get; init; }

    /// <summary>来源的数据库 Id。</summary>
    public required int SourceId { get; init; }

    /// <summary>标题（通知的第一行）。</summary>
    public required string Title { get; init; }

    /// <summary>正文（几点、提前多久）。</summary>
    public required string Message { get; init; }

    /// <summary>被提醒的那一刻（日程的开始时间 / 待办的截止时间）。</summary>
    public required DateTime Anchor { get; init; }

    /// <summary>提前了多少分钟（0 = 开始时）。</summary>
    public required int LeadMinutes { get; init; }

    /// <summary>提醒实际触发的时刻 = Anchor - LeadMinutes。</summary>
    public required DateTime RemindAt { get; init; }

    /// <summary>
    /// 去重用的键。
    ///
    /// 为什么带上 RemindAt：同一条日程每天都会提醒（重复规则展开后是同一条记录），
    /// 只按 Id 去重会导致"第二次以后都不提醒了"。
    /// </summary>
    public string Key => $"{Kind}:{SourceId}:{RemindAt:yyyyMMddHHmm}";
}