// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HeliumTodo.Desktop.ViewModels;

namespace HeliumTodo.Desktop;

/// <summary>
/// 「ViewModel → View」的自动对应器。
///
/// Avalonia 在界面上遇到一个 ViewModel 对象时，会问这个类「该用什么控件来显示它」。
/// 命名约定：把 ViewModel 的类型名结尾的 "ViewModel" 换成 "View"，
/// 就是它对应的界面类：
///     HeliumTodo.Desktop.ViewModels.DashboardViewModel
///         ↓
///     HeliumTodo.Desktop.Views.DashboardView
///
/// 加了新页面只要按这个命名，就不用写任何绑定代码。
/// </summary>
public class ViewLocator : IDataTemplate
{
    /// <summary>根据 ViewModel 造出对应的界面控件。</summary>
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        // 例如 HeliumTodo.Desktop.ViewModels.DashboardViewModel
        var viewModelName = param.GetType().FullName!;

        // 换成 HeliumTodo.Desktop.Views.DashboardView
        var viewName = viewModelName
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var type = Type.GetType(viewName);

        if (type is null)
        {
            // 找不到就显示一个提示，而不是让程序崩掉 —— 这样调试时能一眼看出问题
            return new TextBlock { Text = $"找不到界面：{viewName}" };
        }

        try
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            return new TextBlock { Text = $"创建界面失败：{viewName}\n{ex.Message}" };
        }
    }

    /// <summary>只有 ViewModel 才交给这个定位器处理。</summary>
    public bool Match(object? data) => data is ViewModelBase;
}
