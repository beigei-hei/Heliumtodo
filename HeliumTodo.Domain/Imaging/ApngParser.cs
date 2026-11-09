// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;

namespace HeliumTodo.Domain.Imaging;

/// <summary>APNG 帧的处置方式（这一帧画完之后，下一帧画之前要怎么收拾画面）。</summary>
public enum ApngDisposeOp
{
    /// <summary>什么都不做，下一帧直接盖在现在这层上面。</summary>
    None = 0,

    /// <summary>把这一帧占的矩形区域清空成透明。</summary>
    Background = 1,

    /// <summary>把这一帧占的矩形区域恢复成画这一帧之前的样子。</summary>
    Previous = 2,
}

/// <summary>APNG 帧的混合方式（这一帧怎么和下面的内容叠）。</summary>
public enum ApngBlendOp
{
    /// <summary>直接替换 —— 连透明度都替换掉。</summary>
    Source = 0,

    /// <summary>按透明度叠加，也就是普通的「盖一层」。</summary>
    Over = 1,
}

/// <summary>
/// APNG 里的一帧。
///
/// 关键点是 <see cref="PngBytes"/>：它是一张**独立、合法的普通 PNG 文件**，
/// 也就是说拿到它直接丢给任何图片解码库都能单独解出这一帧。
/// </summary>
public sealed class ApngFrame
{
    public ApngFrame(
        int index,
        int x,
        int y,
        int width,
        int height,
        int delayMilliseconds,
        ApngDisposeOp dispose,
        ApngBlendOp blend,
        byte[] pngBytes)
    {
        Index = index;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        DelayMilliseconds = delayMilliseconds;
        Dispose = dispose;
        Blend = blend;
        PngBytes = pngBytes;
    }

    /// <summary>第几帧，从 0 开始。</summary>
    public int Index { get; }

    /// <summary>这一帧贴在画布上的左上角横坐标。</summary>
    public int X { get; }

    /// <summary>这一帧贴在画布上的左上角纵坐标。</summary>
    public int Y { get; }

    /// <summary>这一帧的宽（可能比画布小 —— APNG 只存变化的那一小块）。</summary>
    public int Width { get; }

    /// <summary>这一帧的高。</summary>
    public int Height { get; }

    /// <summary>这一帧要显示多久（毫秒）。</summary>
    public int DelayMilliseconds { get; }

    /// <summary>画完这一帧之后怎么收拾画面。</summary>
    public ApngDisposeOp Dispose { get; }

    /// <summary>这一帧怎么叠到画面上。</summary>
    public ApngBlendOp Blend { get; }

    /// <summary>这一帧的数据，已经拼成一张独立的 PNG 文件。</summary>
    public byte[] PngBytes { get; }
}

/// <summary>解析出来的 APNG 信息。</summary>
public sealed class ApngInfo
{
    public ApngInfo(int width, int height, int loopCount, IReadOnlyList<ApngFrame> frames)
    {
        Width = width;
        Height = height;
        LoopCount = loopCount;
        Frames = frames;
    }

    /// <summary>画布宽度（所有帧都贴在这么大的画布上）。</summary>
    public int Width { get; }

    /// <summary>画布高度。</summary>
    public int Height { get; }

    /// <summary>循环次数。0 表示无限循环。</summary>
    public int LoopCount { get; }

    /// <summary>所有帧，按显示顺序排列。</summary>
    public IReadOnlyList<ApngFrame> Frames { get; }

    /// <summary>总时长（毫秒）。</summary>
    public int TotalMilliseconds
    {
        get
        {
            var total = 0;
            foreach (var frame in Frames) total += frame.DelayMilliseconds;
            return total;
        }
    }
}

/// <summary>
/// APNG（动态 PNG）解析器。
///
/// 【为什么要自己写这一段？】
///   本来指望图片库直接支持，但实测下来 SkiaSharp 3.119.4 的 SKCodec
///   把 APNG 当成**普通静态 PNG**：FrameCount 返回 0，只认第一帧。
///   （同一份代码解 GIF 是正常的，FrameCount=12、每帧 80ms 都对，
///     所以不是"动图都不支持"，是专门缺 APNG。）
///
///   自己写个完整的 PNG 解码器工作量太大（zlib 解压 + 逐行滤波还原），
///   但其实有个取巧的办法：
///
///     APNG 里每一帧的数据（fdAT 块）本质就是"一段普通的 PNG 图像数据"，
///     和普通 PNG 的 IDAT 块是同一个格式 —— 只是外面换了个块名。
///     所以只要把它抠出来，配上一副新的外壳
///     （PNG 文件头 + 帧自己的 IHDR + 这段数据当 IDAT + IEND），
///     就得到一张**完全合法的普通 PNG**，交给现成的解码库就行。
///
///   压缩/解压缩、滤波这些麻烦事全都不用自己干，
///   我们只做"搬运和重新打包"。实测这个方法解出来的像素完全正确。
///
/// 【这个类为什么在 Core 层？】
///   它只做字节层面的切分和重新拼装，一行都不需要图片库，
///   所以可以放在 Core 里，单元测试直接喂字节数组就能验证。
///   真正把像素解出来（需要 SkiaSharp）是界面层的事。
/// </summary>
public static class ApngParser
{
    /// <summary>所有 PNG 文件开头的 8 个固定字节。</summary>
    private static ReadOnlySpan<byte> PngSignature => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>延迟分母为 0 时的兜底值（PNG 规范规定：分母是 0 就当作 100）。</summary>
    private const int DefaultDelayDenominator = 100;

