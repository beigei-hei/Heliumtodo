using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkiaSharp;
using HeliumTodo.Core.Media;

namespace HeliumTodo.App.Services;

/// <summary>
/// 用 Windows 自带的 Media Foundation 把视频拆成一串帧。
///
/// 【为什么要自己写这一坨？】
///   GIF / APNG 有现成的解码器（Skia 和本项目的 ApngParser），
///   但 MP4 里装的是 H.264/H.265 这种「帧间预测」的压缩数据，
///   一个像素的值可能依赖前面几十帧 —— 这不是几十行代码能解的东西。
///   自己实现等于重写一个视频解码器，不现实。
///
///   好在这台机器是 Windows：系统自带 Media Foundation（简称 MF），
///   它已经装好了 H.264 解码器。我们只要把「系统解码器」当工具调用，
///   就能拿到一帧帧的原始像素。这也符合本项目「不引入额外依赖」的原则：
///   mfplat.dll / mfreadwrite.dll 是 Windows 自带的，不是我们要打包的第三方库。
///
/// 【代价：这一段必须和 COM 打交道】
///   MF 是 COM 接口，C# 里要用 [ComImport] 把接口的「方法顺序」抄一遍。
///   下面那些接口声明看着长，但都是照抄头的顺序，没有逻辑；
///   真正有逻辑的只有 <see cref="TryDecode"/> 那几十行。
///
/// 【拿到的帧是什么格式？】
///   统一要 RGB32（每像素 4 字节，B,G,R,X）。
///   解码器原本输出的是 NV12（YUV），投给 MF 的「视频处理器」做转换 + 缩放，
///   这样我们自己不用写 YUV→RGB 的换算，也不用单独做缩放。
/// </summary>
[SupportedOSPlatform("windows")]
internal static class MediaFoundationVideo
{
    // ══════════════════════════════════════════════════════════
    //  常量（全部来自 Windows SDK 头文件，值都是抄的，不是猜的）
    // ══════════════════════════════════════════════════════════

    /// <summary>MF_VERSION = (MF_SDK_VERSION &lt;&lt; 16) | MF_API_VERSION，SDK 里就是这么拼的。</summary>
    private const uint MfVersion = 0x00020070;

    /// <summary>「第一个视频流」这个特殊索引。</summary>
    private const uint FirstVideoStream = 0xFFFFFFFC;

    /// <summary>「所有流」，批量设置时用。</summary>
    private const uint AllStreams = 0xFFFFFFFE;

    /// <summary>读到文件末尾。</summary>
    private const uint FlagEndOfStream = 0x2;

    /// <summary>读的过程中出错了。</summary>
    private const uint FlagError = 0x1;

    // ⚠️ 这些 GUID 故意写成「可读写的静态字段」而不是属性：
    //    MF 的方法签名是 REFGUID（C# 里即 ref Guid），而 ref 只能接变量 ——
    //    属性接不了，写成属性会直接编译不过。
    private static Guid MajorTypeVideo = new("73646976-0000-0010-8000-00AA00389B71");
    private static Guid VideoFormatRgb32 = new("00000016-0000-0010-8000-00AA00389B71");
    private static Guid AttributeMajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static Guid AttributeSubType = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static Guid AttributeFrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    private static Guid AttributeInterlaceMode = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    private static Guid ReaderEnableVideoProcessing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");

    // 「高级视频处理」比上面那个权限更大：允许视频处理器做任意尺寸缩放和格式转换。
    // 只开基础版时，请求「4500×3000 → 1280×852」会被拒（MF_E_INVALIDMEDIATYPE）。
    private static Guid ReaderDisableDxva = new("aa456cfd-3943-4a1e-a77d-1838c0ea2e35");


