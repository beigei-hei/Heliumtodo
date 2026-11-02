using Avalonia.Controls;
using Avalonia.Input;
using HeliumTodo.App.ViewModels;
using HeliumTodo.App.Windowing;

namespace HeliumTodo.App.Views;

/// <summary>
/// 主窗口。
///
/// 这个文件只做"窗口该做的事"，具体实现分给三个协作对象（都在 Windowing 目录下）：
///   · <see cref="WindowChrome"/>       —— 系统边框样式位 + 非客户区归零（Windows 专属）；
///   · <see cref="WindowAnimator"/>     —— 打开时的淡入、最大化 / 还原的过渡；
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

    public MainWindow()
    {
        InitializeComponent();

        // ① 外壳：把 WS_THICKFRAME 加回来（系统才当成普通窗口），再把非客户区抹成 0（壁纸铺满）
        WindowChrome.Attach(this);

        // ② 动画：打开淡入 / 最大化 / 还原
        //    动画和拖拽都要在"窗口正在动"的这段时间临时降质（见 InteractionQuality 的实测数字），
        //    所以先把降质开关建出来，两个协作对象共用它（引用计数，重叠也没事）。
        _quality = new InteractionQuality(this);
        _animator = new WindowAnimator(this, _quality) { Trace = App.LogBackground };

        // 开场动画靠这层遮罩淡出（而不是改窗口不透明度，见 WindowAnimator.PlayIntro 的注释）。
        _animator.SetIntroVeil(IntroVeil);

        // ③ 拖拽改大小
        _grips = new WindowResizeGrips(this, _quality);
        _grips.Attach();

        Opened += OnOpened;
        Closing += OnClosing;
    }

    /// <summary>最大化 / 还原（带过渡）。标题栏双击和右上角按钮都走这里。</summary>
    public void ToggleMaximizeAnimated() => _animator.ToggleMaximize();

    /// <summary>
    /// 从托盘唤回时再播一次"出现"动画 —— 窗口是 Hide/Show 的，不走 Opened，
    /// 所以由调用方（App 的 RestoreMainWindow）显式叫一下。
    /// </summary>
    public void PlayShowAnimation() => _animator.PlayIntro();

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

        // 窗口出现的过渡：淡入 + 轻微放大（对齐 Windows 11 的观感）
        _animator.PlayIntro();

        // 只在自动化验证时生效（环境变量不设就什么都不做）
        _animator.ScheduleSelfTestIfRequested();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (App.Services.GetService(typeof(Core.Configuration.ConfigService))
            is not Core.Configuration.ConfigService config)
        {
            return;
        }

        // 最大化时不要把最大化后的尺寸记下来，否则下次恢复会得到一个「假最大化」的窗口
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