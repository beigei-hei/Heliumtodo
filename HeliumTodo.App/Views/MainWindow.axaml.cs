using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using HeliumTodo.App.ViewModels;
using HeliumTodo.App.Windowing;

namespace HeliumTodo.App.Views;

/// <summary>
/// 主窗口。
///
/// 这个文件只做"窗口该做的事"，具体实现分给三个协作对象（都在 Windowing 目录下）：
///   · <see cref="WindowChrome"/>       —— 系统边框样式位 + 非客户区归零（Windows 专属）；
///   · <see cref="WindowAnimator"/>     —— 打开时的遮罩淡出（最大化 / 还原交给 DWM）；
///   · <see cref="WindowResizeGrips"/>  —— 没有系统边框之后，四边四角的拖拽改大小。
///
/// 这样拆的理由：这三件事互不相干，也都跟"这个应用有哪些页面"无关。
/// 以前它们散在 MainWindow.axaml.cs / MainWindow.Win32.cs / MainWindow.Animation.cs 三个
/// partial 文件里，读一处逻辑要在三个文件之间跳；现在窗口类只剩"装配 + 存取窗口尺寸"，
/// 想改动画就只看 WindowAnimator，想改拖拽就只看 WindowResizeGrips。
/// </summary>
public partial class MainWindow : Window
{
    private readonly InteractionQuality _quality;
    private readonly WindowAnimator _animator;
    private readonly WindowResizeGrips _grips;

    /// <summary>上一次看到的窗口状态：用来判断"这次是从最小化回来"，还是别的切换。</summary>
    private WindowState _lastWindowState = WindowState.Normal;

    /// <summary>这次恢复是不是托盘唤回的（那条路 App 自己会播出现动画，这里不能重复播）。</summary>
    private bool _trayRestorePending;

    /// <summary>图标轨展开 / 收起期间那份"降质"的凭据，以及到点收回它的表。</summary>
    private IDisposable? _railQuality;
    private DispatcherTimer? _railQualityTimer;

    /// <summary>预缩放后的壁纸（和窗口像素 1:1），以及它是按哪张源图、哪个尺寸做的。</summary>
    private Avalonia.Media.Imaging.RenderTargetBitmap? _scaledWallpaper;
    private object? _scaledWallpaperSource;
    private PixelSize _scaledWallpaperSize;

    /// <summary>窗口尺寸变化时的防抖表：拖拽改大小期间不会每帧都重做一次壁纸缩放。</summary>
    private DispatcherTimer? _wallpaperScaleTimer;
    private ShellViewModel? _wallpaperViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // ① 外壳：把 WS_THICKFRAME 加回来（系统才当成普通窗口），再把非客户区抹成 0（壁纸铺满）
        WindowChrome.Attach(this);

        // ② 动画：只剩"出现时淡出遮罩"这一件（最大化和还原交给 DWM，见 WindowChrome）。
        //    拖拽改大小要在"窗口正在动"的这段时间临时降质（见 InteractionQuality 的实测数字）。
        _quality = new InteractionQuality(this);
        _animator = new WindowAnimator(this) { Trace = App.LogBackground };

        // 开场动画靠这层遮罩淡出（而不是改窗口不透明度，见 WindowAnimator.PlayIntro 的注释）。
        _animator.SetIntroVeil(IntroVeil);

        // ③ 拖拽改大小
        _grips = new WindowResizeGrips(this, _quality);
        _grips.Attach();

        Opened += OnOpened;
        Closing += OnClosing;

        // ④ 壁纸：先缩放成窗口尺寸再贴（这样每帧重绘不用重新采样整张图）
        SizeChanged += OnWindowSizeChanged;
        DataContextChanged += OnWindowDataContextChanged;

