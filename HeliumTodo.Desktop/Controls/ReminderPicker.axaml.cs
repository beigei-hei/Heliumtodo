// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace HeliumTodo.Desktop.Controls;

/// <summary>
/// 「提醒」选择器：下拉选提前量，选到「自定义…」时多出小时 / 分钟两个数字框。
///
/// 逻辑全在 <see cref="ViewModels.ReminderEditState"/> 里，这里不需要代码 ——
/// 保留这个类是为了让 XAML 的 x:Class 有落点（也方便以后真要加交互时不必改结构）。
/// </summary>
public partial class ReminderPicker : UserControl
{
    public ReminderPicker()
    {
        InitializeComponent();
    }
}