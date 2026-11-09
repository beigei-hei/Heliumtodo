// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;
using HeliumTodo.Infrastructure;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 「每日工作流」页面的 ViewModel —— 软件的门面。
///
/// 这一页把三大核心功能汇总到一条 24 小时时间轴上：
///   · 日程   → 蓝色方块
///   · 待办   → 紫色方块（只显示已安排时间或今天截止的）
///   · 专注   → 金色方块（含预订的专注时长）
///   时间重叠的地方会自动【错位】成左右并排，并生成冲突提醒。
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly ScheduleRepository _scheduleRepo;
    private readonly TodoRepository _todoRepo;
    private readonly FocusRepository _focusRepo;
    private readonly AppState _state;

    /// <summary>一小时的像素高度（要和 TimelineGrid 的默认值保持一致）。</summary>
    public const double HourHeight = 38.0;

    /// <summary>时间轴总高度 = 24 小时 × 每小时高度。</summary>
    /// <summary>
    /// 时间轴上下各留的空白（像素），和 <see cref="Controls.TimelineGrid.VerticalPadding"/> 保持一致。
    /// 不留的话 00:00 / 24:00 的刻度文字会被切掉一半（"0 点顶格、24 点触底"）。
    /// </summary>
    public const double TimelinePadding = 14.0;

    /// <summary>时间轴内容的实际高度：24 小时 + 上下留白。方块与网格共用这个值。</summary>
    public double TimelineHeight => 24 * HourHeight + 2 * TimelinePadding;

    public DashboardViewModel(IServiceProvider services)
    {
        _scheduleRepo = (ScheduleRepository)services.GetService(typeof(ScheduleRepository))!;
        _todoRepo = (TodoRepository)services.GetService(typeof(TodoRepository))!;
        _focusRepo = (FocusRepository)services.GetService(typeof(FocusRepository))!;
        _state = (AppState)services.GetService(typeof(AppState))!;

        // 别的页面改了数据，这里要跟着刷新（三大功能联动的关键）
        _state.DataChanged += async (_, type) =>
        {
            // 只有"待办"这一位发生变化时也要刷新 —— 用 Includes 判断，
            // 别用 HasFlag（后者要求这位之外还在，单条待办变化会漏掉）。
            if (type.Includes(AppDataType.Timeline))
            {
                await RefreshAsync();
            }
        };
    }

    // ─────────────────────────────────────────────────────────
    // 界面绑定的数据
    // ─────────────────────────────────────────────────────────

    /// <summary>时间轴上的所有方块。</summary>
    public ObservableCollection<TimelineEntryViewModel> Entries { get; } = new();

    /// <summary>检测出来的冲突列表。</summary>
    public ObservableCollection<ConflictViewModel> Conflicts { get; } = new();

    /// <summary>左侧时间刻度（每 2 小时一个，够用又不挤）。</summary>
    public ObservableCollection<HourLabel> HourLabels { get; } = new();

    /// <summary>顶部的总结文字，例如「8 项安排 · 共 6 小时 30 分 · 2 处冲突」。</summary>
    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>日期标题，例如「1月6日 星期一」。</summary>
    [ObservableProperty]
    private string _dateText = string.Empty;

    /// <summary>是不是今天（不是今天就不画「现在」的红线）。</summary>
    [ObservableProperty]
    private bool _isToday = true;

    /// <summary>
    /// 当前正在看的日期。
    /// 真正存这个值的地方是 <see cref="AppState.SelectedDate"/>，
    /// 这里只是转出来给界面用（界面代码里 vm.SelectedDate 比 vm.State.SelectedDate 好写）。
    /// </summary>
    public DateOnly SelectedDate => _state.SelectedDate;

    /// <summary>「现在」在一天中的位置（红线用）。</summary>
    [ObservableProperty]
    private TimeSpan _now = DateTime.Now.TimeOfDay;

    // ── 统计卡片 ──

    [ObservableProperty] private string _focusTodayText = "0 分钟";
    [ObservableProperty] private int _focusSessionCount;
    [ObservableProperty] private int _todoDoneToday;
    [ObservableProperty] private int _todoActiveCount;
    [ObservableProperty] private int _conflictCount;
    [ObservableProperty] private string _busyText = "0 分钟";
    [ObservableProperty] private string _freeText = "24 小时";

    /// <summary>今天/这一天的「重点任务」（按优先级排序取前 5 个未完成的）。</summary>
    public ObservableCollection<TodoItem> TopTodos { get; } = new();

    /// <summary>空闲时段建议（可以拿来安排待办）。</summary>
    public ObservableCollection<FreeSlotViewModel> FreeSlots { get; } = new();

    /// <summary>当前时间轴的宽度（由界面在尺寸变化时写进来，用于错位计算）。</summary>
    private double _timelineWidth = 600;


    // ─────────────────────────────────────────────────────────
    // 命令
    // ─────────────────────────────────────────────────────────

    /// <summary>前一天。</summary>
    [RelayCommand]
    private async Task PreviousDayAsync()
    {
        _state.SelectedDate = _state.SelectedDate.AddDays(-1);
        await RefreshAsync();
    }

    /// <summary>后一天。</summary>
    [RelayCommand]
    private async Task NextDayAsync()
    {
        _state.SelectedDate = _state.SelectedDate.AddDays(1);
        await RefreshAsync();
    }

    /// <summary>回到今天。</summary>
    [RelayCommand]
    private async Task GoTodayAsync()
    {
        _state.SelectedDate = DateOnly.FromDateTime(DateTime.Now);
        await RefreshAsync();
    }

    /// <summary>刷新（界面上有刷新按钮）。</summary>
    [RelayCommand]
    private async Task RefreshAllAsync() => await RefreshAsync();

    /// <summary>勾选 / 取消勾选待办（直接在这一页完成，不用跳转）。</summary>
    [RelayCommand]
    private async Task ToggleTodoAsync(TodoItem? todo)
    {
        if (todo is null) return;

        var updated = await _todoRepo.ToggleCompletedAsync(todo.Id);
        if (updated is not null)
        {

            // 通知其它页面（待办页、专注页）一起刷新
            _state.NotifyChanged(AppDataType.Todos);
        }
    }

    /// <summary>把待办直接安排到某个空闲时段（演示「待办 → 日程」的联动）。</summary>
    [RelayCommand]
    private async Task ScheduleTodoAsync(FreeSlotViewModel? slot)
    {
        if (slot?.Todo is null) return;

        var todo = slot.Todo;
        var date = _state.SelectedDate;

        todo.PlannedStart = date.ToDateTime(TimeOnly.MinValue).Add(slot.Start);

        // 如果空闲时段比预计时长还短，就缩短预计时长，保证能塞进去
        var available = slot.End - slot.Start;
        if (todo.EstimatedDuration > available)
        {
            todo.EstimatedDuration = available;
        }

        await _todoRepo.UpdateAsync(todo);
        _state.NotifyChanged(AppDataType.Todos);
        await RefreshAsync();
    }

    // ─────────────────────────────────────────────────────────
    // 加载数据
    // ─────────────────────────────────────────────────────────

    public override async Task OnNavigatedToAsync() => await RefreshAsync();

    /// <summary>重新读数据并重建时间轴。</summary>
    public async Task RefreshAsync()
    {
        IsBusy = true;

        try
        {
            _state.RefreshTodayIfNeeded();

            var date = _state.SelectedDate;
            IsToday = date == DateOnly.FromDateTime(DateTime.Now);
            Now = DateTime.Now.TimeOfDay;
            DateText = FormatDate(date);

            // ① 读三种数据
            var schedules = await _scheduleRepo.GetAllAsync();
            var todos = await _todoRepo.GetAllAsync();
            var focusSessions = await _focusRepo.GetAllAsync();

            // ② 交给算法，得到时间轴 + 冲突
            var timeline = DayTimelineBuilder.Build(date, schedules, todos, focusSessions);

            // ③ 转成界面能直接用的方块
            var conflictedIds = timeline.Conflicts
                .SelectMany(c => c.InvolvedSourceIds)
                .ToHashSet();

            Entries.Clear();
            foreach (var entry in timeline.Entries)
            {
                var vm = new TimelineEntryViewModel(entry, conflictedIds.Contains(entry.SourceId));
                vm.Resize(_timelineWidth, TimelineHeight, TimelinePadding, TimelineMetrics.LeftInset);
                Entries.Add(vm);
            }

            Conflicts.Clear();
            foreach (var conflict in timeline.Conflicts)
            {
                Conflicts.Add(new ConflictViewModel(conflict));
            }

            // ④ 刻度
            BuildHourLabels();

            // ⑤ 汇总数字
            SummaryText = timeline.SummaryText;
            ConflictCount = timeline.Conflicts.Count;
            BusyText = FormatDuration(timeline.BusyDuration);
            FreeText = FormatDuration(timeline.FreeDuration);

            // ⑥ 今天的统计（用真实「今天」，不受翻页影响）
            var stats = await _focusRepo.GetStatsAsync();
            FocusTodayText = FormatDuration(stats.Today);
            FocusSessionCount = stats.TodayCount;

            var todoStats = await _todoRepo.GetStatsAsync();
            TodoDoneToday = todoStats.CompletedToday;
            TodoActiveCount = todoStats.Active;

            // ⑦ 重点任务：优先显示「今天要做的」
            TopTodos.Clear();
            var todayTodos = todos
                .Where(t => !t.IsCompleted)
                .Where(t => (t.DueAt.HasValue && DateOnly.FromDateTime(t.DueAt.Value) == date)
                            || (t.PlannedStart.HasValue && DateOnly.FromDateTime(t.PlannedStart.Value) == date))
                .ToList();

            // 今天没有特别的，就显示全部未完成的靠前的几个
            var source = todayTodos.Count > 0 ? todayTodos : todos.Where(t => !t.IsCompleted).ToList();

            foreach (var todo in source
                         .OrderByDescending(t => t.Priority)
                         .ThenBy(t => t.DueAt ?? DateTime.MaxValue)
                         .Take(5))
            {
                TopTodos.Add(todo);
            }

            // ⑧ 空闲时段建议（从「现在」开始找，最短 30 分钟）
            FreeSlots.Clear();
            var fromTime = IsToday ? DateTime.Now.TimeOfDay : TimeSpan.Zero;

            var freeSlots = DayTimelineBuilder.FindFreeSlots(
                timeline.Entries, fromTime, TimeSpan.FromMinutes(30));

            // 拿两个最靠前的未安排待办，用来演示「一键安排」
            var unscheduled = todos
                .Where(t => !t.IsCompleted && !t.PlannedStart.HasValue)
                .OrderByDescending(t => t.Priority)
                .ToList();

            var slotIndex = 0;
            foreach (var (start, end) in freeSlots.Take(5))
            {
                var suggestion = slotIndex < unscheduled.Count ? unscheduled[slotIndex] : null;
                FreeSlots.Add(new FreeSlotViewModel(start, end, suggestion));
                slotIndex++;
            }

        }
        catch (Exception ex)
        {
            // 出错时给用户看得懂的提示，而不是白屏
            SummaryText = $"加载失败：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[Dashboard] 刷新失败：{ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 界面尺寸变化时调用：把所有方块重新摆一遍。
    /// 这就是「响应式」错位布局的关键 —— 窗口变宽时，并排的方块也一起变宽。
    /// </summary>
    public void ResizeTimeline(double width)
    {
        if (width <= 0) return;

        _timelineWidth = width;

        foreach (var entry in Entries)
        {
            entry.Resize(_timelineWidth, TimelineHeight, TimelinePadding, TimelineMetrics.LeftInset);
        }
    }

    /// <summary>生成左侧的时间刻度：每 2 小时一个。</summary>
    private void BuildHourLabels()
    {
        // 只在第一次生成，之后不用重复创建（刻度是固定的）
        if (HourLabels.Count > 0) return;

        for (int hour = 0; hour <= 24; hour += 2)
        {
            // Top = 这个时刻在时间轴上的纵向位置（像素）。
            // 界面把它绑到 Canvas.Top 上，外面再套一个「跟随滚动」的垂直平移。
            HourLabels.Add(new HourLabel(hour, $"{hour:D2}:00", hour * HourHeight));
        }
    }

    /// <summary>
    /// 算出时间轴应该滚到哪个位置（像素）。
    ///
    /// 为什么要这个？
    ///   一天 24 小时全长 24 × 38 = 912 像素，而可视区域只有 330 像素左右。
    ///   如果停在最上面，用户看到的是凌晨 0 点，今天的安排全在下面看不见 ——
    ///   得手动往下拉半天，体验很差。
    ///
    /// 规则：
    ///   · 看「今天」  → 定位到当前时刻，并往上留 <see cref="ScrollContextHours"/> 小时，
    ///                   这样刚过去的事也看得到
    ///   · 看别的日期  → 定位到第一个有安排的时刻；完全没安排就停在早上 7 点
    /// </summary>
    /// <param name="viewportHeight">可视区域高度（像素），从界面传进来。</param>
    public double GetScrollOffset(double viewportHeight)
    {
        if (viewportHeight <= 0) return 0;

        // ── 决定「想让它停在几点」──
        double focusHour;

        if (IsToday)
        {
            focusHour = DateTime.Now.TimeOfDay.TotalHours;
        }
        else
        {
            var firstEntry = Entries
                .OrderBy(e => e.Y)
                .FirstOrDefault();

            // 没有安排就停在早上 7 点（一天的开始，比较自然）
            // Y 里含上面那圈留白，减掉才是真正的小时数
            focusHour = firstEntry is null ? 7 : (firstEntry.Y - TimelinePadding) / HourHeight;
        }

        // 往上留一点上下文，别让关注点贴在顶部
        const double ScrollContextHours = 1.5;
        var targetY = TimelinePadding + (focusHour - ScrollContextHours) * HourHeight;

        // 尽量把这个时刻放到可视区域的中间偏上（1/3 处），看起来最舒服
        targetY -= viewportHeight / 3.0;

        var maxOffset = Math.Max(0, TimelineHeight - viewportHeight);
        return Math.Clamp(targetY, 0, maxOffset);
    }

    /// <summary>把日期格式化成「1月6日 星期一」。</summary>
    private static string FormatDate(DateOnly date)
    {
        var weekday = date.DayOfWeek switch
        {
            DayOfWeek.Monday => "星期一",
            DayOfWeek.Tuesday => "星期二",
            DayOfWeek.Wednesday => "星期三",
            DayOfWeek.Thursday => "星期四",
            DayOfWeek.Friday => "星期五",
            DayOfWeek.Saturday => "星期六",
            _ => "星期日",
        };

        var today = DateOnly.FromDateTime(DateTime.Now);
        var suffix = date == today ? "  ·  今天"
            : date == today.AddDays(1) ? "  ·  明天"
            : date == today.AddDays(-1) ? "  ·  昨天"
            : string.Empty;

        return $"{date.Month}月{date.Day}日  {weekday}{suffix}";
    }

    /// <summary>把时长格式化成「2 小时 30 分」这种好读的文字。</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return "0 分钟";

        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;

        if (hours == 0) return $"{minutes} 分钟";
        if (minutes == 0) return $"{hours} 小时";
        return $"{hours} 小时 {minutes} 分";
    }
}

