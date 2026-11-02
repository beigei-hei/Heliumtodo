using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using HeliumTodo.Core.Windowing;

// WindowState 既是 Window 的属性名，也是枚举名。在窗口类里裸写会解析成属性，
// 所以给枚举起个别名，代码里就不会误读成"属性 = 属性"。
using WindowStateKind = Avalonia.Controls.WindowState;

namespace HeliumTodo.App.Windowing;

/// <summary>
/// 窗口的过渡动画：最大化 / 还原、以及打开时的出现动画。
///
/// 为什么要自己写（而不是指望系统）：
///   ① 最大化动画由 DWM 负责，而它只对"系统认为正常的窗口"生效。我们为了壁纸铺满，
///      做了两处非常规处理（WindowDecorations="None" + 把 WM_NCCALCSIZE 的非客户区抹成 0），
///      样式位虽然补齐了 WS_THICKFRAME，DWM 仍然不给动画 —— 实测点最大化就是瞬间跳变，
///      DwmGetWindowAttribute 也确认没有任何开关被关掉。
///   ② 打开动画同理：窗口是自绘的，系统那套"淡入 + 由小到大"自然不会替我们播。
///   与其继续猜 DWM 的脾气，不如把这 180 毫秒交给自己：窗口本来就是自绘的，
///   尺寸也一直是我们自己在改（见 WindowResizeGrips）。
///
/// 实现上只有一件事：把"起点 → 终点"按缓动曲线切成十几帧，每帧设一次值。
/// 纯托管，不碰任何平台 API，所以三个平台都能跑。
/// </summary>
internal sealed class WindowAnimator
{
    private readonly Window _window;
    private readonly InteractionQuality _quality;

    /// <summary>动画期间持有"降质"作用域，动画结束/被打断时释放。</summary>
    private IDisposable? _qualityScope;

    /// <summary>动画进行中的定时器。null = 没在动画。</summary>
    private DispatcherTimer? _timer;

    /// <summary>最大化之前那一份"还原尺寸"，还原动画要用它当终点。</summary>
    private PixelRect? _restoreRect;

    /// <summary>当前动画播完该做的事（比如把 WindowState 切成 Maximized）。中断时要替它做掉。</summary>
    private Action? _pendingFinish;

    /// <summary>开场动画用的遮罩（由 MainWindow 注入，名字是 IntroVeil）。</summary>
    private Border? _veil;

    /// <summary>遮罩画刷：只改它的 Alpha，不换实例（换实例会触发样式重新求值）。</summary>
    private readonly SolidColorBrush _veilBrush = new(Color.FromArgb(255, 255, 255, 255));

    /// <summary>开场动画的计时（从第一帧真正到来起算）。</summary>
    private Stopwatch? _introClock;

    /// <summary>保险丝：帧请求丢了也要强制收尾。</summary>
    private DispatcherTimer? _introWatchdog;

    /// <summary>开场动画已经播了多少帧（只用于日志）。</summary>
    private int _introFrame;

    /// <summary>上一帧已经播到的进度（用来夹住单帧步进，见 WindowTransitionPolicy）。</summary>
    private double _introProgress;

    public WindowAnimator(Window window, InteractionQuality quality)
    {
        _window = window;
        _quality = quality;
    }

    /// <summary>
    /// 把开场动画用的遮罩交给动画器。由 MainWindow 在 InitializeComponent 之后调用 ——
    /// 用显式注入而不是在可视树里按名字找，因为无头环境里第一次调用动画时
    /// 模板可能还没应用完，找不到就会静默不播。
    /// </summary>
    public void SetIntroVeil(Border veil) => _veil = veil;

    /// <summary>
    /// 诊断输出口。App 会把它接到 background.log —— 窗口透明度在 Win32 那边读不出来，
    /// 只有我们自己报出来，外部探针才能确认"这 180 毫秒里帧真的是一帧一帧来的"。
    /// </summary>
    public Action<string>? Trace { get; set; }

    /// <summary>只有设了 HELIUM_INTRO_TRACE=1 才逐帧记日志，正常使用一行都不写。</summary>
    private readonly bool _traceIntro = Environment.GetEnvironmentVariable("HELIUM_INTRO_TRACE") == "1";

    /// <summary>
    /// 只有设了 HELIUM_ANIM_TRACE=1 才逐帧记"这一帧花了多久"。
    /// 用来在真实窗口上确认优化真的生效（无头自检的数字和真机不一定一样）。
    /// </summary>
    private readonly bool _traceAnim = Environment.GetEnvironmentVariable("HELIUM_ANIM_TRACE") == "1";