    // ══════════════════════════════════════════════════════════
    //  原生函数
    // ══════════════════════════════════════════════════════════

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(uint version, uint flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateAttributes(out IMFAttributes attributes, uint initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMediaType(out IMFMediaType mediaType);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int MFCreateSourceReaderFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string url,
        IMFAttributes? attributes,
        out IMFSourceReader sourceReader);

    // ══════════════════════════════════════════════════════════
    //  COM 接口声明
    //
    //  ⚠️ 这里的「方法顺序」必须和 C++ 头文件完全一致 —— COM 是靠
    //     虚表里的位置找方法的，顺序错了就会调到别的方法上去，
    //     症状通常是「调用成功但结果是垃圾」或者直接崩溃。
    //     为了能用到后面的方法，前面用不到的方法也得照样写出来。
    // ══════════════════════════════════════════════════════════

    /// <summary>通用的「属性袋」。MF 里所有东西都靠它挂参数。</summary>
    [ComImport]
    [Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFAttributes
    {
        [PreserveSig] int GetItem(ref Guid key, IntPtr value, out uint size);
        [PreserveSig] int GetItemType(ref Guid key, out uint type);
        [PreserveSig] int CompareItem(ref Guid key, IntPtr value, out int result);
        [PreserveSig] int Compare(IMFAttributes theirs, uint matchType, out int result);
        [PreserveSig] int GetUINT32(ref Guid key, out uint value);
        [PreserveSig] int GetUINT64(ref Guid key, out ulong value);
        [PreserveSig] int GetDouble(ref Guid key, out double value);
        [PreserveSig] int GetGUID(ref Guid key, out Guid value);
        [PreserveSig] int GetStringLength(ref Guid key, out uint length);
        [PreserveSig] int GetString(ref Guid key, IntPtr value, uint size, IntPtr length);
        [PreserveSig] int GetAllocatedString(ref Guid key, out IntPtr value, out uint length);
        [PreserveSig] int GetBlobSize(ref Guid key, out uint size);
        [PreserveSig] int GetBlob(ref Guid key, IntPtr value, uint size, IntPtr blobSize);
        [PreserveSig] int GetAllocatedBlob(ref Guid key, out IntPtr value, out uint size);
        [PreserveSig] int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
        [PreserveSig] int SetItem(ref Guid key, IntPtr value, uint size);
        [PreserveSig] int DeleteItem(ref Guid key);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid key, uint value);
        [PreserveSig] int SetUINT64(ref Guid key, ulong value);
        [PreserveSig] int SetDouble(ref Guid key, double value);
        [PreserveSig] int SetGUID(ref Guid key, ref Guid value);
        [PreserveSig] int SetString(ref Guid key, IntPtr value);
        [PreserveSig] int SetBlob(ref Guid key, IntPtr value, uint size);
        [PreserveSig] int SetUnknown(ref Guid key, IntPtr value);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetItemByIndex(uint index, out Guid key, IntPtr value);
        [PreserveSig] int CopyAllItems(IMFAttributes destination);
    }

    /// <summary>媒体类型（描述「这串数据长什么样」）。</summary>
    [ComImport]
    [Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType : IMFAttributes
    {
        [PreserveSig] new int GetItem(ref Guid key, IntPtr value, out uint size);
        [PreserveSig] new int GetItemType(ref Guid key, out uint type);
        [PreserveSig] new int CompareItem(ref Guid key, IntPtr value, out int result);
        [PreserveSig] new int Compare(IMFAttributes theirs, uint matchType, out int result);
        [PreserveSig] new int GetUINT32(ref Guid key, out uint value);
        [PreserveSig] new int GetUINT64(ref Guid key, out ulong value);
        [PreserveSig] new int GetDouble(ref Guid key, out double value);
        [PreserveSig] new int GetGUID(ref Guid key, out Guid value);
        [PreserveSig] new int GetStringLength(ref Guid key, out uint length);
        [PreserveSig] new int GetString(ref Guid key, IntPtr value, uint size, IntPtr length);
        [PreserveSig] new int GetAllocatedString(ref Guid key, out IntPtr value, out uint length);
        [PreserveSig] new int GetBlobSize(ref Guid key, out uint size);
        [PreserveSig] new int GetBlob(ref Guid key, IntPtr value, uint size, IntPtr blobSize);
        [PreserveSig] new int GetAllocatedBlob(ref Guid key, out IntPtr value, out uint size);
        [PreserveSig] new int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
        [PreserveSig] new int SetItem(ref Guid key, IntPtr value, uint size);
        [PreserveSig] new int DeleteItem(ref Guid key);
        [PreserveSig] new int DeleteAllItems();
        [PreserveSig] new int SetUINT32(ref Guid key, uint value);
        [PreserveSig] new int SetUINT64(ref Guid key, ulong value);
        [PreserveSig] new int SetDouble(ref Guid key, double value);
        [PreserveSig] new int SetGUID(ref Guid key, ref Guid value);
        [PreserveSig] new int SetString(ref Guid key, IntPtr value);
        [PreserveSig] new int SetBlob(ref Guid key, IntPtr value, uint size);
        [PreserveSig] new int SetUnknown(ref Guid key, IntPtr value);
        [PreserveSig] new int LockStore();
        [PreserveSig] new int UnlockStore();
        [PreserveSig] new int GetCount(out uint count);
        [PreserveSig] new int GetItemByIndex(uint index, out Guid key, IntPtr value);
        [PreserveSig] new int CopyAllItems(IMFAttributes destination);
        [PreserveSig] int GetMajorType(out Guid majorType);
        [PreserveSig] int IsCompressedFormat(out int compressed);
        [PreserveSig] int IsEqual(IMFMediaType other, out uint flags);
    }

    /// <summary>一块原始像素内存。</summary>
    [ComImport]
    [Guid("045FA593-8799-42B8-BC8D-8968C6453507")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr buffer, out uint maxLength, out uint currentLength);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint length);
        [PreserveSig] int SetCurrentLength(uint length);
        [PreserveSig] int GetMaxLength(out uint length);
    }

