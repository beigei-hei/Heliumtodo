using System.IO;
using HeliumTodo.Core.Configuration;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 渲染后端取值表的测试。
///
/// 为什么要测这个？
///   这一项是"用户手改配置文件 + 环境变量 + 设置页下拉框"三个入口共用的，
///   而它的失败方式特别难看：写错一个词 → 后端解析不出来 →
///   程序可能在最不该出问题的时候（启动、还没画出界面）崩掉。
///   所以归一化规则必须钉死：认不出的值一律回落默认，绝不抛异常。
/// </summary>
public class RenderBackendSettingsTests
{
    [Fact]
    public void 默认后端应是自动显卡渲染()
    {
        // 默认值不是随手定的，它换过一次方向：
        //   · 0.3.3 定成软件渲染 —— 省掉整套显卡驱动映射（实测 −178 MB 私有字节）；
        //   · 0.3.5 改回自动 —— 软件渲染下 2560×1440 的窗口动效只有 25–43 fps，
        //     换 ANGLE 是 74 fps，用户明确要流畅优先。
        // 谁要改这个值，先看 TECH_STACK.md 里两边的数字。
        Assert.Equal("auto", RenderBackendSettings.Default);
        Assert.Equal(RenderBackendSettings.Auto, new AppConfig().RenderBackend);
    }

    [Fact]
    public void 四种后端都能被原样识别()
    {
        Assert.Equal(RenderBackendSettings.Software, RenderBackendSettings.Normalize("software"));
        Assert.Equal(RenderBackendSettings.Auto, RenderBackendSettings.Normalize("auto"));
        Assert.Equal(RenderBackendSettings.OpenGl, RenderBackendSettings.Normalize("wgl"));
        Assert.Equal(RenderBackendSettings.Vulkan, RenderBackendSettings.Normalize("vulkan"));
    }

    [Fact]
    public void 大小写和前后空格应被容忍()
    {
        // 配置文件是人手改的，"Vulkan "、" SOFTWARE " 这种很常见
        Assert.Equal(RenderBackendSettings.Vulkan, RenderBackendSettings.Normalize("  Vulkan "));
        Assert.Equal(RenderBackendSettings.Software, RenderBackendSettings.Normalize("SOFTWARE"));
        Assert.Equal(RenderBackendSettings.OpenGl, RenderBackendSettings.Normalize("\tWGL\n"));
    }

    [Fact]
    public void 常见别名应归到对应后端()
    {
        // 老版本只认环境变量，那时写的是 "angle"；留别名是为了老配置不失效
        Assert.Equal(RenderBackendSettings.Auto, RenderBackendSettings.Normalize("angle"));
        Assert.Equal(RenderBackendSettings.Auto, RenderBackendSettings.Normalize("default"));
        Assert.Equal(RenderBackendSettings.OpenGl, RenderBackendSettings.Normalize("opengl"));
        Assert.Equal(RenderBackendSettings.OpenGl, RenderBackendSettings.Normalize("gl"));
        Assert.Equal(RenderBackendSettings.Software, RenderBackendSettings.Normalize("cpu"));
    }

    [Fact]
    public void 认不出来的值应回落到默认而不是抛异常()
    {
        // 「配置写错不能让程序起不来」——这条比"识别所有写法"重要得多
        Assert.Equal(RenderBackendSettings.Default, RenderBackendSettings.Normalize("direct3d12"));
        Assert.Equal(RenderBackendSettings.Default, RenderBackendSettings.Normalize(""));
        Assert.Equal(RenderBackendSettings.Default, RenderBackendSettings.Normalize("   "));
        Assert.Equal(RenderBackendSettings.Default, RenderBackendSettings.Normalize(null));
    }

    [Fact]
    public void 显示名应覆盖全部四种后端()
    {
        Assert.Equal("软件渲染", RenderBackendSettings.DisplayName(RenderBackendSettings.Software));
        Assert.Equal("Avalonia 自带渲染器", RenderBackendSettings.DisplayName(RenderBackendSettings.Auto));
        Assert.Equal("OpenGL", RenderBackendSettings.DisplayName(RenderBackendSettings.OpenGl));
        Assert.Equal("Vulkan", RenderBackendSettings.DisplayName(RenderBackendSettings.Vulkan));

        // 写错的值也必须有名字可显示（界面不能出现空白项），回落目标就是默认后端
        Assert.Equal(RenderBackendSettings.DisplayName(RenderBackendSettings.Default),
            RenderBackendSettings.DisplayName("???"));
    }

    [Fact]
    public void 渲染后端应能存进配置文件并读回来()
    {
        var directory = TestWorkspace.Create("render-backend");
        try
        {
            var first = new ConfigService(directory);
            first.Load();
            first.Current.RenderBackend = RenderBackendSettings.Vulkan;
            first.Save();

            // 换一个实例重新读 —— 模拟"改完设置、重启程序"
            var second = new ConfigService(directory);
            var loaded = second.Load();

            Assert.Equal(RenderBackendSettings.Vulkan, loaded.RenderBackend);

            // 文件里应该是人可读的字符串，而不是枚举数字
            var json = File.ReadAllText(Path.Combine(directory, "config.json"));
            Assert.Contains("\"RenderBackend\": \"vulkan\"", json);
        }
        finally
        {
            TestWorkspace.Delete(directory);
        }
    }

    /// <summary>
    /// 换成同一个后端不该要重启 —— 否则用户只是把下拉框点回原值，
    /// 也会被弹一次"是否立即重启"。
    /// </summary>
    [Fact]
    public void 换成本来就是的值不需要重启()
    {
        Assert.False(RenderBackendSettings.NeedsRestart("software", "software"));
        Assert.False(RenderBackendSettings.NeedsRestart("vulkan", " VULKAN "));
        Assert.False(RenderBackendSettings.NeedsRestart("angle", "auto"));
    }

    /// <summary>
    /// 换了后端就要重启。大小写/别名写着玩也要认成同一个，不能因为
    /// 字符串长得不一样就弹提示。
    /// </summary>
    [Fact]
    public void 换成别的后端需要重启()
    {
        Assert.True(RenderBackendSettings.NeedsRestart("software", "vulkan"));
        Assert.True(RenderBackendSettings.NeedsRestart("auto", "wgl"));
        Assert.True(RenderBackendSettings.NeedsRestart(null, "vulkan"));
    }
}
