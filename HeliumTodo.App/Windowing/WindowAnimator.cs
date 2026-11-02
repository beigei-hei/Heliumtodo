using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

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

    public WindowAnimator(Window window, InteractionQuality quality)
    {
        _window = window;
        _quality = quality;
    }

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
    /// 窗口出现时的过渡：整体淡入 + 从 96% 放大到 100%。
    ///
    /// 只动两样东西：
    ///   · 窗口自己的 Opacity（0 → 1）—— 这是"淡入"；
    ///   · 内容根元素的 RenderTransform 缩放 —— Avalonia 的 Window 不能直接设缩放，
    ///     但把缩放挂到内容的变换上，视觉上等价。
    /// 为什么不用 Animation/KeyFrame：这里要的帧数、缓动、收尾清理都很明确，
    /// 与其和动画系统的生命周期打交道，不如复用下面那套"定时器切帧"，行为一眼可数。
    /// </summary>
    public void PlayIntro()
    {
        // 最大值 / 最小化状态不该播开场动画（用户刚从托盘唤醒时窗口已经在那儿了）
        if (_window.Content is not Control content) return;

        if (content.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            content.RenderTransform = scale;
            content.RenderTransformOrigin = RelativePoint.Center;
        }

        _window.Opacity = 0;
        scale.ScaleX = WindowAnimationMath.IntroStartScale;
        scale.ScaleY = WindowAnimationMath.IntroStartScale;

        // 计时不能从"现在"开始：窗口刚 Show 出来时第一次布局/渲染可能就要上百毫秒，
        // 等第一帧到来时 180ms 早就过去了 —— 那样动画会一步跳到终点，等于没播。
        // 所以计时从"第一帧真的到来"那一刻起算，保证这 180 毫秒里是有画面的。
        // 这 180 毫秒里窗口每帧都要整窗重画，先把"贵但看不出差别"的东西降级
        BeginQuality();

        Stopwatch? clock = null;
        var timer = new DispatcherTimer { Interval = WindowAnimationMath.FrameInterval };
        _timer = timer;
        _pendingFinish = null;

        timer.Tick += (_, _) =>
        {
            clock ??= Stopwatch.StartNew();
            var progress = WindowAnimationMath.ProgressOf(clock.Elapsed);
            var t = WindowAnimationMath.EaseOut(progress);

            _window.Opacity = t;
            var s = WindowAnimationMath.IntroStartScale + (1 - WindowAnimationMath.IntroStartScale) * t;
            scale.ScaleX = s;
            scale.ScaleY = s;

            if (_traceIntro && Trace is { } trace)
            {
                trace($"[开场动画] progress={progress:0.00} opacity={_window.Opacity:0.00} scale={s:0.000}");
            }

            if (progress < 1) return;

            timer.Stop();
            _timer = null;

            // 收尾一定要回到"完全体"，别留下 0.99 的透明度或 0.999 的缩放
            _window.Opacity = 1;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            EndQuality();
        };

        timer.Start();
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

        // 透明度与缩放回到"完全体"，顺便把这个 ScaleTransform 留着复用（下次动画还要用）
        _window.Opacity = 1;
        if (_window.Content is Control { RenderTransform: ScaleTransform scale })
        {
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

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