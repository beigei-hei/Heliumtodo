using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace HeliumTodo.App.Controls;

/// <summary>
/// 钟表盘面：一圈刻度 + 12 个数字 + 一根指针。
///
/// 指针的角度不是「现在几点」，而是「专注还剩多少」——
/// 12 点方向 = 刚开始，顺时针转满一圈 = 时间到。
/// 这样配合中间的倒计时数字，一眼就能看出还剩多少。
/// </summary>
public sealed class ClockFace : Control
{
    /// <summary>进度 0.0–1.0，控制指针转到哪。</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ClockFace, double>(nameof(Progress));

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>是否空闲（空闲时指针指回 12 点，颜色也变淡）。</summary>
    public static readonly StyledProperty<bool> IsIdleProperty =
        AvaloniaProperty.Register<ClockFace, bool>(nameof(IsIdle), true);

    public bool IsIdle
    {
        get => GetValue(IsIdleProperty);
        set => SetValue(IsIdleProperty, value);
    }

    /// <summary>是否暂停（暂停时指针变灰）。</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<ClockFace, bool>(nameof(IsPaused));

    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    static ClockFace()
    {
        AffectsRender<ClockFace>(ProgressProperty, IsIdleProperty, IsPausedProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 8) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2 - 2;

        // ── 表盘外圈 ──
        var ringBrush = Resolve("CardStrokeColorDefaultBrush", Color.FromArgb(60, 0, 0, 0));
        context.DrawEllipse(null, new Pen(ringBrush, 1), center, radius, radius);

        // ── 60 条分钟刻度（每 5 条加粗，就是小时刻度）──
        var tickBrush = Resolve("TextFillColorSecondaryBrush", Color.FromArgb(120, 0, 0, 0));
        for (int i = 0; i < 60; i++)
        {
            var isHour = i % 5 == 0;
            var angle = i * 6.0 * Math.PI / 180.0;

            // 12 点方向是 -90 度，所以这里减掉 π/2
            var outer = PointOnCircle(center, radius - 3, angle - Math.PI / 2);
            var inner = PointOnCircle(center, radius - (isHour ? 12 : 7), angle - Math.PI / 2);

            context.DrawLine(
                new Pen(tickBrush, isHour ? 1.8 : 0.8),
                inner, outer);
        }

        // ── 表盘数字：只画 12 和 6 ──
        //
        // 为什么 3 和 9 不画？
        //   表盘直径只有 272 像素，中间还要塞下倒计时大字。
        //   3 点和 9 点正处在中间大字的两侧，画上去必然和数字压在一起
        //   （渲染出来确实糊成一团）。而这两个方向的粗刻度已经能表达位置了，
        //   所以干脆不画 —— 这也是很多极简表盘的做法。
        var numberBrush = Resolve("TextFillColorPrimaryBrush", Colors.Black);
        var numberRadius = radius - 16;

        DrawNumber(context, "12", center, numberRadius, -Math.PI / 2, numberBrush);
        DrawNumber(context, "6", center, numberRadius, Math.PI / 2, numberBrush);

        // ── 指针 ──
        // 空闲 → 很淡；暂停 → 灰色；专注中 → 强调色
        var handBrush = IsIdle
            ? Resolve("TextFillColorDisabledBrush", Color.FromArgb(80, 0, 0, 0))
            : IsPaused
                ? Resolve("TextFillColorSecondaryBrush", Color.FromArgb(150, 0, 0, 0))
                : Resolve("SystemAccentColor", Color.Parse("#0F6CBD"));

        var progress = Math.Clamp(Progress, 0, 1);
        var handAngle = progress * 2 * Math.PI - Math.PI / 2;

        // 指针只画到数字内侧一点，太长会盖住刻度数字
        var handEnd = PointOnCircle(center, radius - 52, handAngle);
        context.DrawLine(new Pen(handBrush, 3.5, lineCap: PenLineCap.Round), center, handEnd);

        // 指针尾部的配重（让指针看起来更像真的）
        var tail = PointOnCircle(center, 12, handAngle + Math.PI);
        context.DrawLine(new Pen(handBrush, 3.5, lineCap: PenLineCap.Round), center, tail);

        // 中心轴
        context.DrawEllipse(handBrush, null, center, 5, 5);
    }

