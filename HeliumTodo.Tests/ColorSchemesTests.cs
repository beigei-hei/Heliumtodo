using System.IO;
using HeliumTodo.Core.Configuration;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 明暗模式的取值表测试。
///
/// 为什么要测这个？
///   0.3.3 之前"主题"是个可以随便加的下拉框（浅色/深色/高对比度…），
///   现在界面只留「亮色 / 暗色」两档，主题色改由强调色决定。
///   于是 ColorScheme 成了"配置文件里手改 + 界面下拉 + 老配置迁移"三个入口共用的一列，
///   它的失败方式很难看：拼错一个词就让整个窗口退回亮色，
///   老用户明明用着暗色，一升级突然全白 —— 所以归一化规则必须钉死。
/// </summary>
public class ColorSchemesTests
{
    [Fact]
    public void 默认应当是亮色()
    {
        // 默认亮色不是"随便挑一个"：初次打开时用户没有任何倾向，
        // 亮色在打印屏幕/截图里更好认，也更符合 Windows 的默认观感。
        Assert.Equal("light", ColorSchemes.Default);
        Assert.Equal(ColorSchemes.Light, new AppConfig().ColorScheme);
    }

    [Fact]
    public void 只有亮色和暗色两档()
    {
        // 界面上就两个选项，多一个都会让 ComboBox 里出现没人认识的项。
        Assert.Equal(2, ColorSchemes.All.Count);
        Assert.Contains(ColorSchemes.Light, ColorSchemes.All);
        Assert.Contains(ColorSchemes.Dark, ColorSchemes.All);
    }

    [Fact]
    public void 亮色暗色都能被原样识别()
    {
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize("light"));
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.Normalize("dark"));
    }

    [Fact]
    public void 大小写和空白不该影响识别()
    {
        // 配置文件是人手改的，写成 " Dark " 太常见了。
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.Normalize("  DARK  "));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize(" Light "));
    }

    [Fact]
    public void 中文和英文别名都认()
    {
        // 我自己在配置文件里写中文注释时就这么写过，认不出来很别扭。
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.Normalize("深色"));
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.Normalize("夜间"));
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.Normalize("night"));
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.Normalize("dim"));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize("浅色"));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize("日间"));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize("day"));
    }

    [Fact]
    public void 认不出来的值应回落到亮色而不是抛异常()
    {
        // 这一条是底线：启动早期读配置，抛异常 = 程序起不来。
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize(null));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize(""));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize("随便写的"));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.Normalize("high-contrast"));
    }

    [Fact]
    public void 每个取值都要有中文名字()
    {
        // 界面上显示的是中文，缺一个就会出现空白下拉项。
        foreach (var scheme in ColorSchemes.All)
        {
            var name = ColorSchemes.DisplayName(scheme);
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.NotEqual(scheme, name);
        }
    }

    [Fact]
    public void 明暗到主题编号的换算要能往返()
    {
        // ThemeId 还留着（老配置、备份清单里都有它），
        // 所以 ColorScheme ⇄ ThemeId 必须是一一对应的，否则升级时会串档。
        Assert.Equal("fluent-light-blue", ColorSchemes.ToThemeId(ColorSchemes.Light));
        Assert.Equal("fluent-dark-blue", ColorSchemes.ToThemeId(ColorSchemes.Dark));

        Assert.Equal(ColorSchemes.Light, ColorSchemes.FromThemeId("fluent-light-blue"));
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.FromThemeId("fluent-dark-blue"));

        // 往返一圈应当回到原值。
        foreach (var scheme in ColorSchemes.All)
        {
            Assert.Equal(scheme, ColorSchemes.FromThemeId(ColorSchemes.ToThemeId(scheme)));
        }
    }

    [Fact]
    public void 老配置里的高对比度和未知主题都当暗色或亮色处理()
    {
        // 0.3.2 及更早版本里有 high-contrast 这类主题；高对比度是黑底，
        // 按暗色处理至少不会突然闪成白屏。
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.FromThemeId("high-contrast"));
        Assert.Equal(ColorSchemes.Dark, ColorSchemes.FromThemeId("fluent-dark"));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.FromThemeId(null));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.FromThemeId(""));
        Assert.Equal(ColorSchemes.Light, ColorSchemes.FromThemeId("strange-theme"));
    }

    [Fact]
    public void 没写过明暗字段的老配置应当按主题编号推断()
    {
        // 直接验证 ConfigService 的迁移分支：老 config.json 里没有 "ColorScheme"，
        // 只有 ThemeId。这条要是坏了，用着暗色的老用户升级后会被闪一下白。
        var dir = TestWorkspace.Create("color-scheme-migrate");
        try
        {
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{ \"ThemeId\": \"fluent-dark-blue\" }");

            var config = new ConfigService(dir);
            var loaded = config.Load();

            Assert.Equal(ColorSchemes.Dark, loaded.ColorScheme);
            Assert.Equal("fluent-dark-blue", loaded.ThemeId);
        }
        finally
        {
            TestWorkspace.Delete(dir);
        }
    }
}