    /// <summary>一个「样本」= 一帧画面（视频里）。</summary>
    [ComImport]
    [Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample : IMFAttributes
    {
        [PreserveSig] new int GetItem(ref Guid key, IntPtr value, out uint size);
        [PreserveSig] new int GetItemType(ref Guid key, out uint type);
        [PreserveSig] new int CompareItem(ref Guid key, IntPtr value, out int result);
        [PreserveSig] new int Compare(IMFAttributes theirs, uint matchType, out int result);
        [PreserveSig] new int GetUINT32(ref Guid key, out uint value);
        [PreserveSig] new int GetUINT64(ref Guid key, out ulong value);
        [PreserveSig] new int GetDouble(ref Guid key, out double value);
        [PreserveSig] new int GetGUID(ref Guid key, out Guid value);
        [PreserveSig] new int GetStringLength(ref Guid key, out uint length);
        [PreserveSig] new int GetString(ref Guid key, IntPtr value, uint size, IntPtr length);
        [PreserveSig] new int GetAllocatedString(ref Guid key, out IntPtr value, out uint length);
        [PreserveSig] new int GetBlobSize(ref Guid key, out uint size);
        [PreserveSig] new int GetBlob(ref Guid key, IntPtr value, uint size, IntPtr blobSize);
        [PreserveSig] new int GetAllocatedBlob(ref Guid key, out IntPtr value, out uint size);
        [PreserveSig] new int GetUnknown(ref Guid key, ref Guid riid, out IntPtr value);
        [PreserveSig] new int SetItem(ref Guid key, IntPtr value, uint size);
        [PreserveSig] new int DeleteItem(ref Guid key);
        [PreserveSig] new int DeleteAllItems();
        [PreserveSig] new int SetUINT32(ref Guid key, uint value);
        [PreserveSig] new int SetUINT64(ref Guid key, ulong value);
        [PreserveSig] new int SetDouble(ref Guid key, double value);
        [PreserveSig] new int SetGUID(ref Guid key, ref Guid value);
        [PreserveSig] new int SetString(ref Guid key, IntPtr value);
        [PreserveSig] new int SetBlob(ref Guid key, IntPtr value, uint size);
        [PreserveSig] new int SetUnknown(ref Guid key, IntPtr value);
        [PreserveSig] new int LockStore();
        [PreserveSig] new int UnlockStore();
        [PreserveSig] new int GetCount(out uint count);
        [PreserveSig] new int GetItemByIndex(uint index, out Guid key, IntPtr value);
        [PreserveSig] new int CopyAllItems(IMFAttributes destination);
        [PreserveSig] int GetSampleFlags(out uint flags);
        [PreserveSig] int SetSampleFlags(uint flags);
        [PreserveSig] int GetSampleTime(out long time);
        [PreserveSig] int SetSampleTime(long time);
        [PreserveSig] int GetSampleDuration(out long duration);
        [PreserveSig] int SetSampleDuration(long duration);
        [PreserveSig] int GetBufferCount(out uint count);
        [PreserveSig] int GetBufferByIndex(uint index, out IMFMediaBuffer buffer);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
        [PreserveSig] int AddBuffer(IMFMediaBuffer buffer);
        [PreserveSig] int RemoveBufferByIndex(uint index);
        [PreserveSig] int RemoveAllBuffers();
        [PreserveSig] int GetTotalLength(out uint length);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer buffer);
    }

