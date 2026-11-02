using HeliumTodo.Core.Windowing;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 窗口过渡动画那几条"政策"的测试。
///
/// 为什么要测这个？
///   这几个数不是审美，是从实测里抠出来的：180 ms 里至少 10 帧、最慢一帧
///   不得超过中位数的两倍，做不到就说明动画"一顿一顿"（用户原话：卡顿）。
///   它们一旦被人手滑改掉（比如觉得 180 ms 太短改成 400，或者为了省事只跑 6 帧），
///   界面上不会报错，只会"又变卡了"—— 那时候再回头查是哪次改的就很贵。
///   所以把判断写成纯函数钉在这里。
/// </summary>
public class WindowTransitionPolicyTests
{
    [Fact]
    public void 过渡时长与最少帧数应当是实测定下来的值()
    {
        // 180 ms / 10 帧 = 每帧 18 ms，60 Hz 屏幕上还能看出中间态；
        // 少于 10 帧，肉眼就是"跳了一下"。
        Assert.Equal(180, WindowTransitionPolicy.DurationMs);
        Assert.Equal(10, WindowTransitionPolicy.MinFrames);
        Assert.True(WindowTransitionPolicy.DurationMs >= WindowTransitionPolicy.MinFrames);
    }

    [Fact]
    public void 帧间隔倍率超过两倍就算卡()
    {
        // 这两组正是真机上量到的：改动前 18 / 91 ms（最慢是中位数的 5 倍，用户说卡），
        // 改动后 20 / 48 ms（2.3 倍，仍在临界线上）。
        Assert.False(WindowTransitionPolicy.IsFramePacingAcceptable(p50Ms: 18, worstMs: 91));
        Assert.False(WindowTransitionPolicy.IsFramePacingAcceptable(p50Ms: 20, worstMs: 48));

        // 均匀的过渡才合格：最慢一帧没超过中位数的两倍。
        Assert.True(WindowTransitionPolicy.IsFramePacingAcceptable(p50Ms: 16.7, worstMs: 33.4));
        Assert.True(WindowTransitionPolicy.IsFramePacingAcceptable(p50Ms: 11.8, worstMs: 23.4));
    }

    [Fact]
    public void 帧间隔数据缺失时不能算合格()
    {
        // p50 为 0 说明根本没采到帧（动画没跑起来、或者被提前 Settle 掉）。
        // 这种情况必须判"不合格"，否则一个跑不起来的动画也能"通过"检查。
        Assert.False(WindowTransitionPolicy.IsFramePacingAcceptable(p50Ms: 0, worstMs: 0));
        Assert.False(WindowTransitionPolicy.IsFramePacingAcceptable(p50Ms: -5, worstMs: 10));
    }

    [Fact]
    public void 只有长动画才值得暂停动图壁纸()
    {
        // 180 ms 的动画暂停再恢复动图壁纸是净亏：视频壁纸重启要重新定位、重解一帧。
        Assert.False(WindowTransitionPolicy.ShouldPauseWallpaper(WindowTransitionPolicy.DurationMs));
        Assert.False(WindowTransitionPolicy.ShouldPauseWallpaper(249));

        // 拖边改大小这类可能拖很久的交互才值得暂停。
        Assert.True(WindowTransitionPolicy.ShouldPauseWallpaper(WindowTransitionPolicy.WallpaperPauseThresholdMs));
        Assert.True(WindowTransitionPolicy.ShouldPauseWallpaper(1200));
    }

    [Fact]
    public void 每帧间隔要按目标时长平分()
    {
        // 180 ms / 10 帧 = 18 ms；60 Hz 是 16.7 ms，两者接近，说明选 10 帧是合理的。
        Assert.Equal(18, WindowTransitionPolicy.FrameIntervalMs(180, 10), precision: 3);
        Assert.Equal(16.67, WindowTransitionPolicy.FrameIntervalMs(1000, 60), precision: 2);

        // 参数不合法时不许除零。
        Assert.Equal(0, WindowTransitionPolicy.FrameIntervalMs(0, 10));
        Assert.Equal(0, WindowTransitionPolicy.FrameIntervalMs(180, 0));
    }
}