    /// <summary>算出来的延迟是 0 时用的兜底值（否则会疯狂刷新）。</summary>
    private const int DefaultDelayMilliseconds = 100;

    /// <summary>APNG 里帧数有上限，防止恶意/损坏文件把内存吃光。</summary>
    private const int MaxFrames = 2000;

    /// <summary>
    /// 判断一段字节是不是 APNG。
    ///
    /// 判据是"有 acTL 块" —— 这是 APNG 独有的块，普通 PNG 没有。
    /// 只会扫到第一个 IDAT 之前，所以开销很小。
    /// </summary>
    public static bool IsApng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 || !data[..8].SequenceEqual(PngSignature)) return false;

        int offset = 8;

        while (offset + 8 <= data.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
            if (length < 0 || offset + 12 + length > data.Length) return false;

            var type = data.Slice(offset + 4, 4);

            if (type.SequenceEqual("acTL"u8)) return true;

            // 走到图像数据就不用再找了：按规范 acTL 必须在 IDAT 之前
            if (type.SequenceEqual("IDAT"u8)) return false;

            offset += 12 + length;
        }

        return false;
    }

    /// <summary>
    /// 解析 APNG。
    /// 不是 APNG、或者文件损坏时返回 null（调用方退回普通静态图处理）。
    /// </summary>
    public static ApngInfo? TryParse(ReadOnlySpan<byte> data)
    {
        if (!IsApng(data)) return null;

        // ── 先把所有块扫一遍，分门别类放好 ──
        byte[]? imageHeader = null;          // IHDR 原始内容（像素格式信息，每帧都要复用）
        byte[]? palette = null;              // PLTE 调色板（索引色图片才需要）
        byte[]? transparency = null;         // tRNS 透明色（索引色/GIF 风格透明）
        int declaredFrameCount = -1;
        int loopCount = 0;

        var frameHeaders = new List<byte[]>();       // 每个 fcTL 的原始内容
        var frameDataParts = new List<List<byte[]>>(); // 每帧的图像数据片段

        List<byte[]>? currentParts = null;
        var defaultImageParts = new List<byte[]>();  // 第一个 fcTL 之前出现的 IDAT

        int offset = 8;

        while (offset + 12 <= data.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
            if (length < 0 || offset + 12 + length > data.Length) break;

            var type = data.Slice(offset + 4, 4);
            var payload = data.Slice(offset + 8, length);
            offset += 12 + length;

            if (type.SequenceEqual("IHDR"u8))
            {
                imageHeader = payload.ToArray();
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                palette = payload.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                transparency = payload.ToArray();
            }
            else if (type.SequenceEqual("acTL"u8))
            {
                if (length < 8) return null;
                declaredFrameCount = BinaryPrimitives.ReadInt32BigEndian(payload[..4]);
                loopCount = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(4, 4));
            }
            else if (type.SequenceEqual("fcTL"u8))
            {
                // 每遇到一个 fcTL 就开一帧新的
                if (length < 26) return null;

                frameHeaders.Add(payload.ToArray());
                currentParts = new List<byte[]>();
                frameDataParts.Add(currentParts);

                if (frameHeaders.Count > MaxFrames) return null;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                // 有 fcTL 就是动画第一帧的数据；没有就是"默认图"（静态封面）
                if (currentParts is null) defaultImageParts.Add(payload.ToArray());
                else currentParts.Add(payload.ToArray());
            }
            else if (type.SequenceEqual("fdAT"u8))
            {
                if (currentParts is null || length < 4) continue;

                // fdAT 开头 4 字节是序号，后面才是真正的图像数据
                currentParts.Add(payload[4..].ToArray());
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }
        }

        // 缺 IHDR 或者压根没有帧，说明这个文件不对劲
        if (imageHeader is null || frameHeaders.Count == 0) return null;

        // acTL 里声明的帧数和实际对不上：以实际的为准（有些工具会写错）
        if (declaredFrameCount > 0 && declaredFrameCount != frameHeaders.Count)
        {
            if (frameHeaders.Count > declaredFrameCount)
            {
                frameHeaders.RemoveRange(declaredFrameCount, frameHeaders.Count - declaredFrameCount);
                frameDataParts.RemoveRange(declaredFrameCount, frameDataParts.Count - declaredFrameCount);
            }
        }

        int canvasWidth = BinaryPrimitives.ReadInt32BigEndian(imageHeader.AsSpan(0, 4));
        int canvasHeight = BinaryPrimitives.ReadInt32BigEndian(imageHeader.AsSpan(4, 4));

        if (canvasWidth <= 0 || canvasHeight <= 0) return null;

        // ── 逐帧拼成独立 PNG ──
        var frames = new List<ApngFrame>(frameHeaders.Count);

        for (int i = 0; i < frameHeaders.Count; i++)
        {
            var header = frameHeaders[i];
            var parts = frameDataParts[i];

            int frameWidth = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4));
            int frameHeight = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(8, 4));
            int xOffset = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(12, 4));
            int yOffset = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
            int delayNumerator = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(20, 2));
            int delayDenominator = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(22, 2));
            var disposeOp = (ApngDisposeOp)header[24];
            var blendOp = (ApngBlendOp)header[25];

            // 尺寸不合法就跳过这一帧（宁可少一帧，也不要整张图渲染失败）
            if (frameWidth <= 0 || frameHeight <= 0) continue;

            // 没有数据但是是第 0 帧时，退回去用"默认图"
            if (parts.Count == 0 && i == 0 && defaultImageParts.Count > 0)
            {
                parts = defaultImageParts;
            }

            if (parts.Count == 0) continue;

            int delayMs = ComputeDelay(delayNumerator, delayDenominator);

            var pngBytes = BuildStandalonePng(imageHeader, palette, transparency, frameWidth, frameHeight, parts);

            frames.Add(new ApngFrame(
                index: frames.Count,
                x: xOffset,
                y: yOffset,
                width: frameWidth,
                height: frameHeight,
                delayMilliseconds: delayMs,
                dispose: disposeOp,
                blend: blendOp,
                pngBytes: pngBytes));
        }

        if (frames.Count == 0) return null;

        return new ApngInfo(canvasWidth, canvasHeight, loopCount, frames);
    }

    /// <summary>把 fcTL 里的"分子/分母"秒换算成毫秒。</summary>
    private static int ComputeDelay(int numerator, int denominator)
    {
        if (denominator <= 0) denominator = DefaultDelayDenominator;

        // 先算成秒再乘 1000，避免整数除法把 1/30 秒直接截成 0
        long ms = (long)numerator * 1000 / denominator;

        if (ms <= 0) return DefaultDelayMilliseconds;

        // 上限 10 秒：个别工具会写出离谱的延迟，卡一下防止一帧停半小时
        return (int)Math.Min(ms, 10_000);
    }

    /// <summary>
    /// 把一帧的图像数据重新打包成一张独立的 PNG 文件。
    ///
    /// 结构就是最朴素的 PNG：
    ///   文件头 + IHDR + (PLTE) + (tRNS) + IDAT + IEND
    ///
    /// IHDR 用帧自己的宽高，其余字段（位深、颜色类型、是否隔行）
    /// 原样照抄画布的 IHDR —— 因为同一张 APNG 里所有帧的像素格式一定相同。
    /// </summary>
    private static byte[] BuildStandalonePng(
        byte[] canvasHeader,
        byte[]? palette,
        byte[]? transparency,
        int width,
        int height,
        List<byte[]> imageParts)
    {
        var sum = 0;
        foreach (var part in imageParts) sum += part.Length;

        // 粗算容量，省掉重复扩容（每块多 12 字节的开销）
        using var stream = new MemoryStream(sum + 128 + imageParts.Count * 12);

        stream.Write(PngSignature);

        // 复制画布的 IHDR，只改宽高
        var header = (byte[])canvasHeader.Clone();
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        WriteChunk(stream, "IHDR"u8, header);

        if (palette is not null) WriteChunk(stream, "PLTE"u8, palette);
        if (transparency is not null) WriteChunk(stream, "tRNS"u8, transparency);

        foreach (var part in imageParts)
        {
            WriteChunk(stream, "IDAT"u8, part);
        }

        WriteChunk(stream, "IEND"u8, Array.Empty<byte>());

        return stream.ToArray();
    }

    /// <summary>写一个 PNG 块：长度 + 类型 + 数据 + CRC。</summary>
    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> buffer = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);

        stream.Write(type);
        stream.Write(data);

        // CRC 是"类型 + 数据"一起算的，不含长度字段
        var crc = ComputeCrc32(type, data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        stream.Write(buffer);
    }

    /// <summary>PNG 用的 CRC32（和 zip 那个同一个多项式）。</summary>
    private static uint ComputeCrc32(ReadOnlySpan<byte> type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;

        foreach (var b in type) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFF;
    }

    /// <summary>
    /// CRC32 查表。
    ///
    /// 用查表法而不是一位一位算：一张 1080p 的 APNG 有几十帧、
    /// 每帧几百 KB，逐位算会明显拖慢加载速度。
    /// </summary>
    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (uint n = 0; n < 256; n++)
        {
            uint c = n;

            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
