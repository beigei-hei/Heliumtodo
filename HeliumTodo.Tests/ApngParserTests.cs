// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Text;
using HeliumTodo.Domain.Imaging;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// APNG 拆帧的测试。
///
/// 为什么这批测试要自己拼字节？
///   因为 APNG 是"在 PNG 里塞几个自定义块"的格式，
///   要测解析器就得有真数据。用图片文件当测试素材会有两个问题：
///     1. 测试依赖外部文件，换台机器可能找不到；
///     2. 想测"帧数不对""块被截断"这类边界情况，得准备一堆畸形图片。
///   所以这里直接手写一个最小的 APNG 出来 —— 格式细节一目了然，也方便造畸形数据。
/// </summary>
public class ApngParserTests
{
    /// <summary>PNG 文件头那 8 个固定字节。</summary>
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>拼一个 PNG 块：长度 + 类型 + 数据 + CRC。</summary>
    /// <remarks>
    /// 这里的 CRC 一律填 0。解析器不校验输入数据的 CRC（校验 CRC 是解码器的事），
    /// 所以填 0 不影响测试；解析器自己生成输出 PNG 时会重新算真 CRC。
    /// </remarks>
    private static void Chunk(List<byte> output, string type, byte[] data)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)data.Length);
        output.AddRange(header);
        output.AddRange(Encoding.ASCII.GetBytes(type));
        output.AddRange(data);
        output.AddRange(new byte[4]); // CRC 占位
    }

    private static byte[] UInt32(params uint[] values)
    {
        var buffer = new byte[values.Length * 4];

        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(i * 4), values[i]);
        }

        return buffer;
    }

    /// <summary>IHDR 的数据部分：宽、高，后面跟 5 个固定值（8 位深、RGBA、不交错）。</summary>
    private static byte[] Ihdr(uint width, uint height)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0), width);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), height);
        data[8] = 8;  // 位深
        data[9] = 6;  // 颜色类型 6 = RGBA
        data[10] = 0; // 压缩方法
        data[11] = 0; // 过滤方法
        data[12] = 0; // 不交错
        return data;
    }

    /// <summary>fcTL 的数据部分：序号、宽高、左上角坐标、延迟分数、处置方式、混合方式。</summary>
    private static byte[] Fctl(
        uint sequence,
        uint width,
        uint height,
        uint x,
        uint y,
        ushort delayNumerator,
        ushort delayDenominator,
        byte dispose = 0,
        byte blend = 0)
    {
        var data = new byte[26];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0), sequence);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), width);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), height);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12), x);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), y);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(20), delayNumerator);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(22), delayDenominator);
        data[24] = dispose;
        data[25] = blend;
        return data;
    }

    /// <summary>
    /// 造一个两帧的 APNG：
    ///   第 0 帧是整张 4x4，延迟 1/10 秒（100 毫秒）；
    ///   第 1 帧是左上角 (2,2) 处一块 2x2，延迟 1/10 秒。
    /// </summary>
    private static byte[] BuildTwoFrameApng(uint loopCount = 0)
    {
        var png = new List<byte>();
        png.AddRange(Signature);
        Chunk(png, "IHDR", Ihdr(4, 4));

        // acTL：总共 2 帧，播放次数 loopCount（0 表示无限循环）
        var actl = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(actl.AsSpan(0), 2);
        BinaryPrimitives.WriteUInt32BigEndian(actl.AsSpan(4), loopCount);
        Chunk(png, "acTL", actl);

        // 第 0 帧：整张画布
        Chunk(png, "fcTL", Fctl(0, 4, 4, 0, 0, 1, 10));
        Chunk(png, "IDAT", new byte[] { 1, 2, 3, 4 });

        // 第 1 帧：2x2 的局部块，数据放在 fdAT 里
        Chunk(png, "fcTL", Fctl(1, 2, 2, 2, 2, 1, 10, dispose: 1, blend: 1));
        var fdat = new List<byte>();
        fdat.AddRange(UInt32(2)); // 序号
        fdat.AddRange(new byte[] { 5, 6, 7, 8 });
        Chunk(png, "fdAT", fdat.ToArray());

        Chunk(png, "IEND", Array.Empty<byte>());

        return png.ToArray();
    }

    [Fact]
    public void 认得出来是APNG()
    {
        Assert.True(ApngParser.IsApng(BuildTwoFrameApng()));
    }

    [Fact]
    public void 普通PNG不会被当成APNG()
    {
        var png = new List<byte>();
        png.AddRange(Signature);
        Chunk(png, "IHDR", Ihdr(4, 4));
        Chunk(png, "IDAT", new byte[] { 1, 2, 3, 4 });
        Chunk(png, "IEND", Array.Empty<byte>());

        Assert.False(ApngParser.IsApng(png.ToArray()));
        Assert.Null(ApngParser.TryParse(png.ToArray()));
    }

    [Fact]
    public void 解析出正确的画布尺寸和帧数()
    {
        var info = ApngParser.TryParse(BuildTwoFrameApng());

        Assert.NotNull(info);
        Assert.Equal(4, info!.Width);
        Assert.Equal(4, info.Height);
        Assert.Equal(2, info.Frames.Count);
    }

    [Fact]
    public void 每一帧的区域和延迟都对得上()
    {
        var info = ApngParser.TryParse(BuildTwoFrameApng())!;

        var first = info.Frames[0];
        Assert.Equal(0, first.X);
        Assert.Equal(0, first.Y);
        Assert.Equal(4, first.Width);
        Assert.Equal(4, first.Height);
        Assert.Equal(100, first.DelayMilliseconds);

        var second = info.Frames[1];
        Assert.Equal(2, second.X);
        Assert.Equal(2, second.Y);
        Assert.Equal(2, second.Width);
        Assert.Equal(2, second.Height);
        Assert.Equal(100, second.DelayMilliseconds);

        // 1/10 秒 × 2 帧 = 200 毫秒
        Assert.Equal(200, info.TotalMilliseconds);
    }

    [Fact]
    public void 处置方式和混合方式能读出来()
    {
        var info = ApngParser.TryParse(BuildTwoFrameApng())!;

        Assert.Equal(ApngDisposeOp.None, info.Frames[0].Dispose);
        Assert.Equal(ApngBlendOp.Source, info.Frames[0].Blend);

        Assert.Equal(ApngDisposeOp.Background, info.Frames[1].Dispose);
        Assert.Equal(ApngBlendOp.Over, info.Frames[1].Blend);
    }

    [Fact]
    public void 循环次数能读出来()
    {
        var once = ApngParser.TryParse(BuildTwoFrameApng(loopCount: 1))!;
        Assert.Equal(1, once.LoopCount);

        var forever = ApngParser.TryParse(BuildTwoFrameApng(loopCount: 0))!;
        Assert.Equal(0, forever.LoopCount);
    }

    [Fact]
    public void 每一帧都拼成了一张能单独解码的PNG()
    {
        var info = ApngParser.TryParse(BuildTwoFrameApng())!;

        foreach (var frame in info.Frames)
        {
            // 每帧都得是一个完整的独立 PNG：文件头 + IHDR + IDAT + IEND
            Assert.True(frame.PngBytes.Length > Signature.Length);
            Assert.Equal(Signature, frame.PngBytes.Take(8));

            // 结尾必须是 IEND 块（类型 4 字节 + CRC 4 字节）
            var tail = Encoding.ASCII.GetString(frame.PngBytes, frame.PngBytes.Length - 8, 4);
            Assert.Equal("IEND", tail);
        }
    }

    [Fact]
    public void 局部块那一帧的IHDR用的是自己区域的尺寸()
    {
        var info = ApngParser.TryParse(BuildTwoFrameApng())!;

        // 第 1 帧是 2x2，它拼出来的 PNG 里 IHDR 也必须是 2x2，
        // 而不是整张画布的 4x4 —— 否则贴到画布上位置就错了
        var second = info.Frames[1].PngBytes;
        var width = BinaryPrimitives.ReadUInt32BigEndian(second.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(second.AsSpan(20, 4));

        Assert.Equal(2u, width);
        Assert.Equal(2u, height);
    }

    [Fact]
    public void 数据被截断时返回空而不是抛异常()
    {
        var full = BuildTwoFrameApng();

        // 切掉一半
        Assert.Null(ApngParser.TryParse(full.AsSpan(0, full.Length / 2).ToArray()));
    }

    [Fact]
    public void 空数据和乱数据都不会崩()
    {
        Assert.False(ApngParser.IsApng(ReadOnlySpan<byte>.Empty));
        Assert.Null(ApngParser.TryParse(Array.Empty<byte>()));

        var garbage = new byte[256];
        new Random(20261025).NextBytes(garbage);
        Assert.False(ApngParser.IsApng(garbage));
        Assert.Null(ApngParser.TryParse(garbage));
    }

    [Fact]
    public void 延迟分母为零时用默认值()
    {
        // 分母为 0 在规范里是"未指定"，解析器会当成 100。
        // 这里造一帧 1/0，期望得到 10 毫秒而不是除零崩溃。
        var png = new List<byte>();
        png.AddRange(Signature);
        Chunk(png, "IHDR", Ihdr(2, 2));

        var actl = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(actl.AsSpan(0), 1);
        BinaryPrimitives.WriteUInt32BigEndian(actl.AsSpan(4), 0);
        Chunk(png, "acTL", actl);

        Chunk(png, "fcTL", Fctl(0, 2, 2, 0, 0, delayNumerator: 1, delayDenominator: 0));
        Chunk(png, "IDAT", new byte[] { 9, 9, 9, 9 });
        Chunk(png, "IEND", Array.Empty<byte>());

        var info = ApngParser.TryParse(png.ToArray());

        Assert.NotNull(info);
        Assert.Equal(10, info!.Frames[0].DelayMilliseconds);
    }
}