        // 探针入口（tools/probe-rail-anim.ps1 用）：等界面稳下来之后自己展开一次图标轨，
        // 好让 HELIUM_RAIL_TRACE 的逐帧日志有东西可打。生产里这个变量不存在。
        if (Environment.GetEnvironmentVariable("HELIUM_RAIL_TEST") == "1")
        {
            var trigger = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            trigger.Tick += (_, _) =>
            {
                trigger.Stop();
                BeginRailAnimation();
                if (DataContext is ShellViewModel shell)
                {
                    shell.ToggleRailCommand.Execute(null);
                }
            };
            trigger.Start();
        }
    }

    private void OnWindowDataContextChanged(object? sender, EventArgs e)
    {
        if (_wallpaperViewModel is not null)
        {
            _wallpaperViewModel.PropertyChanged -= OnShellPropertyChanged;
        }

        _wallpaperViewModel = DataContext as ShellViewModel;
        if (_wallpaperViewModel is not null)
        {
            _wallpaperViewModel.PropertyChanged += OnShellPropertyChanged;
        }

        RefreshScaledWallpaper();
    }

    private void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.WallpaperImage) or nameof(ShellViewModel.IsWallpaperAnimated))
        {
            RefreshScaledWallpaper();
        }
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // 防抖：拖拽改大小的时候每帧都会 SizeChanged，每帧重做一张全窗口位图不合适。
        // 停下 160 ms 之后再按最终尺寸重做一次（期间旧图会被拉伸，看不清差别）。
        _wallpaperScaleTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _wallpaperScaleTimer.Stop();
        _wallpaperScaleTimer.Tick -= OnWallpaperScaleTimerTick;
        _wallpaperScaleTimer.Tick += OnWallpaperScaleTimerTick;
        _wallpaperScaleTimer.Start();
    }

    private void OnWallpaperScaleTimerTick(object? sender, EventArgs e)
    {
        _wallpaperScaleTimer?.Stop();
        RefreshScaledWallpaper();
    }

    /// <summary>
    /// 把壁纸按当前窗口像素尺寸预缩放一次，再贴到 <c>WallpaperImage</c> 上。
    ///
    /// 不这么做的话，<c>Stretch="UniformToFill"</c> 每帧重绘都要重新采样整张图：
    /// 2560×1440 窗口实测这一步要 18–27 ms/帧，页面切换、对话框、窗口动画全都在替它交钱。
    /// 动图/视频不参与（每帧都是新图，预缩放等于每帧多做一次全窗口绘制）。
    /// </summary>
    private void RefreshScaledWallpaper()
    {
        var viewModel = _wallpaperViewModel ?? DataContext as ShellViewModel;
        var source = viewModel?.WallpaperImage;

        if (source is null)
        {
            WallpaperImage.Source = null;
            _scaledWallpaper = null;
            _scaledWallpaperSource = null;
            return;
        }

        // 会动的壁纸：原图直接贴，交给 Image 自己撑满（避免每帧重做一次全窗口缩放）。
        if (viewModel!.IsWallpaperAnimated)
        {
            WallpaperImage.Stretch = Avalonia.Media.Stretch.UniformToFill;
            WallpaperImage.Source = source;
            _scaledWallpaper = null;
            _scaledWallpaperSource = null;
            return;
        }

        var scaling = RenderScaling <= 0 ? 1.0 : RenderScaling;
        var width = (int)Math.Round(Bounds.Width * scaling);
        var height = (int)Math.Round(Bounds.Height * scaling);
        if (width < 2 || height < 2)
        {
            return;
        }

        var target = new PixelSize(width, height);
        if (_scaledWallpaper is not null
            && ReferenceEquals(_scaledWallpaperSource, source)
            && _scaledWallpaperSize == target)
        {
            WallpaperImage.Stretch = Avalonia.Media.Stretch.Fill;
            WallpaperImage.Source = _scaledWallpaper;
            return;
        }

        var scaled = Services.WallpaperScaler.Scale(source, target);
        if (scaled is null)
        {
            WallpaperImage.Source = source;
            return;
        }

        _scaledWallpaper = scaled;
        _scaledWallpaperSource = source;
        _scaledWallpaperSize = target;

        WallpaperImage.Stretch = Avalonia.Media.Stretch.Fill;
        WallpaperImage.Source = scaled;
    }

    /// <summary>
    /// 从托盘唤回时再播一次"出现"动画 —— 窗口是 Hide/Show 的，不走 Opened，
    /// 所以由调用方（App 的 RestoreMainWindow）显式叫一下。
    /// </summary>
    public void PlayShowAnimation()
    {
        // 走到这里说明"由调用方负责这次出现动画"，托盘那条路标记就此用掉。
        _trayRestorePending = false;
        _animator.PlayIntro();
    }

    /// <summary>
    /// 图标轨开始展开 / 收起。
    ///
    /// 为什么这件事要报到窗口这里：轨道宽度一变，右边整页要重排、整窗要重画，
    /// 这是"内部控件动画"里最贵的一种（不是改窗口尺寸，但每帧一样出整张图）。
    /// 所以这段时间也走 <see cref="InteractionQuality"/>：壁纸降成低成本插值、卡片阴影先关掉。
    /// 壁纸本身不停（图标轨只动 260ms，停一次视频再启要重新 seek，反而更卡）。
    ///
    /// 收回靠 <see cref="DispatcherTimer"/> 而不是 rAF：动画结束后就没有帧了，
    /// rAF 那条路会永远等不到下一次回调，降质状态收不回来。
    /// </summary>
    public void BeginRailAnimation()
    {
        _railQuality?.Dispose();
        _railQuality = _quality.Begin(
            HeliumTodo.Core.Theming.RailMetrics.AnimationDurationMs,
            pauseWallpaper: false);

        _railQualityTimer?.Stop();
        _railQualityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                HeliumTodo.Core.Theming.RailMetrics.AnimationDurationMs + 60),
        };
        _railQualityTimer.Tick += (_, _) =>
        {
            _railQualityTimer?.Stop();
            _railQuality?.Dispose();
            _railQuality = null;
        };
        _railQualityTimer.Start();

        TraceRailFramesIfAsked();
    }

    /// <summary>
    /// HELIUM_RAIL_TRACE=1 时逐帧打一条日志（帧序号、距上一帧多久、轨道当前宽度）。
    /// 卡顿是"最慢一帧有多慢"的问题，只有逐帧间隔能说明，所以留着这条通道给探针用。
    /// </summary>
    private void TraceRailFramesIfAsked()
    {
        if (Environment.GetEnvironmentVariable("HELIUM_RAIL_TRACE") != "1")
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        var last = 0.0;
        var frame = 0;

        void OnFrame(TimeSpan _)
        {
            frame++;
            var elapsed = clock.Elapsed.TotalMilliseconds;
            App.LogBackground($"[图标轨] 第 {frame} 帧 间隔 {elapsed - last:0.0} ms 已用 {elapsed:0.0} ms");
            last = elapsed;

            if (elapsed < HeliumTodo.Core.Theming.RailMetrics.AnimationDurationMs + 120)
            {
                topLevel.RequestAnimationFrame(OnFrame);
            }
        }

        topLevel.RequestAnimationFrame(OnFrame);
    }

    /// <summary>
    /// 告诉窗口"接下来这次恢复是托盘唤回的"。App 会在 Show() 之前调一次 ——
    /// 否则"最小化 → 恢复"和"托盘 → 恢复"两条路各播一次，动画会叠着播两遍。
    /// </summary>
    public void BeginTrayRestore() => _trayRestorePending = true;

    /// <summary>
    /// 盯住窗口状态的变化。
    ///
    /// 为什么要盯：
    ///   ① 最小化之后窗口拿不到帧 —— 正在播的开场动画永远等不到下一帧，
    ///      窗口就会停在"遮罩还盖着 / 内容还带着变换"的状态里。从任务栏点回来时
    ///      没人替它收尾（以前只有"藏到托盘"那条路会调 Settle），看着就是个半透明窗口。
    ///   ② 从最小化恢复时也应该有一次出现动画，跟第一次打开手感一致。
    ///
    /// Avalonia 12 里 WindowState 是 StyledProperty，没有 WindowStateChanged 事件，
    /// 所以走 OnPropertyChanged。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WindowStateProperty) return;

        var state = change.GetNewValue<WindowState>();
        var wasMinimized = _lastWindowState == WindowState.Minimized;
        _lastWindowState = state;

        if (state == WindowState.Minimized)
        {
            // 就地收尾：窗口看不见了，放映中的动画没有意义，也不能留下半截状态。
            _animator.Settle();
            App.LogBackground("[窗口] 状态 → 最小化，动画就地收尾");
            return;
        }

        if (_trayRestorePending)
        {
            // 托盘那条路由 App 显式播，别播两遍。
            _trayRestorePending = false;
            App.LogBackground($"[窗口] 状态 → {state}（托盘唤回，出现动画由 App 播）");
            return;
        }

        if (wasMinimized)
        {
            _animator.PlayIntro();
            App.LogBackground($"[窗口] 状态 → {state}，从最小化恢复，补播出现动画");
        }
        else
        {
            App.LogBackground($"[窗口] 状态 → {state}");
        }
    }

    /// <summary>
    /// 立刻结束进行中的窗口动画。窗口要藏到托盘（或退出）之前调一次，
    /// 免得动画播到一半被打断，留下半透明的窗口。
    /// </summary>
    public void FinishAnimationsImmediately() => _animator.Settle();

    private void OnOpened(object? sender, EventArgs e)
    {
        // 恢复上次的窗口大小（做个范围检查，防止配置被改坏导致窗口看不见）
        if (DataContext is ShellViewModel
            && App.Services.GetService(typeof(Core.Configuration.ConfigService))
                is Core.Configuration.ConfigService config)
        {
            Width = Math.Clamp(config.Current.WindowWidth, MinWidth, 4000);
            Height = Math.Clamp(config.Current.WindowHeight, MinHeight, 4000);

            if (config.Current.StartMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        // 窗口出现的过渡：一层遮罩从实到透（对齐 Windows 11 的观感）
        _animator.PlayIntro();

        // 最大化 / 最小化的过渡动画由 DWM 负责（WindowChrome 补了 WS_CAPTION），
        // 这里不再安排自己的自测钩子 —— 真机验证交给 tools/probe-native-anim.ps1。
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (App.Services.GetService(typeof(Core.Configuration.ConfigService))
            is not Core.Configuration.ConfigService config)
        {
            return;
        }

        // 先让动画就地收尾，再读尺寸 —— 动画途中关窗会读到中间值。
        _animator.Settle();

        // 只记"正常态"的尺寸：最大化记的是最大化后的尺寸，下次恢复会得到一个「假最大化」的窗口；
        // 最小化时 Width/Height 是系统给的最小化矩形，记下来更糟。
        if (WindowState == WindowState.Normal)
        {
            config.Current.WindowWidth = Width;
            config.Current.WindowHeight = Height;
        }

        config.Current.StartMaximized = WindowState == WindowState.Maximized;
        config.Save();
    }

    /// <summary>XAML 里 8 个抓手的 PointerPressed 都指向这里，实际逻辑在 WindowResizeGrips。</summary>
    private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
        => _grips.OnGripPressed(sender, e);
}