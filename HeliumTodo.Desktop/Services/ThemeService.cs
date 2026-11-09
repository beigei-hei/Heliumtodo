// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Theming;
using Material.Styles.Themes;

namespace HeliumTodo.Desktop.Services;

/// <summary>
/// 主题服务：把「Core 里的主题定义」变成「Avalonia 真正能用的明暗与颜色」。
///
/// 这里就是前面说的「界面层负责转换」的地方：
///   Core 里的 <see cref="IHeliumTheme"/> 只说「我是深色，强调色是 #0F6CBD」，
///   至于怎么变成 Avalonia 的 ThemeVariant 和画刷，由这个类负责。
/// </summary>
public sealed class ThemeService
{
    private readonly ThemeRegistry _registry;

    /// <summary>
    /// 上一次真正应用过的组合（主题 Id + 明暗 + 强调色）。
    ///
    /// 为什么要记这个？
    ///   动图壁纸播放时每秒会触发十几次「壁纸变了」的通知。
    ///   如果每次都把七个强调色资源重写一遍，等于每秒让整个界面重绘十几次，
    ///   既费电又可能看到闪烁。记下来做对比，参数没变就直接返回。
    /// </summary>
    private string? _lastAppliedKey;

    public ThemeService(ThemeRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>当前生效的主题。</summary>
    public IHeliumTheme Current => _registry.Current;

    /// <summary>
    /// 应用一套主题：切换明暗 + 更新强调色。
    /// </summary>
    public void Apply(IHeliumTheme theme) => Apply(theme, accentOverrideHex: null);

    /// <summary>
    /// 应用一套主题，并允许覆盖强调色。
    ///
    /// 为什么要能覆盖？
    ///   「跟随壁纸」和「自定义颜色」这两种模式下，强调色不再是主题自带的那个，
    ///   但明暗、字号、圆角这些还是跟着主题走。所以拆成两个参数：
    ///   主题决定"长什么样"，覆盖值决定"用哪个强调色"。
    /// </summary>
    /// <param name="accentOverrideHex">null 表示用主题自带的强调色。</param>
    public void Apply(IHeliumTheme theme, string? accentOverrideHex)
    {
        var app = Avalonia.Application.Current;
        if (app is null) return;

        // ① 切换浅色 / 深色
        var variant = theme.Kind == ThemeKind.Dark
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;

        app.RequestedThemeVariant = variant;

        // ② 更新强调色
        var accent = string.IsNullOrWhiteSpace(accentOverrideHex) ? theme.AccentColorHex : accentOverrideHex;

        // 和上次一样就什么都不用做
        var key = $"{theme.Id}|{theme.Kind}|{accent}";
        if (string.Equals(key, _lastAppliedKey, StringComparison.OrdinalIgnoreCase)) return;
        _lastAppliedKey = key;

        ApplyMaterialBaseTheme(theme.Kind == ThemeKind.Dark);
        ApplyAccentColor(accent, isDark: theme.Kind == ThemeKind.Dark);
    }


    /// <summary>
    /// 把强调色真正应用上去。
    ///
    /// 这里的做法换过一次，把经过记下来，免得以后又绕回去：
    ///
    ///   原来的写法是往 <c>Application.Current.Resources</c> 里写
    ///   "SystemAccentColor" 等 7 个颜色键。这个写法**其实是有效的**
    ///   （实测：换成紫色主题后，SystemAccentColor 与 FluentAvalonia
    ///   自己算出来的 AccentFillColorDefaultBrush 都跟着变成了紫色）。
    ///
    ///   现在改成用 FluentAvaloniaTheme 的公开属性 CustomAccentColor，
    ///   原因是：
    ///     · 它是官方给的入口，语义清楚，不像写资源那样靠"猜"查找顺序
    ///     · 整套色阶（Light1..3 / Dark1..3）由它自己算，我们不必
    ///       手工维护 6 个深浅色，也就不可能出现"某个色阶忘了改"的偏差
    ///   （两种写法实测颜色几乎一致，只有 1 个最低位的差别。）
    ///
    ///   ⚠️ 顺带记一个踩过的假坑：我曾以为"强调色从来没生效过"，
    ///   排查半天。真实原因是改配置文件时把键名写成了 camelCase，
    ///   而配置文件里是 PascalCase —— 配置根本没被读进去，
    ///   于是主题一直是默认的蓝色。教训：改外部配置文件前，
    ///   先确认它真实的键名格式。
    /// </summary>
    private static void ApplyAccentColor(string hex, bool isDark)
    {
        if (!TryParseColor(hex, out var color)) return;

        // 深色主题下把颜色提亮一点，保证在深色背景上看得清
        var baseColor = isDark ? Lighten(color, 0.35) : color;

        // ① 先算两个派生色，让「语义色板」也跟着主题走。
        //    优先级标签、日程块、冲突提示原来把 #0F6CBD 写死了，
        //    主题换成紫色以后那几处还是蓝的 —— 这就是"主题色不统一"的来源。
        ApplyDerivedAccentTokens(baseColor, isDark);

        // ② 主途径：Material 主题自己算色阶（0.5.0 P3 起）
        ApplyMaterialAccent(baseColor);

        // ③ 过渡期：FA 控件还没换完，继续把强调色交给 FluentAvaloniaTheme，
        //    否则设置页、导航轨里的 FA 控件会停在默认蓝色。P4 删除。
        var fluentTheme = FindFluentAvaloniaTheme();
        if (fluentTheme is not null)
        {
            fluentTheme.CustomAccentColor = baseColor;
            return;
        }

        // ④ 兜底：万一样式表里没有 FluentAvaloniaTheme（比如无头测试换了别的样式），
        //    直接写资源也能生效，只是色阶要自己算。
        if (Avalonia.Application.Current?.Resources is not { } resources) return;

        resources["SystemAccentColor"] = baseColor;
        resources["SystemAccentColorLight1"] = Lighten(baseColor, 0.15);
        resources["SystemAccentColorLight2"] = Lighten(baseColor, 0.30);
        resources["SystemAccentColorLight3"] = Lighten(baseColor, 0.45);
        resources["SystemAccentColorDark1"] = Darken(baseColor, 0.15);
        resources["SystemAccentColorDark2"] = Darken(baseColor, 0.30);
        resources["SystemAccentColorDark3"] = Darken(baseColor, 0.45);
    }


    /// <summary>
    /// 根据强调色算出两个"柔和色"，写进应用资源。
    ///
    /// 为什么需要这两个色：
    ///   强调色本身只能当描边、图标、按钮底色用。像优先级标签、
    ///   日程块这种"浅底 + 深字"的地方，需要的是同色系的浅底和深字。
    ///   以前这两处直接写死成了 #DCEBF9 / #09457A（默认蓝），
    ///   所以把主题色调成紫色时，它们还是蓝的 —— 看起来就像"没统一"。
    ///
    /// 计算方式：
    ///   浅底 = 强调色往背景色方向混 85%（深色主题往黑混，浅色主题往白混）
    ///   深字 = 强调色往黑/白方向压暗或提亮，保证在浅底上够清楚
    /// </summary>
    private static void ApplyDerivedAccentTokens(Avalonia.Media.Color accent, bool isDark)
    {
        if (Avalonia.Application.Current?.Resources is not { } resources) return;

        // 浅底：混进大量背景色，只留一点点色相
        var backdrop = isDark ? Avalonia.Media.Colors.Black : Avalonia.Media.Colors.White;
        var softFill = Blend(accent, backdrop, 0.86);

        // 浅底上的文字：浅色主题压暗、深色主题提亮
        var softText = isDark ? Lighten(accent, 0.62) : Darken(accent, 0.42);

        resources["AccentSoftFill"] = softFill;
        resources["AccentSoftText"] = softText;
    }


    /// <summary>
    /// 把 <paramref name="color"/> 按 <paramref name="ratio"/> 混向 <paramref name="target"/>。
    /// ratio = 0 返回原色，ratio = 1 返回 target。
    /// </summary>
    private static Avalonia.Media.Color Blend(Avalonia.Media.Color color, Avalonia.Media.Color target, double ratio)
    {
        var t = Math.Clamp(ratio, 0.0, 1.0);

        static byte Mix(byte a, byte b, double t) => (byte)Math.Round(a + ((b - a) * t));

        return Avalonia.Media.Color.FromArgb(
            Mix(color.A, target.A, t),
            Mix(color.R, target.R, t),
            Mix(color.G, target.G, t),
            Mix(color.B, target.B, t));
    }


    /// <summary>
    /// 找到 App.axaml 里声明的 MaterialTheme（0.5.0 P3 起）。
    /// </summary>
    private static Material.Styles.Themes.MaterialTheme? FindMaterialTheme()
    {
        if (Avalonia.Application.Current is not { } app) return null;

        foreach (var style in app.Styles)
        {
            if (style is Material.Styles.Themes.MaterialTheme material) return material;
        }

        return null;
    }


    /// <summary>
    /// 让 MaterialTheme 的明暗跟应用主题一致。
    ///
    /// 为什么不能只改 <c>RequestedThemeVariant</c>？
    ///   Material 的 ThemeVariant 是 MaterialLight / MaterialDark 两套资源，
    ///   MaterialTheme 用它自己的 BaseTheme 决定挂哪一套。只改应用整体变体，
    ///   Material 控件会停在启动时那一套，出现"界面深色、输入框还是浅色"。
    ///   （MaterialTheme 内部有 100ms 合并延迟，切换后要等一等才能断言像素。）
    /// </summary>
    private static void ApplyMaterialBaseTheme(bool isDark)
    {
        if (FindMaterialTheme() is not { } material) return;

        material.BaseTheme = isDark
            ? Material.Styles.Themes.Base.BaseThemeMode.Dark
            : Material.Styles.Themes.Base.BaseThemeMode.Light;
    }


    /// <summary>
    /// 把任意十六进制强调色写进 Material 的资源键。
    ///
    /// 为什么不用 MaterialTheme.PrimaryColor：
    ///   它只接受 PrimaryColor 枚举（19 个固定色板），接不住用户在设置页里
    ///   手调的 #RRGGBB。为什么不用 PaletteHelper：
    ///   3.21.0 的 PaletteHelper.GetTheme() 找不到 MaterialTheme 内部的 ITheme，
    ///   返回的是默认主题，写回去也进不了 Material 的资源字典（已实测：
    ///   MaterialPrimaryMidBrush 仍是 #00000000）。
    ///
    /// 所以这里按 Material 自己的键名算三档色阶，写到 Application.Resources。
    /// 资源查找时应用级字典优先于样式里的默认值，Material 控件用
    /// DynamicResource 取色时会先命中这里 —— 这是唯一能保住任意 RGB 的通道。
    /// </summary>
    private static void ApplyMaterialAccent(Avalonia.Media.Color color)
    {
        if (Avalonia.Application.Current?.Resources is not { } resources) return;

        var light = Lighten(color, 0.20);
        var mid = color;
        var dark = Darken(color, 0.18);

        // 三档前景色：保证在对应底色上看得清（亮的用黑字，深的用白字）
        var lightFg = ReadableForeground(light);
        var midFg = ReadableForeground(mid);
        var darkFg = ReadableForeground(dark);

        WriteAccent(resources, "MaterialPrimary", light, mid, dark, lightFg, midFg, darkFg);
        WriteAccent(resources, "MaterialSecondary", light, mid, dark, lightFg, midFg, darkFg);

        static void WriteAccent(Avalonia.Controls.IResourceDictionary resources, string prefix,
            Avalonia.Media.Color light, Avalonia.Media.Color mid, Avalonia.Media.Color dark,
            Avalonia.Media.Color lightFg, Avalonia.Media.Color midFg, Avalonia.Media.Color darkFg)
        {
            resources[$"{prefix}Light"] = light;
            resources[$"{prefix}Mid"] = mid;
            resources[$"{prefix}Dark"] = dark;

            resources[$"{prefix}LightBrush"] = new Avalonia.Media.SolidColorBrush(light);
            resources[$"{prefix}MidBrush"] = new Avalonia.Media.SolidColorBrush(mid);
            resources[$"{prefix}DarkBrush"] = new Avalonia.Media.SolidColorBrush(dark);

            resources[$"{prefix}LightForegroundBrush"] = new Avalonia.Media.SolidColorBrush(lightFg);
            resources[$"{prefix}MidForegroundBrush"] = new Avalonia.Media.SolidColorBrush(midFg);
            resources[$"{prefix}DarkForegroundBrush"] = new Avalonia.Media.SolidColorBrush(darkFg);
        }
    }


    /// <summary>
    /// 给定底色，返回一个对比度足够的前景色（白或黑）。
    /// 用相对亮度近似（sRGB 权重 0.2126/0.7152/0.0722），阈值 0.55。
    /// </summary>
    private static Avalonia.Media.Color ReadableForeground(Avalonia.Media.Color background)
    {
        var luminance = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255.0;

        return luminance > 0.55 ? Avalonia.Media.Colors.Black : Avalonia.Media.Colors.White;
    }


    /// <summary>
    /// 在应用的样式表里找那个 FluentAvaloniaTheme 实例（App.axaml 里声明的那一个）。
    ///
    /// 为什么不自己 new 一个？
    ///   强调色是"样子"的一部分，必须设在真正生效的那个样式对象上。
    ///   另起一个新的没有任何控件用它，改了也白改。
    /// </summary>
    private static FluentAvalonia.Styling.FluentAvaloniaTheme? FindFluentAvaloniaTheme()
    {
        if (Avalonia.Application.Current is not { } app) return null;

        foreach (var style in app.Styles)
        {
            if (style is FluentAvalonia.Styling.FluentAvaloniaTheme fluent) return fluent;
        }

        return null;
    }

    /// <summary>
    /// 按「当前配置 + 当前壁纸」算出该用哪个强调色，并应用。
    ///
    /// 强调色有三种来源（见 <see cref="AccentSources"/>）：
    ///   · 跟随主题   → 用主题自带的颜色
    ///   · 跟随壁纸   → 用从壁纸里取出来的主色调（壁纸没加载就退回主题色）
    ///   · 自定义颜色 → 用用户手调的那个颜色
    ///
    /// 这个方法是唯一决定"强调色到底是谁"的地方，
    /// 界面里改了任何相关设置只要重新调它一次就行，不用各页面各写一遍判断。
    /// </summary>
    public void ApplyFor(HeliumTodo.Application.Configuration.AppConfig config, WallpaperService? wallpaper)
    {
        var overrideHex = ResolveAccentOverride(config, wallpaper);
        Apply(Current, overrideHex);
    }

    /// <summary>按配置算出强调色覆盖值；返回 null 表示用主题自带的。</summary>
    public static string? ResolveAccentOverride(
        HeliumTodo.Application.Configuration.AppConfig config,
        WallpaperService? wallpaper)
    {
        // 真正的判断逻辑在 Core 层（AccentSources.ResolveOverride），那边有单元测试。
        // 这里只负责把"壁纸当前的颜色"从服务里取出来递进去 ——
        // 壁纸没加载好（或加载失败）就传 null，规则自然退回主题色。
        var wallpaperHex = wallpaper is { IsLoaded: true } ? wallpaper.Accent.ToHex() : null;

        return HeliumTodo.Application.Configuration.AccentSources.ResolveOverride(
            config.AccentSource,
            config.CustomAccentHex,
            wallpaperHex);
    }

    /// <summary>把 "#0F6CBD" 这样的文字解析成颜色。</summary>
    private static bool TryParseColor(string hex, out Avalonia.Media.Color color)
    {
        try
        {
            color = Avalonia.Media.Color.Parse(hex);
            return true;
        }
        catch
        {
            color = Avalonia.Media.Colors.SteelBlue;
            return false;
        }
    }

    /// <summary>把颜色往白色方向调 <paramref name="amount"/>（0–1）。</summary>
    private static Avalonia.Media.Color Lighten(Avalonia.Media.Color color, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Avalonia.Media.Color.FromArgb(
            color.A,
            (byte)(color.R + (255 - color.R) * amount),
            (byte)(color.G + (255 - color.G) * amount),
            (byte)(color.B + (255 - color.B) * amount));
    }

    /// <summary>把颜色往黑色方向调 <paramref name="amount"/>（0–1）。</summary>
    private static Avalonia.Media.Color Darken(Avalonia.Media.Color color, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Avalonia.Media.Color.FromArgb(
            color.A,
            (byte)(color.R * (1 - amount)),
            (byte)(color.G * (1 - amount)),
            (byte)(color.B * (1 - amount)));
    }
}
