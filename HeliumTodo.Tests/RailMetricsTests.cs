// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Theming;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 左侧图标轨的尺寸表。
///
/// 为什么要测这个？
///   图标轨要能"展开成带名称的宽条、再收回去"，宽度同时被容器、每个按钮、
///   以及自检三处用到。以前这类数字是散在 XAML 里写死的，改一处就会出现
///   "轨道展开了、按钮还是 44 宽、名称被裁掉一半"的假样子。
///   把它钉在 Core 里之后，界面只绑定、自检只比常量，规则只有一份。
/// </summary>
public class RailMetricsTests
{
    [Fact]
    public void 收起态就是原来的图标条尺寸()
    {
        Assert.Equal(56, RailMetrics.RailWidth(expanded: false), 3);
        Assert.Equal(44, RailMetrics.ItemWidth(expanded: false), 3);
    }

    [Fact]
    public void 展开态要给名称留出位置()
    {
        // 188 = 44 图标位 + 名称 + 两侧留白；按钮跟着变宽，否则名称会被裁掉。
        Assert.Equal(188, RailMetrics.RailWidth(expanded: true), 3);
        Assert.Equal(176, RailMetrics.ItemWidth(expanded: true), 3);
        Assert.True(RailMetrics.ItemWidth(expanded: true) > RailMetrics.ItemWidth(expanded: false));
    }

    [Fact]
    public void 按钮永远比轨道窄一个外边距()
    {
        // 这条不变量是"按钮不能被轨道裁掉"的底线，两态都要成立。
        foreach (var expanded in new[] { false, true })
        {
            var rail = RailMetrics.RailWidth(expanded);
            var item = RailMetrics.ItemWidth(expanded);
            Assert.Equal(RailMetrics.HorizontalMargin, rail - item, 3);
        }
    }
}
