// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>
/// 让标题栏最大化按钮的字形跟随窗口状态：最大化显示「还原」，普通显示「最大化」。
///
/// 为什么值得单独一个行为：双击标题栏、点按钮、Win+↑、DWM 系统菜单都会改
/// <see cref="Window.WindowState"/>，如果只在 XAML 里画死一个字形，界面就会和
/// 真实状态脱节——这正是「右上角 UI 不同步」的复现条件。
/// </summary>
public sealed class WindowStateGlyphBehavior : Behavior<TextBlock>
{
    /// <summary>Windows 自带图标字体里的「最大化」字形。</summary>
    public const string MaximizeGlyph = "\uE922";

    /// <summary>Windows 自带图标字体里的「还原」字形。</summary>
    public const string RestoreGlyph = "\uE923";

    private Window? _window;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is null) return;

        if (TopLevel.GetTopLevel(AssociatedObject) is Window window)
        {
            AttachTo(window);
        }
        else
        {
            // Behavior 可能在控件进可视树之前挂上（XAML 加载顺序），
            // 先等 AttachedToVisualTree 再找窗口。
            AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (AssociatedObject is not null)
        {
            AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
        }

        DetachFromWindow();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (AssociatedObject is null) return;

        AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;

        if (TopLevel.GetTopLevel(AssociatedObject) is Window window)
        {
            AttachTo(window);
        }
    }

    private void AttachTo(Window window)
    {
        DetachFromWindow();
        _window = window;
        _window.PropertyChanged += OnWindowPropertyChanged;
        UpdateGlyph();
    }

    private void DetachFromWindow()
    {
        if (_window is null) return;

        _window.PropertyChanged -= OnWindowPropertyChanged;
        _window = null;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            UpdateGlyph();
        }
    }

    private void UpdateGlyph()
    {
        if (AssociatedObject is null) return;

        var state = _window?.WindowState ?? WindowState.Normal;
        AssociatedObject.Text = state is WindowState.Maximized or WindowState.FullScreen
            ? RestoreGlyph
            : MaximizeGlyph;
    }
}
