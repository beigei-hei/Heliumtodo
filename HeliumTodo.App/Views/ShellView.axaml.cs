using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HeliumTodo.App.ViewModels;

namespace HeliumTodo.App.Views;

/// <summary>
/// 应用外壳：自绘标题栏 + 左侧图标轨 + 内容区。
///
/// 这里有三类代码，各自解决一件事：
///   ① 启动时把第一个页面真的塞进内容区（否则窗口是空的）；
///   ② 图标轨上的按钮被点 → 交给 ViewModel 切页；
///   ③ 标题栏拖动 / 双击最大化 / 三个窗口按钮 —— 因为标题栏是我们自己画的，
///      系统不会替我们做这些。
/// </summary>
public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();

        // ⚠️ 启动时要把页面**真的导航一次**，不能只设 SelectedItem。
        //
        // ViewModel 的构造函数只是把 SelectedItem 指向第一项，
        // 那只改了它自己的字段 —— 内容区（CurrentPage）还是 null，窗口就是一片空白。
        // 改版前是靠"把选中项同步给导航栏控件、由控件的 SelectionChanged 补上"，
        // 现在导航栏是我们自己画的，没有那个事件了，所以直接在这里补。
        //
        // 这个坑值得留个记录：用「单独渲染每个页面」的自检永远发现不了它，
        // 因为那种渲染方式完全绕过了外壳 —— 当初就是加 --shell 渲染才暴露的。
        Loaded += (_, _) => EnsureFirstPage();
    }

    /// <summary>
    /// 保证内容区至少有一个页面。
    ///
    /// 为什么挂在 AttachedToVisualTree 上而不是只挂 Loaded：
    ///   Loaded 要等布局排完、窗口真正显示才发；而"被挂到可视化树上"一定会先发生。
    ///   无头渲染（自检）和真机启动都会走 AttachedToVisualTree，
    ///   但无头时 Loaded 的时机并不保证 —— 只挂 Loaded 就可能出现"内容区空白"，
    ///   而且这种空白在真机上看不出来（真机 Loaded 一定会发）。
    ///   两处都挂、方法本身幂等，代价几乎为零。
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureFirstPage();
    }

    private void EnsureFirstPage()
    {
        if (DataContext is not ShellViewModel shell) return;
        if (shell.CurrentPage is not null) return;
        if (shell.SelectedItem is null) return;

        _ = shell.NavigateAsync(shell.SelectedItem);
    }

    /// <summary>图标轨上的某一项被点了。</summary>
    private async void OnRailItemClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;
        if (sender is not Button { Tag: NavigationItemViewModel item }) return;

        await shell.NavigateAsync(item);
    }

    /// <summary>
    /// 点轨道顶部那个按钮：展开 / 收起"图标 + 名称"。
    ///
    /// 这里只改状态，宽度与动画交给 XAML 的绑定 + Transition ——
    /// 在代码里再手写一套逐帧动画，会和 Transition 抢同一个属性，
    /// 结果是"两个动画互相打断、看起来一顿一顿"。
    /// </summary>
    private void OnToggleRailClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        // 先报一声再切：轨道一宽，整页重排 + 整窗重画，这段时间要降质（见 MainWindow.BeginRailAnimation）。
        (TopLevel.GetTopLevel(this) as MainWindow)?.BeginRailAnimation();
        shell.ToggleRailCommand.Execute(null);
    }

    // ────────────── 标题栏 ──────────────

    /// <summary>
    /// 按住标题栏拖窗口。
    ///
    /// 只认左键：右键拖窗口是另一个系统的习惯，混进来会让人误触。
    /// 另外要排除按钮区域 —— 从按钮上按下时不该开始拖，否则点击会被拖拽吃掉。
    /// </summary>
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control source && IsInsideButton(source)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (TopLevel.GetTopLevel(this) is not Window window) return;
        if (window.WindowState == WindowState.FullScreen) return;

        window.BeginMoveDrag(e);
    }

    /// <summary>双击标题栏 = 最大化 / 还原。</summary>
    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source && IsInsideButton(source)) return;
        ToggleMaximize();
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }

    private void ToggleMaximize()
    {
        // 只切状态，过渡动画交给 DWM。
        // 以前这里走自己写的逐帧改窗口矩形版本，是因为样式位缺 WS_CAPTION，DWM 判定
        // "这不是正常窗口"就不播过渡；补上之后软件渲染下也是满帧（见 WindowChrome）。
        if (TopLevel.GetTopLevel(this) is not Window window) return;

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>往上找一找，这个控件是不是在某个按钮里面。</summary>
    private static bool IsInsideButton(Control? control)
    {
        for (var current = control; current is not null; current = current.Parent as Control)
        {
            if (current is Button) return true;

            // Button 的模板内容（TextBlock）Parent 可能不是 Button 而是它模板里的面板，
            // 所以再来一层 Visual 父级兜底。
            if (current.GetVisualParent() is Button) return true;
        }

        return false;
    }
}