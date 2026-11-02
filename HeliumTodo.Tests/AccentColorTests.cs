using HeliumTodo.Core.Theming;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>颜色换算与 HSL 拆合的测试。</summary>
public class AccentColorTests
{
    [Fact]
    public void 默认色是fluent蓝()
    {
        Assert.Equal("#0F6CBD", AccentColor.Default.ToHex());
        Assert.False(AccentColor.Default.IsFallback);
    }

    [Fact]
    public void 十六进制补零不能漏()
    {
        // 1,2,3 必须写成 #010203，不能变成 #123
        Assert.Equal("#010203", new AccentColor(1, 2, 3).ToHex());
        Assert.Equal("#FFFFFF", new AccentColor(255, 255, 255).ToHex());
        Assert.Equal("#000000", new AccentColor(0, 0, 0).ToHex());
    }

    [Fact]
    public void 纯红换算成hsl是零度()
    {
        AccentColor.RgbToHsl(255, 0, 0, out var hue, out var saturation, out var lightness);

        Assert.Equal(0, hue, 3);
        Assert.Equal(1, saturation, 3);
        Assert.Equal(0.5, lightness, 3);
    }

    [Fact]
    public void 纯蓝的色相是240度()
    {
        AccentColor.RgbToHsl(0, 0, 255, out var hue, out _, out _);

        Assert.Equal(240, hue, 3);
    }

    [Fact]
    public void 灰色没有饱和度()
    {
        AccentColor.RgbToHsl(128, 128, 128, out _, out var saturation, out var lightness);

        Assert.Equal(0, saturation, 3);

        // 128/255 ≈ 0.502
        Assert.Equal(0.502, lightness, 3);
    }

    [Fact]
    public void hsl转rgb能还原原色()
    {
        // 随便挑几个颜色，走一圈 HSL 再回来应该基本不变
        var samples = new[]
        {
            new AccentColor(15, 108, 189),
            new AccentColor(196, 43, 28),
            new AccentColor(73, 130, 5),
            new AccentColor(91, 45, 142),
        };

        foreach (var original in samples)
        {
            AccentColor.RgbToHsl(original.R, original.G, original.B, out var h, out var s, out var l);
            var (r, g, b) = AccentColor.HslToRgb(h, s, l);

            // 允许 ±1 的取整误差
            Assert.InRange(Math.Abs(r - original.R), 0, 1);
            Assert.InRange(Math.Abs(g - original.G), 0, 1);
            Assert.InRange(Math.Abs(b - original.B), 0, 1);
        }
    }

    [Fact]
    public void 从hsl构造会保留兜底标记()
    {
        var color = AccentColor.FromHsl(210, 0.6, 0.4, isFallback: true);

        Assert.True(color.IsFallback);
    }

    [Fact]
    public void 色相为负也能换算()
    {
        // -30 度等于 330 度（粉红那一片），不应该抛异常
        var (r, g, b) = AccentColor.HslToRgb(-30, 1, 0.5);

        Assert.True(r > 200);
        Assert.True(g < 60);
    }
}
