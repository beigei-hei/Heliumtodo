namespace HeliumTodo.Core.Configuration;

/// <summary>
/// 强调色来源的取值。
///
/// 之所以单独定义成常量而不是直接用字符串字面量到处写，
/// 是因为这几个值会同时出现在三处：配置文件、设置界面的下拉框、
/// 以及程序启动时决定用哪个颜色的那段逻辑。
/// 只要有一处拼错，程序不会报错，只会"莫名其妙不生效"——
/// 这种 bug 最难查。集中成常量以后，写错就是编译错误。
/// </summary>
public static class AccentSources
{
    /// <summary>跟着内置主题走。默认值，行为和老版本完全一致。</summary>
    public const string Theme = "theme";

    /// <summary>从壁纸里自动提取主色调。</summary>
    public const string Wallpaper = "wallpaper";

    /// <summary>用户自己调的颜色。</summary>
    public const string Custom = "custom";

    /// <summary>所有合法的取值（给界面做下拉框用）。</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Theme, Wallpaper, Custom };

    /// <summary>
    /// 把配置里读到的值规范化成合法值。
    ///
    /// 为什么需要它：配置文件是纯文本，用户可能手改成 "Wallpaper"、
    /// "壁纸"、或者干脆写错。这时候不该报错、也不该崩溃，
    /// 而是安静地退回默认值 —— 用户的其它设置不能因为一个字段写错就全丢。
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Theme;

        var trimmed = value.Trim();

        // 大小写不敏感：手写配置的人不一定会注意大小写
        foreach (var candidate in All)
        {
            if (string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return Theme;
    }

    /// <summary>给界面显示的中文名字。</summary>
    public static string ToDisplayName(string value) => Normalize(value) switch
    {
        Wallpaper => "跟随壁纸",
        Custom => "自定义颜色",
        _ => "跟随主题",
    };

    /// <summary>
    /// 算出"要不要覆盖主题自带的强调色"，要的话用哪个颜色。
    ///
    /// 返回 null 表示不覆盖（用主题自带的）；返回 "#RRGGBB" 表示用这个。
    ///
    /// 为什么这个判断放在 Core 层而不是界面层？
    ///   它纯粹是"看配置决定用哪个颜色"的规则，跟窗口、控件、图片都没关系。
    ///   放在 Core 里就能直接写单元测试 —— 而放在界面层的话，
    ///   想测这四种分支就得真的把整个应用跑起来，那太重了。
    /// </summary>
    /// <param name="source">配置里的强调色来源。</param>
    /// <param name="customHex">用户自定义的颜色（可能没填或填错）。</param>
    /// <param name="wallpaperHex">从壁纸里取到的颜色；壁纸没加载好时传 null。</param>
    public static string? ResolveOverride(string? source, string? customHex, string? wallpaperHex)
    {
        return Normalize(source) switch
        {
            // 自定义：颜色必须真的能解析，否则当没设置，退回主题色 ——
            // 不能让配置文件里一个手滑打错的十六进制把界面搞成黑的
            Custom => NormalizeHex(customHex),

            // 跟随壁纸：壁纸还没加载好（或者图片读不出来）时也用主题色
            Wallpaper => NormalizeHex(wallpaperHex),

            _ => null,
        };
    }

    /// <summary>
    /// 校验并规范化一个 "#RRGGBB" 字符串，不合法就返回 null。
    /// </summary>
    private static string? NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;

        var text = hex.Trim();

        if (text.StartsWith('#')) text = text[1..];

        if (text.Length != 6) return null;

        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c)) return null;
        }

        return "#" + text.ToUpperInvariant();
    }
}
