// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HeliumTodo.Desktop.Controls;

/// <summary>
/// 一个会「报告自己有多宽」的面板。
///
/// 为什么需要它？
///   时间轴上的方块要按窗口宽度算位置（窗口变宽，并排的方块也要一起变宽）。
///   但 ViewModel 是不知道界面有多宽的 —— 那是界面层才知道的事。
///   所以用这个面板：它一发现自己宽度变了，就把新宽度通过事件报出去，
///   界面收到后转告 ViewModel 去重算方块位置。
///
/// 这是一种常见做法，比在 ViewModel 里塞一个「界面宽度」属性要干净。
/// </summary>
public sealed class WidthObserverPanel : Panel
{
    /// <summary>宽度变化时触发（参数是新的宽度）。</summary>
    public event EventHandler<double>? WidthChanged;

    /// <summary>上次报告的宽度，用来避免重复触发。</summary>
    private double _lastReportedWidth = -1;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);

        // 只在宽度真的有变化时才通知，避免每帧都触发造成死循环
        if (Math.Abs(finalSize.Width - _lastReportedWidth) > 0.5)
        {
            _lastReportedWidth = finalSize.Width;
            WidthChanged?.Invoke(this, finalSize.Width);
        }

        return result;
    }
}
