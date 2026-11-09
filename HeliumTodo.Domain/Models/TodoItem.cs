// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Models;

/// <summary>
/// 待办事项 —— 三大核心功能之二。
/// 表示「要做但还没定时间的某件事」。
///
/// 和日程的区别：
///   · 待办：有截止时间、有优先级，但不占用具体时段。
///   · 日程：占用具体时段，会出现在 24 小时时间轴上。
///   · 两者通过 <see cref="PlannedStart"/> 和 <see cref="EstimatedDuration"/> 联动：
///     给一个待办「安排时间」，就会生成一条 <see cref="ScheduleItem"/>。
/// </summary>
public class TodoItem
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>待办标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>详细说明。</summary>
    public string? Notes { get; set; }

    /// <summary>优先级。数值越大越重要，会影响列表排序和「今日焦点」的挑选。</summary>
    public Priority Priority { get; set; } = Priority.Normal;

    /// <summary>是否已完成。</summary>
    public bool IsCompleted { get; set; }

    /// <summary>完成时间（用于统计「今天完成了多少」）。</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>截止时间，可以为空（表示「不急」）。</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// 预计要花多久做完，例如 45 分钟。
    /// 这个字段是「待办 → 日程」的桥梁：安排时间时用它当默认时长。
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 打算什么时候做（可为空）。
    /// 一旦填了它，界面上就会出现一条对应的日程，于是待办进入了时间轴。
    /// </summary>
    public DateTime? PlannedStart { get; set; }

    /// <summary>所属分类 / 标签，例如「学习」「生活」。暂时是纯文本，方便以后升级成标签表。</summary>
    public string? Category { get; set; }

    /// <summary>创建时间，用于排序。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>排序用的顺序号（拖拽排序时使用，暂时保留）。</summary>
    public int SortOrder { get; set; }

    /// <summary>已经为此待办投入的专注时长（由专注记录累加）。</summary>
    public TimeSpan FocusedDuration { get; set; }

    /// <summary>
    /// 提前多少分钟提醒。null = 不提醒，0 = 到点提醒。
    /// 待办的"点"取 <see cref="DueAt"/>（有截止时间才有意义）；
    /// 只有 <see cref="PlannedStart"/> 时退而用计划开始时间。
    /// </summary>
    public int? ReminderMinutes { get; set; }

    /// <summary>
    /// 这个待办该在哪一刻提醒。
    /// 优先用截止时间；没填截止就用计划开始时间；两个都没有 → 不提醒。
    /// </summary>
    public DateTime? ReminderAt
    {
        get
        {
            if (ReminderMinutes is not { } minutes) return null;

            var anchor = DueAt ?? PlannedStart;
            return anchor?.AddMinutes(-minutes);
        }
    }

    /// <summary>
    /// 是否已经逾期：有截止时间、还没完成、且截止时间已经过去。
    /// </summary>
    public bool IsOverdue => !IsCompleted && DueAt.HasValue && DueAt.Value < DateTime.Now;

    /// <summary>
    /// 是否已经安排了具体时间（即会出现在时间轴上）。
    /// </summary>
    public bool IsScheduled => PlannedStart.HasValue;

    /// <summary>计划结束时间（计划开始 + 预计时长）。</summary>
    public DateTime? PlannedEnd => PlannedStart.HasValue ? PlannedStart.Value + EstimatedDuration : null;

    /// <summary>基础校验。</summary>
    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            error = "待办标题不能为空。";
            return false;
        }

        if (EstimatedDuration <= TimeSpan.Zero)
        {
            error = "预计时长必须大于 0。";
            return false;
        }

        error = null;
        return true;
    }
}
