namespace HeliumTodo.Core.Notifications;

/// <summary>
/// 界面右上/顶部那种"改好了"的提示条，该显示多久。
///
/// 【为什么要写进 Core 而不是界面里写个 3000？】
///   这个时长有两个地方要一致：真正排进 <c>DispatcherTimer</c> 的间隔，
///   和自检里判断"过了这么久应该自己消失"的阈值。
///   散在两处，改一处忘了另一处，自检就会开始骗人。
///
/// 【为什么不是"点一下关掉"就够了？】
///   设置页的提示条以前会一直挂着，用户改完一个选项去改下一个，
///   上次的提示还压在卡片上，看着像一直没保存成功。
///   3 秒足够看清楚，又不至于挡住下面的内容。
/// </summary>
public static class NoticePolicy
{
    /// <summary>提示条默认显示多久（毫秒）。</summary>
    public const int DisplayMilliseconds = 3000;

    /// <summary>
    /// 已经显示了 <paramref name="visibleMilliseconds"/> 毫秒，该不该自动收起来。
    /// </summary>
    public static bool ShouldExpire(double visibleMilliseconds) =>
        visibleMilliseconds >= DisplayMilliseconds;
}
