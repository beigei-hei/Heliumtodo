// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Theming;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 壁纸蒙版的强度换算测试。
///
/// 为什么要测这个？
///   0.3.3 之前是「蒙版浓度 + 暗色灰蒙版」两条滑块，叠起来的效果谁也算不清；
///   现在合并成一条，由明暗决定颜色、暗色额外加压。这个换算决定了
///   "壁纸还能不能看见"和"文字压不压得住"两件事，一旦算错，用户看到的是
///   要么壁纸糊成一片白、要么深色下字全糊在花壁纸上——都不好复现，
///   所以把规则钉在 Core 里，用测试锁住。
/// </summary>
public class WallpaperScrimTests
{
    [Fact]
    public void 亮色下强度就是用户拖的百分比()
    {
        // 亮色不需要额外加压：白蒙版本身已经在提亮照片了。
        Assert.Equal(0.0, WallpaperScrim.ComputeStrength(0, isDark: false), 3);
        Assert.Equal(0.45, WallpaperScrim.ComputeStrength(45, isDark: false), 3);
        Assert.Equal(1.0, WallpaperScrim.ComputeStrength(100, isDark: false), 3);
    }

    [Fact]
    public void 暗色下同样百分比要更强()
    {
        // 深色界面里彩色壁纸是唯一的光源，必须压得更狠一点。
        var light = WallpaperScrim.ComputeStrength(40, isDark: false);
        var dark = WallpaperScrim.ComputeStrength(40, isDark: true);
        Assert.True(dark > light, $"暗色 {dark} 应当大于亮色 {light}");
        Assert.Equal(0.64, dark, 3);   // 0.40 × 1.6
    }

    [Fact]
    public void 暗色下强度有上限()
    {
        // 100% 也最多 0.95：再往上壁纸就彻底看不见了，那不如直接关掉壁纸。
        Assert.Equal(WallpaperScrim.MaxStrength, WallpaperScrim.ComputeStrength(100, isDark: true), 3);
        Assert.True(WallpaperScrim.ComputeStrength(100, isDark: true) < 1.0);
    }

    [Fact]
    public void 越界的百分比会被夹住()
    {
        // 配置文件是纯文本、可以手改，负数或 300 都不能让界面算出非法的不透明度。
        Assert.Equal(0.0, WallpaperScrim.ComputeStrength(-20, isDark: false), 3);
        Assert.Equal(1.0, WallpaperScrim.ComputeStrength(300, isDark: false), 3);
    }

    [Fact]
    public void 蒙版颜色由明暗决定()
    {
        // 亮色白 = 提亮；暗色黑 = 只压暗，不洗掉颜色。
        Assert.Equal("#FFFFFF", WallpaperScrim.ColorHex(isDark: false));
        Assert.Equal("#000000", WallpaperScrim.ColorHex(isDark: true));
    }
}
