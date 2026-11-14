// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Domain.Models;
using HeliumTodo.Infrastructure;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 「待办事项」页面。
///
/// 三件事让它和另外两个核心功能联动起来：
///   ① 给待办「安排时间」→ 它会出现在「今日工作流」的 24 小时时间轴上
///   ② 对待办「开始专注」→ 跳到专注页开始计时，结束后时长会累加到这条待办上
///   ③ 勾选完成 → 时间轴和统计立刻跟着更新
/// </summary>
public sealed partial class TodoViewModel : ViewModelBase
{
    private readonly TodoRepository _todoRepo;
    private readonly AppState _state;

    public TodoViewModel(IServiceProvider services)
    {
        _todoRepo = (TodoRepository)services.GetService(typeof(TodoRepository))!;
        _state = (AppState)services.GetService(typeof(AppState))!;

        // 新建时默认的截止时间 = 今天晚上 22:00
        _newDueDate = DateTime.Today;
        _newDueTime = new TimeSpan(22, 0, 0);

        _state.DataChanged += async (_, type) =>
        {
            if (type.Includes(AppDataType.Todos)) await RefreshAsync();
        };
    }

    // ─────────────────────────────────────────────────────────
    // 列表
    // ─────────────────────────────────────────────────────────

    /// <summary>按当前筛选条件显示出来的待办。</summary>
    public ObservableCollection<TodoItem> Items { get; } = new();

    /// <summary>统计：全部 / 未完成 / 已完成 / 今天完成。</summary>
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _completedTodayCount;

    /// <summary>空列表时的提示文字。</summary>
    [ObservableProperty] private string _emptyHint = "还没有待办，添加一个吧。";

    // ─────────────────────────────────────────────────────────
    // 筛选
    // ─────────────────────────────────────────────────────────

    /// <summary>筛选方式：全部 / 未完成 / 已完成 / 今天。</summary>
    [ObservableProperty] private TodoFilter _filter = TodoFilter.Active;

    /// <summary>搜索关键字。</summary>
    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterChanged(TodoFilter value) => ApplyFilter();

