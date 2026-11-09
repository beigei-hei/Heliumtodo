// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Windowing;

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
///   · MaxFrameIntervalFactor = 2 —— 尾部帧（P95）超过中位数的两倍就是"一顿一顿"
///     （实测改动前是 18 / 91 / 64 / 58 ms，P95 也是中位数的三四倍，用户说的卡就是它）；
///   · MaxSingleFrameFactor = 4 —— 单帧尖峰容忍到中位数的四倍。
///     为什么不拿"最慢一帧"卡 2 倍：一轮只采 20 多帧，GC、字体缓存、别的进程抢 CPU
///     都会打出孤立的一帧，按它判红等于让断言随机变红（实测有过 P50 63 ms、最慢 135 ms，
///     比值 2.13，而肉眼完全看不出卡）。真正的卡顿是"尾部整体抬高"，那是 P95 的事。
///   · WallpaperPauseThresholdMs = 250 —— 只给"真会拖很久"的动画暂停壁纸动图。
///     180 ms 的动画暂停动图反而更贵：视频壁纸重启要重新定位、重解一帧。
/// </summary>
public static class WindowTransitionPolicy
{
    /// <summary>过渡动画的总时长（毫秒）。</summary>
    public const int DurationMs = 180;

    /// <summary>一次过渡至少要有的动画帧数，少于这个数就算"跳变"。</summary>
    public const int MinFrames = 10;

    /// <summary>尾部帧（P95）允许是中位数的多少倍（超过就是卡顿）。</summary>
    public const double MaxFrameIntervalFactor = 2.0;

    /// <summary>孤立的单帧尖峰允许是 <see cref="MaxFrameIntervalFactor"/> 的几倍。</summary>
    public const double MaxSingleFrameFactor = 4.0;

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
    /// 尾部帧是否可接受：P95 不能超过中位数的两倍。
    ///
    /// 为什么比较倍率而不是绝对值：无头环境和 GPU 环境的绝对毫秒差得很远
    /// （同一个界面 1.4 ms vs 45 ms），但"匀不匀"是同一个标准 ——
    /// 用户感觉到的是忽快忽慢，不是平均帧率。
    /// </summary>
    /// <param name="p50Ms">帧间隔中位数（毫秒）。</param>
    /// <param name="p95Ms">帧间隔 P95（毫秒）。</param>
    public static bool IsFramePacingAcceptable(double p50Ms, double p95Ms)
    {
        if (p50Ms <= 0)
        {
            return false;
        }

        return p95Ms <= p50Ms * MaxFrameIntervalFactor;
    }

    /// <summary>
    /// 最慢的一帧有没有离谱到"肉眼一定看得见"：允许到中位数的
    /// <see cref="MaxSingleFrameFactor"/> 倍，再多就不只是噪声了。
    /// </summary>
    /// <param name="p50Ms">帧间隔中位数（毫秒）。</param>
    /// <param name="worstMs">最慢一帧（毫秒）。</param>
    public static bool IsWorstFrameAcceptable(double p50Ms, double worstMs)
    {
        if (p50Ms <= 0)
        {
            return false;
        }

        return worstMs <= p50Ms * MaxSingleFrameFactor;
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

    /// <summary>开场淡入最少要有几帧。</summary>
    /// <remarks>
    /// 冷启动时第一帧到第二帧之间可能隔 170 ms 以上（首次布局 + 字体光栅化 + 首次出图），
    /// 如果进度按真实时间走，第二帧就已经 progress=1，淡出变成"闪一下"。
    /// 所以单帧进度不许超过 1/5：帧慢了就把动画拉长，而不是一次跳到终点。
    /// </remarks>
    public const int MinIntroFrames = 5;

    /// <summary>单帧最多推进多少进度（= 1 / <see cref="MinIntroFrames"/>）。</summary>
    public const double IntroMaxProgressStep = 1.0 / MinIntroFrames;

    /// <summary>
    /// 把这一帧的原始进度夹到「上一帧 + 单帧步进上限」以内，保证至少走
    /// <see cref="MinIntroFrames"/> 帧、且不会倒退。
    /// </summary>
    /// <param name="previous">上一帧已经播到的进度（0~1）。</param>
    /// <param name="raw">按真实时间算出来的进度（0~1）。</param>
    public static double ClampIntroProgress(double previous, double raw)
    {
        var floor = Math.Clamp(previous, 0, 1);
        var target = Math.Clamp(raw, 0, 1);

        if (target <= floor)
        {
            return floor;
        }

        return Math.Min(target, floor + IntroMaxProgressStep);
    }
}