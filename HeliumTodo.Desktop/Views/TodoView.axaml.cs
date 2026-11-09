// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using HeliumTodo.Desktop.ViewModels;

namespace HeliumTodo.Desktop.Views;

/// <summary>「待办事项」页面。</summary>
public partial class TodoView : UserControl
{
    public TodoView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 点了「学习 / 生活 / 社团」这些常用分类按钮。
    ///
    /// 为什么用事件而不是命令？
    ///   这些按钮的 Tag 上直接写着分类名，用事件取 Tag 再写回 ViewModel 最直观，
    ///   比给每个按钮配一条命令、再传参数要少写不少代码。
    /// </summary>
    private void OnCategoryPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string category }) return;
        if (DataContext is not TodoViewModel vm) return;

        vm.NewCategory = category;
    }
}
