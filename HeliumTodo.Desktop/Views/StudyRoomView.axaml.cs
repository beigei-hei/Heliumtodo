// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using HeliumTodo.Desktop.ViewModels;

namespace HeliumTodo.Desktop.Views;

/// <summary>「自习室」页面。</summary>
public partial class StudyRoomView : UserControl
{
    public StudyRoomView()
    {
        InitializeComponent();
    }

    /// <summary>在消息输入框里按回车就直接发送（不用去点按钮）。</summary>
    private void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (DataContext is StudyRoomViewModel vm && vm.SendMessageCommand.CanExecute(null))
        {
            vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }
}
