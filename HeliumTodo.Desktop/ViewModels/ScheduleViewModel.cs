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
/// 「日程」页面。
///
/// 日程是三大核心功能里「占时间」的那一个：
///   日程 ──决定哪些时段是忙的──▶ 今日工作流（时间轴）
///   日程 ──撞上待办截止时间──▶ 生成冲突提醒
///   日程 ──挤掉预订的专注──▶ 生成专注冲突
/// </summary>
public sealed partial class ScheduleViewModel : ViewModelBase
{
    private readonly ScheduleRepository _scheduleRepo;
    private readonly AppState _state;

    public ScheduleViewModel(IServiceProvider services)
    {
        _scheduleRepo = (ScheduleRepository)services.GetService(typeof(ScheduleRepository))!;
        _state = (AppState)services.GetService(typeof(AppState))!;

        _startDate = DateTimeOffset.Now.Date;
        _startTime = new TimeSpan(8, 0, 0);

        _state.DataChanged += async (_, type) =>
        {
            if (type.Includes(AppDataType.Schedules)) await RefreshAsync();
        };
    }

    // ─────────────────────────────────────────────────────────
    // 列表
    // ─────────────────────────────────────────────────────────

    /// <summary>从今天起接下来两周内的日程（重复日程已按规则展开）。</summary>
    public ObservableCollection<ScheduleOccurrence> Upcoming { get; } = new();

    /// <summary>按日期分组的显示用数据。</summary>
    public ObservableCollection<ScheduleDayGroup> DayGroups { get; } = new();

    [ObservableProperty] private string _summaryText = string.Empty;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private int _weekCount;
    [ObservableProperty] private string _busyTodayText = "0 分钟";

    /// <summary>
    /// 「接下来两周还没有安排」这类空列表提示。
    ///
    /// 注意它和 <see cref="ErrorMessage"/> 是两回事：
    ///   空列表提示 —— 列表真的没内容时显示，属于正常状态
    ///   错误提示   —— 用户输入不合法时显示，属于异常状态
    /// 以前这两个共用一个属性，结果列表里有数据时也会显示空列表提示。
    /// </summary>
    public string EmptyHint => Upcoming.Count == 0 ? "接下来还没有安排。" : string.Empty;

    /// <summary>输入不合法时的错误提示（为空表示没有错误）。</summary>
    [ObservableProperty] private string? _errorMessage;

    /// <summary>列表内容变了，重新算一下空列表提示。</summary>
    private void NotifyEmptyHintChanged() => OnPropertyChanged(nameof(EmptyHint));

    /// <summary>接下来看几天。</summary>
    [ObservableProperty] private int _lookaheadDays = 14;

    /// <summary>可选的查看范围（天）。</summary>
    public IReadOnlyList<int> LookaheadOptions { get; } = new[] { 7, 14, 30 };

    /// <summary>
    /// 重复方式的下拉选项。
    ///
    /// 为什么要做成一个有 Key/Label 的小对象？
    ///   因为下拉框要同时显示中文（不重复）和保存英文键（none）。
    ///   用 ItemsSource + SelectedValueBinding 就能两全其美，
    ///   比在 XAML 里塞 ComboBoxItem 再读 Tag 可靠得多。
    /// </summary>
    public IReadOnlyList<RecurrenceOption> RecurrenceOptions { get; } = new[]
    {
        new RecurrenceOption("none", "不重复"),
        new RecurrenceOption("daily", "每天"),
        new RecurrenceOption("weekly", "每周"),
    };

    partial void OnLookaheadDaysChanged(int value)
    {
        // 改范围要重新展开日程（不只是重新分组）
        _ = RefreshAsync();
    }

    // ─────────────────────────────────────────────────────────
    // 新建 / 编辑
    // ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _newNotes = string.Empty;
    [ObservableProperty] private Priority _newPriority = Priority.Normal;
    [ObservableProperty] private TimeBlock _newBlock = TimeBlock.Morning;

    /// <summary>开始日期。</summary>
    [ObservableProperty] private DateTimeOffset? _startDate;

