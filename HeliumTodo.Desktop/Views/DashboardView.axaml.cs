// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using HeliumTodo.Desktop.Behaviors;
using HeliumTodo.Desktop.ViewModels;

namespace HeliumTodo.Desktop.Views;

/// <summary>
/// 「每日工作流」页面。
///
/// 代码这边做两件界面层才能做的事：
///   ① 把「时间轴有多宽」告诉 ViewModel，让它重新计算所有方块的错位位置。
///      （ViewModel 不知道界面尺寸，这是 MVVM 的基本分工。）
///   ② 打开页面时把时间轴滚到「现在」附近。
///      一天 24 小时全长 912 像素（外加 28 像素留白），如果停在最上面，
///      用户一进来看到的是凌晨 0 点，今天的安排全在下面看不见。
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>本次浏览是否已经自动滚过一次（滚过之后就不再打扰用户手动滚动的位置）。</summary>
    private bool _hasAutoScrolled;

    /// <summary>上次自动滚动时看的是哪一天，用来判断用户是不是翻页了。</summary>
    private DateOnly _lastScrolledDate;

    public DashboardView()
    {
        InitializeComponent();

        // 时间轴宽度变了 → 通知 ViewModel 重排
        TimelineHost.WidthChanged += OnTimelineWidthChanged;

        // 时间轴滚动由 XAML 上的 ScrollSyncBehavior 转发；目前只留扩展点。
        var scrollSync = Interaction.GetBehaviors(TimelineScroll)
            .OfType<ScrollSyncBehavior>()
            .FirstOrDefault();
        if (scrollSync is not null)
        {
            scrollSync.Scrolled += OnTimelineScrolled;
        }

        DataContextChanged += (_, _) =>
        {
            _hasAutoScrolled = false;
            RefreshTimelineLayout();
        };
    }

    private void OnTimelineWidthChanged(object? sender, double width)
    {
        if (DataContext is not DashboardViewModel vm) return;

        vm.ResizeTimeline(width);

        // 宽度变化说明布局刚算完，这时候滚动位置才是准的
        AutoScrollToRelevantTime(vm);
    }

    /// <summary>
    /// 时间轴滚动了。
    /// 目前不需要额外处理（刻度是画在时间轴里面的，会跟着一起滚），
    /// 保留这个方法是为了以后想在滚动时做点什么（比如显示「正在查看 20:00」）时有个现成的接入点。
    /// </summary>
    private void OnTimelineScrolled(object? sender, Vector offset)
    {
        // 故意留空
    }

    private void RefreshTimelineLayout()
    {
        if (DataContext is DashboardViewModel vm && TimelineHost.Bounds.Width > 0)
        {
            vm.ResizeTimeline(TimelineHost.Bounds.Width);
            AutoScrollToRelevantTime(vm);
        }
    }

    /// <summary>
    /// 把时间轴滚到有意义的位置。
    ///
    /// 规则：
    ///   · 看「今天」    → 滚到当前时刻，并往上留一点，方便看到刚过去的事
    ///   · 看别的日期    → 滚到第一个有安排的时刻；完全没有安排就停在早上 7 点
    ///   · 同一天只自动滚一次，之后尊重用户自己滚动的位置
    /// </summary>
    private void AutoScrollToRelevantTime(DashboardViewModel vm)
    {
        if (_hasAutoScrolled && _lastScrolledDate == vm.SelectedDate)
        {
            return;
        }

        _lastScrolledDate = vm.SelectedDate;
        _hasAutoScrolled = true;

        // 自动化验证用：HELIUM_TIMELINE_SCROLL=top/bottom 时不做"自动滚到当前时刻"，
        // 而是滚到时间轴两头，用来确认 00:00 / 24:00 的刻度文字没有被切掉。
        var pinned = Environment.GetEnvironmentVariable("HELIUM_TIMELINE_SCROLL");
        if (!string.IsNullOrWhiteSpace(pinned))
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    var max = Math.Max(0, TimelineScroll.Extent.Height - TimelineScroll.Viewport.Height);
                    TimelineScroll.Offset = new Vector(
                        0,
                        pinned.Equals("bottom", StringComparison.OrdinalIgnoreCase) ? max : 0);
                },
                DispatcherPriority.Loaded);
            return;
        }

        // 延后到布局完成再滚：否则这时候 ScrollViewer 的视口高度还是 0，算出来的位置是错的
        Dispatcher.UIThread.Post(
            () =>
            {
                var offset = vm.GetScrollOffset(TimelineScroll.Viewport.Height);

                if (offset > 0)
                {
                    TimelineScroll.Offset = new Vector(0, offset);
                }
            },
            DispatcherPriority.Loaded);
    }
}
