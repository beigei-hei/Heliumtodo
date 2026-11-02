namespace HeliumTodo.Core.Configuration;

/// <summary>
/// 亮色 / 暗色两套配色。
///
/// 【为什么要单独一个类型，而不是直接用 ThemeKind？】
///   <see cref="HeliumTodo.Core.Theming.ThemeKind"/> 属于"主题系统"，
///   而这里的东西属于"用户配置"。配置是要写进 config.json、还要能被手改的，
///   拿枚举序列化出来是 0/1，人看着不知所云；写成 "light"/"dark" 一眼就懂。
///   两者的对应关系放在 <see cref="ToThemeId"/>，只有这一处需要维护。
///
/// 【为什么强调色不在这里？】
///   用户后来的要求是"主题颜色直接取强调色" —— 也就是说，
///   明暗是"底"，强调色是"面"，两者合起来才是一套外观。
///   强调色单独存在 <see cref="AppConfig.CustomAccentHex"/> 里，
///   这样换明暗不会把用户挑的颜色弄丢。
/// </summary>
public static class ColorSchemes
{
    /// <summary>亮色。</summary>
    public const string Light = "light";

    /// <summary>暗色。</summary>
    public const string Dark = "dark";

    /// <summary>默认值：亮色。</summary>
    public const string Default = Light;

    /// <summary>所有合法的取值（给界面做下拉框用）。</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Light, Dark };

    /// <summary>
    /// 把配置里读到的值规范化。
    /// 认不出来的一律当亮色 —— 手改配置写错一个词不该让程序崩。
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Default;

        var trimmed = value.Trim();

        if (string.Equals(trimmed, Dark, StringComparison.OrdinalIgnoreCase)) return Dark;
        if (string.Equals(trimmed, Light, StringComparison.OrdinalIgnoreCase)) return Light;

        // 容错：用户可能写成中文或者别的叫法
        return trimmed switch
        {
            "深色" or "夜间" or "night" or "dim" => Dark,
            "浅色" or "日间" or "day" => Light,
            _ => Default,
        };
    }

    /// <summary>给界面显示的中文名字。</summary>
    public static string DisplayName(string? value) =>
        Normalize(value) == Dark ? "暗色" : "亮色";

    /// <summary>
    /// 明暗 → 内置主题 Id。
    ///
    /// 从 0.3.3 起界面上不再让用户从九个主题里挑，
    /// 而是"选明暗 + 选强调色"。主题剩下的作用只有一个：
    /// 决定 Avalonia 的 ThemeVariant（深色控件模板）和一批中性色。
    /// 强调色由 ThemeService 覆盖，所以这里固定用蓝那一套当底。
    /// </summary>
    public static string ToThemeId(string? scheme) =>
        Normalize(scheme) == Dark ? "fluent-dark-blue" : "fluent-light-blue";

    /// <summary>
    /// 内置主题 Id → 明暗。老配置文件里存的是 ThemeId，
    /// 读进来要靠它换算成新的 ColorScheme 字段，否则升级后用户的外观会跳掉。
    /// </summary>
    public static string FromThemeId(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return Default;

        var id = themeId.Trim();

        // 已知的深色主题：fluent-dark-* 与 high-contrast
        if (id.StartsWith("fluent-dark", StringComparison.OrdinalIgnoreCase)) return Dark;
        if (id.Equals("high-contrast", StringComparison.OrdinalIgnoreCase)) return Dark;

        return Light;
    }
}