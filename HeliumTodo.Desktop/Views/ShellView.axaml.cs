// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HeliumTodo.Desktop.ViewModels;

namespace HeliumTodo.Desktop.Views;

/// <summary>
/// 应用外壳：自绘标题栏 + 左侧图标轨 + 内容区。
///
/// 标题栏拖动、双击最大化、三个窗口按钮、轨道展开与 8 个改大小抓手
/// 都已迁到 <c>Behaviors/</c>（P5）；这里只处理"启动时导航一次"和"点轨道条目"。
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
}