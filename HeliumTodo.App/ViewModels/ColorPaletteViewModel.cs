using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HeliumTodo.App.ViewModels;

/// <summary>
/// 自定义调色板的 ViewModel：让「RGB 数值」「HSV 方块」「十六进制文本」三套表示始终一致。
///
/// 为什么不把这些逻辑直接写在 XAML 的代码后置里？
///   因为三方同步里有几个坑（下面注释标了），集中在一个类里更好维护、也更好测。
///
/// 三套表示的关系：
///   · RGB 和 HSV 描述的是同一个颜色，只是坐标不同
///   · 十六进制是 RGB 的文本形式
///   所以任意一套变了，另外两套都要跟着重算 —— 这就是「同步」。
/// </summary>
public sealed partial class ColorPaletteViewModel : ObservableObject
{
    /// <summary>
    /// 防递归闸门。
    ///
    /// 为什么不加这个会栈溢出？
    ///   改 R → 重算 HSV → 触发 HSV 的 Changed → 又去重算 RGB → 又触发 R 的 Changed → …
    ///   设一个"我正在同步中"的标志，同步期间不再反向触发，环就断了。
    /// </summary>
    private bool _syncing;

    // ── RGB 三个通道 ──

    [ObservableProperty]
    private byte _red = 0x0F;

    [ObservableProperty]
    private byte _green = 0x6C;

    [ObservableProperty]
    private byte _blue = 0xBD;

    // ── HSV（方形选色用的坐标）──

    /// <summary>色相 0~360。</summary>
    [ObservableProperty]
    private double _hue;

    /// <summary>饱和度 0~1（方形横轴）。</summary>
    [ObservableProperty]
    private double _saturation = 1;

    /// <summary>明度 0~1（方形纵轴）。</summary>
    [ObservableProperty]
    private double _value = 1;

    // ── 十六进制文本 ──

    [ObservableProperty]
    private string _hexText = "#0F6CBD";

    /// <summary>颜色变了（界面拿去当强调色）。</summary>
    public event EventHandler<Color>? ColorChanged;

    /// <summary>当前颜色。</summary>
    public Color Color => Color.FromRgb(Red, Green, Blue);

    /// <summary>预览方块用的画刷。</summary>
    public IBrush PreviewBrush => new SolidColorBrush(Color);

    // ── 给界面控件用的「换算桥」 ──
    //
    // 为什么需要这几个属性？
    //   自绘的滑块控件只知道「0~1 的比例」，不知道色相是 0~360、也不是 0~255 的字节。
    //   与其在 XAML 里挂一堆转换器，不如在这里直接开几个属性把单位换好 ——
    //   哪边改了都能落到真正的字段上，界面写起来也干净。

    /// <summary>色相条滑块的位置（0~1，对应 0~360 度）。</summary>
    public double HueRatio
    {
        get => Hue / 360.0;
        set => Hue = Math.Clamp(value, 0, 1) * 360.0;
    }

    /// <summary>红色滑块的位置（0~1，对应 0~255）。</summary>
    public double RedRatio
    {
        get => Red / 255.0;
        set => Red = RatioToByte(value);
    }

    /// <summary>绿色滑块的位置。</summary>
    public double GreenRatio
    {
        get => Green / 255.0;
        set => Green = RatioToByte(value);
    }

    /// <summary>蓝色滑块的位置。</summary>
    public double BlueRatio
    {
        get => Blue / 255.0;
        set => Blue = RatioToByte(value);
    }

    /// <summary>R 的整数值（给数值框用）。</summary>
    public int RedInt
    {
        get => Red;
        set => Red = (byte)Math.Clamp(value, 0, 255);
    }

    /// <summary>G 的整数值。</summary>
    public int GreenInt
    {
        get => Green;
        set => Green = (byte)Math.Clamp(value, 0, 255);
    }

    /// <summary>B 的整数值。</summary>
    public int BlueInt
    {
        get => Blue;
        set => Blue = (byte)Math.Clamp(value, 0, 255);
    }

    private static byte RatioToByte(double ratio) =>
        (byte)Math.Round(Math.Clamp(ratio, 0, 1) * 255);

    // ── 几条渐变轨道的画刷（颜色变了要重算）──

