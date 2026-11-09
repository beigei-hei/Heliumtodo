// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace HeliumTodo.Desktop.Controls;

/// <summary>
/// 一条可以拖动的渐变条 —— 色相条和 R/G/B 三条滑块都是它。
///
/// 为什么不直接用 Avalonia 自带的 Slider？
///   因为 Slider 的轨道是一套内置样式，想让它显示"从红到蓝的渐变"得整个重写模板，
///   比自己画一条还麻烦。这里只要画一个圆角矩形加一个圆点，二十行就够了。
/// </summary>
public class GradientSlider : Control
{
    /// <summary>轨道上要画的渐变（由调用方按当前颜色算好）。</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<GradientSlider, IBrush?>(nameof(TrackBrush));

    /// <summary>
    /// 当前值，范围 0~1（0 在最左边，1 在最右边）。
    /// <para>
    /// ⚠️ 必须声明 <c>BindingMode.TwoWay</c>：自定义控件的属性默认是单向绑定，
    /// 只从 ViewModel 流到控件。少了这个，拖动只会改控件自己的值，ViewModel 一无所知 ——
    /// 表现就是「拖了滑块，上面的方块和右边的数字都不跟着动，颜色也不生效」。
    /// </para>
    /// </summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<GradientSlider, double>(
            nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>拖动时才会变成 true，用来区分"自己改的"和"外面改的"。</summary>
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<GradientSlider, bool>(nameof(IsDragging));

    private const double TrackHeight = 14;
    private const double ThumbRadius = 7;

    static GradientSlider()
    {
        // 这三个属性任何一个变了都要重画
        AffectsRender<GradientSlider>(TrackBrushProperty, ValueProperty);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsDragging
    {
        get => GetValue(IsDraggingProperty);
        set => SetValue(IsDraggingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width;
        return new Size(width, TrackHeight + ThumbRadius);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var centerY = h / 2;
        var trackRect = new Rect(ThumbRadius, centerY - TrackHeight / 2, Math.Max(1, w - ThumbRadius * 2), TrackHeight);

        // 轨道
        context.DrawRectangle(TrackBrush ?? Brushes.Gray, null, trackRect, TrackHeight / 2, TrackHeight / 2);

        // 拇指：白底 + 半透明深色描边，放在任何颜色上都看得清
        var ratio = Math.Clamp(Value, 0, 1);
        var thumbX = ThumbRadius + ratio * Math.Max(0, w - ThumbRadius * 2);
        var thumbCenter = new Point(thumbX, centerY);

        context.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 1.5), thumbCenter, ThumbRadius, ThumbRadius);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        IsDragging = true;
        e.Pointer.Capture(this);
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!IsDragging) return;

        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!IsDragging) return;

        IsDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void UpdateFromPoint(Point p)
    {
        var usable = Math.Max(1, Bounds.Width - ThumbRadius * 2);
        var ratio = Math.Clamp((p.X - ThumbRadius) / usable, 0, 1);

        // 只在真的变了的时候赋值，避免拖一下发几百个通知
        if (Math.Abs(ratio - Value) < 0.0005) return;

        Value = ratio;
    }
}

/// <summary>
/// 方形选色：横轴是饱和度，纵轴是明度，底色是当前色相。
///
/// 这是"方形选色"那一半，另一半是 <see cref="GradientSlider"/> 做的色相条。
/// </summary>
public class SaturationValueSquare : Control
{
    /// <summary>
    /// 色相，0~360。
    /// <para>⚠️ 三个属性都要 <c>BindingMode.TwoWay</c>，理由同 <see cref="GradientSlider.ValueProperty"/>：
    /// 自定义控件属性默认单向，拖动不会回写 ViewModel。</para>
    /// </summary>
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<SaturationValueSquare, double>(
            nameof(Hue), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>饱和度，0~1。对应横轴位置。</summary>
    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<SaturationValueSquare, double>(
            nameof(Saturation), defaultValue: 1.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>明度，0~1。对应纵轴位置（1 在最上面）。</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SaturationValueSquare, double>(
            nameof(Value), defaultValue: 1.0, defaultBindingMode: BindingMode.TwoWay);

    private const double ThumbRadius = 7;

    /// <summary>鼠标是不是正按在这个方块上（决定要不要跟着 PointerMoved 更新）。</summary>
    private bool _isDragging;

    static SaturationValueSquare()
    {
        AffectsRender<SaturationValueSquare>(HueProperty, SaturationProperty, ValueProperty);
    }

    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // 做成正方形，边长取可用的较小值
        var side = double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width;
        if (!double.IsInfinity(availableSize.Height))
        {
            side = Math.Min(side, availableSize.Height);
        }

        side = Math.Clamp(side, 80, 200);
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var rect = new Rect(0, 0, w, h);
        var corner = 6.0;

        // ① 底色：当前色相的纯色
        var baseColor = HsvToColor(Hue, 1, 1);
        context.DrawRectangle(new SolidColorBrush(baseColor), null, rect, corner, corner);

        // ② 横向：白 → 透明（越靠左饱和度越低）
        var whiteFade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        whiteFade.GradientStops.Add(new GradientStop(Colors.White, 0));
        whiteFade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));

        // ③ 纵向：透明 → 黑（越靠下明度越低）
        var blackFade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        };
        blackFade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
        blackFade.GradientStops.Add(new GradientStop(Colors.Black, 1));

        using (context.PushClip(new RoundedRect(rect, corner)))
        {
            context.DrawRectangle(whiteFade, null, rect);
            context.DrawRectangle(blackFade, null, rect);
        }

        // ④ 拇指
        var x = Math.Clamp(Saturation, 0, 1) * w;
        var y = (1 - Math.Clamp(Value, 0, 1)) * h;
        var center = new Point(
            Math.Clamp(x, ThumbRadius, Math.Max(ThumbRadius, w - ThumbRadius)),
            Math.Clamp(y, ThumbRadius, Math.Max(ThumbRadius, h - ThumbRadius)));

        context.DrawEllipse(null, new Pen(Brushes.White, 2), center, ThumbRadius, ThumbRadius);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1), center, ThumbRadius + 1.5, ThumbRadius + 1.5);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _isDragging = true;
        e.Pointer.Capture(this);
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging) return;

        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDragging) return;

        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void UpdateFromPoint(Point p)
    {
        var w = Math.Max(1, Bounds.Width);
        var h = Math.Max(1, Bounds.Height);

        var s = Math.Clamp(p.X / w, 0, 1);
        var v = Math.Clamp(1 - p.Y / h, 0, 1);

        // 同样只在真的变了时才赋值
        if (Math.Abs(s - Saturation) > 0.0005) Saturation = s;
        if (Math.Abs(v - Value) > 0.0005) Value = v;
    }

    /// <summary>HSV → 颜色。色相用度，其余 0~1。</summary>
    private static Color HsvToColor(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var c = value * saturation;
        var hPrime = hue / 60.0;
        var x = c * (1 - Math.Abs(hPrime % 2 - 1));
        var m = value - c;

        (double r, double g, double b) = (int)Math.Floor(hPrime) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
