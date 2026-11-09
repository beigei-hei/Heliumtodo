// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>三个自绘窗口按钮各自要做的动作。</summary>
public enum WindowAction
{
    /// <summary>最小化。</summary>
    Minimize,

    /// <summary>最大化 / 还原。</summary>
    Maximize,

    /// <summary>关闭窗口。</summary>
    Close,
}

/// <summary>
/// 自绘窗口按钮（最小化 / 最大化 / 关闭）。
///
/// 标题栏是我们自己画的，系统不会替我们接这三个按钮，所以这里显式声明动作。
/// </summary>
public sealed class WindowStateBehavior : Behavior<Button>
{
    /// <summary>按钮对应的窗口动作。</summary>
    public static readonly StyledProperty<WindowAction> ActionProperty =
        AvaloniaProperty.Register<WindowStateBehavior, WindowAction>(nameof(Action));

    /// <summary>按钮对应的窗口动作。</summary>
    public WindowAction Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
        {
            AssociatedObject.Click += OnClick;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.Click -= OnClick;
        }

        base.OnDetaching();
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        if (BehaviorTargets.GetWindow(AssociatedObject) is not { } window) return;

        switch (Action)
        {
            case WindowAction.Minimize:
                window.WindowState = WindowState.Minimized;
                break;
            case WindowAction.Maximize:
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                break;
            case WindowAction.Close:
                window.Close();
                break;
        }
    }
}