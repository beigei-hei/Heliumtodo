import io, sys

root = "D:\\代码\\dsh project\\Helium todo demo\\"


def patch(rel, pairs):
    path = root + rel
    text = io.open(path, encoding="utf-8-sig").read()
    for label, old, new in pairs:
        if old not in text:
            print("MISS:", rel, label)
            sys.exit(1)
        text = text.replace(old, new, 1)
    io.open(path, "w", encoding="utf-8", newline="\r\n").write(text)
    print("ok:", rel)


patch(
    "HeliumTodo.App\\Views\\MainWindow.axaml.cs",
    [
        (
            "ctor",
            """        // ② 动画：打开淡入 / 最大化 / 还原
        //    动画和拖拽都要在"窗口正在动"的这段时间临时降质（见 InteractionQuality 的实测数字），
        //    所以先把降质开关建出来，两个协作对象共用它（引用计数，重叠也没事）。
        _quality = new InteractionQuality(this);
        _animator = new WindowAnimator(this, _quality) { Trace = App.LogBackground };""",
            """        // ② 动画：只剩"出现时淡出遮罩"这一件（最大化和还原交给 DWM，见 WindowChrome）。
        //    拖拽改大小要在"窗口正在动"的这段时间临时降质（见 InteractionQuality 的实测数字）。
        _quality = new InteractionQuality(this);
        _animator = new WindowAnimator(this) { Trace = App.LogBackground };""",
        ),
        (
            "toggle",
            """    /// <summary>最大化 / 还原（带过渡）。标题栏双击和右上角按钮都走这里。</summary>
    public void ToggleMaximizeAnimated() => _animator.ToggleMaximize();

""",
            "",
        ),
        (
            "intro-comment",
            """        // 窗口出现的过渡：淡入 + 轻微放大（对齐 Windows 11 的观感）
        _animator.PlayIntro();

        // 只在自动化验证时生效（环境变量不设就什么都不做）
        _animator.ScheduleSelfTestIfRequested();""",
            """        // 窗口出现的过渡：一层遮罩从实到透（对齐 Windows 11 的观感）
        _animator.PlayIntro();

        // 最大化 / 最小化的过渡动画由 DWM 负责（WindowChrome 补了 WS_CAPTION），
        // 这里不再安排自己的自测钩子 —— 真机验证交给 tools/probe-native-anim.ps1。""",
        ),
    ],
)

patch(
    "HeliumTodo.App\\Views\\ShellView.axaml.cs",
    [
        (
            "toggle",
            """    private void ToggleMaximize()
    {
        // 最大化走 MainWindow 自己写的过渡动画（Win11 的 DWM 动画对无边框窗口不生效，
        // 实测点一下是瞬间跳变）。非主窗口（比如渲染检查里的小窗口）就退回系统行为。
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
        {
            mainWindow.ToggleMaximizeAnimated();
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window window) return;

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }""",
            """    private void ToggleMaximize()
    {
        // 只切状态，过渡动画交给 DWM。
        // 以前这里走自己写的逐帧改窗口矩形版本，是因为样式位缺 WS_CAPTION，DWM 判定
        // "这不是正常窗口"就不播过渡；补上之后软件渲染下也是满帧（见 WindowChrome）。
        if (TopLevel.GetTopLevel(this) is not Window window) return;

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }""",
        )
    ],
)