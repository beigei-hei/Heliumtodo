// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace HeliumTodo.Desktop.Windowing;

/// <summary>
/// "没有系统边框"之后的改大小抓手。
///
/// 背景：窗口用了 WindowDecorations="None"（为了壁纸铺满到边），
/// 系统不再把边缘当成"可拖拽的边框"，鼠标落在边缘也只是普通点击。
/// 所以 XAML 里铺了 8 个透明 Border（四条边 + 四个角），由这个类接管拖拽。
///
/// 为什么赋值前要减掉一圈外框（2026-11-13）：
///   WindowChrome 给窗口补了 WS_CAPTION / WS_THICKFRAME（DWM 的最小化/最大化过渡需要），
///   再用 WM_NCCALCSIZE 把非客户区抹成 0 —— 视觉上没边框，客户区 = 窗口矩形。
///   但 Avalonia 设置 Width/Height 时仍然按"窗口矩形 = 客户区 + 外框"计算，
///   于是它把请求值加上外框再 SetWindowPos；多加的那圈外框落在客户区里，
///   表现为第一次移动时窗口凭空高 +56 / 宽 +22 物理像素（用户报的"整窗等比例放大"）。
///   这里在每次赋值前减掉外框，窗口矩形才是我们算的那个值。
///
/// 为什么合并到渲染帧：鼠标回报率动辄 125–1000 Hz，而屏幕只有 60 Hz；
/// 每来一个移动事件就改一次窗口尺寸，等于一帧里重排重画好几次。
/// </summary>
internal sealed class WindowResizeGrips
{
    private const double MinimumWidthDip = 1080;
    private const double MinimumHeightDip = 720;

    private static readonly bool TraceOn =
        Environment.GetEnvironmentVariable("HELIUM_RESIZE_TRACE") == "1";

    private readonly Window _window;
    private readonly InteractionQuality _quality;

    /// <summary>拖动期间持有的"降质"作用域（松开时释放）。</summary>
    private IDisposable? _qualityScope;

    /// <summary>这一帧的窗口矩形已经算好、只等渲染了。</summary>
    private bool _frameScheduled;

    private PixelPoint _pendingPosition;
    private double _pendingWidth;
    private double _pendingHeight;

    /// <summary>正在拖哪条边。null = 没在拖。</summary>
    private WindowEdge? _edge;

    /// <summary>按下那一刻鼠标在全屏坐标系里的位置（物理像素）。</summary>
    private PixelPoint _startPointer;

    /// <summary>按下那一刻的窗口客户区矩形（物理像素）。</summary>
    private PixelRect _startRect;

    /// <summary>这个窗口在系统眼里的外框厚度（DIP）；尺寸赋值前减掉。</summary>
    private WindowFrame _frame;

    private double _scaling = 1.0;

    public WindowResizeGrips(Window window, InteractionQuality quality)
    {
        _window = window;
        _quality = quality;
    }

    /// <summary>是否正在拖拽。自检和窗口其它逻辑可以据此跳过某些处理。</summary>
    public bool IsResizing => _edge is not null;

