// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HeliumTodo.Domain.Theming;
using SkiaSharp;

namespace HeliumTodo.Desktop.Services;

/// <summary>解码出来的一张壁纸。</summary>
public sealed class DecodedWallpaper
{
    public DecodedWallpaper(
        int width,
        int height,
        IReadOnlyList<Bitmap> frames,
        IReadOnlyList<int> frameDelays,
        int sourceFrameCount,
        AccentColor accent)
    {
        Width = width;
        Height = height;
        Frames = frames;
        FrameDelays = frameDelays;
        SourceFrameCount = sourceFrameCount;
        Accent = accent;
    }

    /// <summary>解码后的实际宽（可能比原图小，见 <see cref="WallpaperDecoder.MaxLongSide"/>）。</summary>
    public int Width { get; }

    /// <summary>解码后的实际高。</summary>
    public int Height { get; }

    /// <summary>每一帧的位图。静态图就只有一帧。</summary>
    public IReadOnlyList<Bitmap> Frames { get; }

    /// <summary>每一帧停留多久（毫秒），和 <see cref="Frames"/> 一一对应。</summary>
    public IReadOnlyList<int> FrameDelays { get; }

    /// <summary>原图本来有多少帧（用来告诉用户"这张图 60 帧，为了省内存保留了 30 帧"）。</summary>
    public int SourceFrameCount { get; }

    /// <summary>从这张壁纸里提取出来的强调色。</summary>
    public AccentColor Accent { get; }

    /// <summary>是不是动图。</summary>
    public bool IsAnimated => Frames.Count > 1;
}

/// <summary>
/// 把壁纸文件解码成一张可以直接显示的位图。
///
/// 0.5.0 起壁纸只支持静态图片：GIF / MP4 等动态壁纸不再播放，
/// 动图文件会按第一帧静态显示（或在选择时直接拒绝）。
///
/// 【这个类为什么单独拆出来？】
///   它只做一件事：字节 → 位图。没有状态、不碰界面、不读配置。
///   拆开以后 <see cref="WallpaperService"/> 就只管「什么时候显示这张图」，
///   出问题也容易定位是"解不出来"还是"没显示"。
/// </summary>
public static class WallpaperDecoder
{
    /// <summary>
    /// 解码时把长边限制在这个像素数以内。
    ///
    /// 为什么不能按原图存？
    ///   一张 3840×2160 的壁纸，一帧就要 33 MB（每像素 4 字节）。
    ///   如果它是 60 帧的动图，光像素数据就是 2 GB —— 直接爆内存。
    ///   而窗口最大也就 4K 屏幕那么大，超过这个尺寸的部分根本看不出来，
    ///   所以在解码阶段就缩小，等于用一次性的画质损失换内存安全。
    /// </summary>
    public const int MaxLongSide = 1920;

    /// <summary>抽取颜色时把图缩到这么小就够 —— 64×64 有 4096 个采样点，足够判断主色。</summary>
    private const int AccentSampleSize = 64;

    /// <summary>缩放用的重采样器。Mitchell 是"清晰但不锯齿"的折中，适合缩图。</summary>
    private static readonly SKSamplingOptions ResizeSampling =
        new(new SKCubicResampler(1f / 3f, 1f / 3f));

    /// <summary>
    /// 解码一张壁纸。
    /// </summary>
    /// <param name="bytes">文件内容。</param>
    /// <param name="blurRadius">模糊半径（0 表示不模糊）。</param>
    /// <param name="kind">当前界面是浅色还是深色，影响取色时的明度调整。</param>
    /// <exception cref="InvalidOperationException">文件不是能识别的图片。</exception>
    public static DecodedWallpaper Decode(byte[] bytes, int blurRadius, ThemeKind kind)
    {
        if (bytes.Length == 0) throw new InvalidOperationException("壁纸文件是空的。");

        // 0.5.0 起不再播放动图：APNG / GIF 一律只取第一帧，交给 Skia。
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidOperationException("认不出这张图的格式（支持 PNG / JPG / WebP / BMP）。");

        return DecodeWithCodec(codec, blurRadius, kind);
    }

    /// <summary>
    /// 解码一个壁纸文件。0.5.0 起只接受静态图片；
    /// GIF / MP4 等动态格式在这里直接拒绝，不会进入解码。
    /// </summary>
    /// <param name="path">壁纸文件。</param>
    /// <param name="blurRadius">模糊半径（0 = 不模糊）。</param>
    /// <param name="kind">浅色 / 深色，影响取色的明度调整。</param>
    public static DecodedWallpaper DecodeFile(string path, int blurRadius, ThemeKind kind)
    {
        if (IsDynamicWallpaper(path))
        {
            throw new InvalidOperationException(
                "0.5.0 起壁纸只支持静态图片（PNG / JPG / WebP / BMP），不再支持 GIF / MP4 动态壁纸。");
        }

        return Decode(File.ReadAllBytes(path), blurRadius, kind);
    }

