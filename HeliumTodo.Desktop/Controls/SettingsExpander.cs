// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace HeliumTodo.Desktop.Controls;

/// <summary>
/// 设置页的分组折叠控件（替代 FluentAvalonia 的 FASettingsExpander）。
///
/// 为什么自己画：
///   P4 要把 FluentAvalonia 整体移除，而设置页 10 个分组、34 行都挂在
///   FASettingsExpander / FASettingsExpanderItem 上。它们的 API 很窄：
///   Header / Description / IsExpanded / Items / Footer，自己实现一份即可，
///   还能顺手去掉 FA 模板里写死的 #B3FFFFFF 底色（之前要四条 fa| 样式去压）。
/// </summary>
public class SettingsExpander : ContentControl
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<SettingsExpander, object?>(nameof(Header));

    public static readonly StyledProperty<object?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsExpander, object?>(nameof(Description));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SettingsExpander, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<AvaloniaList<SettingsExpanderItem>?> ItemsProperty =
        AvaloniaProperty.Register<SettingsExpander, AvaloniaList<SettingsExpanderItem>?>(nameof(Items));

    public SettingsExpander()
    {
        SetValue(ItemsProperty, new AvaloniaList<SettingsExpanderItem>());
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public AvaloniaList<SettingsExpanderItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
}


/// <summary>
/// 设置页的一行（替代 FASettingsExpanderItem）。
///
/// Content 是行标题或整块内容，Description 是副标题，Footer 是标题下面的控件。
/// 这样既兼容原来 <c>Content="明暗模式" + Footer=&lt;ComboBox/&gt;</c> 的写法，
/// 也兼容 <c>&lt;StackPanel/&gt;</c> 直接当整行内容的写法。
/// </summary>
public class SettingsExpanderItem : ContentControl
{
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsExpanderItem, string?>(nameof(Description));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SettingsExpanderItem, object?>(nameof(Footer));

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }
}