    /// <summary>色相条：从红到红绕一圈，中间七个停靠点。</summary>
    public IBrush HueTrack
    {
        get
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0.5, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 0.5, Avalonia.RelativeUnit.Relative),
            };

            for (var i = 0; i <= 6; i++)
            {
                brush.GradientStops.Add(new GradientStop(HsvToColor(i * 60, 1, 1), i / 6.0));
            }

            return brush;
        }
    }

    /// <summary>红色滑块：从"绿蓝不变、红为 0"渐变到"红为 255"。</summary>
    public IBrush RedTrack => ChannelTrack(0);

    /// <summary>绿色滑块。</summary>
    public IBrush GreenTrack => ChannelTrack(1);

    /// <summary>蓝色滑块。</summary>
    public IBrush BlueTrack => ChannelTrack(2);

    /// <summary>做某一条通道的渐变轨道：两端只有该通道在变，另两条固定。</summary>
    private IBrush ChannelTrack(int channel)
    {
        Color At(byte v) => channel switch
        {
            0 => Color.FromRgb(v, Green, Blue),
            1 => Color.FromRgb(Red, v, Blue),
            _ => Color.FromRgb(Red, Green, v),
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0.5, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 0.5, Avalonia.RelativeUnit.Relative),
        };

        brush.GradientStops.Add(new GradientStop(At(0), 0));
        brush.GradientStops.Add(new GradientStop(At(255), 1));

        return brush;
    }

    // ── 同步 ──

    partial void OnRedChanged(byte value) => SyncFromRgb();

    partial void OnGreenChanged(byte value) => SyncFromRgb();

    partial void OnBlueChanged(byte value) => SyncFromRgb();

    partial void OnHueChanged(double value) => SyncFromHsv();

    partial void OnSaturationChanged(double value) => SyncFromHsv();

    partial void OnValueChanged(double value) => SyncFromHsv();

    partial void OnHexTextChanged(string value)
    {
        if (_syncing) return;

        if (!TryParseHex(value, out var color)) return;

        SetColorCore(color);
    }

    /// <summary>从外部塞一个颜色进来（例如切换来源、点预设色板）。</summary>
    public void SetColor(Color color)
    {
        SetColorCore(color);
    }

    /// <summary>把 RGB 当作源，重算 HSV 与十六进制。</summary>
    private void SyncFromRgb()
    {
        if (_syncing) return;

        _syncing = true;
        try
        {
            var color = Color;
            var (h, s, v) = RgbToHsv(color.R, color.G, color.B);

            // ⚠️ 坑：灰阶（饱和度 0）时色相是算不出来的，
            //    如果硬写成 0，色相条会突然跳到红色那端。
            //    所以这种情况下保留用户原来的色相，只更新饱和度与明度。
            if (s > 0.0001)
            {
                Hue = h;
            }

            Saturation = s;
            Value = v;

            HexText = ToHex(color);

            RaiseDerived();
            ColorChanged?.Invoke(this, color);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>把 HSV 当作源，重算 RGB 与十六进制。</summary>
    private void SyncFromHsv()
    {
        if (_syncing) return;

        _syncing = true;
        try
        {
            var color = HsvToColor(Hue, Saturation, Value);

            Red = color.R;
            Green = color.G;
            Blue = color.B;

            HexText = ToHex(color);

            RaiseDerived();
            ColorChanged?.Invoke(this, color);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>整体换色（RGB 与 HSV 都直接给，不经过互相推导，避免精度抖动）。</summary>
    private void SetColorCore(Color color)
    {
        _syncing = true;
        try
        {
            Red = color.R;
            Green = color.G;
            Blue = color.B;

            var (h, s, v) = RgbToHsv(color.R, color.G, color.B);
            if (s > 0.0001) Hue = h;
            Saturation = s;
            Value = v;

            HexText = ToHex(color);

            RaiseDerived();
            ColorChanged?.Invoke(this, color);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>通知界面：几个算出来的属性要重新读了。</summary>
    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(PreviewBrush));
        OnPropertyChanged(nameof(HueTrack));
        OnPropertyChanged(nameof(RedTrack));
        OnPropertyChanged(nameof(GreenTrack));
        OnPropertyChanged(nameof(BlueTrack));

        // 换算桥也得通知，否则滑块和数值框不会跟着动
        OnPropertyChanged(nameof(HueRatio));
        OnPropertyChanged(nameof(RedRatio));
        OnPropertyChanged(nameof(GreenRatio));
        OnPropertyChanged(nameof(BlueRatio));
        OnPropertyChanged(nameof(RedInt));
        OnPropertyChanged(nameof(GreenInt));
        OnPropertyChanged(nameof(BlueInt));
    }

    // ── 颜色换算（都是纯函数，可以单独测）──

    /// <summary>#RRGGBB 或 RRGGBB（也容忍 #RGB 缩写）。</summary>
    public static bool TryParseHex(string? text, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim().TrimStart('#');

        // #RGB 这种三位缩写先展开成六位
        if (s.Length == 3)
        {
            s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        }

        if (s.Length != 6) return false;

        if (!byte.TryParse(s.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)) return false;
        if (!byte.TryParse(s.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)) return false;
        if (!byte.TryParse(s.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)) return false;

        color = Color.FromRgb(r, g, b);
        return true;
    }

    /// <summary>转成 #RRGGBB（大写）。</summary>
    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>RGB → HSV。色相用度，其余 0~1。</summary>
    public static (double Hue, double Saturation, double Value) RgbToHsv(byte r8, byte g8, byte b8)
    {
        var r = r8 / 255.0;
        var g = g8 / 255.0;
        var b = b8 / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue = 0;
        if (delta > 0.000001)
        {
            if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }
        }

        if (hue < 0) hue += 360;

        var saturation = max <= 0.000001 ? 0 : delta / max;

        return (hue, saturation, max);
    }

    /// <summary>HSV → 颜色。</summary>
    public static Color HsvToColor(double hue, double saturation, double value)
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
