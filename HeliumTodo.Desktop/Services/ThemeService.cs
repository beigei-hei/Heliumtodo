// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using FluentAvalonia.Styling;
using HeliumTodo.Domain.Theming;

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

        ApplyAccentColor(accent, isDark: theme.Kind == ThemeKind.Dark);
    }


    /// <summary>
    /// 把强调色真正应用上去。
    ///
    /// 这里的做法换过两次，把经过记下来，免得以后又绕回去：
    ///
    ///   0.3.x：往 <c>Application.Current.Resources</c> 里写
    ///   "SystemAccentColor" 等 7 个颜色键。有效，但色阶要自己算，
    ///   容易漏掉某个色阶。
    ///
    ///   0.4.x：改用 FluentAvaloniaTheme.CustomAccentColor。
    ///
    ///   0.5.0 P3：短暂换成 Material 3，当时发现 MaterialTheme.PrimaryColor
    ///   只接受枚举色板，只能绕过它直接写 MaterialPrimary* 资源键。
    ///
    ///   0.5.0-alpha 热修 2：按用户反馈切回 FluentAvalonia（Material 控件
    ///   在小尺寸按钮、下拉框上观感明显不如 Fluent）。当前途径回到
    ///   <see cref="FindFluentAvaloniaTheme"/> 的 CustomAccentColor，
    ///   同时保留 SystemAccentColor 系列给 Avalonia 内建控件兜底。
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

        // ② 主途径：FluentAvaloniaTheme 的强调色（0.5.0-alpha 热修 2 起）
        FindFluentAvaloniaTheme()?.CustomAccentColor = baseColor;

        // ③ 兜底：Avalonia 内建控件仍读 SystemAccentColor 系列。
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
    /// 找到 App.axaml 里声明的 FluentAvaloniaTheme。
    /// </summary>
    private static FluentAvaloniaTheme? FindFluentAvaloniaTheme()
    {
        if (Avalonia.Application.Current is not { } app) return null;

        foreach (var style in app.Styles)
        {
            if (style is FluentAvaloniaTheme fluent) return fluent;
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
