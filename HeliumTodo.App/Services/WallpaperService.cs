using Avalonia.Media;
using Avalonia.Threading;
using HeliumTodo.Core.Media;
using HeliumTodo.Core.Theming;

namespace HeliumTodo.App.Services;

/// <summary>
/// 背景壁纸服务。
///
/// 【它负责什么？】
///   · 记住「现在用的是哪张壁纸」
///   · 动图的话按每帧的延迟自动播放
///   · 把「当前该显示哪一帧」和「从壁纸里取到的强调色」暴露给界面
///
/// 【它不负责什么？】
///   · 怎么解码图片 —— 那是 <see cref="WallpaperDecoder"/> 的事
///   · 界面长什么样 —— 那是 XAML 的事
///
/// 拆开以后，这个类本身几乎没有"算法"，只有状态和定时器，
/// 出错的时候一眼就能判断是解码的问题还是播放的问题。
/// </summary>
public sealed class WallpaperService : IDisposable
{
    /// <summary>播放帧率上限：30 帧/秒（数值来源见 Core 的 PlaybackLimits）。</summary>
    public const int MaxFramesPerSecond = PlaybackLimits.MaxFramesPerSecond;

    /// <summary>最小帧间隔（毫秒），对应上面的帧率上限。</summary>
    public const int MinFrameIntervalMs = PlaybackLimits.MinFrameIntervalMs;

    private readonly DispatcherTimer _timer;

    private DecodedWallpaper? _decoded;
    private int _frameIndex;
    private bool _isPlaying;
    private bool _disposed;

    public WallpaperService()
    {
        // 用 DispatcherTimer 而不是 System.Threading.Timer：
        // 因为这个定时器的回调要改界面绑定的属性，必须在界面线程上跑。
        // 后台线程的定时器改属性会让 Avalonia 直接抛异常。
        _timer = new DispatcherTimer();
        _timer.Tick += OnTimerTick;
    }

    /// <summary>壁纸状态变了（换了图、播到下一帧、清空了）。界面收到后刷新绑定。</summary>
    public event EventHandler? Changed;

    /// <summary>当前该显示的那一帧。没加载壁纸时是 null。</summary>
    public IImage? CurrentFrame =>
        _decoded is null || _frameIndex >= _decoded.Frames.Count
            ? null
            : _decoded.Frames[_frameIndex];

    /// <summary>
    /// 当前这一帧【实际】会停多久（毫秒，已经夹过 30 帧/秒上限）。
    ///
    /// 和 <see cref="FrameDelays"/> 的区别：那个是解码出来的原始间隔
    /// （GIF 里可能写 20 ms），这个是真正排进定时器的值。
    /// 自检要验的是后者 —— 前者只是素材，后者才是"用户看到的帧率"。
    /// </summary>
    public int NextFrameDelayMs =>
        _decoded is null || _decoded.FrameDelays.Count == 0
            ? PlaybackLimits.FallbackFrameIntervalMs
            : PlaybackLimits.ClampFrameDelay(_decoded.FrameDelays[_frameIndex]);

    /// <summary>每一帧的原始间隔（毫秒，未夹上限）。</summary>
    public IReadOnlyList<int> FrameDelays =>
        _decoded is null ? Array.Empty<int>() : _decoded.FrameDelays;

    /// <summary>从壁纸里取出来的强调色。</summary>
    public AccentColor Accent { get; private set; } = AccentColor.Default;

    /// <summary>有没有加载成功。</summary>
    public bool IsLoaded => _decoded is not null;

    /// <summary>是不是动图。</summary>
    public bool IsAnimated => _decoded?.IsAnimated ?? false;

    /// <summary>实际保留了多少帧（超预算时会被抽帧，比原图少）。</summary>
    public int FrameCount => _decoded?.Frames.Count ?? 0;

    /// <summary>原图本来有多少帧。</summary>
    public int SourceFrameCount => _decoded?.SourceFrameCount ?? 0;

    /// <summary>解码后的宽高。</summary>
    public int PixelWidth => _decoded?.Width ?? 0;

    /// <summary>解码后的高。</summary>
    public int PixelHeight => _decoded?.Height ?? 0;

    /// <summary>当前壁纸文件的路径。</summary>
    public string? FilePath { get; private set; }

    /// <summary>加载失败的原因（成功时是 null）。</summary>
    public string? Error { get; private set; }

    /// <summary>是不是正在播放。</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>因为内存预算被丢掉了多少帧。</summary>
    public int DroppedFrameCount => Math.Max(0, SourceFrameCount - FrameCount);