    /// <summary>
    /// 挂上"移动"和"松开"两个事件。
    /// 按下在 XAML 里挂（每个抓手各一行），因为只有 6px 宽的 Border 才知道自己被按了；
    /// 按下之后指针已经被窗口捕获，后续事件发给窗口，不一定再经过那个窄条。
    /// 用 Tunnel 是为了抢在页面内容之前处理 —— 拖动过程中内容不该再收到移动事件。
    /// </summary>
    public void Attach()
    {
        _window.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        _window.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// 按住四边/四角那几条透明抓手时开始改大小（XAML 里 PointerPressed 指向这个方法）。
    /// </summary>
    public void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string edgeName }) return;
        if (!e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed) return;
        if (_window.WindowState != WindowState.Normal) return;
        if (!Enum.TryParse<WindowEdge>(edgeName, out var edge)) return;

        _edge = edge;
        _scaling = _window.RenderScaling > 0 ? _window.RenderScaling : 1.0;
        _frame = FramelessWindowFrame.Measure(_window);
        _startPointer = _window.PointToScreen(e.GetPosition(_window));
        _startRect = new PixelRect(
            _window.Position.X,
            _window.Position.Y,
            (int)Math.Round(_window.Bounds.Width * _scaling),
            (int)Math.Round(_window.Bounds.Height * _scaling));

        // 托管的最小尺寸是"客户区"尺寸；Avalonia 的 MinWidth/MinHeight 也是"客户区"，
        // 但它比较的是赋值减外框之前的数。把系统最小放宽到"补偿后仍不低于逻辑最小"，
        // 否则高度方向的补偿会被 MinHeight 顶回去，第一次移动仍会多长 37 DIP。
        _window.MinWidth = Math.Max(1, MinimumWidthDip - _frame.WidthDip);
        _window.MinHeight = Math.Max(1, MinimumHeightDip - _frame.HeightDip);

        // 拖动全程降质：壁纸换低品质、卡片阴影关掉、动图壁纸暂停
        _qualityScope ??= _quality.Begin();

        e.Pointer.Capture(_window);
        e.Handled = true;

        Trace($"press edge={edge} rect={_startRect} scale={_scaling} frame={_frame}");
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_edge is not { } edge) return;

        var current = _window.PointToScreen(e.GetPosition(_window));
        var dxPx = current.X - _startPointer.X;
        var dyPx = current.Y - _startPointer.Y;

        var minWidthPx = (int)Math.Round(MinimumWidthDip * _scaling);
        var minHeightPx = (int)Math.Round(MinimumHeightDip * _scaling);

        var x = _startRect.X;
        var y = _startRect.Y;
        var width = _startRect.Width;
        var height = _startRect.Height;

        if (edge is WindowEdge.East or WindowEdge.NorthEast or WindowEdge.SouthEast)
        {
            width = Math.Max(minWidthPx, _startRect.Width + dxPx);
        }
        else if (edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest)
        {
            width = Math.Max(minWidthPx, _startRect.Width - dxPx);
            x = _startRect.X + (_startRect.Width - width);
        }

        if (edge is WindowEdge.South or WindowEdge.SouthEast or WindowEdge.SouthWest)
        {
            height = Math.Max(minHeightPx, _startRect.Height + dyPx);
        }
        else if (edge is WindowEdge.North or WindowEdge.NorthEast or WindowEdge.NorthWest)
        {
            height = Math.Max(minHeightPx, _startRect.Height - dyPx);
            y = _startRect.Y + (_startRect.Height - height);
        }

        _pendingPosition = new PixelPoint(x, y);
        _pendingWidth = width / _scaling;
        _pendingHeight = height / _scaling;
        ScheduleApply();

        e.Handled = true;
    }

    /// <summary>把这一帧算好的尺寸真正套到窗口上（每帧最多一次）。</summary>
    private void ScheduleApply()
    {
        if (_frameScheduled) return;

        _frameScheduled = true;

        // 用渲染帧回调而不是定时器：它一定在"下一次真正要画"之前执行，
        // 这样"改尺寸"和"画出来"是同一帧的两件事，不会白算。
        TopLevel.GetTopLevel(_window)?.RequestAnimationFrame(_ =>
        {
            _frameScheduled = false;
            if (_edge is null) return;

            if (_window.Position != _pendingPosition)
            {
                _window.Position = _pendingPosition;
            }

            // 两个方向每帧都写同一个目标值：只改一个方向时，Avalonia 会把另一个方向的
            // 当前值一起重新 SetWindowPos；若那个当前值还是"没减外框"的旧值，就会多长一圈。
            var widthDip = _frame.SubtractWidth(_pendingWidth);
            var heightDip = _frame.SubtractHeight(_pendingHeight);

            if (Math.Abs(_window.Width - widthDip) > 0.01)
            {
                _window.Width = widthDip;
            }

            if (Math.Abs(_window.Height - heightDip) > 0.01)
            {
                _window.Height = heightDip;
            }

            Trace($"apply pos={_pendingPosition} want={_pendingWidth}x{_pendingHeight} set={widthDip}x{heightDip} bounds={_window.Bounds}");
        });
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_edge is null) return;

        _edge = null;
        e.Pointer.Capture(null);

        _qualityScope?.Dispose();
        _qualityScope = null;

        e.Handled = true;
    }

    private static void Trace(string message)
    {
        if (TraceOn)
        {
            Console.Error.WriteLine("[grip] " + message);
        }
    }
}
