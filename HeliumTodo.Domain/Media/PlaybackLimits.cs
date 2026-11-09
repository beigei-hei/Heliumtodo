// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Media;

/// <summary>
/// 动态壁纸的播放限制。
///
/// 为什么单独放一个类？
///   这个数字有两个地方要用：
///     · 解码端（从视频里挑帧时按最小间隔跳过）
///     · 播放端（定时器排下一帧时夹住间隔）
///   两边各写一个 33 迟早会写歪，而且"30 帧/秒"是个明确的用户需求
///   （「动图支持mp4并在实际渲染时将帧率锁定在30帧以下」），
///   放在 Core 里就能被测到，不用起界面。
/// </summary>
public static class PlaybackLimits
{
    /// <summary>壁纸播放的帧率上限：30 帧/秒。</summary>
    public const int MaxFramesPerSecond = 30;

    /// <summary>
    /// 对应的最小帧间隔（毫秒）。
    ///
    /// 1000 / 30 = 33.33…，这里向上取到 34：
    /// 取 33 的话算出来是 30.3 帧/秒，就又超了。
    /// </summary>
    public const int MinFrameIntervalMs = 34;

    /// <summary>最长的单帧停留时间（毫秒）：10 秒。再长就像卡死了。</summary>
    public const int MaxFrameIntervalMs = 10_000;

    /// <summary>延迟写 0 或负数时的兜底值（毫秒）。</summary>
    public const int FallbackFrameIntervalMs = 100;

    /// <summary>
    /// 把一帧的停留时间夹进合法区间。
    ///
    /// 三种情况都要管：
    ///   · 0 / 负数（有些 GIF 真这么写）→ 用兜底值，不能疯狂刷新
    ///   · 小于 34 ms → 抬到 34 ms，这就是"30 帧/秒上限"的落地点
    ///   · 大于 10 秒 → 压到 10 秒
    /// </summary>
    public static int ClampFrameDelay(int milliseconds)
    {
        if (milliseconds <= 0) return FallbackFrameIntervalMs;
        if (milliseconds < MinFrameIntervalMs) return MinFrameIntervalMs;
        if (milliseconds > MaxFrameIntervalMs) return MaxFrameIntervalMs;
        return milliseconds;
    }
}