    /// <summary>
    /// 加载一张壁纸。
    ///
    /// 解码放在后台线程做，因为一张动图可能要解几十帧、
    /// 每帧还要缩放和模糊，在主线程干这个界面会僵住一两秒。
    /// </summary>
    /// <returns>成功返回 true。失败时 <see cref="Error"/> 里会有原因。</returns>
    public async Task<bool> LoadAsync(string path, int blurRadius, ThemeKind kind)
    {
        Stop();

        try
        {
            if (!File.Exists(path))
            {
                Error = $"找不到这个文件：{path}";
                Apply(null);
                return false;
            }

            // 读文件 + 解码 + 缩放 + 模糊，全都在后台线程做完。
            // 这里特意【不】写 ConfigureAwait(false)：
            // 因为下面的 Apply() 会改界面绑定的属性、还要发事件给界面，
            // 必须回到界面线程上做，否则 Avalonia 会抛"跨线程访问"。
            // DecodeFile 自己按扩展名分流：视频走 Media Foundation，图片照旧走 Skia，
            // 所以这里不需要判断"这是不是 mp4"。
            var decoded = await Task.Run(() => WallpaperDecoder.DecodeFile(path, blurRadius, kind));

            Error = null;
            FilePath = path;
            Apply(decoded);

            if (decoded.IsAnimated)
            {
                Start();
            }

            return true;
        }
        catch (Exception ex)
        {
            // 这里不往上抛：壁纸坏了不该让整个程序起不来，
            // 退回"没有壁纸"的状态继续用就是了。
            Error = ex.Message;
            FilePath = path;
            Apply(null);
            return false;
        }
    }

    /// <summary>把当前壁纸撤掉（不影响磁盘上的文件）。</summary>
    public void Clear()
    {
        Stop();
        Error = null;
        FilePath = null;
        Apply(null);
    }

    /// <summary>开始（或继续）播放动图。静态图调用没有副作用。</summary>
    public void Start()
    {
        if (_decoded is null || !_decoded.IsAnimated) return;

        _isPlaying = true;
        ScheduleNextFrame();
    }

    /// <summary>暂停播放。</summary>
    public void Stop()
    {
        _isPlaying = false;
        _timer.Stop();
    }

    /// <summary>换上新解码结果（或清空）。</summary>
    private void Apply(DecodedWallpaper? decoded)
    {
        _decoded = decoded;
        _frameIndex = 0;
        Accent = decoded?.Accent ?? AccentColor.Default;

        NotifyChanged();
    }

    /// <summary>
    /// 给下一帧排个定时器。
    ///
    /// 为什么每帧单独排一次，而不是用一个固定间隔的定时器？
    ///   因为动图每一帧的停留时间可以不一样（这是 GIF/APNG 格式本身支持的）。
    ///   用固定间隔的话，慢帧会被提前切掉、快帧会卡住，
    ///   结果就是"动作看着不对劲"。
    /// </summary>
    private void ScheduleNextFrame()
    {
        _timer.Stop();

        if (!_isPlaying || _decoded is null || _decoded.Frames.Count <= 1) return;

        // 排下一帧之前把间隔夹进合法区间：
        //   0/负数 → 兜底值；< 34 ms → 抬到 34 ms（30 帧/秒上限）；
        //   > 10 秒 → 压到 10 秒。
        //
        // 为什么解码端丢了帧，这里还要再夹一次？
        //   解码端是"按时间戳跳过"，只能压到接近 30 帧/秒；
        //   GIF 里写 20 ms、MP4 里写 16 ms 的情况都很常见。
        //   播放端按真实间隔夹一下，才是"无论如何都不超过 30 帧/秒"的硬保证。
        var delay = NextFrameDelayMs;

        _timer.Interval = TimeSpan.FromMilliseconds(delay);
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!AdvanceFrame())
        {
            Stop();
            return;
        }

        ScheduleNextFrame();
    }

    /// <summary>
    /// 前进到下一帧。定时器到点时会调它。
    ///
    /// 单独抽成一个公开方法，是因为无头渲染自检里 DispatcherTimer 根本不会触发
    /// （那条消息循环是假的），只能手动推帧来验证"帧序列对不对、循环得回去吗"。
    /// </summary>
    /// <returns>还能继续播就返回 true。</returns>
    public bool AdvanceFrame()
    {
        if (_decoded is null || _decoded.Frames.Count <= 1) return false;

        _frameIndex = (_frameIndex + 1) % _decoded.Frames.Count;

        NotifyChanged();

        return true;
    }

    private void NotifyChanged()
    {
        if (_disposed) return;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _decoded = null;
    }
}
