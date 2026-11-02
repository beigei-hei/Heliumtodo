import io, sys

path = r"D:\代码\dsh project\Helium todo demo\HeliumTodo.App\Windowing\WindowAnimator.cs"
text = io.open(path, encoding="utf-8-sig").read()
orig = text
hits = []


def replace(old, new, label):
    global text
    if old not in text:
        print("MISS:", label)
        sys.exit(1)
    text = text.replace(old, new, 1)
    hits.append(label)


# ── 1. 类注释：不再自己写最大化动画 ──
replace(
    """/// <summary>
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
/// </summary>""",
    """/// <summary>
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
/// </summary>""",
    "class-doc",
)

# ── 2. 删掉只服务旧动画的三个字段 ──
replace(
    """    /// <summary>动画进行中的定时器。null = 没在动画。</summary>
    private DispatcherTimer? _timer;

    /// <summary>最大化之前那一份"还原尺寸"，还原动画要用它当终点。</summary>
    private PixelRect? _restoreRect;

    /// <summary>当前动画播完该做的事（比如把 WindowState 切成 Maximized）。中断时要替它做掉。</summary>
    private Action? _pendingFinish;

""",
    "",
    "fields-timer",
)

# ── 3. 构造函数不再需要 InteractionQuality（降质由 WindowResizeGrips 直接用） ──
replace(
    """    public WindowAnimator(Window window, InteractionQuality quality)
    {
        _window = window;
        _quality = quality;
    }""",
    """    public WindowAnimator(Window window) => _window = window;""",
    "ctor",
)

replace(
    """    private readonly Window _window;
    private readonly InteractionQuality _quality;

    /// <summary>动画期间持有"降质"作用域，动画结束/被打断时释放。</summary>
    private IDisposable? _qualityScope;

""",
    """    private readonly Window _window;

""",
    "fields-quality",
)

# ── 4. 删 _traceAnim、IsAnimating 以及整块"最大化 / 还原" ──
start = text.index("""    /// <summary>
    /// 只有设了 HELIUM_ANIM_TRACE=1 才逐帧记"这一帧花了多久"。""")
end = text.index("    // ───────────────────────── 打开 / 唤醒 ─────────────────────────")
text = text[:start] + text[end:]
hits.append("maximize-section")

# ── 5. Settle 里不再替被打断的动画补做状态切换 ──
replace(
    """        FinishIntro();

        // 动画被打断时，本该在收尾阶段完成的状态切换不能丢：比如最大化动画播到一半被中断，
        // 窗口尺寸已经停在中间值了，这时必须真的切成 Maximized，交给系统摆正。
        var finish = _pendingFinish;
        _pendingFinish = null;
        finish?.Invoke();
    }""",
    """        FinishIntro();
    }""",
    "settle-finish",
)

replace(
    """    public void Settle()
    {
        EndQuality();

        if (_timer is { } running)
        {
            running.Stop();
            _timer = null;
        }

        // 收尾""",
    """    public void Settle()
    {
        // 收尾""",
    "settle-head",
)

# ── 6. 删掉"共用"整段（降质作用域、逐帧改矩形、自测钩子） ──
start = text.index("    // ───────────────────────── 共用 ─────────────────────────")
end = text.rindex("}")
text = text[:start].rstrip("\n") + "\n}\n"
hits.append("shared-tail")

io.open(path, "w", encoding="utf-8", newline="\r\n").write(text)
print("ok:", ", ".join(hits))
print("lines:", orig.count("\n"), "->", text.count("\n"))