    /// <summary>动画是否正在进行。动画期间忽略重复点击，避免两个定时器互相打架。</summary>
    public bool IsAnimating => _timer is not null;

    // ───────────────────────── 最大化 / 还原 ─────────────────────────

    /// <summary>
    /// 最大化 / 还原（带过渡）。标题栏的双击和右上角的按钮都走这里。
    /// </summary>
    public void ToggleMaximize()
    {
        if (IsAnimating) return;

        if (_window.WindowState == WindowStateKind.Maximized)
        {
            Restore();
        }
        else
        {
            Maximize();
        }
    }

    private void Maximize()
    {
        var target = GetWorkArea();
        if (target is not { } workArea)
        {
            // 拿不到显示器信息（极少数情况）就退回系统的瞬间最大化，至少能用
            _window.WindowState = WindowStateKind.Maximized;
            return;
        }

        var start = CurrentPixelRect();
        _restoreRect = start;

        AnimateWindow(start, workArea, () => _window.WindowState = WindowStateKind.Maximized);
    }

    private void Restore()
    {
        if (_restoreRect is not { } target)
        {
            _window.WindowState = WindowStateKind.Normal;
            return;
        }

        var start = CurrentPixelRect();

        // 先让系统把窗口切回 Normal（它会自己摆到"还原尺寸"——也就是 target），
        // 然后立刻把窗口按回最大化的样子，再开始做动画。
        // 这两步在同一个 UI 帧里完成，屏幕上不会闪。
        _window.WindowState = WindowStateKind.Normal;
        ApplyRect(start);

        AnimateWindow(start, target, static () => { });
    }

    // ───────────────────────── 打开 / 唤醒 ─────────────────────────

    /// <summary>
    /// 窗口出现时的过渡：把盖在界面最上层的一层遮罩从"不透明"淡到"透明"。
    ///
    /// 为什么不再改 Window.Opacity：
    ///   给窗口设小于 1 的不透明度，Win32 会走 WS_EX_LAYERED（分层窗口）那条路，
    ///   每帧都要把整窗重新合成一次。实测 1920×1280 下这一帧 37 ms（见 --perf-check 的 F 档），
    ///   而且改扩展样式会让 DWM 重建重定向表面 —— 那正好也会让系统原生的
    ///   最小化/最大化过渡播不出来。所以这条路必须彻底放弃。
    ///
    /// 现在只改一层遮罩画刷的 Alpha：
    ///   · 改画刷颜色不会 push 离屏图层，成本就是"重绘一帧"（实测 4.8 ms，和纯重绘等价）；
    ///   · 窗口本身始终是完全不透明的，DWM 那边看到的就是一个正常窗口；
    ///   · 顺便也不再缩放内容 —— 那 4% 的缩放在 180 毫秒里几乎看不出来，
    ///     却要让整棵内容树每帧重算，而且会把抓手坐标算偏（曾经偏 26 px）。
    ///
    /// 帧驱动换成 RequestAnimationFrame：定时器按 11.25 ms 排，而真机一帧要 45 ms，
    /// 排进来的帧只会越积越多，量出来是 18 / 91 / 64 / 58 这种极不均匀的间隔 ——
    /// 不均匀比慢更显卡。交给合成器"下一帧叫我"就没有这个问题。
    /// </summary>
    public void PlayIntro()
    {
        if (_veil is null) return;

        _window.Opacity = 1;

        // 遮罩颜色跟界面底色一致，淡出时看到的就是"内容从底色里浮出来"，
        // 不会出现黑闪或白闪。
        _veilBrush.Color = _window.ActualThemeVariant == ThemeVariant.Dark
            ? Color.FromArgb(255, 0x1C, 0x1C, 0x1C)
            : Color.FromArgb(255, 0xFF, 0xFF, 0xFF);
        _veil.Background = _veilBrush;
        _veil.IsVisible = true;
        _veil.Opacity = 1;

        _pendingFinish = null;
        _introFrame = 0;
        _introProgress = 0;
        RequestIntroFrame();
    }

