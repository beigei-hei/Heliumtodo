namespace HeliumTodo.Core.Theming;

/// <summary>
/// 从壁纸里挑一个「主题色」。
///
/// 【这个文件为什么在 Core 层，不在界面层？】
///   因为「怎么从一个颜色堆里挑出代表色」是一门算法，
///   跟用哪个界面框架、用哪个图片解码库都没关系。
///   放在 Core 里有两个好处：
///     1. 算法可以单独写单元测试（不用真的加载一张图片，直接喂像素数组）；
///     2. 以后换界面框架，这段代码一个字都不用改。
///
///   而「把图片文件解码成像素」那一步才需要 SkiaSharp，
///   那部分放在界面层（HeliumTodo.App/Services/WallpaperService.cs），
///   它解出像素后把数组交给这里。
///
/// 【为什么要「挑」而不是「算平均」？】
///   平均色几乎总是灰扑扑的。一张蓝天+太阳的照片平均下来是脏灰蓝，
///   拿它当强调色，按钮看起来像得了病。
///   人也一样 —— 看一张照片说「主色是什么」，指的是那个最抢眼的颜色，
///   不是把所有像素搅匀的结果。
///
///   所以这里的思路是：
///     · 按色相（红的红、蓝的蓝）分桶，
///     · 每个像素按「有多鲜艳」「有多亮」加权投票，
///     · 得票最高的那一桶取加权平均。
///   这样灰色天空几乎不投票，一小块鲜艳的红旗反而能胜出 —— 符合人眼感受。
/// </summary>
public static class WallpaperAccentExtractor
{
    /// <summary>色相分成多少个桶。24 个桶 = 每桶 15 度，人的眼睛基本分不出更细了。</summary>
    private const int HueBucketCount = 24;

    /// <summary>色相桶的宽度（度）。</summary>
    private const double HueBucketWidth = 360.0 / HueBucketCount;

    /// <summary>全透明的像素不参与投票（阈值取一半，半透明也算数）。</summary>
    private const byte MinAlpha = 128;

    /// <summary>
    /// 从 BGRA 像素里提取强调色。
    ///
    /// <paramref name="bgra"/> 的排列顺序是「蓝、绿、红、透明度」，
    /// 也就是 Avalonia 的 <c>PixelFormat.Bgra8888</c> 和 SkiaSharp 的
    /// <c>SKColorType.Bgra8888</c> 用的顺序（两个库都一样，所以中间不用转）。
    /// 每个像素 4 字节，行与行之间是紧密排列的（没有额外填充）。
    /// </summary>
    /// <param name="bgra">像素数据。</param>
    /// <param name="width">宽度（像素）。</param>
    /// <param name="height">高度（像素）。</param>
    /// <param name="kind">界面是浅色还是深色 —— 决定取出来的颜色要不要提亮/压暗。</param>
    public static AccentColor Extract(
        ReadOnlySpan<byte> bgra,
        int width,
        int height,
        ThemeKind kind = ThemeKind.Light)
    {
        if (width <= 0 || height <= 0) return Fallback(kind);

        long pixelCount = (long)width * height;
        if (bgra.Length < pixelCount * 4) return Fallback(kind);

        // 每个色相桶累计三样东西：权重、红色和、绿色和、蓝色和
        Span<double> bucketWeight = stackalloc double[HueBucketCount];
        Span<double> bucketR = stackalloc double[HueBucketCount];
        Span<double> bucketG = stackalloc double[HueBucketCount];
        Span<double> bucketB = stackalloc double[HueBucketCount];

        // 同时统计「整张图有多鲜艳」—— 用来判断壁纸是不是纯黑白
        double chromaTotal = 0;
        double chromaPixels = 0;
        double allWeight = 0;

        for (long i = 0; i < pixelCount; i++)
        {
            int offset = (int)(i * 4);

            // BGRA 顺序：0=蓝 1=绿 2=红 3=透明度
            byte b8 = bgra[offset];
            byte g8 = bgra[offset + 1];
            byte r8 = bgra[offset + 2];
            byte a8 = bgra[offset + 3];

            if (a8 < MinAlpha) continue;

            double r = r8 / 255.0;
            double g = g8 / 255.0;
            double b = b8 / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            // HSV 的饱和度和明度（这里用 HSV 而不是 HSL，因为「亮不亮」用 V 判断更直观）
            double saturation = max <= 1e-9 ? 0 : delta / max;
            double value = max;

            chromaTotal += saturation;
            chromaPixels++;

            // 太暗的像素忽略：深色壁纸的阴影部分颜色不准，容易算出脏色
            if (value < 0.06) continue;

            // 接近纯白的像素也忽略：白墙、白纸会把色相带偏
            if (value > 0.96 && saturation < 0.10) continue;

            // 权重公式：鲜艳度是主导因素（平方放大差距），亮度是次要因素。
            // 为什么鲜艳度用平方？因为「差一点点鲜艳」在人眼里差距很大，
            // 线性加权的话一片浅灰蓝会盖过一小块正红。
            double weight = (saturation * saturation) * (0.30 + 0.70 * value) + 0.002;

            allWeight += weight;

            // 灰色像素没有有意义的色相，只给后面的兜底用，不参与分桶
            if (saturation < 0.06) continue;

            AccentColor.RgbToHsl(r8, g8, b8, out var hue, out _, out _);

            int bucket = (int)(hue / HueBucketWidth);
            if (bucket < 0) bucket = 0;
            if (bucket >= HueBucketCount) bucket = HueBucketCount - 1;

            bucketWeight[bucket] += weight;
            bucketR[bucket] += r8 * weight;
            bucketG[bucket] += g8 * weight;
            bucketB[bucket] += b8 * weight;
        }

        if (chromaPixels <= 0) return Fallback(kind);

        // ── 判断是不是「几乎没有颜色」的壁纸（黑白照片、纯色背景） ──
        // 平均饱和度低于这个值就认为没有可靠色相。
        double averageChroma = chromaTotal / chromaPixels;

        // ── 找票数最高的色相桶 ──
        int bestBucket = -1;
        double bestWeight = 0;

        for (int i = 0; i < HueBucketCount; i++)
        {
            if (bucketWeight[i] > bestWeight)
            {
                bestWeight = bucketWeight[i];
                bestBucket = i;
            }
        }

        // 最高票也少得可怜（比如整张图都是灰的，只有几个噪点有色），
        // 说明这张壁纸没有明确的主题色
        if (bestBucket < 0 || bestWeight < allWeight * 0.02)
        {
            return Fallback(kind, averageChroma);
        }

        byte avgR = (byte)Math.Clamp(Math.Round(bucketR[bestBucket] / bestWeight), 0, 255);
        byte avgG = (byte)Math.Clamp(Math.Round(bucketG[bestBucket] / bestWeight), 0, 255);
        byte avgB = (byte)Math.Clamp(Math.Round(bucketB[bestBucket] / bestWeight), 0, 255);

        var raw = new AccentColor(avgR, avgG, avgB, isFallback: false);

        return MakeUsable(raw, kind);
    }

