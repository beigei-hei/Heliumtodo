// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Services;

/// <summary>冲突的严重程度。</summary>
public enum ConflictSeverity
{
    /// <summary>提示：只是提醒一下，不影响执行。</summary>
    Info = 0,

    /// <summary>警告：时间撞了，建议调整。</summary>
    Warning = 1,

    /// <summary>严重：两个日程硬重叠，必须处理。</summary>
    Error = 2,
}

/// <summary>冲突的种类。</summary>
public enum ConflictKind
{
    /// <summary>两条日程时间重叠（时间轴上会错位显示）。</summary>
    ScheduleOverlap = 0,

    /// <summary>预订的专注时段撞上了已有的日程。</summary>
    FocusOverlapsSchedule = 1,

    /// <summary>待办的截止时间落在了忙碌时段里，来不及做。</summary>
    TodoDueInBusyTime = 2,

    /// <summary>安排做待办的时间撞上了日程。</summary>
    PlannedTodoOverlapsSchedule = 3,

    /// <summary>一天安排得太满，几乎没有喘息时间。</summary>
    OverloadedDay = 4,
}

/// <summary>
/// 一条冲突。
/// 「每日工作流展现冲突」就是把这个列表渲染出来。
/// </summary>
public class Conflict
{
    /// <summary>种类。</summary>
    public ConflictKind Kind { get; init; }

    /// <summary>严重程度。</summary>
    public ConflictSeverity Severity { get; init; }

    /// <summary>一句话说明，例如「高等数学 与 线性代数 时间重叠 40 分钟」。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>涉及的条目 Id（可能 1 个或 2 个）。</summary>
    public IReadOnlyList<int> InvolvedSourceIds { get; init; } = Array.Empty<int>();

    /// <summary>冲突发生的时段（用于在时间轴上高亮）。</summary>
    public TimeSpan? Start { get; init; }

    /// <summary>冲突时段的结束。</summary>
    public TimeSpan? End { get; init; }

    /// <summary>重叠了多久。</summary>
    public TimeSpan Overlap => Start.HasValue && End.HasValue ? End.Value - Start.Value : TimeSpan.Zero;

    /// <summary>给界面用的图标字符（FluentAvalonia 的 Symbol 名称由界面层映射，这里只给语义）。</summary>
    public string SeverityText => Severity switch
    {
        ConflictSeverity.Error => "严重",
        ConflictSeverity.Warning => "警告",
        _ => "提示",
    };

    /// <summary>给界面显示的时间文字，例如 "08:40 – 09:20"。</summary>
    public string TimeRangeText => Start.HasValue && End.HasValue
        ? $"{Format(Start.Value)} – {Format(End.Value)}"
        : string.Empty;

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 24 ? "24:00" : $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
}