    /// <summary>开场动画推迟到下一帧再走一步（合成器每画一帧回调一次）。</summary>
    private void RequestIntroFrame()
    {
        if (_veil is null) return;

        var topLevel = TopLevel.GetTopLevel(_window);
        if (topLevel is null)
        {
            FinishIntro();
            return;
        }

        // 保险丝：万一某一帧的请求丢了（窗口被藏起来、合成器停了），
        // 也不能让遮罩永久留在屏幕上 —— 动画时长再多给 300 ms 就强制收尾。
        _introWatchdog ??= new DispatcherTimer { Interval = WindowAnimationMath.Duration + TimeSpan.FromMilliseconds(300) };
        _introWatchdog.Tick -= OnIntroWatchdogTick;
        _introWatchdog.Tick += OnIntroWatchdogTick;
        _introWatchdog.Stop();
        _introWatchdog.Start();

        topLevel.RequestAnimationFrame(OnIntroFrame);
    }

    private void OnIntroFrame(TimeSpan _)
    {
        _introWatchdog?.Stop();

        // 记第一帧而不是"点开动画那一刻"：窗口刚 Show 出来时首次布局/渲染可能上百毫秒，
        // 从点开计时会把整段动画吞掉（曾经只剩 4 帧）。
        _introClock ??= Stopwatch.StartNew();
        var raw = WindowAnimationMath.ProgressOf(_introClock.Elapsed);

        // 单帧最多推进 1/5：冷启动时第二帧可能迟到 170 ms，那时 raw 已经是 1，
        // 直接用它就变成"闪一下"。夹一下之后帧慢了动画就慢，但每一帧都看得见。
        var progress = WindowTransitionPolicy.ClampIntroProgress(_introProgress, raw);
        _introProgress = progress;
        var t = WindowAnimationMath.EaseOut(progress);
        _introFrame++;

        if (_veil is not null)
        {
            var alpha = (byte)Math.Round(255 * (1 - t));
            _veilBrush.Color = Color.FromArgb(alpha, _veilBrush.Color.R, _veilBrush.Color.G, _veilBrush.Color.B);
        }

        if (_traceIntro && Trace is { } trace)
        {
            trace($"[开场动画] 第 {_introFrame} 帧 progress={progress:0.00} 遮罩={_veilBrush.Color.A}");
        }

        if (progress < 1)
        {
            RequestIntroFrame();
            return;
        }

        FinishIntro();
    }

    private void OnIntroWatchdogTick(object? sender, EventArgs e)
    {
        if (_traceIntro && Trace is { } trace)
        {
            trace("[开场动画] 等不到下一帧，强制收尾");
        }

        FinishIntro();
    }

    /// <summary>开场动画收尾：遮罩藏起来、计时清掉。必须幂等（被打断时会再调一次）。</summary>
    private void FinishIntro()
    {
        _introWatchdog?.Stop();

        if (_veil is not null)
        {
            _veil.IsVisible = false;
            _veil.Opacity = 1;
        }

        _introClock = null;
        _introFrame = 0;
        _introProgress = 0;
    }

    /// <summary>
    /// 立刻结束进行中的动画，把窗口还原成"完全体"。
    ///
    /// 为什么必须有这个：动画是靠定时器一帧一帧推的，万一最后一帧没来（窗口在动画中途被隐藏、
    /// 关闭，或者系统忙到把定时器丢了），窗口就会永久停在半透明 + 96% 缩放的状态。
    /// 这不是理论问题 —— 无头自检里定时器根本不触发，窗口一直保持 opacity=0、scale=0.96，
    /// 连"按在右边缘上"都会因为坐标被缩放而算错位置。
    /// 所以只要窗口要藏起来或要关掉，就先调一次这里。
    /// </summary>
    public void Settle()
    {
        EndQuality();

        if (_timer is { } running)
        {
            running.Stop();
            _timer = null;
        }

        // 收尾一律回到"完全体"：窗口不透明（我们本来就不改它，这里兜底）、
        // 内容不留任何变换（残留缩放会让 TranslatePoint 把抓手坐标算偏）、
        // 遮罩藏起来。三件事都必须走这里，因为动画随时可能被打断。
        _window.Opacity = 1;
        if (_window.Content is Control { RenderTransform: not null } content)
        {
            content.RenderTransform = null;
            content.RenderTransformOrigin = RelativePoint.Center;
        }

        FinishIntro();

        // 动画被打断时，本该在收尾阶段完成的状态切换不能丢：比如最大化动画播到一半被中断，
        // 窗口尺寸已经停在中间值了，这时必须真的切成 Maximized，交给系统摆正。
        var finish = _pendingFinish;
        _pendingFinish = null;
        finish?.Invoke();
    }

    // ───────────────────────── 共用 ─────────────────────────

    /// <summary>进入"降质"状态（幂等：动画互相打断时只算一次）。</summary>
    private void BeginQuality() => _qualityScope ??= _quality.Begin();

