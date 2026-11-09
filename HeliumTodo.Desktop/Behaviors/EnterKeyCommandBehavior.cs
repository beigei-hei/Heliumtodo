// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>输入框里按回车直接执行命令（自习室发消息用）。</summary>
public sealed class EnterKeyCommandBehavior : Behavior<Control>
{
    /// <summary>回车时要执行的命令。</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<EnterKeyCommandBehavior, ICommand?>(nameof(Command));

    /// <summary>回车时要执行的命令。</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyDown += OnKeyDown;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyDown -= OnKeyDown;
        }

        base.OnDetaching();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Command?.CanExecute(null) != true) return;

        Command.Execute(null);
        e.Handled = true;
    }
}