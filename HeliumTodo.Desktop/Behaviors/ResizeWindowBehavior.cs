// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using HeliumTodo.Desktop.Views;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>
/// 四边四角的改大小抓手。
///
/// 方向仍然写在 XAML 的 <c>Tag</c> 上（North / SouthWest …），
/// 真正的逐帧调整交给 <see cref="WindowResizeGrips"/> —— 这里只负责把事件交给窗口。
/// </summary>
public sealed class ResizeWindowBehavior : Behavior<Control>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
        {
            AssociatedObject.PointerPressed += OnPointerPressed;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.PointerPressed -= OnPointerPressed;
        }

        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (BehaviorTargets.GetWindow(AssociatedObject) is MainWindow window)
        {
            window.HandleGripPressed(AssociatedObject, e);
        }
    }
}