    /// <summary>退出"降质"状态。动画正常收尾和被打断（Settle）两条路都要走这里。</summary>
    private void EndQuality()
    {
        _qualityScope?.Dispose();
        _qualityScope = null;
    }

    private void AnimateWindow(PixelRect from, PixelRect to, Action onFinished)
    {
        // 改尺寸的每一帧都要整窗重排重画（软件渲染下 45~58 ms），降质能砍掉一半左右。
        // 曾经还试过"拍一张快照、动画期间只拉伸这张图"，真机上量下来一点没快 ——
        // 说明软件渲染下这部分开销不在"画内容"上，而在"换尺寸本身"（见 PROGRESS）。
        BeginQuality();

        Stopwatch? clock = null;
        var timer = new DispatcherTimer { Interval = WindowAnimationMath.FrameInterval };
        _timer = timer;
        _pendingFinish = onFinished;

        var lastFrameMs = 0.0;

        timer.Tick += (_, _) =>
        {
            clock ??= Stopwatch.StartNew();
            var elapsedMs = clock.Elapsed.TotalMilliseconds;
            var progress = WindowAnimationMath.ProgressOf(clock.Elapsed);

            if (_traceAnim && Trace is { } trace)
            {
                trace($"[窗口动画] 帧间隔 {(elapsedMs - lastFrameMs):0.0} ms progress={progress:0.00}");
            }

            lastFrameMs = elapsedMs;
            ApplyRect(WindowAnimationMath.Interpolate(from, to, WindowAnimationMath.EaseOut(progress)));

            if (progress < 1) return;

            timer.Stop();
            _timer = null;
            _pendingFinish = null;
            ApplyRect(to);
            onFinished();
            EndQuality();
        };

        timer.Start();
    }

    /// <summary>当前窗口的物理矩形（位置是物理像素，宽高是 DIP，要先换算）。</summary>
    private PixelRect CurrentPixelRect()
    {
        var scaling = _window.RenderScaling <= 0 ? 1.0 : _window.RenderScaling;
        return new PixelRect(
            _window.Position,
            new PixelSize(
                Math.Max(1, (int)Math.Round(_window.Width * scaling)),
                Math.Max(1, (int)Math.Round(_window.Height * scaling))));
    }

    /// <summary>把物理矩形套到窗口上。</summary>
    private void ApplyRect(PixelRect rect)
    {
        var scaling = _window.RenderScaling <= 0 ? 1.0 : _window.RenderScaling;

        // 先改位置再改大小：反过来的话窗口会先长到旧位置外面一帧，看着像"抖一下"
        _window.Position = new PixelPoint(rect.X, rect.Y);
        _window.Width = rect.Width / scaling;
        _window.Height = rect.Height / scaling;
    }

    /// <summary>当前显示器的工作区（扣掉任务栏），物理像素。拿不到就返回 null。</summary>
    private PixelRect? GetWorkArea()
    {
        var screen = _window.Screens.ScreenFromWindow(_window) ?? _window.Screens.Primary;
        return screen?.WorkingArea;
    }

    /// <summary>
    /// 仅用于自动化验证：设了环境变量 HELIUM_MAXIMIZE_TEST 时，
    /// 过一会儿自己点一次"最大化"，1.5 秒后再还原。
    /// 外部探针（tools/probe-maximize-anim.ps1）会在这段时间里高频采样窗口矩形，
    /// 看尺寸是不是逐帧变化 —— 这是"动画真的在跑"而不是"瞬间跳变"的唯一客观证据。
    /// 正常使用时这个变量不存在，这段代码不会执行。
    /// </summary>
    public void ScheduleSelfTestIfRequested()
    {
        // 值可以是毫秒数（给探针留出"找到窗口再开始采样"的时间），写 "1" 就用默认 4 秒
        var raw = Environment.GetEnvironmentVariable("HELIUM_MAXIMIZE_TEST");
        if (string.IsNullOrWhiteSpace(raw) || raw == "0") return;

        var delayMs = int.TryParse(raw, out var parsed) && parsed > 100 ? parsed : 4000;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ToggleMaximize();

            // 再等 1.5 秒还原一次 —— 还原走的是另一条分支（先 Normal 再往回缩），
            // 一并验证掉，省得只测了一半。
            var restore = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            restore.Tick += (_, _) =>
            {
                restore.Stop();
                ToggleMaximize();
            };
            restore.Start();
        };
        timer.Start();
    }
}