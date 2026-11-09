// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Configuration;
using HeliumTodo.Domain.Theming;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>从壁纸像素里挑主题色的测试。</summary>
public class WallpaperAccentExtractorTests
{
    /// <summary>造一张纯色图（BGRA 排列，和 Avalonia / SkiaSharp 一致）。</summary>
    private static byte[] Solid(int width, int height, byte r, byte g, byte b, byte a = 255)
    {
        var buffer = new byte[width * height * 4];

        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = b;
            buffer[i + 1] = g;
            buffer[i + 2] = r;
            buffer[i + 3] = a;
        }

        return buffer;
    }

    [Fact]
    public void 纯色壁纸取出来的就是那个色系()
    {
        // 一张纯红图，取出来应该还是红（色相接近 0 度）
        var accent = WallpaperAccentExtractor.Extract(Solid(32, 32, 220, 30, 30), 32, 32);

        Assert.InRange(accent.Hue, 0, 12);
        Assert.False(accent.IsFallback);
    }

    [Fact]
    public void 蓝色壁纸取出来是蓝色()
    {
        var accent = WallpaperAccentExtractor.Extract(Solid(32, 32, 30, 90, 220), 32, 32);

        Assert.InRange(accent.Hue, 200, 240);
    }

    [Fact]
    public void 纯黑白壁纸走兜底()
    {
        var accent = WallpaperAccentExtractor.Extract(Solid(32, 32, 128, 128, 128), 32, 32);

        // 灰色没有任何色相可用，只能给兜底色
        Assert.True(accent.IsFallback);
    }

    [Fact]
    public void 尺寸为零不抛异常()
    {
        var accent = WallpaperAccentExtractor.Extract(ReadOnlySpan<byte>.Empty, 0, 0);

        Assert.True(accent.IsFallback);
    }

    [Fact]
    public void 缓冲区太小时不越界读()
    {
        // 说好是 10x10，但只给了 4 个像素的数据
        var tooSmall = Solid(2, 2, 200, 20, 20);
        var accent = WallpaperAccentExtractor.Extract(tooSmall, 10, 10);

        Assert.True(accent.IsFallback);
    }

    [Fact]
    public void 全透明壁纸走兜底()
    {
        var accent = WallpaperAccentExtractor.Extract(Solid(16, 16, 220, 30, 30, a: 0), 16, 16);

        Assert.True(accent.IsFallback);
    }

    [Fact]
    public void 大面积灰色里的一小块鲜艳色能胜出()
    {
        // 这是这个算法存在的核心理由：平均色会灰扑扑，但人眼看到的是那块红旗
        const int size = 40;
        var buffer = new byte[size * size * 4];

        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 200;      // B
            buffer[i + 1] = 200;  // G
            buffer[i + 2] = 200;  // R
            buffer[i + 3] = 255;  // A
        }

        // 右下角抠出一块 10x10 的鲜红
        for (var y = 30; y < size; y++)
        {
            for (var x = 30; x < size; x++)
            {
                var i = (y * size + x) * 4;
                buffer[i] = 20;      // B
                buffer[i + 1] = 20;  // G
                buffer[i + 2] = 230; // R
            }
        }

        var accent = WallpaperAccentExtractor.Extract(buffer, size, size);

        Assert.InRange(accent.Hue, 0, 15);
        Assert.False(accent.IsFallback);
    }

    [Fact]
    public void 深色界面取出来的颜色比浅色界面亮()
    {
        var pixels = Solid(16, 16, 120, 170, 230);

        var light = WallpaperAccentExtractor.Extract(pixels, 16, 16, ThemeKind.Light);
        var dark = WallpaperAccentExtractor.Extract(pixels, 16, 16, ThemeKind.Dark);

        // 方向别搞反了：
        //   浅色主题背景是白的 → 强调色要够深，白字才看得清；
        //   深色主题背景是黑的 → 强调色要够亮，否则和背景糊成一片。
        // 所以深色主题取出来的颜色应该更亮，这也是 Fluent 深色主题强调色偏亮的原因。
        Assert.True(
            dark.Lightness > light.Lightness,
            $"深色主题的明度 {dark.Lightness:F3} 应该大于浅色主题的 {light.Lightness:F3}");
    }

    [Fact]
    public void 深色界面取出来的颜色落在深色主题的安全区里()
    {
        // 故意给一个很暗的颜色（纯深蓝），深色主题下应该被提亮到能看见
        var accent = WallpaperAccentExtractor.Extract(Solid(16, 16, 10, 20, 90), 16, 16, ThemeKind.Dark);

        Assert.InRange(accent.Lightness, 0.55, 0.76);
    }

    [Fact]
    public void 取出来的颜色不会太淡也不会太暗()
    {
        // 保证强调色能撑起白字/深色字，别给个几乎发白或者几乎全黑的颜色
        var accent = WallpaperAccentExtractor.Extract(Solid(16, 16, 250, 240, 120), 16, 16);

        Assert.InRange(accent.Lightness, 0.25, 0.55);
        Assert.InRange(accent.Saturation, 0.35, 0.95);
    }
}

