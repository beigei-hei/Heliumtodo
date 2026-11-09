// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Theming;

/// <summary>
/// 一个强调色。
///
/// 【为什么要单独定义这个类型，不直接用字符串 "#0F6CBD"？】
///   因为从壁纸里取出来的颜色是「算」出来的，中途要反复做
///   RGB → HSL → 调饱和度/明度 → 再转回 RGB 这些运算。
///   如果每一步都序列化成字符串再解析回来，既慢又容易出错，
///   而且会丢掉「这个颜色是靠不靠得住」这类信息。
///
///   所以计算过程用这个结构体，只在最后写进配置时才转成十六进制字符串。
///
/// 注意这里是个 struct（值类型）：颜色就是三个字节，复制起来比引用更省事，
/// 也不会出现「拿到了一个颜色对象，结果别处把它改了」这种问题。
/// </summary>
public readonly record struct AccentColor
{
    /// <summary>构造一个强调色。</summary>
    public AccentColor(byte r, byte g, byte b, bool isFallback = false)
    {
        R = r;
        G = g;
        B = b;
        IsFallback = isFallback;
    }

    /// <summary>红色分量 0-255。</summary>
    public byte R { get; }

    /// <summary>绿色分量 0-255。</summary>
    public byte G { get; }

    /// <summary>蓝色分量 0-255。</summary>
    public byte B { get; }

    /// <summary>
    /// 这个颜色是不是「兜底」来的。
    ///
    /// 什么情况算兜底：
    ///   · 壁纸几乎是纯黑白（没有可用的色相）
    ///   · 壁纸整张透明 / 读不出来
    /// 这时候颜色还是会给一个，但界面上可以提示用户
    /// 「这张壁纸没有明显的主题色，用的是中性色」。
    /// </summary>
    public bool IsFallback { get; }

    /// <summary>转成 "#RRGGBB" 形式，方便写进配置文件。</summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>色相，0-360 度。</summary>
    public double Hue
    {
        get
        {
            RgbToHsl(R, G, B, out var h, out _, out _);
            return h;
        }
    }

    /// <summary>饱和度，0-1。0 表示纯灰。</summary>
    public double Saturation
    {
        get
        {
            RgbToHsl(R, G, B, out _, out var s, out _);
            return s;
        }
    }

    /// <summary>明度，0-1。0 是全黑，1 是全白。</summary>
    public double Lightness
    {
        get
        {
            RgbToHsl(R, G, B, out _, out _, out var l);
            return l;
        }
    }

    /// <summary>用 HSL 三分量构造一个颜色（超出范围会自动夹住）。</summary>
    public static AccentColor FromHsl(double hue, double saturation, double lightness, bool isFallback = false)
    {
        // 色相是环形的，先把任意角度折回 0-360
        hue %= 360;
        if (hue < 0) hue += 360;

        // 饱和度和明度不是环形的，直接夹到 0-1
        saturation = Math.Clamp(saturation, 0, 1);
        lightness = Math.Clamp(lightness, 0, 1);

        var (r, g, b) = HslToRgb(hue, saturation, lightness);
        return new AccentColor(r, g, b, isFallback);
    }

    /// <summary>默认强调色（Fluent 2 标准蓝），读不出壁纸颜色时用它。</summary>
    public static AccentColor Default => new(0x0F, 0x6C, 0xBD);

    /// <summary>转成界面要用的数组形式（方便写进测试断言）。</summary>
    public (byte R, byte G, byte B) ToTuple() => (R, G, B);

    public override string ToString() => ToHex();

    // ─────────────────────────────────────────────────────────
    //  颜色空间转换
    //
    //  为什么用 HSL 而不是直接用 RGB？
    //    因为「把颜色调亮一点」在 RGB 里是个很难定义的操作
    //    （三个分量各加多少？加完会不会偏色？），
    //    而在 HSL 里就是 l += 0.1 这么简单，色相不会跑掉。
    //    Fluent 2 的强调色阶（Light1/2/3、Dark1/2/3）本质就是这么生成的。
    // ─────────────────────────────────────────────────────────

    /// <summary>RGB → HSL。三个输出分量：色相 0-360、饱和度 0-1、明度 0-1。</summary>
    public static void RgbToHsl(byte r8, byte g8, byte b8, out double hue, out double saturation, out double lightness)
    {
        double r = r8 / 255.0;
        double g = g8 / 255.0;
        double b = b8 / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        // 明度就是最亮和最暗的中间值 —— 这个定义和色相无关
        lightness = (max + min) / 2.0;

        if (delta < 1e-9)
        {
            // 三个分量一模一样：纯灰，没有色相可言
            hue = 0;
            saturation = 0;
            return;
        }

        // 饱和度：在明度接近两端（全黑/全白）时要特殊处理，
        // 否则分母接近 0 会把结果放大得离谱
        saturation = lightness > 0.5
            ? delta / (2.0 - max - min)
            : delta / (max + min);

        // 色相：看哪个分量最大，再算它在色环上的位置
        if (max == r)
        {
            hue = (g - b) / delta + (g < b ? 6 : 0);
        }
        else if (max == g)
        {
            hue = (b - r) / delta + 2;
        }
        else
        {
            hue = (r - g) / delta + 4;
        }

        hue *= 60;
    }

    /// <summary>HSL → RGB。</summary>
    public static (byte R, byte G, byte B) HslToRgb(double hue, double saturation, double lightness)
    {
        if (saturation < 1e-9)
        {
            // 没有饱和度就是灰阶，三个分量相同
            byte v = ToByte(lightness);
            return (v, v, v);
        }

        double q = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - lightness * saturation;

        double p = 2 * lightness - q;

        double hk = hue / 360.0;

        double r = HueToRgb(p, q, hk + 1.0 / 3.0);
        double g = HueToRgb(p, q, hk);
        double b = HueToRgb(p, q, hk - 1.0 / 3.0);

        return (ToByte(r), ToByte(g), ToByte(b));
    }

    /// <summary>HSL → RGB 的中间步骤：给定色环上的位置，算出一个分量的值。</summary>
    private static double HueToRgb(double p, double q, double t)
    {
        // 把 t 折回 0-1 区间（色环是首尾相接的）
        if (t < 0) t += 1;
        if (t > 1) t -= 1;

        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;

        return p;
    }

    /// <summary>0-1 的小数 → 0-255 的字节，带四舍五入和范围夹取。</summary>
    private static byte ToByte(double value)
    {
        var scaled = value * 255.0;
        if (scaled <= 0) return 0;
        if (scaled >= 255) return 255;
        return (byte)Math.Round(scaled);
    }
}