    /// <summary>开始时刻。</summary>
    [ObservableProperty] private TimeSpan? _startTime;

    /// <summary>持续多少分钟。</summary>
    [ObservableProperty] private int _durationMinutes = 100;

    /// <summary>
    /// 重复方式。
    /// 和 <see cref="RecurrenceExpander"/> 支持的格式对应：
    ///   none / daily / weekly（配合星期几复选框）
    /// </summary>
    [ObservableProperty] private string _recurrenceKind = "none";

    /// <summary>周一 ~ 周日，选中表示每周重复。</summary>
    [ObservableProperty] private bool _monday;
    [ObservableProperty] private bool _tuesday;
    [ObservableProperty] private bool _wednesday;
    [ObservableProperty] private bool _thursday;
    [ObservableProperty] private bool _friday;
    [ObservableProperty] private bool _saturday;
    [ObservableProperty] private bool _sunday;

    /// <summary>是不是每周重复（界面上决定要不要显示星期几复选框）。</summary>
    public bool IsWeekly => RecurrenceKind == "weekly";

    partial void OnRecurrenceKindChanged(string value) => OnPropertyChanged(nameof(IsWeekly));

    /// <summary>正在编辑的日程（null = 新建）。</summary>
    [ObservableProperty] private ScheduleItem? _editingItem;

    /// <summary>
    /// 新建 / 编辑区是不是展开着。
    /// 默认收起：这一页的主视觉是「接下来几天的日程」，表单一直摊开会把列表挤到半屏。
    /// 点卡片信息栏的「＋ 新建」或某条日程的铅笔按钮才展开（后者见 <see cref="Edit"/>）。
    /// </summary>
    [ObservableProperty] private bool _isEditorOpen;

    public bool IsEditing => EditingItem is not null;
    public string EditorTitle => IsEditing ? "编辑日程" : "新建日程";

    partial void OnEditingItemChanged(ScheduleItem? value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
    }

    public IReadOnlyList<Priority> PriorityOptions { get; } = new[]
    {
        Priority.Low, Priority.Normal, Priority.High, Priority.Urgent,
    };

    public IReadOnlyList<TimeBlock> BlockOptions { get; } = new[]
    {
        TimeBlock.Any, TimeBlock.Morning, TimeBlock.Afternoon, TimeBlock.Evening,
    };

    /// <summary>把重复方式拼成算法能认的规则字符串。</summary>
    private string BuildRecurrenceRule()
    {
        if (RecurrenceKind == "daily") return "daily";

        if (RecurrenceKind != "weekly") return string.Empty;

        var days = new List<int>();
        if (Monday) days.Add(1);
        if (Tuesday) days.Add(2);
        if (Wednesday) days.Add(3);
        if (Thursday) days.Add(4);
        if (Friday) days.Add(5);
        if (Saturday) days.Add(6);
        if (Sunday) days.Add(0);

        // 一个星期都没勾，就当成「不重复」
        return days.Count == 0 ? string.Empty : "weekly:" + string.Join(",", days);
    }

    /// <summary>把规则字符串还原到界面的复选框上。</summary>
    private void LoadRecurrenceRule(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            RecurrenceKind = "none";
            Monday = Tuesday = Wednesday = Thursday = Friday = Saturday = Sunday = false;
            return;
        }

        if (rule.Trim().Equals("daily", StringComparison.OrdinalIgnoreCase))
        {
            RecurrenceKind = "daily";
            return;
        }

        RecurrenceKind = "weekly";
        var days = RecurrenceExpander.Parse(rule);

