// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace HeliumTodo.Desktop.Views;

/// <summary>「待办事项」页面。常用分类按钮直接绑 UseCategoryPresetCommand。</summary>
public partial class TodoView : UserControl
{
    public TodoView()
    {
        InitializeComponent();
    }
}