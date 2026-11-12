// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Windowing;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 启动尺寸/居中的纯算术测试。
///
/// 为什么要测这个？
///   用户要求"默认 1920×1080、启动在屏幕正中间"，但开发机是 2560×1600 @150%，
///   1920×1080 DIP 根本放不下，只能按物理像素换算成 1280×720 DIP。
///   这类换算错了不会报错，只会让窗口偏到屏幕外或忽大忽小；
///   所以把"按缩放换算、只收缩溢出维、居中到物理坐标"写成纯函数钉在这里。
/// </summary>
public class StartupWindowPlacementTests
{
    [Fact]
    public void 在百分之一百五十缩放下1920乘1080物理像素应当是1280乘720并居中()
    {
        // 开发机：2560×1600 屏幕，150% 缩放，工作区约 2560×1529 物理像素。
        // 1920×1080 物理像素 = 1280×720 DIP；居中后左上角是 (320, 224)。
        var placement = StartupWindowPlacement.Compute(
            workX: 0, workY: 0, workAreaWidth: 2560, workAreaHeight: 1529,
            scaling: 1.5, minWidthDip: 1080, minHeightDip: 720);

        Assert.Equal(1280, placement.WidthDip, 3);
        Assert.Equal(720, placement.HeightDip, 3);
        Assert.Equal(320, placement.LeftPx);
        Assert.Equal(224, placement.TopPx);
    }

    [Fact]
    public void 只有高度装不下时只收缩高度而不是两维一起缩()
    {
        // 100% 缩放下 1920×1080 的宽度放得下，高度只差一点；
        // 如果两维都乘 0.94，宽度会莫名其妙少 115 px。
        var placement = StartupWindowPlacement.Compute(
            workX: 0, workY: 0, workAreaWidth: 1920, workAreaHeight: 1040,
            scaling: 1.0, minWidthDip: 1080, minHeightDip: 720);

        Assert.Equal(1920, placement.WidthDip, 3);
        Assert.Equal(977.6, placement.HeightDip, 3);
        Assert.Equal(0, placement.LeftPx);
        Assert.Equal(31, placement.TopPx);
    }

    [Fact]
    public void 缩放值非法时按百分之一百处理()
    {
        // DPI 查询失败时 Avalonia 可能给出 0 或 NaN，不能让它把窗口算成 0 宽。
        foreach (var scaling in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
        {
            var placement = StartupWindowPlacement.Compute(
                workX: 0, workY: 0, workAreaWidth: 2560, workAreaHeight: 1440,
                scaling: scaling, minWidthDip: 1080, minHeightDip: 720);

            Assert.Equal(1920, placement.WidthDip, 3);
            Assert.Equal(1080, placement.HeightDip, 3);
            Assert.Equal(320, placement.LeftPx);
            Assert.Equal(180, placement.TopPx);
        }
    }

    [Fact]
    public void 工作区小于最小尺寸时优先保住最小尺寸()
    {
        // 小屏/远程桌面下不能让窗口缩到不可用；宁可超出工作区，也守住 1080×720 DIP。
        var placement = StartupWindowPlacement.Compute(
            workX: 0, workY: 0, workAreaWidth: 800, workAreaHeight: 600,
            scaling: 1.0, minWidthDip: 1080, minHeightDip: 720);

        Assert.Equal(1080, placement.WidthDip, 3);
        Assert.Equal(720, placement.HeightDip, 3);
        Assert.Equal(0, placement.LeftPx);
        Assert.Equal(0, placement.TopPx);
    }

    [Fact]
    public void 工作区不可用时退回1920乘1080并把左上角放在工作区原点()
    {
        var placement = StartupWindowPlacement.Compute(
            workX: 100, workY: 50, workAreaWidth: 0, workAreaHeight: 0,
            scaling: 1.0, minWidthDip: 1080, minHeightDip: 720);

        Assert.Equal(1920, placement.WidthDip, 3);
        Assert.Equal(1080, placement.HeightDip, 3);
        Assert.Equal(100, placement.LeftPx);
        Assert.Equal(50, placement.TopPx);
    }
}
