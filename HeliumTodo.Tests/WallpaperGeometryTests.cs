// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Imaging;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 壁纸预缩放几何的测试。
///
/// 为什么要测这个？
///   用户报"手动缩放窗口时壁纸会莫名其妙突然放大缩小"。根因不是采样算法，
///   而是预缩放位图被裁成了旧窗口的长宽比：拖拽时 UniformToFill 重新 cover，
///   松手重做位图又跳回 1:1。只要位图**始终保留原图长宽比**，任何尺寸的同比例
///   位图在 UniformToFill 下裁出的画面都一样，替换位图就不会跳。
///   这个几何换算错了不会报错，只会让窗口边缘露底色或构图跑偏，所以钉成纯函数测。
/// </summary>
public class WallpaperGeometryTests
{
    [Fact]
    public void 横图放进竖一点的窗口时要横向超出并保留原图比例()
    {
        // 16:9 的 2560×1440 放进 1400×900（3:2）窗口：
        // 高度限制更大（900/1440 = 0.625），位图应为 1600×900，宽度超出窗口 200 px。
        var cover = WallpaperGeometry.Compute(2560, 1440, 1400, 900);

        Assert.Equal(1600, cover.Width);
        Assert.Equal(900, cover.Height);
        Assert.Equal(16.0 / 9.0, (double)cover.Width / cover.Height, 3);
    }

    [Fact]
    public void 同比例时位图尺寸正好等于窗口()
    {
        var cover = WallpaperGeometry.Compute(3840, 2160, 1920, 1080);

        Assert.Equal(1920, cover.Width);
        Assert.Equal(1080, cover.Height);
    }

    [Fact]
    public void 竖图放进横窗口时高度超出且两边都不小于窗口()
    {
        // 9:16 竖图放进 1920×1080 横窗口：宽度限制更大，位图 1920×3414。
        var cover = WallpaperGeometry.Compute(1080, 1920, 1920, 1080);

        Assert.Equal(1920, cover.Width);
        Assert.True(cover.Height >= 1080);
        Assert.InRange((double)cover.Width / cover.Height, 9.0 / 16.0 - 0.001, 9.0 / 16.0 + 0.001);
    }

    [Fact]
    public void 非法源尺寸退回目标尺寸而不是除零()
    {
        var cover = WallpaperGeometry.Compute(0, 0, 800, 600);

        Assert.Equal(800, cover.Width);
        Assert.Equal(600, cover.Height);
    }

    [Fact]
    public void 非法目标尺寸退回原图尺寸()
    {
        var cover = WallpaperGeometry.Compute(32, 18, 0, 0);

        Assert.Equal(32, cover.Width);
        Assert.Equal(18, cover.Height);
    }
}