    /// <summary>这个扩展名是不是已经不再支持的动态壁纸（GIF / 视频）。</summary>
    public static bool IsDynamicWallpaper(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension)) return false;

        return extension.ToLowerInvariant() is ".gif" or ".mp4" or ".m4v" or ".mov"
            or ".wmv" or ".avi" or ".mkv" or ".webm";
    }

    // ══════════════════════════════════════════════════════════
    //  静态图：交给 SKCodec（动图只取第一帧）

    // ══════════════════════════════════════════════════════════

    private static DecodedWallpaper DecodeWithCodec(SKCodec codec, int blurRadius, ThemeKind kind)
    {
        int sourceWidth = codec.Info.Width;
        int sourceHeight = codec.Info.Height;

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new InvalidOperationException("这张图没有有效的尺寸。");
        }

        var (targetWidth, targetHeight) = ComputeTargetSize(sourceWidth, sourceHeight, MaxLongSide);

        var nativeInfo = new SKImageInfo(sourceWidth, sourceHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var nativeBitmap = new SKBitmap(nativeInfo);

        var result = codec.GetPixels(nativeInfo, nativeBitmap.GetPixels(), new SKCodecOptions(0));
        if (result is not SKCodecResult.Success && result is not SKCodecResult.IncompleteInput)
        {
            throw new InvalidOperationException("这张图解不出来。");
        }

        var targetInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var scaled = nativeBitmap.Resize(targetInfo, ResizeSampling)
            ?? throw new InvalidOperationException("这张图缩放失败。");

        var snapshot = ApplyBlurIfNeeded(scaled, blurRadius);
        try
        {
            return new DecodedWallpaper(
                targetWidth,
                targetHeight,
                new[] { ToAvaloniaBitmap(snapshot) },
                new[] { 0 },
                1,
                ExtractAccent(snapshot));
        }
        finally
        {
            if (!ReferenceEquals(snapshot, scaled)) snapshot.Dispose();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  公共的小工具
    // ══════════════════════════════════════════════════════════

    /// <summary>算出解码后的目标尺寸（长边不超过上限，小图不放大）。</summary>
    private static (int Width, int Height) ComputeTargetSize(int width, int height, int maxLongSide)
    {
        int longSide = Math.Max(width, height);
        if (longSide <= maxLongSide) return (width, height);

        var ratio = (double)maxLongSide / longSide;

        return (
            Math.Max(1, (int)Math.Round(width * ratio)),
            Math.Max(1, (int)Math.Round(height * ratio)));
    }

    /// <summary>需要的话把图模糊一下。返回的可能是原图（不需要模糊时），调用方要判断能不能 Dispose。</summary>
    private static SKBitmap ApplyBlurIfNeeded(SKBitmap source, int blurRadius)
    {
        if (blurRadius <= 0) return source;

        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var blurred = new SKBitmap(info);

        using var canvas = new SKCanvas(blurred);
        using var filter = SKImageFilter.CreateBlur(blurRadius, blurRadius, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { ImageFilter = filter };

        canvas.DrawBitmap(source, 0, 0, paint);

        return blurred;
    }

    /// <summary>从一个位图里提取强调色（先缩到 64×64 再算，快得多且结果几乎一样）。</summary>
    private static AccentColor ExtractAccent(SKBitmap source)
    {
        var smallInfo = new SKImageInfo(
            AccentSampleSize,
            AccentSampleSize,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        using var small = source.Resize(smallInfo, ResizeSampling);
        if (small is null) return AccentColor.Default;

        return WallpaperAccentExtractor.Extract(
            small.GetPixelSpan(),
            small.Width,
            small.Height);
    }

    /// <summary>
    /// Skia 的位图 → Avalonia 的位图。
    ///
    /// 为什么不用「编码成 PNG 再让 Avalonia 解回来」这种偷懒写法？
    ///   那样每帧要多走一次压缩+解压缩，一张 1920×1080 的图大约多花 50-80ms，
    ///   几十帧就是好几秒。而两边的像素格式都是 BGRA8888（预乘 alpha），
    ///   直接按行搬运内存就行，每帧只要几毫秒。
    /// </summary>
    private static Bitmap ToAvaloniaBitmap(SKBitmap source)
    {
        var width = source.Width;
        var height = source.Height;

        var writeable = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var buffer = writeable.Lock();

        var sourceSpan = source.GetPixelSpan();
        int sourceStride = source.RowBytes;
        int targetStride = buffer.RowBytes;
        int rowBytes = width * 4;

        // 逐行拷贝。为什么不一次性拷完？因为两边的「行间距」可能不一样
        // （Skia 为了对齐，一行末尾可能多几个填充字节）。
        var row = new byte[rowBytes];

        for (int y = 0; y < height; y++)
        {
            int sourceOffset = y * sourceStride;

            if (sourceOffset + rowBytes > sourceSpan.Length) break;

            sourceSpan.Slice(sourceOffset, rowBytes).CopyTo(row);
            Marshal.Copy(row, 0, IntPtr.Add(buffer.Address, y * targetStride), rowBytes);
        }

        return writeable;
    }
}
