// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>
/// 按住标题栏拖窗口。
///
/// 只认左键：右键拖窗口是另一个系统的习惯，混进来会让人误触。
/// 另外要排除按钮区域 —— 从按钮上按下时不该开始拖，否则点击会被拖拽吃掉。
/// </summary>
public class DragWindowBehavior : Behavior<Control>
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

    /// <summary>真正启动系统拖拽。单独抽出来，无头自检才能记录"确实调用了"。</summary>
    protected virtual void StartDrag(Window window, PointerPressedEventArgs e)
        => window.BeginMoveDrag(e);

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control source && BehaviorTargets.IsInsideButton(source)) return;
        if (!e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed) return;
        if (BehaviorTargets.GetWindow(AssociatedObject) is not { } window) return;
        if (window.WindowState == WindowState.FullScreen) return;

        StartDrag(window, e);
    }
}