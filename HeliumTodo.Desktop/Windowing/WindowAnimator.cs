// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using HeliumTodo.Domain.Windowing;

namespace HeliumTodo.Desktop.Windowing;

/// <summary>
/// 窗口的过渡动画：只剩"打开时的出现动画"一件事。
///
/// 最大化 / 还原已经不归这里管了 —— 那 180 毫秒交给 DWM：
///   我们为了壁纸铺满做了两处非常规处理（WindowDecorations="None" + 把 WM_NCCALCSIZE 的
///   非客户区抹成 0），以前样式位只补了 WS_THICKFRAME，DWM 判定"这不是正常窗口"就不播过渡，
///   于是自己写了逐帧改窗口矩形的动画（软件渲染下每帧 45~58 ms，5 帧跑完，看着还是卡）。
///   真正的开关是 WS_CAPTION（= WS_BORDER | WS_DLGFRAME）：补上之后 DWM 就在合成器里播，
///   软件渲染下也是满帧，连 Win+↑ / 拖到顶边 / Aero Snap 一起白捡。见 WindowChrome。
///
/// 打开动画为什么也不改 Window.Opacity：
///   给窗口设小于 1 的不透明度会走 WS_EX_LAYERED（分层窗口），每帧整窗重新合成，
///   还会让 DWM 重建重定向表面 —— 那正好会让它拒绝播上面那些原生过渡。
///   现在只改一层遮罩画刷的 Alpha，成本等于"重绘一帧"（实测 4.8 ms）。
/// </summary>
internal sealed class WindowAnimator
{
    private readonly Window _window;

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

    public WindowAnimator(Window window) => _window = window;

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
    }
}