    /// <summary>点常用分类按钮 → 直接填进"新分类"输入框。</summary>
    [RelayCommand]
    private void UseCategoryPreset(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            NewCategory = category;
        }
    }
    [RelayCommand] private void SetFilter(string? filterName)
    {
        if (Enum.TryParse<TodoFilter>(filterName, out var parsed))
        {
            Filter = parsed;
        }
    }

    // 筛选按钮的选中状态（界面上用来高亮当前选中的那个）
    public bool IsFilterAll => Filter == TodoFilter.All;
    public bool IsFilterActive => Filter == TodoFilter.Active;
    public bool IsFilterCompleted => Filter == TodoFilter.Completed;
    public bool IsFilterToday => Filter == TodoFilter.Today;

    // ─────────────────────────────────────────────────────────
    // 新建待办
    // ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private Priority _newPriority = Priority.Normal;
    [ObservableProperty] private string _newCategory = "学习";

    /// <summary>预计时长（分钟）。</summary>
    [ObservableProperty] private int _newEstimatedMinutes = 30;

    /// <summary>截止日期。</summary>
    [ObservableProperty] private DateTimeOffset? _newDueDate;

    /// <summary>是否设置截止时间。</summary>
    [ObservableProperty] private bool _newHasDueTime = true;

    /// <summary>截止时刻（几点几分）。</summary>
    [ObservableProperty] private TimeSpan? _newDueTime = new TimeSpan(22, 0, 0);

    /// <summary>新建面板是否展开。</summary>
    [ObservableProperty] private bool _isEditorOpen = true;

    /// <summary>编辑中的待办（null 表示「新建」模式）。</summary>
    [ObservableProperty] private TodoItem? _editingTodo;

    /// <summary>当前是在编辑已有待办吗。</summary>
    public bool IsEditing => EditingTodo is not null;

    /// <summary>新建面板的标题。</summary>
    public string EditorTitle => IsEditing ? "编辑待办" : "新建待办";

    partial void OnEditingTodoChanged(TodoItem? value)
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
    }

    /// <summary>可选的优先级（界面上用下拉框选）。</summary>
    public IReadOnlyList<Priority> PriorityOptions { get; } = new[]
    {
        Priority.Low, Priority.Normal, Priority.High, Priority.Urgent,
    };

    /// <summary>新增或保存待办。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
        {
            EmptyHint = "标题不能为空哦";
            return;
        }

        // 把「日期 + 时刻」拼成一个完整的截止时间
        DateTime? dueAt = null;
        if (NewHasDueTime && NewDueDate.HasValue)
        {
            var date = NewDueDate.Value.Date;
            var time = NewDueTime ?? new TimeSpan(22, 0, 0);
            dueAt = date.Add(time);
        }

        if (EditingTodo is not null)
        {
            // ── 修改已有待办 ──
            EditingTodo.Title = NewTitle.Trim();
            EditingTodo.Priority = NewPriority;
            EditingTodo.Category = NewCategory;
            EditingTodo.EstimatedDuration = TimeSpan.FromMinutes(Math.Max(1, NewEstimatedMinutes));
            EditingTodo.DueAt = dueAt;
            EditingTodo.ReminderMinutes = Reminder.LeadMinutes;

            await _todoRepo.UpdateAsync(EditingTodo);
            EditingTodo = null;
        }
        else
        {
            // ── 新建 ──
            var todo = new TodoItem
            {
                Title = NewTitle.Trim(),
                Priority = NewPriority,
                Category = NewCategory,
                EstimatedDuration = TimeSpan.FromMinutes(Math.Max(1, NewEstimatedMinutes)),
                DueAt = dueAt,
                ReminderMinutes = Reminder.LeadMinutes,
                CreatedAt = DateTime.Now,
            };

            var saved = await _todoRepo.AddAsync(todo);
        }

        // 清空输入，方便连续添加
        NewTitle = string.Empty;
        NewEstimatedMinutes = 30;
        Reminder.SetFrom(null);
        EmptyHint = string.Empty;

        _state.NotifyChanged(AppDataType.Todos);

        // 新建之后自动切到「未完成」，否则刚加的待办可能在当前筛选下看不见
        if (Filter == TodoFilter.Completed)
        {
            Filter = TodoFilter.Active;
        }
        else
        {
            await RefreshAsync();
        }
    }

    /// <summary>
    /// 编辑区里的「提醒」选择器（和日程页共用同一套选项与换算）。
    /// 待办没有截止时间时，提醒会退到「计划开始时间」上，这一点由 Core 层判断。
    /// </summary>
    public ReminderEditState Reminder { get; } = new();

    /// <summary>取消编辑。</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        EditingTodo = null;
        NewTitle = string.Empty;
        NewPriority = Priority.Normal;
        NewEstimatedMinutes = 30;
        Reminder.SetFrom(null);
    }

    /// <summary>进入编辑模式。</summary>
    [RelayCommand]
    private void Edit(TodoItem? todo)
    {
        if (todo is null) return;

        EditingTodo = todo;
        NewTitle = todo.Title;
        NewPriority = todo.Priority;
        NewCategory = todo.Category ?? "学习";
        NewEstimatedMinutes = (int)Math.Max(1, todo.EstimatedDuration.TotalMinutes);
        NewHasDueTime = todo.DueAt.HasValue;
        NewDueDate = todo.DueAt?.Date ?? DateTime.Today;
        NewDueTime = todo.DueAt?.TimeOfDay ?? new TimeSpan(22, 0, 0);
        Reminder.SetFrom(todo.ReminderMinutes);
        IsEditorOpen = true;
    }

    // ─────────────────────────────────────────────────────────
    // 列表上的操作
    // ─────────────────────────────────────────────────────────

    /// <summary>勾选 / 取消勾选。</summary>
    [RelayCommand]
    private async Task ToggleAsync(TodoItem? todo)
    {
        if (todo is null) return;

        var updated = await _todoRepo.ToggleCompletedAsync(todo.Id);
        if (updated is null) return;

        _state.NotifyChanged(AppDataType.Todos);
    }

    /// <summary>删除。</summary>
    [RelayCommand]
    private async Task DeleteAsync(TodoItem? todo)
    {
        if (todo is null) return;

        await _todoRepo.DeleteAsync(todo.Id);
        _state.NotifyChanged(AppDataType.Todos);
    }

    /// <summary>
    /// 调整优先级：点一下「↑」就升一级，「↓」就降一级。
    /// 比下拉框快，也更适合手机那样的操作习惯。
    /// </summary>
    [RelayCommand]
    private async Task CyclePriorityAsync(TodoItem? todo)
    {
        if (todo is null) return;

        // 循环：低 → 普通 → 较高 → 紧急 → 低
        todo.Priority = todo.Priority switch
        {
            Priority.Low => Priority.Normal,
            Priority.Normal => Priority.High,
            Priority.High => Priority.Urgent,
            _ => Priority.Low,
        };

        await _todoRepo.UpdateAsync(todo);
        _state.NotifyChanged(AppDataType.Todos);
    }

    /// <summary>
    /// 给待办安排一个时间 → 它会立刻出现在「今日工作流」的时间轴上。
    /// 这是「待办 → 日程」的联动入口。
    /// </summary>
    [RelayCommand]
    private async Task ScheduleAsync(TodoItem? todo)
    {
        if (todo is null) return;

        // 默认安排到「今天下一个整点」
        var now = DateTime.Now;
        var suggested = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);

        // 如果这条待办本来就有安排，就在原时间上往后挪一天（相当于「顺延」）
        if (todo.PlannedStart.HasValue)
        {
            todo.PlannedStart = todo.PlannedStart.Value.AddDays(1);
        }
        else
        {
            todo.PlannedStart = suggested;
        }

        await _todoRepo.UpdateAsync(todo);
        _state.NotifyChanged(AppDataType.Todos);
    }

    /// <summary>取消安排（把它从时间轴上拿下来）。</summary>
    [RelayCommand]
    private async Task UnscheduleAsync(TodoItem? todo)
    {
        if (todo is null) return;

        todo.PlannedStart = null;
        await _todoRepo.UpdateAsync(todo);
        _state.NotifyChanged(AppDataType.Todos);
    }

    // ─────────────────────────────────────────────────────────
    // 加载
    // ─────────────────────────────────────────────────────────

    public override async Task OnNavigatedToAsync() => await RefreshAsync();

    /// <summary>重新读数据。</summary>
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var all = await _todoRepo.GetAllAsync();

            TotalCount = all.Count;
            ActiveCount = all.Count(t => !t.IsCompleted);
            CompletedCount = all.Count(t => t.IsCompleted);

            var today = DateTime.Today;
            CompletedTodayCount = all.Count(t =>
                t.IsCompleted && t.CompletedAt >= today && t.CompletedAt < today.AddDays(1));

            _allTodos = all;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            EmptyHint = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>最近一次读到的全部待办（筛选时从这里挑）。</summary>
    private List<TodoItem> _allTodos = new();

    /// <summary>按当前筛选条件和搜索词过滤。</summary>
    private void ApplyFilter()
    {
        var today = DateTime.Today;

        IEnumerable<TodoItem> query = Filter switch
        {
            TodoFilter.Active => _allTodos.Where(t => !t.IsCompleted),
            TodoFilter.Completed => _allTodos.Where(t => t.IsCompleted),
            TodoFilter.Today => _allTodos.Where(t =>
                !t.IsCompleted &&
                ((t.DueAt.HasValue && t.DueAt.Value.Date == today) ||
                 (t.PlannedStart.HasValue && t.PlannedStart.Value.Date == today))),
            _ => _allTodos,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            query = query.Where(t =>
                t.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (t.Category?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Notes?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // 排序：未完成在前 → 优先级高的在前 → 截止早的在前
        var result = query
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueAt ?? DateTime.MaxValue)
            .ToList();

        Items.Clear();
        foreach (var item in result)
        {
            Items.Add(item);
        }

        // 通知界面刷新筛选按钮的高亮状态
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(IsFilterCompleted));
        OnPropertyChanged(nameof(IsFilterToday));

        // 只有「列表真的空了」才显示提示文字。
        // 之前这里是无条件赋值的，结果列表里明明有 4 条待办，
        // 也会显示「没有未完成的待办，全清空了！」—— 这是个明显的 bug。
        EmptyHint = Items.Count == 0
            ? Filter switch
            {
                TodoFilter.Completed => "还没有完成任何待办。",
                TodoFilter.Today => "今天没有要做的待办，轻松一下也不错。",
                TodoFilter.Active => "没有未完成的待办，全清空了！",
                _ => "还没有待办，添加一个-_-",
            }
            : string.Empty;
    }
}

/// <summary>待办列表的筛选方式。</summary>
public enum TodoFilter
{
    /// <summary>全部。</summary>
    All,

    /// <summary>未完成。</summary>
    Active,

    /// <summary>已完成。</summary>
    Completed,

    /// <summary>今天要做的。</summary>
    Today,
}