    /// <summary>在表盘上写一个数字。</summary>
    private static void DrawNumber(
        DrawingContext context, string text, Point center, double distance, double angle, IBrush brush)
    {
        var position = PointOnCircle(center, distance, angle);

        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI"),
            13,
            brush);

        context.DrawText(formatted, new Point(
            position.X - formatted.Width / 2,
            position.Y - formatted.Height / 2));
    }

    /// <summary>算出圆上某角度对应的点。</summary>
    private static Point PointOnCircle(Point center, double radius, double angle) =>
        new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));

    /// <summary>从应用资源里取颜色，取不到就用兜底色。</summary>
    private static IBrush Resolve(string resourceKey, Color fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var value) == true)
        {
            if (value is IBrush brush) return brush;
            if (value is Color color) return new ImmutableSolidColorBrush(color);
        }
        return new ImmutableSolidColorBrush(fallback);
    }
}

/// <summary>
/// 圆环进度条。
///
/// 从 12 点方向开始，顺时针画一段弧，弧长 = 进度。
/// 用法：<c>&lt;ctrl:ProgressRing Value="0.4" /&gt;</c>
/// </summary>
public sealed class ProgressRing : Control
{
    /// <summary>进度 0.0–1.0。</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(Value));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>环的粗细。</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(Thickness), 6.0);

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    static ProgressRing()
    {
        AffectsRender<ProgressRing>(ValueProperty, ThicknessProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 8) return;

        var thickness = Thickness;
        var radius = size / 2 - thickness / 2 - 1;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        // 底环（浅色轨道）
        var trackBrush = FindBrush("ControlStrokeColorDefaultBrush",
            new ImmutableSolidColorBrush(Color.FromArgb(40, 0, 0, 0)));
        context.DrawEllipse(null, new Pen(trackBrush, thickness), center, radius, radius);

        // 进度弧
        var progress = Math.Clamp(Value, 0, 1);
        if (progress <= 0.0001) return;

        var accentBrush = FindBrush("SystemAccentColor",
            new ImmutableSolidColorBrush(Color.Parse("#0F6CBD")));

        var startAngle = -Math.PI / 2;                       // 12 点方向
        var sweepAngle = progress * 2 * Math.PI;             // 顺时针扫过的角度
        var geometry = BuildArc(center, radius, startAngle, sweepAngle);

        context.DrawGeometry(null, new Pen(accentBrush, thickness, lineCap: PenLineCap.Round), geometry);
    }

    /// <summary>
    /// 用折线近似圆弧。
    ///
    /// 为什么不用 ArcSegment？
    ///   圆弧大于 180° 时 ArcSegment 的 IsLargeArc 要特别处理，容易出错；
    ///   直接用很多小线段拼，代码简单而且绝对不会画错。
    ///   每 3° 一段，肉眼看已经是平滑的圆了。
    /// </summary>
    private static StreamGeometry BuildArc(Point center, double radius, double startAngle, double sweep)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            var steps = Math.Max(8, (int)(Math.Abs(sweep) / (Math.PI / 60)));
            var points = new List<Point>(steps + 1);

            for (int i = 0; i <= steps; i++)
            {
                var angle = startAngle + sweep * i / steps;
                points.Add(new Point(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)));
            }

            ctx.BeginFigure(points[0], isFilled: false);
            for (int i = 1; i < points.Count; i++)
            {
                ctx.LineTo(points[i]);
            }
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static IBrush FindBrush(string resourceKey, IBrush fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var value) == true)
        {
            if (value is IBrush brush) return brush;
            if (value is Color color) return new ImmutableSolidColorBrush(color);
        }
        return fallback;
    }
}