/// <summary>强调色来源常量与显示名的测试。</summary>
public class AccentSourcesTests
{
    [Fact]
    public void 三种来源都在列表里()
    {
        Assert.Contains(AccentSources.Theme, AccentSources.All);
        Assert.Contains(AccentSources.Wallpaper, AccentSources.All);
        Assert.Contains(AccentSources.Custom, AccentSources.All);
        Assert.Equal(3, AccentSources.All.Count);
    }

    [Fact]
    public void 认不出来的来源退回跟随主题()
    {
        Assert.Equal(AccentSources.Theme, AccentSources.Normalize(null));
        Assert.Equal(AccentSources.Theme, AccentSources.Normalize(""));
        Assert.Equal(AccentSources.Theme, AccentSources.Normalize("随便写的"));
    }

    [Fact]
    public void 来源名的匹配不分大小写()
    {
        Assert.Equal(AccentSources.Wallpaper, AccentSources.Normalize("WALLPAPER"));
        Assert.Equal(AccentSources.Custom, AccentSources.Normalize("  Custom  "));
    }

    [Fact]
    public void 每种来源都有中文显示名()
    {
        foreach (var source in AccentSources.All)
        {
            var name = AccentSources.ToDisplayName(source);

            Assert.False(string.IsNullOrWhiteSpace(name));

            // 没翻译过的会原样返回英文 key，这里保证真的翻译了
            Assert.NotEqual(source, name);
        }
    }

    // ── 强调色到底用哪个：四种分支的规则 ──

    [Fact]
    public void 跟随主题时不覆盖强调色()
    {
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Theme, "#C42B1C", "#2996B4"));
    }

    [Fact]
    public void 跟随壁纸时用壁纸的颜色()
    {
        var result = AccentSources.ResolveOverride(AccentSources.Wallpaper, "#C42B1C", "#2996B4");

        Assert.Equal("#2996B4", result);
    }

    [Fact]
    public void 壁纸还没加载好时退回主题色()
    {
        // 壁纸 Hex 传 null（还没解码完 / 图读不出来）
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Wallpaper, "#C42B1C", null));
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Wallpaper, "#C42B1C", ""));
    }

    [Fact]
    public void 自定义颜色会被规范化()
    {
        Assert.Equal("#C42B1C", AccentSources.ResolveOverride(AccentSources.Custom, "C42B1C", null));
        Assert.Equal("#AABBCC", AccentSources.ResolveOverride(AccentSources.Custom, "#aabbcc", null));
        Assert.Equal("#AABBCC", AccentSources.ResolveOverride(AccentSources.Custom, "  #AABBCC  ", null));
    }

    [Fact]
    public void 自定义颜色写错了就退回主题色()
    {
        // 配置文件是纯文本，用户可能手滑。写错的十六进制不能让界面变成黑的。
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Custom, "not-a-color", null));
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Custom, "#12345", null));
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Custom, "#GGGGGG", null));
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Custom, null, null));
        Assert.Null(AccentSources.ResolveOverride(AccentSources.Custom, "", null));
    }

    [Fact]
    public void 来源字段写错时按跟随主题处理()
    {
        Assert.Null(AccentSources.ResolveOverride("胡乱写的", "#C42B1C", "#2996B4"));
    }
}