    /// <summary>
    /// 把一个「从壁纸里算出来的颜色」调整成「能当界面强调色用的颜色」。
    ///
    /// 【为什么必须调？】
    ///   壁纸里的颜色是为好看服务的，不是为可读性服务的。
    ///   直接从壁纸取色会踩两个坑：
    ///     1. 太浅的颜色（比如天空的淡蓝）当强调色时，
    ///        白色文字压上去完全看不清；
    ///     2. 太暗的颜色（比如夜景的黑蓝）在深色主题里和背景糊成一片。
    ///
    ///   所以这里做三件事：
    ///     · 饱和度抬到最低线 —— 太灰的颜色当强调色会显得「没电」；
    ///     · 明度夹进一个安全区间 —— 保证白字/黑字总有一边看得清；
    ///     · 浅色主题压暗、深色主题提亮 —— 让强调色和背景拉开距离。
    /// </summary>
    private static AccentColor MakeUsable(AccentColor raw, ThemeKind kind)
    {
        var hue = raw.Hue;
        var saturation = Math.Clamp(raw.Saturation, MinSaturation, MaxSaturation);

        // 明度的安全区间。两个主题不一样，因为"看得清"的条件正好相反：
        //   · 浅色主题：背景是白的，强调色得够深，白字压上去才看得见
        //     → 夹在 0.30 ~ 0.46
        //   · 深色主题：背景是黑的，太深的强调色会和背景糊在一起
        //     → 夹在 0.58 ~ 0.74（这也正是 Fluent 深色主题强调色偏亮的原因）
        var targetLightness = kind == ThemeKind.Dark
            ? Math.Clamp(raw.Lightness, MinLightnessDark, MaxLightnessDark)
            : Math.Clamp(raw.Lightness, MinLightnessLight, MaxLightnessLight);

        return AccentColor.FromHsl(hue, saturation, targetLightness);
    }

    /// <summary>浅色主题下明度的下限：再暗就和深色文字糊在一起了。</summary>
    private const double MinLightnessLight = 0.30;

    /// <summary>浅色主题下明度的上限：再亮按钮看起来像被禁用了。</summary>
    private const double MaxLightnessLight = 0.46;

    /// <summary>深色主题下明度的下限：太暗会在深色背景上消失。</summary>
    private const double MinLightnessDark = 0.58;

    /// <summary>深色主题下明度的上限：太亮会显得刺眼、发飘。</summary>
    private const double MaxLightnessDark = 0.74;


    /// <summary>饱和度下限：再低就显得灰扑扑没有生气。</summary>
    private const double MinSaturation = 0.42;

    /// <summary>饱和度上限：再高就刺眼，而且容易和 Fluent 2 的整体调性打架。</summary>
    private const double MaxSaturation = 0.86;

    /// <summary>
    /// 兜底颜色。
    ///
    /// 什么情况下会走到这儿：
    ///   · 图片读不出来 / 尺寸为 0
    ///   · 整张图都是纯黑、纯白、或完全透明
    ///   · 有颜色但太分散，没有哪一种色相占得住脚
    ///
    /// 此时的策略分两种：
    ///   · 壁纸还有一点点色彩倾向（<paramref name="averageChroma"/> 略微大于 0）
    ///     → 顺着这个倾向给一个「很淡的同色系」颜色，比硬塞一个蓝色自然；
    ///   · 壁纸完全没颜色 → 用 Fluent 2 标准蓝，至少不会难看。
    /// </summary>
    private static AccentColor Fallback(ThemeKind kind, double averageChroma = 0)
    {
        if (averageChroma > 0.02)
        {
            // 有一点点色彩倾向：明度夹进安全区，饱和度压得很低（表示「不太确定」）
            return AccentColor.FromHsl(210, 0.30, kind == ThemeKind.Dark ? 0.68 : 0.42, isFallback: true);
        }

        var baseColor = AccentColor.Default;

        if (kind == ThemeKind.Dark)
        {
            // 深色主题下把标准蓝提亮一点，否则在深色背景上看不见
            return AccentColor.FromHsl(baseColor.Hue, baseColor.Saturation, 0.62, isFallback: true);
        }

        return new AccentColor(baseColor.R, baseColor.G, baseColor.B, isFallback: true);
    }
}
