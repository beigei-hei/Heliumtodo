// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using CommunityToolkit.Mvvm.ComponentModel;
using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;
using Avalonia.Controls;
using Avalonia.Media;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 时间轴上的一个方块。
///
/// 它就是「错位展示」的落地点：把算法算出来的 Lane / LaneCount
/// 换算成具体的像素位置（X / Y / 宽度 / 高度），界面直接绑定这四个值画出来。
///
/// 换算公式（这是整个页面的核心，值得看懂）：
///   Y      = 开始时刻 / 24 小时 × 时间轴总高度
///   高度   = 持续时长 / 24 小时 × 时间轴总高度
///   宽度   = 可用宽度 / 这一组的总列数
///   X      = 第几列 × 宽度
/// 于是同一时段的日程就自动并排错开了。
/// </summary>
public sealed partial class TimelineEntryViewModel : ObservableObject
{
    private const double PixelsPerHour = 38.0;

    private readonly TimelineEntry _entry;

    /// <summary>时间轴总高度（像素）。</summary>
    private double _canvasHeight = 24 * PixelsPerHour;

    /// <summary>时间轴可用宽度（像素）。</summary>
    private double _canvasWidth = 600;

    public TimelineEntryViewModel(TimelineEntry entry, bool hasConflict)
    {
        _entry = entry;
        HasConflict = hasConflict;
    }

    // ── 直接来自算法的信息 ──

    /// <summary>来源对象的 Id。</summary>
    public int SourceId => _entry.SourceId;

    /// <summary>标题。</summary>
    public string Title => _entry.Title;

    /// <summary>备注（地点 / 分类）。</summary>
    public string? Notes => _entry.Notes;

    /// <summary>优先级。</summary>
    public Priority Priority => _entry.Priority;

    /// <summary>时间段文字，例如 "08:00 – 09:40"。</summary>
    public string TimeRangeText => _entry.TimeRangeText;

    /// <summary>是否是重叠（错位）的条目。</summary>
    public bool IsOverlapping => _entry.IsOverlapping;

    /// <summary>是否牵涉冲突（界面上标红）。</summary>
    public bool HasConflict { get; }

    /// <summary>种类：日程 / 待办 / 专注。</summary>
    public TimelineEntryKind Kind => _entry.Kind;

    /// <summary>第几列（调试和测试用）。</summary>
    public int Lane => _entry.Lane;

    /// <summary>这一组共几列。</summary>
    public int LaneCount => _entry.LaneCount;

    // ── 换算出来的像素位置（界面直接绑定）──

    private double _x;
    public double X
    {
        get => _x;
        private set => SetProperty(ref _x, value);
    }

    private double _y;
    public double Y
    {
        get => _y;
        private set => SetProperty(ref _y, value);
    }

    private double _entryWidth;
    public double EntryWidth
    {
        get => _entryWidth;
        private set => SetProperty(ref _entryWidth, value);
    }

    private double _entryHeight;
    public double EntryHeight
    {
        get => _entryHeight;
        private set => SetProperty(ref _entryHeight, value);
    }

    /// <summary>
    /// 方块太矮，塞不下文字时设为 true。
    /// 界面会改成「只显示一条颜色带」，避免文字挤成一团看不清。
    /// </summary>
    private bool _isCompact;
    public bool IsCompact
    {
        get => _isCompact;
        private set => SetProperty(ref _isCompact, value);
    }

    /// <summary>文字能不能显示（比 IsCompact 更细：极矮时连时间都不显示）。</summary>
    public bool ShowTime => EntryHeight >= 40;

    /// <summary>能不能显示备注（要更高一点才放得下）。</summary>
    public bool ShowNotes => EntryHeight >= 62 && !string.IsNullOrWhiteSpace(Notes);

    /// <summary>背景色：按种类和优先级决定。</summary>
    public IBrush BackgroundBrush => Kind switch
    {
        TimelineEntryKind.FocusReservation => Brush("FocusEntryBrush"),
        TimelineEntryKind.RunningFocus => Brush("RunningFocusEntryBrush"),
        TimelineEntryKind.PlannedTodo => Brush("TodoEntryBrush"),
        _ => Priority switch
        {
            Priority.Urgent => Brush("PriorityUrgentBrush"),
            Priority.High => Brush("PriorityHighBrush"),
            Priority.Low => Brush("PriorityLowBrush"),
            _ => Brush("ScheduleEntryBrush"),
        },
    };

    /// <summary>边框色。有冲突时用红色，让人一眼看到问题在哪。</summary>
    public IBrush BorderBrush => HasConflict
        ? Brush("ConflictErrorBrush")
        : Kind switch
        {
            TimelineEntryKind.FocusReservation => Brush("FocusEntryBorderBrush"),
            TimelineEntryKind.RunningFocus => Brush("RunningFocusEntryBorderBrush"),
            TimelineEntryKind.PlannedTodo => Brush("TodoEntryBorderBrush"),
            _ => Priority switch
            {
                Priority.Urgent => Brush("PriorityUrgentBorderBrush"),
                Priority.High => Brush("PriorityHighBorderBrush"),
                Priority.Low => Brush("PriorityLowBorderBrush"),
                _ => Brush("ScheduleEntryBorderBrush"),
            },
        };

