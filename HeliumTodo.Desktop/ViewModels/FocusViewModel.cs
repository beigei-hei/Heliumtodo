// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;
using HeliumTodo.Infrastructure;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 「专注」页面 —— 时钟样式的专注时长。
///
/// 时间显示有两个地方：
///   · 中间的大圆圈是「表盘」，指针随进度转动，一圈 = 一次专注
///   · 表盘中间的大数字是「还剩多少」，精确到秒
///
/// 还负责「预订专注时长」：先约好一个时段，到点儿在「今日工作流」上就能看到；
/// 如果那个时段已经安排了别的日程，时间轴会直接标出冲突。
/// </summary>
public sealed partial class FocusViewModel : ViewModelBase
{
    private readonly FocusRepository _focusRepo;
    private readonly TodoRepository _todoRepo;
    private readonly FocusTimerService _timer;
    private readonly AppState _state;

    /// <summary>界面刷新用的定时器（每 250 毫秒跳一次，秒数看起来才顺滑）。</summary>
    private readonly DispatcherTimer _uiTimer;

    /// <summary>上一次 Tick 的时间，用来算「过了多久」。</summary>
    private DateTime _lastTick;

    /// <summary>本次专注是从哪条预订记录开始的（用来更新那条记录）。</summary>
    private FocusSession? _runningFromReservation;

    public FocusViewModel(IServiceProvider services)
    {
        _focusRepo = (FocusRepository)services.GetService(typeof(FocusRepository))!;
        _todoRepo = (TodoRepository)services.GetService(typeof(TodoRepository))!;
        _timer = (FocusTimerService)services.GetService(typeof(FocusTimerService))!;
        _state = (AppState)services.GetService(typeof(AppState))!;

        _focusMinutes = _state.Config.DefaultFocusMinutes;
        _reserveDate = DateTimeOffset.Now.Date;
        _reserveTime = NextHalfHour();

        _timer.StateChanged += (_, _) => UpdateFromTimer();
        _timer.FocusCompleted += OnFocusCompleted;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _uiTimer.Tick += OnUiTick;

        _state.DataChanged += async (_, type) =>
        {
            if (type.Includes(AppDataType.Focus | AppDataType.Todos))
            {
                await RefreshAsync();
            }
        };
    }

    // ─────────────────────────────────────────────────────────
    // 时钟显示
    // ─────────────────────────────────────────────────────────

    /// <summary>表盘中间的大字：剩余时间（没在计时就显示计划时长）。</summary>
    [ObservableProperty] private string _clockText = "25:00";

    /// <summary>表盘指针进度 0–1。</summary>
    [ObservableProperty] private double _clockProgress;

    /// <summary>一句话状态：「准备开始」/「专注中」/「已暂停」/「休息中」。</summary>
    [ObservableProperty] private string _phaseText = "准备开始";

    /// <summary>当前是否在专注（决定按钮显示「开始」还是「暂停」）。</summary>
    [ObservableProperty] private bool _isRunning;

    /// <summary>当前是否暂停。</summary>
    [ObservableProperty] private bool _isPaused;

    /// <summary>当前是否空闲（空闲时「放弃」按钮要禁用）。</summary>
    [ObservableProperty] private bool _isIdle = true;

    /// <summary>是否在休息阶段。</summary>
    [ObservableProperty] private bool _isBreak;

    /// <summary>本次专注的标题。</summary>
    [ObservableProperty] private string _sessionTitle = string.Empty;

    /// <summary>被打扰次数。</summary>
    [ObservableProperty] private int _interruptionCount;

    /// <summary>计划时长（分钟），开始前可以改。</summary>
    [ObservableProperty] private int _focusMinutes = 25;

    /// <summary>一个更详细的进度说明。</summary>
    [ObservableProperty] private string _progressText = "还没有开始";

    /// <summary>正在为之专注的待办。</summary>
    [ObservableProperty] private TodoItem? _targetTodo;

