// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace HeliumTodo.Desktop.Views;

/// <summary>「自习室」页面。输入框回车发送由 EnterKeyCommandBehavior 处理。</summary>
public partial class StudyRoomView : UserControl
{
    public StudyRoomView()
    {
        InitializeComponent();
    }
}