    /// <summary>文字色。</summary>
    public IBrush ForegroundBrush => Priority switch
    {
        Priority.Urgent => Brush("PriorityUrgentTextBrush"),
        Priority.High => Brush("PriorityHighTextBrush"),
        Priority.Low => Brush("PriorityLowTextBrush"),
        _ => Brush("PriorityNormalTextBrush"),
    };

    /// <summary>时间轴上高优先级用更粗的左边条表示（「优先级柱状」的直观体现）。</summary>
    public double PriorityBarWidth => Priority switch
    {
        Priority.Urgent => 5,
        Priority.High => 4,
        Priority.Normal => 2,
        _ => 1,
    };

    /// <summary>悬停提示，鼠标停在方块上会显示完整信息。</summary>
    public string Tooltip
    {
        get
        {
            var kindText = Kind switch
            {
                TimelineEntryKind.Schedule => "日程",
                TimelineEntryKind.PlannedTodo => "待办（已安排时间）",
                TimelineEntryKind.FocusReservation => "预订的专注",
                TimelineEntryKind.RunningFocus => "正在专注",
                _ => "条目",
            };

            var text = $"{Title}\n{kindText} · {TimeRangeText}\n优先级：{PriorityText}";

            if (!string.IsNullOrWhiteSpace(Notes))
            {
                text += $"\n{Notes}";
            }

            if (IsOverlapping)
            {
                text += $"\n⚠ 与其它安排重叠（错位显示在第 {Lane + 1} / {LaneCount} 列）";
            }

            if (HasConflict)
            {
                text += "\n⚠ 存在时间冲突";
            }

            return text;
        }
    }

    /// <summary>优先级的文字说明。</summary>
    public string PriorityText => Priority switch
    {
        Priority.Urgent => "紧急",
        Priority.High => "较高",
        Priority.Low => "较低",
        _ => "普通",
    };

    /// <summary>
    /// 时间轴尺寸变了（窗口缩放 / 首次布局）时重新算位置。
    /// </summary>
    /// <param name="canvasWidth">时间轴宽度（像素）。</param>
    /// <param name="canvasHeight">时间轴总高度（像素，含上下留白）。</param>
    /// <param name="verticalPadding">
    /// 上下留白（像素）。方块只画在"总高度 - 2 × 留白"这段里，
    /// 这样它和 TimelineGrid 画的网格线对得上（网格也整体下移了同样的距离）。
    /// </param>
    /// <param name="leftInset">
    /// 左侧让给整点刻度文字的宽度（像素，见 <see cref="TimelineMetrics"/>）。
    ///
    /// 以前这里是 0，方块从 x=0 开始排，结果整条方块的左边正好盖住
    /// "14:00 / 16:00"这些刻度文字 —— 看着就是文字被方块压住，
    /// 也对不上它指的时间。现在先把这段让出来，方块从刻度右边开始排。
    /// </param>
    public void Resize(double canvasWidth, double canvasHeight, double verticalPadding = 0,
                       double leftInset = 0)
    {
        _canvasWidth = Math.Max(0, canvasWidth);
        _canvasHeight = Math.Max(1, canvasHeight);

        var laneCount = Math.Max(1, _entry.LaneCount);

        // 横向：扣掉左侧刻度区，剩下的宽度平分给每一列
        var inset = Math.Clamp(leftInset, 0, Math.Max(0, _canvasWidth - 1));
        var laneSpan = Math.Max(0, _canvasWidth - inset);
        var laneWidth = laneSpan / laneCount;
        X = inset + _entry.Lane * laneWidth;
        EntryWidth = Math.Max(0, laneWidth);

        // 纵向：按「一天的比例」映射到像素。可用高度要扣掉上下留白。
        var usable = Math.Max(1, _canvasHeight - verticalPadding * 2);
        Y = verticalPadding + _entry.TopRatio * usable;
        EntryHeight = Math.Max(2, _entry.HeightRatio * usable);

        // 太矮就只画色带，不排文字
        IsCompact = EntryHeight < 22;

        OnPropertyChanged(nameof(ShowTime));
        OnPropertyChanged(nameof(ShowNotes));
        OnPropertyChanged(nameof(Tooltip));
    }

    /// <summary>从应用资源里取画刷（颜色定义集中在 DesignTokens.axaml）。</summary>
    private static IBrush Brush(string resourceKey)
    {
        if (Avalonia.Application.Current?.TryFindResource(resourceKey, out var value) == true
            && value is IBrush brush)
        {
            return brush;
        }

        // 取不到就用一个中性色兜底，绝不让界面因为找不到颜色而崩
        return Brushes.Gray;
    }
}
