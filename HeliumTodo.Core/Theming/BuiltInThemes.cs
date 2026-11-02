namespace HeliumTodo.Core.Theming;

/// <summary>
/// 内置主题（Fluent 2 配色）。
///
/// 颜色取自微软 Fluent 2 官方设计文档的调色板：
///   · #0F6CBD 是 Fluent 2 的标准品牌蓝
///   · 深色主题用同一色系，但界面层会自动把它调亮一点，保证深色背景上看得清
/// </summary>
public static class BuiltInThemes
{
    // ── Fluent 2 官方强调色 ──────────────────────────────
    private const string FluentBlue   = "#0F6CBD";
    private const string FluentTeal   = "#038387";
    private const string FluentPurple = "#5B2D8E";
    private const string FluentOrange = "#CA5010";
    private const string FluentGreen  = "#107C10";

    /// <summary>默认主题：Fluent 2 浅色 + 标准蓝。</summary>
    public static readonly IHeliumTheme Default = new SimpleTheme(
        id: "fluent-light-blue",
        displayName: "Fluent 浅色 · 蓝",
        kind: ThemeKind.Light,
        accentHex: FluentBlue);

    /// <summary>所有内置主题。</summary>
    public static IReadOnlyList<IHeliumTheme> All { get; } = new List<IHeliumTheme>
    {
        Default,

        new SimpleTheme("fluent-light-teal",   "Fluent 浅色 · 青", ThemeKind.Light, FluentTeal),
        new SimpleTheme("fluent-light-purple", "Fluent 浅色 · 紫", ThemeKind.Light, FluentPurple),
        new SimpleTheme("fluent-light-green",  "Fluent 浅色 · 绿", ThemeKind.Light, FluentGreen),

        new SimpleTheme("fluent-dark-blue",    "Fluent 深色 · 蓝", ThemeKind.Dark,  FluentBlue),
        new SimpleTheme("fluent-dark-teal",    "Fluent 深色 · 青", ThemeKind.Dark,  FluentTeal),
        new SimpleTheme("fluent-dark-purple",  "Fluent 深色 · 紫", ThemeKind.Dark,  FluentPurple),
        new SimpleTheme("fluent-dark-orange",  "Fluent 深色 · 橙", ThemeKind.Dark,  FluentOrange),

        // 下面这套演示「主题就是一行数据」—— 加主题不需要写新类
        new SimpleTheme("high-contrast", "高对比度（预览）", ThemeKind.Dark, "#FFD700", isExperimental: true),
    };

    /// <summary>
    /// 最简单的主题实现。
    /// 因为一套主题就是「一个明暗基调 + 一个强调色」，所以用一个通用类就够了，
    /// 不用为每套主题单独写一个类。
    /// </summary>
    private sealed class SimpleTheme : IHeliumTheme
    {
        public SimpleTheme(string id, string displayName, ThemeKind kind, string accentHex, bool isExperimental = false)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            AccentColorHex = accentHex;
            IsExperimental = isExperimental;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public ThemeKind Kind { get; }
        public string AccentColorHex { get; }
        public bool IsExperimental { get; }
    }
}
