using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using HeliumTodo.App.Services;

namespace HeliumTodo.App.Windowing;

/// <summary>
/// 「窗口正在动」这段时间里的临时降质开关。
///
/// 为什么需要它：这个应用默认走软件渲染（见 Program.CreateWin32Options 里的取舍），
/// 而软件渲染下"改一次窗口尺寸"要整窗重画一遍。实测（--perf-check，1400×900）：
///
///   改尺寸 + 高品质壁纸      约 55 ms/帧  → 18 帧/秒，肉眼就是一顿一顿的
///   改尺寸 + 低品质壁纸      约 35 ms/帧  → 29 帧/秒
///   改尺寸 + 不要卡片阴影    约 26 ms/帧  → 38 帧/秒
///
/// 也就是说"壁纸的高品质重采样"和"卡片阴影的模糊"各吃掉十几毫秒，
/// 而这两样在窗口快速变形的那 180 毫秒里根本看不出来 —— 图在动，谁去数边缘像素。
///
/// 所以：最大化 / 还原 / 拖边改大小 / 开场淡入期间降质，结束后立刻恢复。
/// 用引用计数是因为这几件事会重叠（比如开场动画还没播完用户就拖边缘）。
/// </summary>
internal sealed class InteractionQuality
{
    private readonly Window _window;

    /// <summary>壁纸图层。第一次用到时才在可视树里找 —— 构造时可视树还没建起来。</summary>
    private Image? _wallpaper;
    private bool _wallpaperSearched;

    /// <summary>嵌套层数：>0 表示"正在动"。</summary>
    private int _depth;

    /// <summary>壁纸动图是被我们停掉的（停下才需要负责恢复，静态图本来就不播）。</summary>
    private bool _pausedWallpaper;

    public InteractionQuality(Window window)
    {
        _window = window;
    }

    /// <summary>当前是否处于降质状态（自检和调试用）。</summary>
    public bool IsLowQuality => _depth > 0;

    /// <summary>
    /// 进入降质状态，返回的作用域销毁时退出。
    /// 用法：<c>using var _ = quality.Begin();</c>
    /// </summary>
    public IDisposable Begin()
    {
        if (_disabled)
        {
            return EmptyScope.Instance;
        }

        if (_depth++ == 0)
        {
            Enter();
        }

        return new Scope(this);
    }

    /// <summary>
    /// HELIUM_NO_INTERACTION_QUALITY=1 时整套路子都不走。
    /// 留着是为了做 A/B 对照（确认优化真的有用），以及将来换成 GPU 渲染后
    /// 发现自己起到的只有"壁纸变糊"这个副作用时可以一键关掉。
    /// </summary>
    private static readonly bool _disabled =
        Environment.GetEnvironmentVariable("HELIUM_NO_INTERACTION_QUALITY") == "1";

    private void Enter()
    {
        if (ResolveWallpaper() is { } image)
        {
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.LowQuality);
        }

        App.SetCardShadowSuppressed(true);
        PauseWallpaperAnimation();
    }

    private void Exit()
    {
        if (ResolveWallpaper() is { } image)
        {
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        }

        App.SetCardShadowSuppressed(false);
        ResumeWallpaperAnimation();
    }

    private Image? ResolveWallpaper()
    {
        if (_wallpaperSearched)
        {
            return _wallpaper;
        }

        _wallpaperSearched = true;
        _wallpaper = _window.GetVisualDescendants()
            .OfType<Image>()
            .FirstOrDefault(i => i.Name == "WallpaperImage");

        return _wallpaper;
    }

    /// <summary>
    /// 动图壁纸每 33 毫秒换一帧、让整窗重画一次；窗口正在改尺寸时再叠上它，
    /// 等于一帧干两帧的活。停一下，动完再播（静态图不动，什么也不用做）。
    /// </summary>
    private void PauseWallpaperAnimation()
    {
        if (App.Services?.GetService(typeof(WallpaperService)) is not WallpaperService wallpaper)
        {
            return;
        }

        if (!wallpaper.IsPlaying)
        {
            return;
        }

        wallpaper.Stop();
        _pausedWallpaper = true;
    }

    private void ResumeWallpaperAnimation()
    {
        if (!_pausedWallpaper)
        {
            return;
        }

        _pausedWallpaper = false;

        if (App.Services?.GetService(typeof(WallpaperService)) is WallpaperService wallpaper)
        {
            wallpaper.Start();
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly InteractionQuality _owner;
        private bool _disposed;

        public Scope(InteractionQuality owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (--_owner._depth == 0)
            {
                _owner.Exit();
            }
        }
    }
}