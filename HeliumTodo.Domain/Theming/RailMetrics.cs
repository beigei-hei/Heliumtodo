// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Theming;

/// <summary>
/// 左侧图标轨的尺寸表（收起 56 / 展开 188）。
///
/// 【为什么这几个数要放进 Core？】
///   它们同时被三个地方用到：外壳里那个 Border 的宽度、每个导航按钮的宽度、
///   以及自检里"展开后按钮是不是真的变宽了、名称是不是露出来了"的断言。
///   散在三处，改一个忘一个，就会出现"轨道展开了但按钮还是 44 宽、
///   名称被裁在右边"这种一眼假的样子。放进 Core 之后，界面只负责绑定，
///   自检也能直接对着同一份常量断言。
/// </summary>
public static class RailMetrics
{
    /// <summary>收起态轨道宽度（只有图标）。</summary>
    public const double CollapsedRailWidth = 56;

    /// <summary>展开态轨道宽度（图标 + 名称）。</summary>
    public const double ExpandedRailWidth = 188;

    /// <summary>
    /// 展开 / 收起动画的时长（毫秒）。
    /// 界面上的 <c>DoubleTransition</c>、MainWindow 里"动起来的时候降质"的时长、
    /// 以及自检里的断言都读这一个数 —— 三处对不上就会出现"动画早停了但降质还没恢复"。
    /// </summary>
    public const int AnimationDurationMs = 260;

    /// <summary>轨道的外边距之和（左 8 + 右 4），按钮宽度要把它扣掉。</summary>
    public const double HorizontalMargin = 12;

    /// <summary>当前该用多宽的轨道。</summary>
    public static double RailWidth(bool expanded) =>
        expanded ? ExpandedRailWidth : CollapsedRailWidth;

    /// <summary>
    /// 当前该用多宽的导航按钮。
    /// 收起时 44（和设计令牌里的 RailItemSize 一致），展开时 176。
    /// </summary>
    public static double ItemWidth(bool expanded) =>
        RailWidth(expanded) - HorizontalMargin;
}