/// <summary>
/// 左侧时间轴的一个刻度。
/// </summary>
/// <param name="Hour">整点小时数。</param>
/// <param name="Text">显示的文字，例如 "08:00"。</param>
/// <param name="Top">这一时刻在时间轴上的纵向位置（像素），界面绑到 Canvas.Top。</param>
public sealed record HourLabel(int Hour, string Text, double Top);

/// <summary>冲突列表里的一条。</summary>
public sealed class ConflictViewModel
{
    public ConflictViewModel(Conflict conflict)
    {
        Message = conflict.Message;
        KindText = conflict.Kind switch
        {
            ConflictKind.ScheduleOverlap => "日程重叠",
            ConflictKind.FocusOverlapsSchedule => "专注冲突",
            ConflictKind.TodoDueInBusyTime => "截止风险",
            ConflictKind.PlannedTodoOverlapsSchedule => "安排冲突",
            ConflictKind.OverloadedDay => "安排过满",
            _ => "提示",
        };

        SeverityText = conflict.SeverityText;
        TimeRangeText = conflict.TimeRangeText;
        Severity = conflict.Severity;

        ResourceKey = conflict.Severity switch
        {
            ConflictSeverity.Error => "ConflictError",
            ConflictSeverity.Warning => "ConflictWarning",
            _ => "ConflictInfo",
        };

        Icon = conflict.Severity switch
        {
            ConflictSeverity.Error => "\u2716",   // ✖
            ConflictSeverity.Warning => "\u26A0", // ⚠
            _ => "\u2139",                        // ℹ
        };
    }