    partial void OnFocusMinutesChanged(int value)
    {
        // 没在计时的时候，改分钟数要立刻反映到表盘上
        if (IsIdle && !IsBreak)
        {
            ClockText = FormatClock(TimeSpan.FromMinutes(value));
            ClockProgress = 0;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 今日统计
    // ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _todayTotalText = "0 分钟";
    [ObservableProperty] private int _todaySessionCount;
    [ObservableProperty] private string _allTimeTotalText = "0 分钟";
    [ObservableProperty] private string _averageSessionText = "0 分钟";
    [ObservableProperty] private int _interruptionTotal;

    /// <summary>今天的专注记录（倒序）。</summary>
    public ObservableCollection<FocusSession> TodaySessions { get; } = new();

    /// <summary>已预订但还没开始的专注。</summary>
    public ObservableCollection<FocusSession> Reservations { get; } = new();

    /// <summary>可以关联的待办。</summary>
    public ObservableCollection<TodoItem> AvailableTodos { get; } = new();

    // ─────────────────────────────────────────────────────────
    // 预订专注时长
    // ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _reserveTitle = string.Empty;
    [ObservableProperty] private DateTimeOffset? _reserveDate;
    [ObservableProperty] private TimeSpan? _reserveTime;
    [ObservableProperty] private int _reserveMinutes = 45;
    [ObservableProperty] private TodoItem? _reserveTodo;

    /// <summary>预订面板是否展开。</summary>
    [ObservableProperty] private bool _isReservePanelOpen;

    /// <summary>预订成功之类的提示。</summary>
    [ObservableProperty] private string _noticeText = string.Empty;

    [ObservableProperty] private bool _isNoticeVisible;

    // ─────────────────────────────────────────────────────────
    // 命令
    // ─────────────────────────────────────────────────────────

    /// <summary>开始 / 暂停 / 继续（一个按钮轮流承担三种动作）。</summary>
    [RelayCommand]
    private void StartOrPause()
    {
        if (_timer.Phase == FocusPhase.Focusing)
        {
            _timer.Pause();
            return;
        }

        if (_timer.Phase == FocusPhase.Paused)
        {
            _timer.Resume();
            return;
        }

        if (_timer.Phase == FocusPhase.Break)
        {
            _timer.SkipBreak();
            return;
        }

        // 空闲 → 开始一次新的专注
        var minutes = Math.Max(1, FocusMinutes);
        var title = string.IsNullOrWhiteSpace(SessionTitle)
            ? TargetTodo?.Title ?? "专注"
            : SessionTitle;

        _timer.Start(TimeSpan.FromMinutes(minutes), title, TargetTodo?.Id);
        _runningFromReservation = null;

        RestartUiTimer();
    }

    /// <summary>快速设置时长（界面上的 25 / 45 / 60 快捷按钮）。</summary>
    [RelayCommand]
    private void SetPreset(string? minutes)
    {
        if (int.TryParse(minutes, out var value) && value > 0)
        {
            FocusMinutes = value;
        }
    }

    /// <summary>放弃本次专注。</summary>
    [RelayCommand]
    private void Abandon()
    {
        var result = _timer.Abandon();
        if (result is not null)
        {
            ShowNotice($"已放弃本次专注（坚持了 {result.ActualDuration.TotalMinutes:0} 分钟）。");
        }

        _runningFromReservation = null;
        StopUiTimer();
    }

    /// <summary>立即完成（不必等到时间走完）。</summary>
    [RelayCommand]
    private async Task FinishNowAsync()
    {
        if (_timer.Phase == FocusPhase.Idle) return;

        var title = _timer.Title;
        var planned = _timer.PlannedDuration;
        var actual = _timer.Elapsed;
        var interruptions = _timer.InterruptionCount;
        var todoId = _timer.TodoId;

        _timer.SkipBreak();   // 回到空闲

        await SaveFocusRecordAsync(title, planned, actual, interruptions, todoId);
        ShowNotice($"本次专注 {actual.TotalMinutes:0} 分钟已记录。");

        _runningFromReservation = null;
        StopUiTimer();
    }

    /// <summary>把某条预订的专注直接开始。</summary>
    [RelayCommand]
    private void StartReservation(FocusSession? reservation)
    {
        if (reservation is null) return;

        // 如果预订时间还没到，就以「计划时长」直接开始，不强行等到那个点
        _runningFromReservation = reservation;
        TargetTodo = AvailableTodos.FirstOrDefault(t => t.Id == reservation.TodoId);

        _timer.Start(reservation.PlannedDuration, reservation.Title, reservation.TodoId);
        RestartUiTimer();

        ShowNotice($"已开始「{reservation.Title}」。");
    }

    /// <summary>取消一条预订。</summary>
    [RelayCommand]
    private async Task CancelReservationAsync(FocusSession? reservation)
    {
        if (reservation is null) return;

        await _focusRepo.DeleteAsync(reservation.Id);
        ShowNotice("已取消该预订。");
        _state.NotifyChanged(AppDataType.Focus);
    }

    /// <summary>保存一条新的预订。</summary>
    [RelayCommand]
    private async Task SaveReservationAsync()
    {
        var date = (ReserveDate ?? DateTimeOffset.Now.Date).Date;
        var time = ReserveTime ?? NextHalfHour();
        var minutes = Math.Max(1, ReserveMinutes);

        var session = new FocusSession
        {
            Title = string.IsNullOrWhiteSpace(ReserveTitle)
                ? (ReserveTodo?.Title ?? "预订专注")
                : ReserveTitle.Trim(),
            TodoId = ReserveTodo?.Id,
            PlannedStart = date.Add(time),
            PlannedDuration = TimeSpan.FromMinutes(minutes),
            Status = FocusSessionStatus.Reserved,
        };

        if (!session.Validate(out var error))
        {
            ShowNotice(error ?? "预订信息不合法。");
            return;
        }

        await _focusRepo.AddAsync(session);

        ReserveTitle = string.Empty;
        IsReservePanelOpen = false;

        ShowNotice($"已预订 {session.PlannedStart:MM-dd HH:mm} 的 {minutes} 分钟专注，" +
                   "它会出现在「今日工作流」的时间轴上。");

        _state.NotifyChanged(AppDataType.Focus);
    }

    /// <summary>快速预订：按默认时长约在今天下一个整点。</summary>
    [RelayCommand]
    private void QuickReserve()
    {
        ReserveDate = DateTimeOffset.Now.Date;
        ReserveTime = NextHalfHour();
        ReserveMinutes = _state.Config.DefaultFocusMinutes * 2;
        IsReservePanelOpen = true;
    }

    /// <summary>关闭提示条。</summary>
    [RelayCommand]
    private void DismissNotice() => IsNoticeVisible = false;

    // ─────────────────────────────────────────────────────────
    // 计时器驱动的界面刷新
    // ─────────────────────────────────────────────────────────

    private void OnUiTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var delta = now - _lastTick;
        _lastTick = now;

        // 防止电脑休眠后醒来一次性补上几个小时
        if (delta > TimeSpan.FromSeconds(5))
        {
            delta = TimeSpan.FromMilliseconds(250);
        }

        _timer.Tick(delta);
    }

    /// <summary>把计时器的状态搬到界面上。</summary>
    private void UpdateFromTimer()
    {
        IsIdle = _timer.Phase == FocusPhase.Idle;
        IsRunning = _timer.Phase == FocusPhase.Focusing;
        IsPaused = _timer.Phase == FocusPhase.Paused;
        IsBreak = _timer.Phase == FocusPhase.Break;

        InterruptionCount = _timer.InterruptionCount;

        switch (_timer.Phase)
        {
            case FocusPhase.Idle:
                PhaseText = "准备开始";
                ClockText = FormatClock(TimeSpan.FromMinutes(Math.Max(1, FocusMinutes)));
                ClockProgress = 0;
                ProgressText = "还没有开始";
                break;

            case FocusPhase.Focusing:
                PhaseText = "专注中";
                ClockText = FormatClock(_timer.Remaining);
                ClockProgress = _timer.Progress;
                ProgressText = $"已进行 {_timer.Elapsed.TotalMinutes:0.#} / {_timer.PlannedDuration.TotalMinutes:0} 分钟";
                break;

            case FocusPhase.Paused:
                PhaseText = "已暂停";
                ClockText = FormatClock(_timer.Remaining);
                ClockProgress = _timer.Progress;
                ProgressText = "点「继续」接着专注";
                break;

            case FocusPhase.Break:
                PhaseText = "休息中";
                ClockText = FormatClock(_timer.Remaining);
                ClockProgress = _timer.Progress;
                ProgressText = "起来走两步，放松一下眼睛";
                break;
        }
    }

    /// <summary>一次专注正常结束。</summary>
    private async void OnFocusCompleted(object? sender, FocusCompletedEventArgs e)
    {
        // 这是 async void（事件处理器必须如此），异常没有调用方接得住 ——
        // 真抛出去就是整个进程退出。一次专注结束不该有这种杀伤力：
        // 界面提示、自动休息这些后续动作失败了顶多是"少了点反馈"。
        try
        {
            StopUiTimer();

            await SaveFocusRecordAsync(
                e.Title, e.PlannedDuration, e.ActualDuration, e.InterruptionCount, e.TodoId);

            ShowNotice($"完成 {e.ActualDuration.TotalMinutes:0} 分钟专注，休息一下吧。");

            // 按配置自动进入休息
            if (_state.Config.FocusCompletionAlert)
            {
                var breakMinutes = _state.Config.BreakMinutes;
                _timer.StartBreak(TimeSpan.FromMinutes(breakMinutes));
                RestartUiTimer();
            }

            _runningFromReservation = null;
            _state.NotifyChanged(AppDataType.Focus);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[专注] 结束流程出错：{ex}");
            ShowNotice("这次专注结束时的收尾动作出错了，数据可能没记全。");
        }
    }

    /// <summary>把一次专注写进数据库，并累加到对应待办上。</summary>
    private async Task SaveFocusRecordAsync(
        string title, TimeSpan planned, TimeSpan actual, int interruptions, int? todoId)
    {
        try
        {
            var now = DateTime.Now;

            if (_runningFromReservation is not null)
            {
                // 这次专注是从一条预订开始的：更新原来那条记录，而不是新建
                var reservation = _runningFromReservation;
                reservation.StartedAt = now - actual;
                reservation.EndedAt = now;
                reservation.Status = FocusSessionStatus.Completed;
                reservation.InterruptionCount = interruptions;

                await _focusRepo.UpdateAsync(reservation);
            }
            else
            {
                await _focusRepo.AddAsync(new FocusSession
                {
                    Title = title,
                    TodoId = todoId,
                    PlannedStart = now - actual,
                    PlannedDuration = planned,
                    StartedAt = now - actual,
                    EndedAt = now,
                    Status = FocusSessionStatus.Completed,
                    InterruptionCount = interruptions,
                });
            }

            // 关键联动：把这次专注的时长累加到对应的待办上
            if (todoId.HasValue)
            {
                await _todoRepo.AddFocusedTimeAsync(todoId.Value, actual);
            }

            _state.NotifyChanged(AppDataType.Focus);
        }
        catch (Exception ex)
        {
            ShowNotice($"保存专注记录失败：{ex.Message}");
        }
    }

    private void RestartUiTimer()
    {
        _lastTick = DateTime.Now;
        _uiTimer.Start();
    }

    private void StopUiTimer()
    {
        _uiTimer.Stop();
        UpdateFromTimer();
    }

    // ─────────────────────────────────────────────────────────
    // 加载
    // ─────────────────────────────────────────────────────────

    public override async Task OnNavigatedToAsync() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var stats = await _focusRepo.GetStatsAsync();
            TodayTotalText = DashboardViewModel.FormatDuration(stats.Today);
            TodaySessionCount = stats.TodayCount;
            AllTimeTotalText = DashboardViewModel.FormatDuration(stats.AllTime);
            AverageSessionText = stats.TodayCount > 0
                ? DashboardViewModel.FormatDuration(
                    TimeSpan.FromMinutes(stats.Today.TotalMinutes / stats.TodayCount))
                : "0 分钟";

            // 今天的专注记录
            var today = DateOnly.FromDateTime(DateTime.Now);
            var todaySessions = await _focusRepo.GetForDateAsync(today);

            TodaySessions.Clear();
            foreach (var session in todaySessions
                         .Where(s => s.Status == FocusSessionStatus.Completed)
                         .OrderByDescending(s => s.EndedAt ?? s.PlannedStart))
            {
                TodaySessions.Add(session);
            }

            // 被打扰总数
            InterruptionTotal = todaySessions.Sum(s => s.InterruptionCount);

            // 待开始的预订
            var reserved = await _focusRepo.GetReservedAsync();
            Reservations.Clear();
            foreach (var reservation in reserved)
            {
                Reservations.Add(reservation);
            }

            // 可关联的待办
            var todos = await _todoRepo.GetActiveAsync();
            var previousTargetId = TargetTodo?.Id;

            AvailableTodos.Clear();
            foreach (var todo in todos.Take(30))
            {
                AvailableTodos.Add(todo);
            }

            // 尽量保持原来选中的那条
            if (previousTargetId.HasValue)
            {
                TargetTodo = AvailableTodos.FirstOrDefault(t => t.Id == previousTargetId.Value);
            }

            UpdateFromTimer();
        }
        catch (Exception ex)
        {
            ShowNotice($"加载失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 小工具
    // ─────────────────────────────────────────────────────────

    /// <summary>把时长格式化成表盘上显示的 MM:SS（超过一小时显示 H:MM:SS）。</summary>
    private static string FormatClock(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    /// <summary>下一个「半点」时间（例如现在是 14:20，就返回 14:30）。</summary>
    private static TimeSpan NextHalfHour()
    {
        var now = DateTime.Now;
        var next = now.Minute < 30
            ? new DateTime(now.Year, now.Month, now.Day, now.Hour, 30, 0)
            : new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);

        return next.TimeOfDay;
    }

    /// <summary>显示一条提示。</summary>
    private void ShowNotice(string message)
    {
        NoticeText = message;
        IsNoticeVisible = true;
    }
}
