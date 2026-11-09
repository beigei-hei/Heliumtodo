// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Services;

/// <summary>计时器当前处于哪个阶段。</summary>
public enum FocusPhase
{
    /// <summary>空闲（没有在专注）。</summary>
    Idle = 0,

    /// <summary>正在专注。</summary>
    Focusing = 1,

    /// <summary>正在休息。</summary>
    Break = 2,

    /// <summary>已暂停。</summary>
    Paused = 3,
}

/// <summary>
/// 专注计时器 —— 纯逻辑，不依赖界面。
///
/// 界面上的「时钟样式的专注时长」就是拿它的数据画的。
/// 它自己不带定时器（不用后台线程），而是由界面每隔一小段时间调用一次 <see cref="Tick"/>，
/// 这样逻辑简单、也方便单元测试。
/// </summary>
public sealed class FocusTimerService
{
    private readonly List<TimeSpan> _completedFocusDurations = new();

    /// <summary>当前阶段。</summary>
    public FocusPhase Phase { get; private set; } = FocusPhase.Idle;

    /// <summary>这一轮计划专注多久。</summary>
    public TimeSpan PlannedDuration { get; private set; } = TimeSpan.FromMinutes(25);

    /// <summary>这一轮实际已经过了多久（只算正在专注的时间，暂停时不计）。</summary>
    public TimeSpan Elapsed { get; private set; } = TimeSpan.Zero;

    /// <summary>正在专注的标题。</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>正在推进的待办 Id（可为空）。</summary>
    public int? TodoId { get; private set; }

    /// <summary>被打扰的次数（切出窗口、暂停都算）。</summary>
    public int InterruptionCount { get; private set; }

    /// <summary>剩余时间（专注阶段用，不会小于 0）。</summary>
    public TimeSpan Remaining
    {
        get
        {
            var remain = PlannedDuration - Elapsed;
            return remain < TimeSpan.Zero ? TimeSpan.Zero : remain;
        }
    }

    /// <summary>进度 0.0–1.0（画圆环进度条用）。</summary>
    public double Progress
    {
        get
        {
            if (PlannedDuration <= TimeSpan.Zero) return 0;
            var ratio = Elapsed.TotalSeconds / PlannedDuration.TotalSeconds;
            return Math.Clamp(ratio, 0, 1);
        }
    }

    /// <summary>是否正在跑（只有正在跑才需要界面刷新）。</summary>
    public bool IsActive => Phase is FocusPhase.Focusing or FocusPhase.Break;

    /// <summary>今天累计完成的专注时长。</summary>
    public TimeSpan TodayTotal { get; private set; } = TimeSpan.Zero;

    /// <summary>今天完成了几次专注。</summary>
    public int TodaySessionCount => _completedFocusDurations.Count;

    /// <summary>每次状态变化都会触发，界面据此刷新（重绘时钟、更新按钮文字）。</summary>
    public event EventHandler? StateChanged;

    /// <summary>每一轮专注正常结束时触发（用来存数据库、发通知）。</summary>
    public event EventHandler<FocusCompletedEventArgs>? FocusCompleted;

    /// <summary>
    /// 开始一次专注。
    /// </summary>
    /// <param name="plannedDuration">计划时长。</param>
    /// <param name="title">标题。</param>
    /// <param name="todoId">关联的待办 Id，可为空。</param>
    /// <param name="carryElapsed">从别的计时器接过来的已用时长（比如从预订的专注升级成实时计时）。</param>
    public void Start(TimeSpan plannedDuration, string title = "", int? todoId = null, TimeSpan? carryElapsed = null)
    {
        PlannedDuration = plannedDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(25) : plannedDuration;
        Title = title;
        TodoId = todoId;
        Elapsed = carryElapsed ?? TimeSpan.Zero;
        InterruptionCount = 0;
        Phase = FocusPhase.Focusing;
        RaiseStateChanged();
    }

    /// <summary>暂停（算一次打扰）。</summary>
    public void Pause()
    {
        if (!IsActive) return;

        Phase = FocusPhase.Paused;
        InterruptionCount++;
        RaiseStateChanged();
    }

    /// <summary>继续。</summary>
    public void Resume()
    {
        if (Phase != FocusPhase.Paused) return;

        Phase = FocusPhase.Focusing;
        RaiseStateChanged();
    }

    /// <summary>
    /// 放弃这一轮（不计入统计）。
    /// </summary>
    public FocusCompletedEventArgs? Abandon()
    {
        if (Phase == FocusPhase.Idle) return null;

        var result = new FocusCompletedEventArgs(
            Title, TodoId, PlannedDuration, Elapsed, InterruptionCount, Completed: false);

        ResetToIdle();
        RaiseStateChanged();
        return result;
    }

    /// <summary>
    /// 由界面定时调用，告诉计时器「过了 <paramref name="delta"/> 这么久」。
    /// 只有正在专注时才累加时间。
    /// </summary>
    public void Tick(TimeSpan delta)
    {
        if (Phase != FocusPhase.Focusing) return;

        Elapsed += delta;

        // 到点了：正常完成
        if (Elapsed >= PlannedDuration)
        {
            Elapsed = PlannedDuration;
            CompleteFocus();
            return;
        }

        RaiseStateChanged();
    }

    /// <summary>进入休息阶段。</summary>
    public void StartBreak(TimeSpan breakDuration)
    {
        PlannedDuration = breakDuration;
        Elapsed = TimeSpan.Zero;
        Phase = FocusPhase.Break;
        RaiseStateChanged();
    }

    /// <summary>跳过休息，回到空闲。</summary>
    public void SkipBreak()
    {
        ResetToIdle();
        RaiseStateChanged();
    }

    /// <summary>记一次打扰（自控模块检测到切出窗口时调用）。</summary>
    public void RegisterInterruption()
    {
        InterruptionCount++;
        RaiseStateChanged();
    }

    /// <summary>手动给今天的统计加时长（用于加载历史数据）。</summary>
    public void SeedTodayTotal(TimeSpan total, int sessionCount)
    {
        TodayTotal = total;
        _completedFocusDurations.Clear();
        for (int i = 0; i < sessionCount; i++)
        {
            _completedFocusDurations.Add(TimeSpan.Zero);
        }
        RaiseStateChanged();
    }

    private void CompleteFocus()
    {
        var completed = new FocusCompletedEventArgs(
            Title, TodoId, PlannedDuration, Elapsed, InterruptionCount, Completed: true);

        _completedFocusDurations.Add(Elapsed);
        TodayTotal += Elapsed;

        Phase = FocusPhase.Idle;
        RaiseStateChanged();

        FocusCompleted?.Invoke(this, completed);
    }

    private void ResetToIdle()
    {
        Phase = FocusPhase.Idle;
        Elapsed = TimeSpan.Zero;
        InterruptionCount = 0;
        Title = string.Empty;
        TodoId = null;
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>一次专注结束时的信息。</summary>
public sealed record FocusCompletedEventArgs(
    string Title,
    int? TodoId,
    TimeSpan PlannedDuration,
    TimeSpan ActualDuration,
    int InterruptionCount,
    bool Completed);
