namespace HeliumTodo.App.ViewModels;

/// <summary>
/// 时间轴的横向尺寸常量。
///
/// 为什么单独抽一个类？
///   网格线（<see cref="Controls.TimelineGrid"/>）和方块（<see cref="TimelineEntryViewModel"/>）
///   是两个各画各的东西：前者自己画刻度文字，后者由 ViewModel 算 Canvas.Left。
///   如果两边各写一个数字，改了一边忘了另一边就会出现"方块压在刻度文字上"。
///   所以把数字集中到这里，XAML 用 <c>{x:Static}</c> 绑、代码直接引用，
///   想调宽度只改一处。
/// </summary>
public static class TimelineMetrics
{
    /// <summary>左侧整点刻度文字占的宽度（像素）。</summary>
    public const double LabelWidth = 46.0;

    /// <summary>
    /// 刻度文字和方块之间再留的一点空隙。
    ///
    /// 不留的话，方块左边框会正好贴着"14:00"这几个字的右边缘，
    /// 视觉上像被文字顶住，读起来很挤。
    /// </summary>
    public const double AxisGap = 8.0;

    /// <summary>方块从这个 x 开始排（= 刻度宽度 + 空隙）。</summary>
    public const double LeftInset = LabelWidth + AxisGap;
}
