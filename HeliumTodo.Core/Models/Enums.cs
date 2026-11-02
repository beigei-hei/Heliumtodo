namespace HeliumTodo.Core.Models;

/// <summary>
/// 任务 / 日程的优先级。
/// 数值越大越重要，方便直接排序（比如 OrderByDescending(x => x.Priority)）。
/// 界面上会用它来决定颜色深浅和柱状图高度。
/// </summary>
public enum Priority
{
    /// <summary>最低：有空再做。</summary>
    Low = 0,

    /// <summary>普通：默认值。</summary>
    Normal = 1,

    /// <summary>较高：今天应该做完。</summary>
    High = 2,

    /// <summary>紧急：必须优先处理，时间轴上会醒目提示。</summary>
    Urgent = 3,
}

/// <summary>
/// 一天中的时段类型。用来给人「什么时候适合做什么」的感觉，
/// 也方便以后做「自动推荐空闲时段」。
/// </summary>
public enum TimeBlock
{
    /// <summary>不限。</summary>
    Any = 0,

    /// <summary>上午（06:00–12:00）：适合需要脑力的深度工作。</summary>
    Morning = 1,

    /// <summary>下午（12:00–18:00）：适合会议、沟通、事务。</summary>
    Afternoon = 2,

    /// <summary>晚上（18:00–24:00）：适合复习、阅读。</summary>
    Evening = 3,
}

/// <summary>
/// 一项日程的状态。
/// </summary>
public enum ScheduleStatus
{
    /// <summary>待办（还没开始）。</summary>
    Planned = 0,

    /// <summary>正在进行。</summary>
    InProgress = 1,

    /// <summary>已完成。</summary>
    Completed = 2,

    /// <summary>已取消（保留记录，但不参与冲突计算）。</summary>
    Cancelled = 3,
}
