// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Theming;

/// <summary>
/// 主题的明暗基调。
///
/// 这里故意不用 Avalonia 的 <c>ThemeVariant</c> 类型，而是自己定义一个枚举 ——
/// 这样 Core 项目完全不依赖任何界面框架（<c>HeliumTodo.Domain.csproj</c> 里没有 Avalonia 引用）。
/// 好处：单元测试跑得快，以后就算换界面框架，Core 里的算法和模型也不用动。
/// 具体怎么变成 Avalonia 的 ThemeVariant，是界面层（HeliumTodo.Desktop）的事。
/// </summary>
public enum ThemeKind
{
    /// <summary>浅色。</summary>
    Light = 0,

    /// <summary>深色。</summary>
    Dark = 1,
}

/// <summary>
/// 一套主题需要提供的信息。
///
/// 【扩展点 1：主题】
/// 想加一套自己的主题，只要：
///   1. 写一个类实现这个接口（或者照着 <c>BuiltInThemes</c> 里现成的抄一份改颜色）；
///   2. 在 <see cref="ThemeRegistry"/> 里注册一下（插件在启动时自动注册）；
///   3. 界面上就能选到它了 —— 不需要改任何界面代码。
/// </summary>
public interface IHeliumTheme
{
    /// <summary>唯一 Id，保存在配置文件里用（例如 "fluent-dark-blue"）。</summary>
    string Id { get; }

    /// <summary>显示名称（界面上给用户看的）。</summary>
    string DisplayName { get; }

    /// <summary>明暗基调。</summary>
    ThemeKind Kind { get; }

    /// <summary>主强调色，写成十六进制字符串，例如 "#0F6CBD"。</summary>
    string AccentColorHex { get; }

    /// <summary>是不是实验性主题（界面上会加「预览」标记）。</summary>
    bool IsExperimental => false;
}

/// <summary>
/// 主题注册表：程序里所有可用主题的清单。
///
/// 这是「易扩展框架」的一部分 —— 界面只跟这个注册表打交道，
/// 所以新增主题时界面代码一行都不用动。
/// </summary>
public sealed class ThemeRegistry
{
    private readonly List<IHeliumTheme> _themes = new();

    /// <summary>当前生效的主题 Id。</summary>
    public string CurrentThemeId { get; private set; } = BuiltInThemes.Default.Id;

    /// <summary>当前生效的主题。</summary>
    public IHeliumTheme Current =>
        _themes.FirstOrDefault(t => t.Id == CurrentThemeId) ?? BuiltInThemes.Default;

    /// <summary>所有已注册的主题。</summary>
    public IReadOnlyList<IHeliumTheme> Themes => _themes;

    /// <summary>主题被切换时触发（界面收到后重新应用颜色）。</summary>
    public event EventHandler<IHeliumTheme>? ThemeChanged;

    public ThemeRegistry()
    {
        // 先注册内置主题，保证任何时候至少有一套主题可用
        foreach (var theme in BuiltInThemes.All)
        {
            Register(theme);
        }
    }

    /// <summary>
    /// 注册一个主题。插件在启动时调用它就能把自己的主题加进列表。
    /// 同 Id 的主题会被忽略（先注册的生效），避免插件覆盖内置主题。
    /// </summary>
    public bool Register(IHeliumTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (string.IsNullOrWhiteSpace(theme.Id))
        {
            return false;
        }

        if (_themes.Any(t => t.Id == theme.Id))
        {
            return false;
        }

        _themes.Add(theme);
        return true;
    }

    /// <summary>切换主题。找不到就退回默认主题。</summary>
    public IHeliumTheme Apply(string themeId)
    {
        CurrentThemeId = _themes.Any(t => t.Id == themeId) ? themeId : BuiltInThemes.Default.Id;
        var theme = Current;
        ThemeChanged?.Invoke(this, theme);
        return theme;
    }

    /// <summary>按 Id 找主题，找不到返回 null。</summary>
    public IHeliumTheme? Find(string themeId) =>
        _themes.FirstOrDefault(t => t.Id == themeId);
}
