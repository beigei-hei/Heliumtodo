// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>
/// 时间轴滚动联动。
///
/// 目前刻度画在时间轴内部、会跟着一起滚，所以这里不做事；
/// 保留这个 Behavior 是为了以后想在滚动时做点什么（例如显示"正在查看 20:00"）时有个接入点。
/// </summary>
public sealed class ScrollSyncBehavior : Behavior<ScrollViewer>
{
    /// <summary>滚动发生时触发；参数是滚动偏移。</summary>
    public event EventHandler<Vector>? Scrolled;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
        {
            AssociatedObject.ScrollChanged += OnScrollChanged;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.ScrollChanged -= OnScrollChanged;
        }

        base.OnDetaching();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        Scrolled?.Invoke(this, e.OffsetDelta);
    }
}