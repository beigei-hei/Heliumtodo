// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Desktop.Windowing;

/// <summary>
/// 窗口动画的"算术部分"：时长、缓动、进度。
///
/// 为什么单独拿出来：
///   这些东西是纯函数 —— 给定输入必得同一个输出，不需要窗口、不需要定时器。
///   而"真正播动画"那部分必须挂在窗口和合成器上，在没有图形环境的地方（自检、
///   单元测试）根本跑不动。把两者分开之后，算术部分就可以被断言钉住：
///   缓动端点对不对、进度是不是按真实时间算。
///   剩下那一半（帧有没有按时到）只能在真实窗口里看，交给 tools/probe-intro.ps1。
///
/// 这里原本还有"矩形插值"和"16 帧"两样东西，是给已删除的"逐帧改窗口矩形"版
/// 最大化动画用的。现在最大化交给 DWM（见 WindowChrome），它们没有用处就一起删了 ——
/// 留着会让人以为窗口尺寸还是我们在逐帧改。
/// </summary>
public static class WindowAnimationMath
{
    /// <summary>动画总时长（毫秒）。参考 Windows 自带的大约 150~200ms，取中间偏慢一点点。</summary>
    public const double DurationMs = 180;

    /// <summary>动画总时长。</summary>
    public static TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);

    /// <summary>
    /// 缓动：ease-out cubic。起步快、收尾慢 —— 和系统动画的手感一致。
    /// 线性插值会让窗口"匀速撞到"屏幕边缘，看着很硬。
    /// </summary>
    public static double EaseOut(double t)
    {
        var clamped = Math.Clamp(t, 0, 1);
        return 1 - Math.Pow(1 - clamped, 3);
    }

    /// <summary>
    /// 已经过去多久 → 动画进度（0~1）。
    ///
    /// 为什么按时间算、而不是"数帧"：
    ///   数帧的写法默认"每帧都会按时到"。可窗口刚起来那一刻机器正忙（解码壁纸、
    ///   加载配置、JIT），帧可能迟到两三倍 —— 数帧的话这段动画就被拖成 400ms，
    ///   看着发飘；而按真实时间算，迟到的帧直接跳到它该在的位置。
    ///
    ///   但"按真实时间算"也有它的坑：冷启动第一帧到第二帧可能隔 176 ms（实测），
    ///   一帧就把 180 ms 走完，淡出变成闪一下。所以真正驱动帧的地方还要用
    ///   WindowTransitionPolicy.ClampIntroProgress 夹住单帧步进。
    /// </summary>
    public static double ProgressOf(TimeSpan elapsed)
        => Math.Clamp(elapsed.TotalMilliseconds / DurationMs, 0, 1);
}