    /// <summary>读文件的家伙：给个时间点就吐一帧出来。</summary>
    [ComImport]
    [Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSourceReader
    {
        [PreserveSig] int GetStreamSelection(uint index, out int selected);
        [PreserveSig] int SetStreamSelection(uint index, int selected);
        [PreserveSig] int GetNativeMediaType(uint index, uint typeIndex, out IMFMediaType mediaType);
        [PreserveSig] int GetCurrentMediaType(uint index, out IMFMediaType mediaType);
        [PreserveSig] int SetCurrentMediaType(uint index, IntPtr reserved, IMFMediaType mediaType);
        [PreserveSig] int SetCurrentPosition(ref Guid timeFormat, IntPtr position);
        [PreserveSig] int ReadSample(uint index, uint flags, out uint actualStreamIndex, out uint streamFlags, out long timestamp, out IMFSample? sample);
        [PreserveSig] int Flush(uint index);
        [PreserveSig] int GetServiceForStream(uint index, ref Guid service, ref Guid riid, out IntPtr serviceInterface);
        [PreserveSig] int GetPresentationAttribute(uint index, ref Guid attribute, IntPtr value);
    }

    // ══════════════════════════════════════════════════════════
    //  对外的一步：视频文件 → 一串 RGB32 位图
    // ══════════════════════════════════════════════════════════

    /// <summary>MF 是不是能用了（初始化失败时返回 false，调用方好退回"不支持视频"）。</summary>
    public static bool IsAvailable { get; private set; }

    private static readonly object StartupLock = new();
    private static bool _startupAttempted;

    /// <summary>
    /// 拆帧。
    /// </summary>
    /// <param name="path">视频文件路径。</param>
    /// <param name="maxLongSide">长边最多多少像素（和图片同一套缩略规则）。</param>
    /// <param name="maxFrames">最多保留多少帧（内存预算算出来的）。</param>
    /// <returns>成功返回帧列表；失败返回 null 并在 <paramref name="error"/> 里说明原因。</returns>
    public static VideoFrames? TryDecode(string path, int maxLongSide, int maxFrames, out string? error)
    {
        error = null;

        if (maxFrames <= 0) maxFrames = 1;

        if (!EnsureStarted())
        {
            error = "这台电脑上没有可用的 Media Foundation，解不了视频。";
            return null;
        }

        IMFAttributes? attributes = null;
        IMFSourceReader? reader = null;
        var frames = new List<SKBitmap>();
        var timestamps = new List<long>();

        try
        {
            // ① 告诉 MF「我要做像素格式转换和缩放」，它才会插一个视频处理器进来。
            ThrowIfFailed(MFCreateAttributes(out attributes, 4), "创建属性袋");

            var enable = 1u;
            ThrowIfFailed(attributes!.SetUINT32(ref ReaderEnableVideoProcessing, enable), "打开视频处理器");
            // 「高级视频处理」是可选优化：这台机器的解码器不认就当没设过，
            // 后面还有"自己缩"这条兜底路，不能因为一个附加属性设不上就整个失败。
            // 关掉 DXVA（硬件解码）：壁纸是背景，软解完全够；
            //   而硬解会在内存里预分配一整池画面，4500×3000 的视频实测峰值曾到 1.5 GB。
            //   这是可选优化，设不上也不该失败。
            var disableDxva = ReaderDisableDxva;
            var dxvaResult = attributes.SetUINT32(ref disableDxva, enable);

            if (Environment.GetEnvironmentVariable("HELIUM_MF_DEBUG") == "1")
            {
                Console.WriteLine($"[MF] 关硬件解码=0x{dxvaResult:X8}");
            }

            ThrowIfFailed(MFCreateSourceReaderFromURL(path, attributes, out reader), "打开视频文件");

            // ② 只留视频流（音频留着也是白解码）。
            ThrowIfFailed(reader!.SetStreamSelection(AllStreams, 0), "关掉所有流");
            ThrowIfFailed(reader.SetStreamSelection(FirstVideoStream, 1), "打开视频流");

            // ③ 先问清楚原始分辨率，再算出我们要多大。
            ThrowIfFailed(reader.GetNativeMediaType(FirstVideoStream, 0, out var nativeType), "读取视频原始格式");
            ThrowIfFailed(nativeType.GetUINT64(ref AttributeFrameSize, out var packed), "读取原始分辨率");

            int sourceWidth = (int)(packed >> 32);
            int sourceHeight = (int)(packed & 0xFFFF_FFFF);
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                error = "这个视频没有读到有效的画面尺寸。";
                return null;
            }

            var (targetWidth, targetHeight) = ScaleLongSide(sourceWidth, sourceHeight, maxLongSide);

            // ④ 把输出格式改成 RGB32 + 我们要的尺寸。
            //
            //    这里准备了三种写法，由「最省事」到「最保险」依次尝试。
            //    为什么需要三种？因为不同的解码器/视频处理器对输出类型的挑剔程度不一样：
            //      A. 在当前类型上改：里面已经带着色彩空间、像素宽高比，通常最容易被接受；
            //      B. 新建一个干净类型：只写主类型/子类型/尺寸/扫描方式，
            //         微软自己的示例代码就是这么干的，A 被拒时常常 B 能成；
            //      C. 只改子类型、保留原始尺寸（不缩放）。
            //    三种都失败才认定「这个编码这台机器解不了」。
            int lastResult = 0;

            int resultA = 0, resultB = 0, resultC = 0;
            var attemptA = TrySetOutputType(reader, useFreshType: false, resize: true,
                                            targetWidth, targetHeight, out resultA);
            var attemptB = !attemptA && TrySetOutputType(reader, useFreshType: true, resize: true,
                                                         targetWidth, targetHeight, out resultB);
            var attemptC = !attemptA && !attemptB && TrySetOutputType(reader, useFreshType: false, resize: false,
                                                                      targetWidth, targetHeight, out resultC);
            var lastAttempt = attemptC ? resultC : attemptB ? resultB : resultA;
            lastResult = lastAttempt;

            if (Environment.GetEnvironmentVariable("HELIUM_MF_DEBUG") == "1")
            {
                Console.WriteLine($"[MF] 源 {sourceWidth}x{sourceHeight} → 目标 {targetWidth}x{targetHeight}；"
                                + $"A(改当前+缩放)=0x{resultA:X8} B(新建+缩放)=0x{resultB:X8} C(不缩放)=0x{resultC:X8}");
            }

            if (attemptA || attemptB || attemptC)
            {
                // 输出尺寸以最终生效的类型为准：C 方案下就没缩放，
                // 后面算 stride / 拷贝都必须用真实尺寸，否则整张图会错位。
                if (TryReadOutputSize(reader, out var actualWidth, out var actualHeight))
                {
                    targetWidth = actualWidth;
                    targetHeight = actualHeight;
                }
            }
            else
            {
                error = "这个视频的编码方式这台电脑解不了"
                      + $"（常见于 HEVC / AV1，需要装系统解码器）。错误码 0x{lastResult:X8}";
                return null;
            }

            // ⑤ 一帧一帧读。
            //
            //    MF 给的时间戳单位是 100 纳秒，所以 1 毫秒 = 10000。
            //    我们只留下「距离上一张保留的帧至少 33 毫秒」的那些 ——
            //    也就是播放时最多 30 帧/秒。视频本身可能是 60 帧的，
            //    全部留下来既费内存又没意义（人眼+壁纸根本看不出差别）。
            // 30 帧/秒 → 帧间隔至少 33.4 ms；MF 的时间戳单位是 100ns，乘 10000 换算。
            const long MinFrameGap = PlaybackLimits.MinFrameIntervalMs * 10_000L;

            int stride = targetWidth * 4;
            long lastKeptTimestamp = long.MinValue;
            int sampleCount = 0;

            while (true)
            {
                var result = reader.ReadSample(FirstVideoStream, 0, out _, out var flags, out var timestamp, out var sample);

                if (result < 0 || (flags & FlagError) != 0)
                {
                    // 读到坏帧就停下，已经读到的照样用 —— 能显示一部分比整个失败好。
                    break;
                }

                if ((flags & FlagEndOfStream) != 0) break;
                if (sample is null) continue;

                // ⚠️ 每个 sample 用完必须显式 ReleaseComObject。
                //    COM 对象靠引用计数活着，光"出了作用域"不会立刻回收 ——
                //    实测不释放的话，4500×3000 的视频读 30 帧能顶到 3 GB 工作集
                //    （每帧一张 54 MB 的像素缓冲，全挂在 GC 队列里等终结器）。
                try
                {
                    sampleCount++;

                    bool first = lastKeptTimestamp == long.MinValue;
                    if (!first && timestamp - lastKeptTimestamp < MinFrameGap)
                    {
                        // 这一帧离上一张太近，跳过
                        continue;
                    }

                    lastKeptTimestamp = timestamp;
                    timestamps.Add(timestamp);

                    var bitmap = CopySampleToBitmap(sample, targetWidth, targetHeight, stride);

                    // MF 的视频处理器不一定肯接受「顺便帮我换个尺寸」的输出类型
                    //（实测 4500×3000 的视频直接返回 MF_E_INVALIDMEDIATYPE），
                    // 那就自己缩：拷完立刻缩到目标尺寸、把大图丢掉，
                    // 否则 4500×3000 一帧就是 54 MB，几十帧能把内存吃干净。
                    if (maxLongSide > 0 && Math.Max(bitmap.Width, bitmap.Height) > maxLongSide)
                    {
                        var scaled = ScaleBitmap(bitmap, maxLongSide);
                        if (!ReferenceEquals(scaled, bitmap))
                        {
                            bitmap.Dispose();
                            bitmap = scaled;
                        }
                    }

                    // 第一帧的最终尺寸就是"真实输出尺寸"（可能被自己缩过），后面都按它来
                    if (frames.Count == 0)
                    {
                        targetWidth = bitmap.Width;
                        targetHeight = bitmap.Height;
                        stride = targetWidth * 4;
                    }

                    frames.Add(bitmap);
                }
                finally
                {
                    Marshal.ReleaseComObject(sample);
                }

                // 已经够多了就提前收工：视频可能有几十秒，
                // 但我们只打算保留 maxFrames 帧，多读的都要扔掉，白花 CPU。
                if (frames.Count >= maxFrames * 2) break;
            }

            if (frames.Count == 0)
            {
                error = "这个视频里没有解出任何一帧。";
                return null;
            }

            // ⑥ 帧还是太多就隔一张丢一张（延迟累加到保留的那张上），
            //    这样总时长不变，只是帧率更低。和图片列表的超预算处理是同一个思路。
            while (frames.Count > maxFrames)
            {
                var keptFrames = new List<SKBitmap>((frames.Count + 1) / 2);
                var keptTimes = new List<long>((timestamps.Count + 1) / 2);

                for (int i = 0; i < frames.Count; i += 2)
                {
                    keptFrames.Add(frames[i]);
                    keptTimes.Add(timestamps[i]);

                    if (i + 1 < frames.Count) frames[i + 1].Dispose();
                }

                frames = keptFrames;
                timestamps = keptTimes;
            }

            // ⑦ 算每帧该停留多久：用「下一帧的时间戳 - 这一帧的」，
            //    最后一帧用前一帧的间隔兜底（视频是循环播的，接回去正好）。
            var delays = new List<int>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                long gap = i + 1 < timestamps.Count
                    ? timestamps[i + 1] - timestamps[i]
                    : (timestamps.Count > 1 ? timestamps[^1] - timestamps[^2] : MinFrameGap);

                var ms = (int)Math.Round(gap / 10_000.0);

                // 夹一下：太快会超过 30 帧/秒，太慢会像卡死（规则和播放端同一份）
                ms = PlaybackLimits.ClampFrameDelay(ms);

                delays.Add(ms);
            }

            if (Environment.GetEnvironmentVariable("HELIUM_MF_DEBUG") == "1")
            {
                Console.WriteLine($"[MF] 读到 {sampleCount} 帧，保留 {frames.Count} 帧；"
                                + $"时间戳 {string.Join(",", timestamps.Take(4))}；"
                                + $"间隔 {string.Join(",", delays.Take(4))} ms；MinFrameGap={MinFrameGap}");
            }

            return new VideoFrames(targetWidth, targetHeight, frames, delays, sampleCount);
        }
        catch (Exception ex)
        {
            // 视频解不出来不该让程序崩 —— 上面所有 ThrowIfFailed 都会走到这里
            foreach (var frame in frames) frame.Dispose();
            error = $"解析视频失败：{ex.Message}";
            return null;
        }
        finally
        {
            if (reader is not null) Marshal.ReleaseComObject(reader);
            if (attributes is not null) Marshal.ReleaseComObject(attributes);
        }
    }

    /// <summary>
    /// 把一帧样本拷成 SKBitmap。
    ///
    /// ⚠️ 两个必须处理的地方：
    ///   1. MF 给的 RGB32 是「自下而上」存的（DIB 的老规矩），
    ///      直接照抄会得到一张上下颠倒的图 —— 所以要按行倒着拷。
    ///   2. 每一行的字节数不一定等于 width*4（末尾可能有对齐填充），
    ///      所以按 stride 跳行，不能整块 memcpy。
    /// </summary>
    private static SKBitmap CopySampleToBitmap(IMFSample sample, int width, int height, int stride)
    {
        ThrowIfFailed(sample.ConvertToContiguousBuffer(out var buffer), "取出帧数据");

        try
        {
            ThrowIfFailed(buffer.Lock(out var source, out _, out _), "锁定帧内存");

            try
            {
                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
                var bitmap = new SKBitmap(info);

                // 目标位图每一行的字节数（正常就是 width*4，但不写死更稳）
                int rowBytes = bitmap.RowBytes;
                var row = new byte[rowBytes];
                var destination = bitmap.GetPixels();

                for (int y = 0; y < height; y++)
                {
                    // 自下而上：目标的第 y 行来自源的第 (height-1-y) 行
                    var sourceRow = IntPtr.Add(source, (height - 1 - y) * stride);

                    Marshal.Copy(sourceRow, row, 0, Math.Min(rowBytes, stride));

                    // RGB32 的第 4 个字节是填充值（X），Avalonia 要的是 BGRA，
                    // 这里把 alpha 统一写成 255，否则整张壁纸会是透明的。
                    for (int x = 3; x < rowBytes; x += 4)
                    {
                        row[x] = 255;
                    }

                    Marshal.Copy(row, 0, IntPtr.Add(destination, y * rowBytes), rowBytes);
                }

                return bitmap;
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            Marshal.ReleaseComObject(buffer);
        }
    }

    /// <summary>把一张位图等比缩到长边不超过 maxLongSide（质量取双线性，够用且不慢）。</summary>
    private static SKBitmap ScaleBitmap(SKBitmap source, int maxLongSide)
    {
        var (width, height) = ScaleLongSide(source.Width, source.Height, maxLongSide);

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        var scaled = source.Resize(info, sampling);
        return scaled ?? source;
    }

    /// <summary>
    /// 试一种「让解码器输出 RGB32」的写法，成功返回 true。
    /// </summary>
    /// <param name="useFreshType">true = 新建干净的媒体类型；false = 改当前类型。</param>
    /// <param name="resize">false 表示不改尺寸（保留原始分辨率）。</param>
    private static bool TrySetOutputType(
        IMFSourceReader reader,
        bool useFreshType,
        bool resize,
        int targetWidth,
        int targetHeight,
        out int result)
    {
        result = 0;

        // 无论哪条路，都要先知道「当前类型」——不缩放时要拿它的原始尺寸
        if (reader.GetCurrentMediaType(FirstVideoStream, out var sourceType) < 0 || sourceType is null)
        {
            return false;
        }

        IMFMediaType outputType;
        if (useFreshType)
        {
            if (MFCreateMediaType(out outputType) < 0) return false;
        }
        else
        {
            outputType = sourceType;
        }

        try
        {
            var majorType = MajorTypeVideo;
            var rgb32 = VideoFormatRgb32;
            var progressive = 2u;

            outputType.SetGUID(ref AttributeMajorType, ref majorType);
            outputType.SetGUID(ref AttributeSubType, ref rgb32);
            outputType.SetUINT32(ref AttributeInterlaceMode, progressive);

            if (resize)
            {
                var packedSize = ((ulong)targetWidth << 32) | (uint)targetHeight;
                outputType.SetUINT64(ref AttributeFrameSize, packedSize);
            }

            result = reader.SetCurrentMediaType(FirstVideoStream, IntPtr.Zero, outputType);
            return result >= 0;
        }
        finally
        {
            // 只有「新建的类型」需要我们自己释放；改的是来源对象，不能替它释放
            if (useFreshType && outputType is not null)
            {
                Marshal.ReleaseComObject(outputType);
            }
        }
    }

    /// <summary>读回最终生效的输出尺寸（拿不到就当调用方给的值是对的）。</summary>
    private static bool TryReadOutputSize(IMFSourceReader reader, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (reader.GetCurrentMediaType(FirstVideoStream, out var currentType) < 0 || currentType is null)
        {
            return false;
        }

        try
        {
            if (currentType.GetUINT64(ref AttributeFrameSize, out var packed) < 0) return false;

            width = (int)(packed >> 32);
            height = (int)(packed & 0xFFFF_FFFF);
            return width > 0 && height > 0;
        }
        finally
        {
            Marshal.ReleaseComObject(currentType);
        }
    }

    /// <summary>长边超过限制就等比缩小；小图不放大（放大会糊，还费内存）。</summary>
    private static (int Width, int Height) ScaleLongSide(int width, int height, int maxLongSide)
    {
        int longSide = Math.Max(width, height);
        if (maxLongSide <= 0 || longSide <= maxLongSide) return (width, height);

        double scale = (double)maxLongSide / longSide;
        int targetWidth = Math.Max(2, (int)Math.Round(width * scale));
        int targetHeight = Math.Max(2, (int)Math.Round(height * scale));

        // 视频编码通常要求宽高是偶数，这里也保持偶数，省得 MF 内部再调整
        return (targetWidth & ~1, targetHeight & ~1);
    }

    private static bool EnsureStarted()
    {
        lock (StartupLock)
        {
            if (_startupAttempted) return IsAvailable;

            _startupAttempted = true;

            try
            {
                IsAvailable = MFStartup(MfVersion, 0) >= 0;
            }
            catch (DllNotFoundException)
            {
                // 极老的系统（或精简版）没有 mfplat.dll
                IsAvailable = false;
            }

            return IsAvailable;
        }
    }

    private static void ThrowIfFailed(int hresult, string what)
    {
        if (hresult < 0) Marshal.ThrowExceptionForHR(hresult);
    }

}

/// <summary>视频拆出来的帧。</summary>
internal sealed class VideoFrames
{
    public VideoFrames(int width, int height, List<SKBitmap> frames, List<int> delays, int sourceFrameCount)
    {
        Width = width;
        Height = height;
        Frames = frames;
        Delays = delays;
        SourceFrameCount = sourceFrameCount;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>RGB32 位图，调用方负责在用完后释放。</summary>
    public List<SKBitmap> Frames { get; }

    /// <summary>每帧停留多少毫秒（已经限制在 30 帧/秒以内）。</summary>
    public List<int> Delays { get; }

    /// <summary>原视频一共有多少个样本（用来显示"为了省内存保留了 26 帧"）。</summary>
    public int SourceFrameCount { get; }
}