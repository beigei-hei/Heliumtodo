// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Models;

/// <summary>
/// 日程 —— 三大核心功能之一。
/// 表示「某天某个时间段要做什么」，例如「高等数学课 08:00–09:40」。
///
/// 设计要点：
///   · 用 <see cref="Start"/> + <see cref="Duration"/> 表示时间，
///     而不是开始/结束两个字段 —— 结束时间由计算得出，不会出现「结束早于开始」的脏数据。
///   · 用 <see cref="RecurrenceRule"/> 字符串支持重复（例如每周一），
///     具体展开逻辑放在 <see cref="Services.RecurrenceExpander"/> 里。
/// </summary>
public class ScheduleItem
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>标题，例如「高等数学」。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>备注 / 地点，例如「三教 305」。</summary>
    public string? Notes { get; set; }

    /// <summary>开始时间（本地时间）。</summary>
    public DateTime Start { get; set; }

    /// <summary>持续时长。约束：必须大于 0（在 <see cref="Validate"/> 里检查）。</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>优先级，影响时间轴上的颜色与高度。</summary>
    public Priority Priority { get; set; } = Priority.Normal;

    /// <summary>状态。已取消的日程不参与冲突检测。</summary>
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Planned;

    /// <summary>时段类型（上午/下午/晚上），可以留空。</summary>
    public TimeBlock Block { get; set; } = TimeBlock.Any;

    /// <summary>
    /// 重复规则。空字符串 = 只发生一次。
    /// 支持两种简单写法（够用且好懂）：
    ///   "daily"           每天
    ///   "weekly:1,3,5"    每周一、三、五（0=周日，1=周一 … 6=周六）
    /// </summary>
    public string RecurrenceRule { get; set; } = string.Empty;

    /// <summary>如果这条日程是「执行某个待办」产生的，这里记录待办的 Id。</summary>
    public int? LinkedTodoId { get; set; }

    /// <summary>
    /// 提前多少分钟提醒。null = 不提醒，0 = 开始时提醒，其他 = 开始前 N 分钟。
    /// 具体怎么把分钟数变成"提前 1 小时 30 分钟"这种文字，见 <see cref="ReminderPresets"/>。
    /// </summary>
    public int? ReminderMinutes { get; set; }

    /// <summary>该提醒的时刻（不提醒就是 null）。后台提醒服务用它算"到点没有"。</summary>
    public DateTime? ReminderAt =>
        ReminderMinutes is { } minutes ? Start.AddMinutes(-minutes) : null;

    /// <summary>结束时间（只读，由开始时间 + 时长算出）。</summary>
    public DateTime End => Start + Duration;

    /// <summary>
    /// 判断这条日程是否算「活跃」—— 只有活跃的日程才参与冲突检测。
    /// </summary>
    public bool IsActive => Status != ScheduleStatus.Cancelled;

    /// <summary>
    /// 基础校验：把明显不合理的日程挡在数据库外面。
    /// 返回 false 时 <paramref name="error"/> 里是给人看的中文提示。
    /// </summary>
    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            error = "日程标题不能为空。";
            return false;
        }

        if (Duration <= TimeSpan.Zero)
        {
            error = "日程时长必须大于 0。";
            return false;
        }

        if (Duration > TimeSpan.FromHours(24))
        {
            error = "单条日程不能超过 24 小时（跨天请拆成两条）。";
            return false;
        }

        error = null;
        return true;
    }
}
