// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>
/// 双击标题栏 = 最大化 / 还原。
///
/// 只切 <see cref="Window.WindowState"/>，过渡动画交给 DWM —— 见 ARCHITECTURE.md 的动效铁律。
/// </summary>
public sealed class ToggleMaximizeBehavior : Behavior<Control>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
        {
            AssociatedObject.DoubleTapped += OnDoubleTapped;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.DoubleTapped -= OnDoubleTapped;
        }

        base.OnDetaching();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source && BehaviorTargets.IsInsideButton(source)) return;
        if (BehaviorTargets.GetWindow(AssociatedObject) is not { } window) return;

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}