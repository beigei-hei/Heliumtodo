// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;

namespace HeliumTodo.Domain.Services;

/// <summary>时间轴上一条内容的种类（决定显示什么颜色和图标）。</summary>
public enum TimelineEntryKind
{
    /// <summary>普通日程。</summary>
    Schedule = 0,

    /// <summary>已安排了具体时间的待办。</summary>
    PlannedTodo = 1,

    /// <summary>预订 / 已完成的专注时段。</summary>
    FocusReservation = 2,

    /// <summary>正在进行中的专注（会画成醒目的竖条）。</summary>
    RunningFocus = 3,
}

/// <summary>
/// 时间轴上的一条矩形块。
///
/// 这就是「错位展示」的核心：
///   <see cref="Lane"/>      它在第几列（0 = 最左）
///   <see cref="LaneCount"/> 它所在的重叠组一共占几列
/// 界面用这两个值算横向位置：
///   宽度 = 100% / LaneCount
///   左边距 = 宽度 * Lane
/// 于是同一时段的日程自然就并排错开了，不会叠在一起。
/// </summary>
public class TimelineEntry
{
    /// <summary>来源对象的 Id（日程 Id 或待办 Id），用于点击跳转。</summary>
    public int SourceId { get; init; }

    /// <summary>种类。</summary>
    public TimelineEntryKind Kind { get; init; }

    /// <summary>标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>备注 / 地点。</summary>
    public string? Notes { get; init; }

    /// <summary>优先级。</summary>
    public Priority Priority { get; init; }

    /// <summary>开始时刻（当天的「几点几分」，用 TimeSpan 表示，例如 8 点 = 08:00:00）。</summary>
    public TimeSpan Start { get; init; }

    /// <summary>结束时刻。</summary>
    public TimeSpan End { get; init; }

    /// <summary>第几列（错位用）。</summary>
    public int Lane { get; set; }

    /// <summary>所在重叠组一共几列（错位用）。</summary>
    public int LaneCount { get; set; } = 1;

    /// <summary>持续时间。</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// 是否和别的条目重叠（即被分到了「要错位」的组里）。
    /// 界面用它来把冲突的块标红。
    /// </summary>
    public bool IsOverlapping => LaneCount > 1;

    /// <summary>
    /// 在一天 24 小时中的纵向位置，0.0 = 00:00，1.0 = 24:00。
    /// 界面拿它乘时间轴总高度，就得到 Y 坐标。
    /// </summary>
    public double TopRatio => Math.Clamp(Start.TotalMinutes / (24 * 60.0), 0, 1);

    /// <summary>在一天 24 小时中的纵向高度比例。</summary>
    public double HeightRatio => Math.Clamp(Duration.TotalMinutes / (24 * 60.0), 0, 1);

    /// <summary>横向位置比例（0.0 = 最左，最大不超过 1 - 宽度）。</summary>
    public double LeftRatio => LaneCount <= 0 ? 0 : (double)Lane / LaneCount;

    /// <summary>横向宽度比例。</summary>
    public double WidthRatio => LaneCount <= 0 ? 1 : 1.0 / LaneCount;

    /// <summary>显示用的时间段文字，例如 "08:00 – 09:40"。</summary>
    public string TimeRangeText => $"{FormatTime(Start)} – {FormatTime(End)}";

    /// <summary>把 TimeSpan 格式化成 HH:mm。</summary>
    private static string FormatTime(TimeSpan t)
    {
        // 24:00 这种边界情况要单独处理，否则会显示成 00:00
        if (t.TotalHours >= 24) return "24:00";
        return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
    }
}

/// <summary>
/// 一天的工作流。
/// 这是「每日信息流」页面唯一需要的数据源：拿着它就能把整天画出来。
/// </summary>
public class DayTimeline
{
    /// <summary>这一天（只用到日期部分）。</summary>
    public DateOnly Date { get; init; }

    /// <summary>所有时间轴条目（已算好 Lane，可直接绑定）。</summary>
    public List<TimelineEntry> Entries { get; init; } = new();

    /// <summary>检测出来的所有冲突。</summary>
    public List<Conflict> Conflicts { get; init; } = new();

    /// <summary>这一天已被占用的总时长。</summary>
    public TimeSpan BusyDuration { get; init; }

    /// <summary>
    /// 空闲时长 = 24 小时 - 已占用。
    /// 注意：这里简化处理，不扣掉睡觉时间，所以是「形式上空闲」。
    /// </summary>
    public TimeSpan FreeDuration => TimeSpan.FromHours(24) - BusyDuration;

    /// <summary>有几个重叠组（也就是时间轴上有几处需要错位展示的地方）。</summary>
    public int OverlapClusterCount => Entries.Where(e => e.IsOverlapping)
                                             .Select(e => e.LaneCount)
                                             .Distinct()
                                             .Count();

    /// <summary>按优先级从高到低排序的条目（用于「今日重点」列表）。</summary>
    public IEnumerable<TimelineEntry> ByPriority =>
        Entries.OrderByDescending(e => e.Priority).ThenBy(e => e.Start);

    /// <summary>是否有严重冲突（界面上会显示红色横幅）。</summary>
    public bool HasCriticalConflict => Conflicts.Any(c => c.Severity == ConflictSeverity.Error);

    /// <summary>概要文字，例如 "8 项安排 · 共 6 小时 30 分 · 2 处冲突"。</summary>
    public string SummaryText
    {
        get
        {
            var busy = BusyDuration.TotalHours >= 1
                ? $"{(int)BusyDuration.TotalHours} 小时 {BusyDuration.Minutes} 分"
                : $"{BusyDuration.Minutes} 分钟";

            var conflictText = Conflicts.Count == 0 ? "无冲突" : $"{Conflicts.Count} 处冲突";
            return $"{Entries.Count} 项安排 · 共 {busy} · {conflictText}";
        }
    }
}
