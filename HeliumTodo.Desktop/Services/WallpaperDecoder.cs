// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HeliumTodo.Domain.Imaging;
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
/// 把壁纸文件解码成「可以直接显示的一串帧」。
///
/// 【这个类为什么单独拆出来？】
///   它只做一件事：字节 → 位图。没有状态、不碰界面、不读配置。
///   拆开以后 <see cref="WallpaperService"/> 就只管「什么时候显示哪一帧」，
///   逻辑清爽很多，出问题也容易定位是"解不出来"还是"没在播"。
///
/// 【为什么不干脆用 Avalonia 自带的 Bitmap 解码？】
///   因为 Avalonia 的 <c>Bitmap</c> 只解**静态图的第一帧**，
///   动图（GIF、APNG）它不会播。要播动图就得自己把每一帧拆出来。
///   而拆帧这件事 SkiaSharp 能做（它本来就是 Avalonia 底层的绘图库），
///   所以直接用它，同一个后端，不用引入第二套图片库。
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

    /// <summary>
    /// 动图用的长边上限，比静态图更狠。
    ///
    /// 为什么动图要额外降一档？
    ///   静态图只占一帧，1920 也就是 8 MB，多花一点无所谓。
    ///   动图是几十帧一起驻留：同样是 1920，40 帧就是 330 MB 内存。
    ///   而壁纸是铺在窗口背景上的、还要过一层蒙版，缩到 1280 肉眼几乎看不出差别，
    ///   拿这点画质换一半内存很划算。
    /// </summary>
    public const int MaxAnimatedLongSide = 1280;

    /// <summary>
    /// 所有帧加起来最多占多少内存。
    ///
    /// 超了就抽帧（每 N 帧保留一帧，延迟累加），
    /// 效果是动作变"卡"一点，但不会把程序拖死。
    ///
    /// 96 MB 是实测定的：1280×720 每帧 3.7 MB，最多留 26 帧，
    /// 8 秒的循环动画大约抽掉三分之二，看上去仍然连贯；
    /// 再往上加（192 MB）实测进程工作集要多 150 MB 以上，不值。
    /// </summary>
    private const long FrameBudgetBytes = 96L * 1024 * 1024;

    /// <summary>抽取颜色时把图缩到这么小就够 —— 64×64 有 4096 个采样点，足够判断主色。</summary>
    private const int AccentSampleSize = 64;

    /// <summary>缩放用的重采样器。Mitchell 是"清晰但不锯齿"的折中，适合缩图。</summary>
    private static readonly SKSamplingOptions ResizeSampling =
        new(new SKCubicResampler(1f / 3f, 1f / 3f));

    /// <summary>不缩放时用的采样器（最近邻，保证像素原样搬运）。</summary>
    private static readonly SKSamplingOptions NoSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

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

        // ── 先看是不是 APNG（要自己拆帧，Skia 不认） ──
        var apng = ApngParser.TryParse(bytes);
        if (apng is not null)
        {
            return DecodeApng(apng, blurRadius, kind);
        }

        // ── 其余（静态 PNG / JPG / GIF）交给 Skia ──
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidOperationException("认不出这张图的格式（支持 PNG / JPG / APNG / GIF）。");

        return DecodeWithCodec(codec, blurRadius, kind);
    }

    /// <summary>
    /// 解码一个壁纸文件 —— 图片还是视频由这里决定。
    ///
    /// 为什么不干脆都读成 byte[] 再判断？
    ///   图片解码需要整份字节；视频不行 —— 一个几十兆的 MP4 读进内存毫无意义，
    ///   而且 Media Foundation 本来就支持「给个路径自己去读」。
    ///   所以按扩展名分流：视频走 MF，其余照旧读字节。
    /// </summary>
    /// <param name="path">壁纸文件。</param>
    /// <param name="blurRadius">模糊半径（0 = 不模糊）。</param>
    /// <param name="kind">浅色 / 深色，影响取色的明度调整。</param>
    public static DecodedWallpaper DecodeFile(string path, int blurRadius, ThemeKind kind)
    {
        if (IsVideoFile(path))
        {
            return DecodeVideo(path, blurRadius, kind);
        }

        return Decode(File.ReadAllBytes(path), blurRadius, kind);
    }

    /// <summary>这些扩展名的文件当视频处理（具体的编码能不能解，由系统解码器说了算）。</summary>
    public static bool IsVideoFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension)) return false;

        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" or ".mov" or ".wmv" or ".avi" or ".mkv" or ".webm" => true,
            _ => false,
        };
    }

    /// <summary>
    /// 视频 → 一串帧。
    ///
    /// 拿到的帧还要和图片走同一套后处理（模糊 / 取强调色 / 转成 Avalonia 位图），
    /// 这样界面上不需要知道"这张壁纸到底是 GIF 还是 MP4"。
    /// </summary>
    private static DecodedWallpaper DecodeVideo(string path, int blurRadius, ThemeKind kind)
    {
        // 视频解码用的是 Windows 的 Media Foundation，其它平台没有这套东西。
        // 写这一句既是为了给出人话提示，也是让"仅 Windows"的分析器知道后面是安全的。
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("视频壁纸目前只支持 Windows 系统。");
        }

        // 先按同样的规则算出目标尺寸，才能知道内存预算里能装几帧
        var (probeWidth, probeHeight) = (MaxAnimatedLongSide, MaxAnimatedLongSide);
        var maxFrames = ComputeMaxFrames(probeWidth, probeHeight);

        var video = MediaFoundationVideo.TryDecode(path, MaxAnimatedLongSide, maxFrames, out var error);
        if (video is null)
        {
            throw new InvalidOperationException(error ?? "这个视频解不出来。");
        }

        try
        {
            var frames = new List<Bitmap>(video.Frames.Count);
            var delays = new List<int>(video.Delays.Count);
            AccentColor? accent = null;

            for (int index = 0; index < video.Frames.Count; index++)
            {
                var source = video.Frames[index];
                var processed = ApplyBlurIfNeeded(source, blurRadius);

                accent ??= ExtractAccent(processed);

                frames.Add(ToAvaloniaBitmap(processed));
                delays.Add(video.Delays[index]);

                // ApplyBlurIfNeeded 在不模糊时返回的是原图，
                // 那就不能在这里 Dispose —— 它还要留给下一轮循环释放
                if (!ReferenceEquals(processed, source)) processed.Dispose();
            }

            return new DecodedWallpaper(
                video.Width,
                video.Height,
                frames,
                delays,
                video.SourceFrameCount,
                accent ?? AccentColor.Default);
        }
        finally
        {
            foreach (var frame in video.Frames) frame.Dispose();
        }
    }

    /// <summary>按内存预算算出最多保留多少帧（视频和动图用同一套预算）。</summary>
    private static int ComputeMaxFrames(int width, int height)
    {
        long bytesPerFrame = (long)width * height * 4;
        if (bytesPerFrame <= 0) return 1;

        return (int)Math.Max(1, FrameBudgetBytes / bytesPerFrame);
    }

    // ══════════════════════════════════════════════════════════
    //  APNG：自己逐帧合成
    // ══════════════════════════════════════════════════════════

    private static DecodedWallpaper DecodeApng(ApngInfo apng, int blurRadius, ThemeKind kind)
    {
        var (targetWidth, targetHeight) = ComputeTargetSize(apng.Width, apng.Height, MaxAnimatedLongSide);
        var scale = (double)targetWidth / apng.Width;

        // 按内存预算决定能保留几帧
        var step = ComputeFrameStep(apng.Frames.Count, targetWidth, targetHeight);

        var canvasInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvasBitmap = new SKBitmap(canvasInfo);
        using var canvas = new SKCanvas(canvasBitmap);

        // 透明底。不能填白色 —— 壁纸可能是带透明度的 PNG，
        // 填白会把它变成一张不透明的图。
        canvas.Clear(SKColors.Transparent);

        var frames = new List<Bitmap>();
        var delays = new List<int>();
        AccentColor? accent = null;

        // 上一帧的处置方式，以及「恢复原样」时要用到的那块内容
        var pendingDispose = ApngDisposeOp.None;
        SKBitmap? savedRegion = null;
        SKRectI savedRect = SKRectI.Empty;
        int delayAccumulator = 0;

        foreach (var frame in apng.Frames)
        {
            // ① 先执行上一帧留下的处置动作
            if (pendingDispose == ApngDisposeOp.Background)
            {
                canvas.Save();
                canvas.ClipRect(new SKRect(savedRect.Left, savedRect.Top, savedRect.Right, savedRect.Bottom));
                canvas.Clear(SKColors.Transparent);
                canvas.Restore();
            }
            else if (pendingDispose == ApngDisposeOp.Previous && savedRegion is not null)
            {
                canvas.DrawBitmap(savedRegion, savedRect.Left, savedRect.Top, null);
                savedRegion.Dispose();
                savedRegion = null;
            }

            // ② 如果这一帧自己要求"画完恢复原样"，先把这块内容存下来
            if (frame.Dispose == ApngDisposeOp.Previous)
            {
                savedRect = ToTargetRect(frame, scale, targetWidth, targetHeight);
                savedRegion = ExtractRegion(canvasBitmap, savedRect);
            }

            // ③ 画这一帧
            DrawFrame(canvas, frame, scale);

            delayAccumulator += frame.DelayMilliseconds;

            // ④ 到了要保留的帧才真正输出
            if (frame.Index % step == 0)
            {
                var snapshot = ApplyBlurIfNeeded(canvasBitmap, blurRadius);

                accent ??= ExtractAccent(snapshot);

                frames.Add(ToAvaloniaBitmap(snapshot));
                delays.Add(delayAccumulator);
                delayAccumulator = 0;

                if (!ReferenceEquals(snapshot, canvasBitmap)) snapshot.Dispose();
            }

            pendingDispose = frame.Dispose;
        }

        savedRegion?.Dispose();

        if (frames.Count == 0) throw new InvalidOperationException("这张 APNG 里没有能显示的帧。");

        return new DecodedWallpaper(
            targetWidth,
            targetHeight,
            frames,
            delays,
            apng.Frames.Count,
            accent ?? AccentColor.Default);
    }

    /// <summary>把 APNG 的一帧画到画布上（带上缩放）。</summary>
    private static void DrawFrame(SKCanvas canvas, ApngFrame frame, double scale)
    {
        using var decoded = SKBitmap.Decode(frame.PngBytes);
        if (decoded is null) return;

        int x = (int)Math.Round(frame.X * scale);
        int y = (int)Math.Round(frame.Y * scale);
        int width = (int)Math.Round(frame.Width * scale);
        int height = (int)Math.Round(frame.Height * scale);

        if (width <= 0 || height <= 0) return;

        // 混合方式：Source 是"连透明一起替换"，Over 是普通叠加。
        // 这两种在 Skia 里都有现成的对应模式，不用自己算 alpha 混合。
        using var paint = new SKPaint
        {
            BlendMode = frame.Blend == ApngBlendOp.Source ? SKBlendMode.Src : SKBlendMode.SrcOver,
            IsAntialias = false,
        };

        if (width == frame.Width && height == frame.Height && x >= 0 && y >= 0)
        {
            // 不用缩放的情况：直接按原尺寸贴，避免重采样把边缘弄糊
            var target = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var scaled = decoded.Resize(target, NoSampling) ?? decoded;
            canvas.DrawBitmap(scaled, x, y, paint);
        }
        else
        {
            var target = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var scaled = decoded.Resize(target, ResizeSampling);
            if (scaled is null) return;
            canvas.DrawBitmap(scaled, x, y, paint);
        }
    }

    /// <summary>把帧的矩形换算到缩放后的画布坐标（并夹进画布范围）。</summary>
    private static SKRectI ToTargetRect(ApngFrame frame, double scale, int canvasWidth, int canvasHeight)
    {
        int left = Math.Clamp((int)Math.Round(frame.X * scale), 0, canvasWidth);
        int top = Math.Clamp((int)Math.Round(frame.Y * scale), 0, canvasHeight);
        int width = Math.Min((int)Math.Round(frame.Width * scale), canvasWidth - left);
        int height = Math.Min((int)Math.Round(frame.Height * scale), canvasHeight - top);

        if (width < 0) width = 0;
        if (height < 0) height = 0;

        return new SKRectI(left, top, left + width, top + height);
    }

    /// <summary>把画布上的一块区域抠出来（dispose=Previous 时要靠它还原）。</summary>
    private static SKBitmap? ExtractRegion(SKBitmap source, SKRectI rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        var info = new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var region = new SKBitmap(info);

        using var canvas = new SKCanvas(region);
        canvas.DrawBitmap(
            source,
            new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom),
            new SKRect(0, 0, rect.Width, rect.Height),
            new SKPaint { BlendMode = SKBlendMode.Src, IsAntialias = false });

        return region;
    }

    // ══════════════════════════════════════════════════════════
    //  静态图 / GIF：交给 SKCodec
    // ══════════════════════════════════════════════════════════

    private static DecodedWallpaper DecodeWithCodec(SKCodec codec, int blurRadius, ThemeKind kind)
    {
        int sourceWidth = codec.Info.Width;
        int sourceHeight = codec.Info.Height;

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new InvalidOperationException("这张图没有有效的尺寸。");
        }

        // FrameCount 对静态图是 0，按 1 帧处理
        int sourceFrameCount = Math.Max(1, codec.FrameCount);

        // 动图和静态图用不同的长边上限（见 MaxAnimatedLongSide 的说明）
        var longSideLimit = sourceFrameCount > 1 ? MaxAnimatedLongSide : MaxLongSide;
        var (targetWidth, targetHeight) = ComputeTargetSize(sourceWidth, sourceHeight, longSideLimit);
        var scale = (double)targetWidth / sourceWidth;

        var nativeInfo = new SKImageInfo(sourceWidth, sourceHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var nativeBitmap = new SKBitmap(nativeInfo);

        var step = ComputeFrameStep(sourceFrameCount, targetWidth, targetHeight);

        var frames = new List<Bitmap>();
        var delays = new List<int>();
        AccentColor? accent = null;
        int delayAccumulator = 0;

        for (int index = 0; index < sourceFrameCount; index++)
        {
            // ⚠️ GIF 的每一帧可能只存了「和上一帧比变了的那一小块」，
            //    所以必须按顺序解码、并且告诉 Skia「这一帧要叠在第几帧上」，
            //    否则解出来的帧是残缺的（大片透明或黑块）。
            //    静态图只有一帧，PriorFrame 传 -1。
            var options = index == 0 ? new SKCodecOptions(0) : new SKCodecOptions(index, index - 1);

            var result = codec.GetPixels(nativeInfo, nativeBitmap.GetPixels(), options);

            // 个别帧可能解失败（图片损坏），跳过去比整个失败好
            if (result is not SKCodecResult.Success && result is not SKCodecResult.IncompleteInput)
            {
                continue;
            }

            delayAccumulator += GetFrameDelay(codec, index);

            if (index % step != 0) continue;

            var targetInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var scaled = nativeBitmap.Resize(targetInfo, ResizeSampling);
            if (scaled is null) continue;

            var snapshot = ApplyBlurIfNeeded(scaled, blurRadius);

            accent ??= ExtractAccent(snapshot);

            frames.Add(ToAvaloniaBitmap(snapshot));
            delays.Add(delayAccumulator);
            delayAccumulator = 0;

            if (!ReferenceEquals(snapshot, scaled)) snapshot.Dispose();
        }

        if (frames.Count == 0) throw new InvalidOperationException("这张图解不出任何一帧。");

        return new DecodedWallpaper(
            targetWidth,
            targetHeight,
            frames,
            delays,
            sourceFrameCount,
            accent ?? AccentColor.Default);
    }

    /// <summary>取一帧的停留时间（毫秒）。静态图没有这个信息，给个不会用到的值。</summary>
    private static int GetFrameDelay(SKCodec codec, int index)
    {
        if (codec.FrameCount <= 1) return 0;

        var info = codec.FrameInfo;
        if (index < 0 || index >= info.Length) return 100;

        var duration = info[index].Duration;

        // 有些 GIF 写 0，浏览器们约定俗成按 100ms 处理
        if (duration <= 0) return 100;

        // 上限 10 秒，防止一帧停太久
        return Math.Min(duration, 10_000);
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

    /// <summary>
    /// 算出「每隔几帧保留一帧」。
    /// 返回 1 表示全部保留。
    /// </summary>
    private static int ComputeFrameStep(int frameCount, int width, int height)
    {
        if (frameCount <= 1) return 1;

        long bytesPerFrame = (long)width * height * 4;
        if (bytesPerFrame <= 0) return 1;

        long maxFrames = Math.Max(1, FrameBudgetBytes / bytesPerFrame);

        if (frameCount <= maxFrames) return 1;

        return (int)Math.Ceiling((double)frameCount / maxFrames);
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