        Monday = days.Contains(DayOfWeek.Monday);
        Tuesday = days.Contains(DayOfWeek.Tuesday);
        Wednesday = days.Contains(DayOfWeek.Wednesday);
        Thursday = days.Contains(DayOfWeek.Thursday);
        Friday = days.Contains(DayOfWeek.Friday);
        Saturday = days.Contains(DayOfWeek.Saturday);
        Sunday = days.Contains(DayOfWeek.Sunday);
    }

    /// <summary>保存（新建或修改）。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
        {
            ErrorMessage = "标题不能为空。";
            return;
        }

        var date = (StartDate ?? DateTimeOffset.Now.Date).Date;
        var time = StartTime ?? new TimeSpan(8, 0, 0);
        var start = date.Add(time);

        var item = EditingItem ?? new ScheduleItem();

        item.Title = NewTitle.Trim();
        item.Notes = string.IsNullOrWhiteSpace(NewNotes) ? null : NewNotes.Trim();
        item.Start = start;
        item.Duration = TimeSpan.FromMinutes(Math.Max(1, DurationMinutes));
        item.Priority = NewPriority;
        item.Block = NewBlock;
        item.RecurrenceRule = BuildRecurrenceRule();
        item.ReminderMinutes = Reminder.LeadMinutes;

        // 用模型自带的校验拦掉明显不合理的输入
        if (!item.Validate(out var error))
        {
            ErrorMessage = error ?? "输入不合法。";
            return;
        }

        if (EditingItem is null)
        {
            await _scheduleRepo.AddAsync(item);
        }
        else
        {
            await _scheduleRepo.UpdateAsync(item);
            EditingItem = null;
        }

        NewTitle = string.Empty;
        NewNotes = string.Empty;
        Reminder.SetFrom(null);
        ErrorMessage = null;

        // 存完把表单收起来，视线回到列表上 —— 不然连着建三条之后，
        // 用户会觉得"点保存好像没反应"（其实只是列表被表单挡住了）
        IsEditorOpen = false;

        _state.NotifyChanged(AppDataType.Schedules);
    }

    /// <summary>
    /// 编辑区里的「提醒」选择器。
    /// 和待办页共用同一个 <see cref="ReminderEditState"/>，保证两边选项与换算完全一致。
    /// </summary>
    public ReminderEditState Reminder { get; } = new();

    /// <summary>取消编辑。</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        EditingItem = null;
        NewTitle = string.Empty;
        NewNotes = string.Empty;
        DurationMinutes = 100;
        RecurrenceKind = "none";
        Reminder.SetFrom(null);
        Monday = Tuesday = Wednesday = Thursday = Friday = Saturday = Sunday = false;
    }

    /// <summary>进入编辑模式。</summary>
    [RelayCommand]
    private void Edit(ScheduleOccurrence? occurrence)
    {
        var item = occurrence?.Source;
        if (item is null) return;

        EditingItem = item;
        NewTitle = item.Title;
        NewNotes = item.Notes ?? string.Empty;
        NewPriority = item.Priority;
        NewBlock = item.Block;
        StartDate = item.Start.Date;
        StartTime = item.Start.TimeOfDay;
        DurationMinutes = (int)Math.Max(1, item.Duration.TotalMinutes);
        LoadRecurrenceRule(item.RecurrenceRule);
        Reminder.SetFrom(item.ReminderMinutes);
        IsEditorOpen = true;
    }

    /// <summary>删除一条日程。</summary>
    [RelayCommand]
    private async Task DeleteAsync(ScheduleOccurrence? occurrence)
    {
        if (occurrence?.Source is null) return;

        await _scheduleRepo.DeleteAsync(occurrence.Source.Id);
        _state.NotifyChanged(AppDataType.Schedules);
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
            var all = await _scheduleRepo.GetAllAsync();
            var today = DateOnly.FromDateTime(DateTime.Now);

            // 把接下来 N 天里会发生的日程全部展开
            var occurrences = new List<ScheduleOccurrence>();

            for (int offset = 0; offset < LookaheadDays; offset++)
            {
                var date = today.AddDays(offset);

                foreach (var item in all)
                {
                    if (!item.IsActive) continue;
                    if (!RecurrenceExpander.OccursOn(item.Start, item.RecurrenceRule, date)) continue;

                    occurrences.Add(new ScheduleOccurrence(
                        item,
                        date,
                        date.ToDateTime(TimeOnly.MinValue).Add(item.Start.TimeOfDay)));
                }
            }

            Upcoming.Clear();
            foreach (var occurrence in occurrences.OrderBy(o => o.Start))
            {
                Upcoming.Add(occurrence);
            }

            // 统计
            TodayCount = occurrences.Count(o => o.Date == today);
            WeekCount = occurrences.Count(o => o.Date < today.AddDays(7));

            var busyToday = occurrences
                .Where(o => o.Date == today)
                .Aggregate(TimeSpan.Zero, (sum, o) => sum + o.Source.Duration);
            BusyTodayText = DashboardViewModel.FormatDuration(busyToday);

            SummaryText = $"接下来 {LookaheadDays} 天共 {occurrences.Count} 项安排" +
                          $"，其中今天 {TodayCount} 项。";

            BuildGroups();
            NotifyEmptyHintChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>把列表按日期分组，界面上显示成「几月几日 星期几」+ 下面一串日程。</summary>
    private void BuildGroups()
    {
        DayGroups.Clear();

        foreach (var group in Upcoming.GroupBy(o => o.Date).OrderBy(g => g.Key))
        {
            DayGroups.Add(new ScheduleDayGroup(group.Key, group.OrderBy(o => o.Start).ToList()));
        }
    }
}

