// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using HeliumTodo.Desktop.ViewModels;
using HeliumTodo.Desktop.Views;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>
/// 图标轨顶部的展开 / 收起按钮。
///
/// 这里只改状态，宽度与动画交给 XAML 的绑定 + Transition ——
/// 在代码里再手写一套逐帧动画，会和 Transition 抢同一个属性，
/// 结果是"两个动画互相打断、看起来一顿一顿"。
/// </summary>
public sealed class ToggleRailBehavior : Behavior<Button>
{
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
        if (AssociatedObject?.DataContext is not ShellViewModel shell) return;

        // 先报一声再切：轨道一宽，整页重排 + 整窗重画，这段时间要降质
        // （见 MainWindow.BeginRailAnimation）。
        if (BehaviorTargets.GetWindow(AssociatedObject) is MainWindow window)
        {
            window.BeginRailAnimation();
        }

        if (shell.ToggleRailCommand.CanExecute(null))
        {
            shell.ToggleRailCommand.Execute(null);
        }
    }
}