using Avalonia;

namespace HeliumTodo.App.Windowing;

/// <summary>
/// 窗口动画的"算术部分"：时长、缓动、进度、矩形插值。
///
/// 为什么单独拿出来：
///   这些东西是纯函数 —— 给定输入必得同一个输出，不需要窗口、不需要定时器。
///   而"真正播动画"那部分必须挂在窗口和定时器上，在没有图形环境的地方（自检、
///   单元测试）根本跑不动。把两者分开之后，至少有一半是可以被断言钉住的：
///   缓动端点对不对、进度是不是按真实时间算、矩形插到一半是不是正好在中间。
///   剩下那一半（帧有没有按时到）只能在真实窗口里看，交给探针脚本。
/// </summary>
public static class WindowAnimationMath
{
    /// <summary>动画总时长（毫秒）。参考 Windows 自带的大约 150~200ms，取中间偏慢一点点。</summary>
    public const double DurationMs = 180;

    /// <summary>切成几帧。16 帧 / 180ms ≈ 11ms 一帧，肉眼是连续的。</summary>
    public const int Frames = 16;

    /// <summary>打开动画的起始缩放。0.96 比 0.9 更"稳"，不会像弹窗那样夸张。</summary>
    public const double IntroStartScale = 0.96;

    /// <summary>动画总时长。</summary>
    public static TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);

    /// <summary>每帧间隔。</summary>
    public static TimeSpan FrameInterval => TimeSpan.FromMilliseconds(DurationMs / Frames);

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
    ///   加载配置、JIT），定时器可能迟到两三倍 —— 数帧的话这段动画就被拖成 400ms，
    ///   看着发飘；而按真实时间算，迟到的帧直接跳到它该在的位置，
    ///   动画总时长稳定在 180ms，只是中间少几帧（肉眼看不出）。
    /// </summary>
    public static double ProgressOf(TimeSpan elapsed)
        => Math.Clamp(elapsed.TotalMilliseconds / DurationMs, 0, 1);

    /// <summary>
    /// 两个矩形之间按比例插值。内部用物理像素算，取整只影响 1px，
    /// 但不取整的话某些帧会出现半像素抖动。
    /// </summary>
    public static PixelRect Interpolate(PixelRect from, PixelRect to, double t)
    {
        static int Lerp(int a, int b, double k) => (int)Math.Round(a + (b - a) * k);

        return new PixelRect(
            Lerp(from.X, to.X, t),
            Lerp(from.Y, to.Y, t),
            Math.Max(1, Lerp(from.Width, to.Width, t)),
            Math.Max(1, Lerp(from.Height, to.Height, t)));
    }
}