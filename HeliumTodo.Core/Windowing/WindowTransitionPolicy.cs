namespace HeliumTodo.Core.Windowing;

/// <summary>
/// 窗口过渡动画的「政策」：写死几个数，界面层照着做，测试照着验。
///
/// 为什么这些数要单独放 Core：
///   它们不是随便定的，每一条都有实测依据（见 --perf-check / PROGRESS.md）。
///   写在 App 层就只能靠"改完跑一次看卡不卡"来验证，写在 Core 就能断言，
///   以后有人手滑把 180 改成 400、或者把帧数从 10 降到 6，测试立刻变红。
///
/// 这一组数字的来历：
///   · DurationMs = 180 —— 再长就"慢吞吞"，再短看不出发生了什么；
///   · MinFrames = 10 —— 少于 10 帧的过渡在肉眼看来就是"跳了一下"，
///     180 ms 里 10 帧 = 每帧 18 ms，60 Hz 屏幕上还能看出光滑的中间态；
///   · MaxFrameIntervalFactor = 2 —— 帧间隔最大值超过中位数的两倍就是"一顿一顿"
///     （实测改动前是 18 / 91 / 64 / 58 ms，最慢是中位数的 5 倍，用户说的卡就是它）；
///   · WallpaperPauseThresholdMs = 250 —— 只给"真会拖很久"的动画暂停壁纸动图。
///     180 ms 的动画暂停动图反而更贵：视频壁纸重启要重新定位、重解一帧。
/// </summary>
public static class WindowTransitionPolicy
{
    /// <summary>过渡动画的总时长（毫秒）。</summary>
    public const int DurationMs = 180;

    /// <summary>一次过渡至少要有的动画帧数，少于这个数就算"跳变"。</summary>
    public const int MinFrames = 10;

    /// <summary>帧间隔最大值允许是中位数的多少倍（超过就是卡顿）。</summary>
    public const double MaxFrameIntervalFactor = 2.0;

    /// <summary>动画时长超过这个值，才值得暂停壁纸动图（180 ms 的动画不值得）。</summary>
    public const int WallpaperPauseThresholdMs = 250;

    /// <summary>
    /// 这次动画要不要顺便停掉动图壁纸？
    ///
    /// 动图壁纸每 33 ms 就让整窗重画一次；动画期间窗口本身每帧都在变尺寸，
    /// 等于一帧干两帧的活。但"停 → 播"本身有成本（视频壁纸要重新 seek 解码），
    /// 所以只有明显更长的过渡才划算。
    /// </summary>
    public static bool ShouldPauseWallpaper(int durationMs) =>
        durationMs >= WallpaperPauseThresholdMs;

    /// <summary>
    /// 帧间隔是否可接受：最慢一帧不能超过中位数的两倍。
    ///
    /// 为什么比较倍率而不是绝对值：无头环境和 GPU 环境的绝对毫秒差得很远
    /// （同一个界面 1.4 ms vs 45 ms），但"匀不匀"是同一个标准 ——
    /// 用户感觉到的是忽快忽慢，不是平均帧率。
    /// </summary>
    public static bool IsFramePacingAcceptable(double p50Ms, double worstMs)
    {
        if (p50Ms <= 0)
        {
            return false;
        }

        return worstMs <= p50Ms * MaxFrameIntervalFactor;
    }

    /// <summary>
    /// 按目标时长算出"每帧应该隔多久"（60 Hz 下就是 16.7 ms）。
    /// 动画循环用它做自检，实际驱动源是 RequestAnimationFrame。
    /// </summary>
    public static double FrameIntervalMs(int durationMs, int frames = MinFrames)
    {
        if (durationMs <= 0 || frames <= 0)
        {
            return 0;
        }

        return (double)durationMs / frames;
    }
}