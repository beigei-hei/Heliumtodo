using System.Text.RegularExpressions;
using HeliumTodo.Core;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 版本号的测试。
///
/// 为什么要测这个？
///   版本号看着是小事，但很容易出错，而且错了不容易发现：
///     · Assembly.GetName().Version 会把 "-dev" 这类预发布标记悄悄丢掉
///     · AssemblyInformationalVersion 会多带一长串编译哈希（0.3.5-alpha+3f2a1b）
///   这两点都踩过。用测试把「用户看到的版本号必须长什么样」固定下来，
///   以后升级版本号（只改 Directory.Build.props 一处）时就不用担心显示出错。
/// </summary>
public class VersionInfoTests
{
    [Fact]
    public void 版本号不应为空()
    {
        Assert.False(string.IsNullOrWhiteSpace(VersionInfo.Current));
    }

    [Fact]
    public void 版本号格式应合法()
    {
        // 允许的两种写法：
        //   0.0.2          正式版
        //   0.3.5-alpha      预发布版（横线后面跟标记）
        // 不允许出现 0.0.2.dev 这种「点后面跟字母」的写法 ——
        // 那不是合法的语义化版本，打包工具会拒绝。
        var pattern = new Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?$");

        Assert.True(
            pattern.IsMatch(VersionInfo.Current),
            $"版本号格式不合法：{VersionInfo.Current}（应为 0.0.2 或 0.3.5-alpha 这样的形式）");
    }

    [Fact]
    public void 版本号不应带编译哈希()
    {
        // + 号后面是源码版本哈希，是给调试用的，不该出现在界面上
        Assert.DoesNotContain('+', VersionInfo.Current);
    }

    [Fact]
    public void 显示用的版本号带v前缀()
    {
        Assert.Equal("v" + VersionInfo.Current, VersionInfo.Display);
        Assert.StartsWith("v", VersionInfo.Display);
    }
}
