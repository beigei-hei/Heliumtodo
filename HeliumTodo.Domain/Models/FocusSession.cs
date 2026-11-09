// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Models;

/// <summary>
/// 专注记录 —— 三大核心功能之三。
/// 表示「我打算专心做某件事 N 分钟」以及「实际做了多久」。
///
/// 它同时承担两件事：
///   · 计时：<see cref="StartedAt"/> / <see cref="EndedAt"/> / <see cref="ActualDuration"/>
///   · 预订：<see cref="PlannedStart"/> + <see cref="PlannedDuration"/>（见下面的状态说明）
///
/// 状态流转：
///   预订(Reserved) ──开始计时──▶ 进行中(Running) ──结束──▶ 已完成(Completed)
///                                        └──放弃──▶ 已放弃(Abandoned)
/// </summary>
public class FocusSession
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>标题，例如「复习线性代数」。不填时界面会显示「专注」。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>关联的待办（可选）。填了就说明这次专注是在推进那个待办。</summary>
    public int? TodoId { get; set; }

    /// <summary>导航属性：关联的待办对象。</summary>
    public TodoItem? Todo { get; set; }

    /// <summary>
    /// 预订开始的时刻。
    ///   · 如果是「直接开始计时」，这里等于实际开始时间。
    ///   · 如果是「预订专注时长」，这里是将来的某个时间点。
    /// </summary>
    public DateTime PlannedStart { get; set; }

    /// <summary>计划专注多久（例如 25 分钟番茄钟）。</summary>
    public TimeSpan PlannedDuration { get; set; } = TimeSpan.FromMinutes(25);

    /// <summary>实际开始计时的时刻（还没开始时为 null）。</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>实际结束的时刻。</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>状态。</summary>
    public FocusSessionStatus Status { get; set; } = FocusSessionStatus.Reserved;

    /// <summary>打扰次数（自控模块统计：切出窗口、手动暂停都会 +1）。</summary>
    public int InterruptionCount { get; set; }

    /// <summary>备注。</summary>
    public string? Notes { get; set; }

    /// <summary>是否属于自习室（多人联机自习）。为 true 时界面会显示自习室标记。</summary>
    public bool IsStudyRoomSession { get; set; }

    /// <summary>自习室房间码（无中心服务器方案下，就是房间的唯一标识）。</summary>
    public string? StudyRoomCode { get; set; }

    /// <summary>计划结束时刻。</summary>
    public DateTime PlannedEnd => PlannedStart + PlannedDuration;

    /// <summary>
    /// 实际专注了多久。
    /// 优先用「结束 - 开始」；如果还在计时，就返回 null（由界面用当前时间临时计算）。
    /// </summary>
    public TimeSpan? ActualDuration =>
        StartedAt.HasValue && EndedAt.HasValue ? EndedAt.Value - StartedAt.Value : null;

    /// <summary>
    /// 专注有效率：实际时长 / 计划时长，最大 1.0。
    /// 用来在统计里显示「这次专注的完成度」。
    /// </summary>
    public double CompletionRatio
    {
        get
        {
            if (PlannedDuration <= TimeSpan.Zero) return 0;
            var actual = ActualDuration ?? TimeSpan.Zero;
            return Math.Clamp(actual.TotalMinutes / PlannedDuration.TotalMinutes, 0, 1);
        }
    }

    /// <summary>是否正在进行中（界面用它决定要不要显示跳动的大时钟）。</summary>
    public bool IsRunning => Status == FocusSessionStatus.Running;

    /// <summary>基础校验。</summary>
    public bool Validate(out string? error)
    {
        if (PlannedDuration <= TimeSpan.Zero)
        {
            error = "专注时长必须大于 0。";
            return false;
        }

        if (PlannedDuration > TimeSpan.FromHours(8))
        {
            error = "单次专注不能超过 8 小时（请拆成几次）。";
            return false;
        }

        error = null;
        return true;
    }
}

/// <summary>
/// 专注记录的状态。
/// </summary>
public enum FocusSessionStatus
{
    /// <summary>已预订，还没开始。</summary>
    Reserved = 0,

    /// <summary>正在计时。</summary>
    Running = 1,

    /// <summary>已完成（正常结束）。</summary>
    Completed = 2,

    /// <summary>已放弃（中途取消）。</summary>
    Abandoned = 3,
}