/// <summary>
/// 一条「某天发生的日程」。
///
/// 为什么要这个包装？
///   一条每周一三五的日程，在两周里会发生 6 次。
///   列表里要显示 6 行，但底层其实是同一条日程记录。
///   这个类就把「哪一个日程」和「哪一天发生」放在一起。
/// </summary>
public sealed class ScheduleOccurrence
{
    public ScheduleOccurrence(ScheduleItem source, DateOnly date, DateTime start)
    {
        Source = source;
        Date = date;
        Start = start;
    }

    /// <summary>原始日程记录。</summary>
    public ScheduleItem Source { get; }

    /// <summary>发生在哪一天。</summary>
    public DateOnly Date { get; }

    /// <summary>这一天的具体开始时间（重复日程每天都不同）。</summary>
    public DateTime Start { get; }

    /// <summary>结束时间。</summary>
    public DateTime End => Start + Source.Duration;

    /// <summary>标题。</summary>
    public string Title => Source.Title;

    /// <summary>备注 / 地点。</summary>
    public string? Notes => Source.Notes;

    /// <summary>优先级。</summary>
    public Priority Priority => Source.Priority;

    /// <summary>时间段文字，例如 "08:00 – 09:40"。</summary>
    public string TimeRangeText => $"{Start:HH:mm} – {End:HH:mm}";

    /// <summary>时长文字，例如 "1 小时 40 分"。</summary>
    public string DurationText => DashboardViewModel.FormatDuration(Source.Duration);

    /// <summary>重复方式的说明，例如 "每周一、三、五"。</summary>
    public string RecurrenceText => RecurrenceExpander.Describe(Source.RecurrenceRule);

    /// <summary>是不是重复日程。</summary>
    public bool IsRecurring => !string.IsNullOrEmpty(Source.RecurrenceRule);
}

/// <summary>按日期分好组的一天。</summary>
public sealed class ScheduleDayGroup
{
    public ScheduleDayGroup(DateOnly date, List<ScheduleOccurrence> items)
    {
        Date = date;
        Items = items;

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
            : string.Empty;

        HeaderText = $"{date.Month}月{date.Day}日  {weekday}{suffix}";
    }

    /// <summary>哪一天。</summary>
    public DateOnly Date { get; }

    /// <summary>这一天的日程。</summary>
    public List<ScheduleOccurrence> Items { get; }

    /// <summary>分组标题。</summary>
    public string HeaderText { get; }

    /// <summary>这一天有几项。</summary>
    public string CountText => $"{Items.Count} 项";

    /// <summary>是不是今天（界面上会高亮）。</summary>
    public bool IsToday => Date == DateOnly.FromDateTime(DateTime.Now);
}

/// <summary>
/// 重复方式下拉框里的一个选项。
/// </summary>
/// <param name="Key">
/// 保存到数据库的值，要和 <c>RecurrenceExpander</c> 认的格式一致：
/// none / daily / weekly。
/// </param>
/// <param name="Label">界面上显示的中文。</param>
public sealed record RecurrenceOption(string Key, string Label);

