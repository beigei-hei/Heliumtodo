using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace HeliumTodo.App.Windowing;

/// <summary>
/// "没有系统边框"之后的改大小抓手。
///
/// 背景：窗口用了 WindowDecorations="None"（为了壁纸铺满到边），
/// 系统不再把边缘当成"可拖拽的边框"，鼠标落在边缘也只是普通点击。
/// 所以 XAML 里铺了 8 个透明 Border（四条边 + 四个角），由这个类接管拖拽。
///
/// 为什么自己算、不用 Window.BeginResizeDrag：
///   BeginResizeDrag 最终是把鼠标交给系统那个"改大小循环"，
///   而那个循环只在窗口带 WS_THICKFRAME（系统边框）时才生效。
///   最早窗口是 WindowDecorations="None"，这个样式位没有 ——
///   实测：抓手能收到点击、BeginResizeDrag 也调用了，窗口纹丝不动，所以改成纯托管实现。
///   后来为了让最大化有动画，WS_THICKFRAME 又被加回去了（见 WindowChrome），
///   理论上 BeginResizeDrag 现在能用了；但这套托管实现已经验证过、且不挑平台，
///   没有理由为了少写几十行去换一个"看系统脸色"的行为，所以保留。
///   好处是不用 P/Invoke、不挑平台；代价是拖动由界面帧驱动，
///   不像系统那样"硬件光标实时跟手"——对这个应用足够。
/// </summary>
internal sealed class WindowResizeGrips
{
    private readonly Window _window;
    private readonly InteractionQuality _quality;

    /// <summary>拖动期间持有的"降质"作用域（松开时释放）。</summary>
    private IDisposable? _qualityScope;

    /// <summary>
    /// 这一帧的窗口矩形已经算好、只等渲染了。
    /// 鼠标回报率动辄 125–1000 Hz，而屏幕只有 60 Hz；
    /// 每来一个移动事件就改一次窗口尺寸，等于一帧里重排重画好几次，
    /// 这是"拖边缘一顿一顿"的直接原因，所以合并到下一帧只做一次。
    /// </summary>
    private bool _frameScheduled;

    private double _pendingWidth;
    private double _pendingHeight;
    private PixelPoint _pendingPosition;

    /// <summary>正在拖哪条边。null = 没在拖。</summary>
    private WindowEdge? _edge;

    /// <summary>按下那一刻鼠标在全屏坐标系里的位置（物理像素）。</summary>
    private PixelPoint _startPointer;

    /// <summary>按下那一刻窗口的位置（物理像素）和大小（DIP）。</summary>
    private PixelPoint _startPosition;
    private double _startWidth;
    private double _startHeight;

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
        _startPointer = _window.PointToScreen(e.GetPosition(_window));
        _startPosition = _window.Position;
        _startWidth = _window.Bounds.Width;
        _startHeight = _window.Bounds.Height;

        // 拖动全程降质：壁纸换低品质、卡片阴影关掉、动图壁纸暂停
        _qualityScope ??= _quality.Begin();

        e.Pointer.Capture(_window);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_edge is not { } edge) return;

        var current = _window.PointToScreen(e.GetPosition(_window));
        var scaling = _window.RenderScaling <= 0 ? 1.0 : _window.RenderScaling;

        // 鼠标移动了多少物理像素 → 换算成 DIP（窗口大小用的是 DIP）
        var dx = (current.X - _startPointer.X) / scaling;
        var dy = (current.Y - _startPointer.Y) / scaling;

        var width = _startWidth;
        var height = _startHeight;

        if (edge is WindowEdge.East or WindowEdge.NorthEast or WindowEdge.SouthEast)
        {
            width = Math.Max(_window.MinWidth, _startWidth + dx);
        }
        else if (edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest)
        {
            width = Math.Max(_window.MinWidth, _startWidth - dx);
        }

        if (edge is WindowEdge.South or WindowEdge.SouthEast or WindowEdge.SouthWest)
        {
            height = Math.Max(_window.MinHeight, _startHeight + dy);
        }
        else if (edge is WindowEdge.North or WindowEdge.NorthEast or WindowEdge.NorthWest)
        {
            height = Math.Max(_window.MinHeight, _startHeight - dy);
        }

        // 往左/往上拉的时候右下角不动，所以左上角要跟着让位。
        // 位置是物理像素，得换算回去。
        var left = _startPosition.X;
        var top = _startPosition.Y;

        if (edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest)
        {
            left = _startPosition.X + (int)Math.Round((_startWidth - width) * scaling);
        }

        if (edge is WindowEdge.North or WindowEdge.NorthEast or WindowEdge.NorthWest)
        {
            top = _startPosition.Y + (int)Math.Round((_startHeight - height) * scaling);
        }

        // 只记下"这一帧应该长什么样"，真正改窗口交给下一帧 —— 见 _frameScheduled 的注释
        _pendingPosition = new PixelPoint(left, top);
        _pendingWidth = width;
        _pendingHeight = height;
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

            _window.Position = _pendingPosition;
            _window.Width = _pendingWidth;
            _window.Height = _pendingHeight;
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
}