    /// <summary>一句话说明。</summary>
    public string Message { get; }

    /// <summary>冲突种类的中文名。</summary>
    public string KindText { get; }

    /// <summary>严重程度文字。</summary>
    public string SeverityText { get; }

    /// <summary>冲突时段。</summary>
    public string TimeRangeText { get; }

    /// <summary>严重程度。</summary>
    public ConflictSeverity Severity { get; }

    /// <summary>图标字符。</summary>
    public string Icon { get; }

    /// <summary>颜色资源键前缀，界面据此取前景色 / 背景色。</summary>
    public string ResourceKey { get; }

    /// <summary>是否严重（界面上会加粗）。</summary>
    public bool IsSevere => Severity == ConflictSeverity.Error;
}

/// <summary>一个空闲时段（可以点一下把待办安排进去）。</summary>
public sealed class FreeSlotViewModel
{
    public FreeSlotViewModel(TimeSpan start, TimeSpan end, TodoItem? todo)
    {
        Start = start;
        End = end;
        Todo = todo;
    }

    /// <summary>空闲开始。</summary>
    public TimeSpan Start { get; }

    /// <summary>空闲结束。</summary>
    public TimeSpan End { get; }

    /// <summary>建议安排进来的待办（可能为空）。</summary>
    public TodoItem? Todo { get; }

    /// <summary>时长。</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>时间文字，例如 "14:00 – 16:00"。</summary>
    public string TimeRangeText => $"{Format(Start)} – {Format(End)}";

    /// <summary>时长文字，例如「空闲 2 小时」。</summary>
    public string DurationText => "空闲 " + DashboardViewModel.FormatDuration(Duration);

    /// <summary>建议文字。</summary>
    public string SuggestionText => Todo is null
        ? "可以安排一段专注"
        : $"可安排「{Todo.Title}」（预计 {Todo.EstimatedDuration.TotalMinutes:0} 分钟）";

    /// <summary>能不能点「安排」按钮。</summary>
    public bool CanSchedule => Todo is not null;

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 24 ? "24:00" : $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
}
