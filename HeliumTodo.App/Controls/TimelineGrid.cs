using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HeliumTodo.App.Controls;

/// <summary>
/// 时间轴的背景网格。
///
/// 为什么用「自己画」而不是摆一堆 Line 控件？
///   24 小时刻度 + 48 条半小时线 = 70 多个控件，既慢又难调。
///   直接重写 <see cref="Render"/> 一次性画出来，又快又干净 ——
///   这也是自定义控件最典型的用法。
///
/// 画三样东西：
///   ① 整点线（深一点）+ 每 3 小时加粗
///   ② 半点线（很淡）
///   ③ 「现在」的红色横线
/// </summary>
public sealed class TimelineGrid : Control
{
    // ───────────── 可以绑定的属性 ─────────────

    /// <summary>一小时的像素高度。</summary>
    public static readonly StyledProperty<double> HourHeightProperty =
        AvaloniaProperty.Register<TimelineGrid, double>(nameof(HourHeight), 38.0);

    public double HourHeight
    {
        get => GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, value);
    }

    /// <summary>一天从几点开始画（默认 0 = 完整 24 小时）。</summary>
    public static readonly StyledProperty<int> StartHourProperty =
        AvaloniaProperty.Register<TimelineGrid, int>(nameof(StartHour), 0);

    public int StartHour
    {
        get => GetValue(StartHourProperty);
        set => SetValue(StartHourProperty, value);
    }

    /// <summary>现在几点（用来画红线）。</summary>
    public static readonly StyledProperty<TimeSpan> NowProperty =
        AvaloniaProperty.Register<TimelineGrid, TimeSpan>(nameof(Now));

    public TimeSpan Now
    {
        get => GetValue(NowProperty);
        set => SetValue(NowProperty, value);
    }

    /// <summary>是否显示「现在」的红线（看别的日期时应该关掉）。</summary>
    public static readonly StyledProperty<bool> ShowNowLineProperty =
        AvaloniaProperty.Register<TimelineGrid, bool>(nameof(ShowNowLine), true);

    public bool ShowNowLine
    {
        get => GetValue(ShowNowLineProperty);
        set => SetValue(ShowNowLineProperty, value);
    }

    /// <summary>
    /// 是否在左侧留一条空白并画出整点刻度文字（00:00 / 02:00 …）。
    ///
    /// 为什么刻度也在这里画，而不是用一堆 TextBlock 去摆？
    ///   试过两种摆法都不行：放进 ItemsControl + Canvas 时，
    ///   Canvas 拿不到正确的高度/宽度，刻度要么被裁掉、要么全叠在同一行。
    ///   而这个控件本来就在画网格线，顺手把刻度文字也画出来最稳 ——
    ///   一次绘制调用搞定，还比摆十几个控件快。
    /// </summary>
    public static readonly StyledProperty<bool> ShowHourLabelsProperty =
        AvaloniaProperty.Register<TimelineGrid, bool>(nameof(ShowHourLabels), true);

    public bool ShowHourLabels
    {
        get => GetValue(ShowHourLabelsProperty);
        set => SetValue(ShowHourLabelsProperty, value);
    }

    /// <summary>刻度文字占用的左侧宽度（像素）。</summary>
    /// <summary>
    /// 上下各留出的空白（像素）。
    ///
    /// 为什么要留：整点刻度文字是"以刻度线为中心垂直居中"的，
    /// 所以 00:00 的文字一半在 y=0 上面、24:00 的一半在底部外面 ——
    /// 渲染出来就是「0 点顶格、24 点触底」，两头都被切掉。
    /// 把 24 小时的绘制区间整体往下挪 14 像素，并让容器相应加高（见
    /// DashboardViewModel.TimelineHeight），两头就都有完整的呼吸空间了。
    /// </summary>
    public static readonly StyledProperty<double> VerticalPaddingProperty =
        AvaloniaProperty.Register<TimelineGrid, double>(nameof(VerticalPadding), 14.0);

    public double VerticalPadding
    {
        get => GetValue(VerticalPaddingProperty);
        set => SetValue(VerticalPaddingProperty, value);
    }

    public static readonly StyledProperty<double> LabelMarginProperty =
        AvaloniaProperty.Register<TimelineGrid, double>(nameof(LabelMargin), 46.0);

    public double LabelMargin
    {
        get => GetValue(LabelMarginProperty);
        set => SetValue(LabelMarginProperty, value);
    }

    /// <summary>每几小时画一个刻度文字（默认 2 小时，够用又不挤）。</summary>
    public static readonly StyledProperty<int> LabelStepHoursProperty =
        AvaloniaProperty.Register<TimelineGrid, int>(nameof(LabelStepHours), 2);

    public int LabelStepHours
    {
        get => GetValue(LabelStepHoursProperty);
        set => SetValue(LabelStepHoursProperty, value);
    }

    // 属性变化要重画
    static TimelineGrid()
    {
        AffectsRender<TimelineGrid>(
            HourHeightProperty,
            StartHourProperty,
            NowProperty,
            ShowNowLineProperty,
            ShowHourLabelsProperty,
            LabelMarginProperty,
            VerticalPaddingProperty,
            LabelStepHoursProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        // 有刻度时，网格线从刻度右边开始画；没刻度就从最左边开始
        var gridLeft = ShowHourLabels ? LabelMargin : 0.0;

        var majorPen = MakePen("TimelineAxisBrush", 0.06, 1.4);   // 每 3 小时
        var hourPen = MakePen("TimelineGridBrush", 0.07, 1.0);    // 整点
        var halfPen = MakePen("TimelineHalfHourBrush", 0.04, 1.0); // 半点

        // 00:00 所在的 y 坐标：先让出 VerticalPadding，再扣掉起点小时
        var top = VerticalPadding - StartHour * HourHeight;

        // ── ① 整点线 + ② 半点线 ──
        for (int hour = 0; hour <= 24; hour++)
        {
            var y = top + hour * HourHeight;
            if (y < -1 || y > height + 1) continue;

            var isMajor = hour % 3 == 0;
            var pen = isMajor ? majorPen : hourPen;
            context.DrawLine(pen, new Point(gridLeft, y), new Point(width, y));

            // 半点线（24:00 之后就不用画了）
            if (hour < 24)
            {
                var halfY = y + HourHeight / 2;
                if (halfY >= 0 && halfY <= height)
                {
                    context.DrawLine(halfPen, new Point(gridLeft, halfY), new Point(width, halfY));
                }
            }
        }

        // ── ③ 整点刻度文字 ──
        if (ShowHourLabels)
        {
            DrawHourLabels(context, top, height);
        }

        // ── ④ 「现在」的红线 ──
        // 注意起点：不用 gridLeft，而是再往右让开 LabelMargin 的距离。
        // 因为红线的左端会画一个小圆点，如果从 gridLeft 开始，圆点就会压在
        // 16:00、20:00 这些刻度文字上（渲染出来确实糊成一团）。
        if (ShowNowLine && Now >= TimeSpan.Zero && Now <= TimeSpan.FromHours(24))
        {
            var y = top + Now.TotalHours * HourHeight;
            if (y >= 0 && y <= height)
            {
                var nowBrush = FindBrush("TimelineNowBrush", Colors.OrangeRed);
                var nowPen = new Pen(nowBrush, 2);

                var lineLeft = ShowHourLabels ? gridLeft + LabelMargin : gridLeft;

                context.DrawLine(nowPen, new Point(lineLeft, y), new Point(width, y));

                // 左端画一个小圆点，更像常见日历应用的样子
                context.DrawEllipse(nowBrush, null, new Point(lineLeft + 4, y), 4, 4);
            }
        }
    }

    /// <summary>
    /// 画左侧的整点刻度文字（00:00 / 02:00 …）。
    ///
    /// 文字右对齐排在刻度条的右边缘，垂直方向以刻度线为中心上下居中。
    /// 每个文字后面垫一块与背景同色的底，这样即使「现在」的红线正好
    /// 从刻度上穿过，文字也还是看得清（不然会被红线划掉一半）。
    /// </summary>
    private void DrawHourLabels(DrawingContext context, double top, double height)
    {
        var step = Math.Max(1, LabelStepHours);
        var brush = FindBrush("TextFillColorSecondaryBrush", Color.FromArgb(150, 0, 0, 0));
        var plate = FindBrush("TimelineSurfaceBrush", Colors.White);

        var typeface = new Typeface("Inter, Microsoft YaHei UI, Segoe UI");

        for (int hour = 0; hour <= 24; hour += step)
        {
            var y = top + hour * HourHeight;

            // 画在可视范围之外的直接跳过，省点力气
            if (y < -12 || y > height + 12) continue;

            var text = new FormattedText(
                $"{hour:D2}:00",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                11,
                brush);

            // 右对齐到刻度条右边缘，并留 6 像素间距；垂直居中到刻度线上
            var x = LabelMargin - text.Width - 6;
            var yTop = y - text.Height / 2;

            // 垫底：比文字四周各多 2 像素
            context.FillRectangle(plate, new Rect(
                x - 2, yTop, text.Width + 4, text.Height));

            context.DrawText(text, new Point(x, yTop));
        }
    }

    /// <summary>造一支画笔；找不到资源就用灰色兜底。</summary>
    private static Pen MakePen(string resourceKey, double fallbackOpacity, double thickness)
    {
        var brush = FindBrush(resourceKey, Color.FromArgb(
            (byte)(fallbackOpacity * 255), 0, 0, 0));
        return new Pen(brush, thickness);
    }

    /// <summary>从应用资源里找画刷。</summary>
    private static IBrush FindBrush(string resourceKey, Color fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var value) == true
            && value is IBrush brush)
        {
            return brush;
        }
        return new SolidColorBrush(fallback);
    }
}
