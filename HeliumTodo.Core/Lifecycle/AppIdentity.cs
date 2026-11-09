namespace HeliumTodo.Core.Lifecycle;

/// <summary>
/// 这个程序"叫什么"——单实例互斥量、唤回事件、日志前缀都按它取名。
///
/// 为什么要能改：
///   默认值 <c>HeliumTodo</c> 是给用户用的。但自检与探针脚本需要在同一台机器上
///   **拉起两份互不相干的实例**（例如验证"第二个实例只喊一声就退出"，
///   或同时跑两个探针），共用一个互斥量就会互相顶掉 —— 探针里真正的那一份
///   会发现自己"不是第一个"，于是不建窗口，测试直接卡在等窗口那一步。
///   这类失败现象一点也不像"名字撞了"，排查成本很高（本项目真踩过）。
///   所以留一个 <see cref="AppIdVariable"/> 环境变量给测试隔离用。
///
/// 它也是"绿色版并存"的实现基础：把程序解压两份、各设一个 app id，
/// 两边就能同时运行、各自一份数据，互不干扰。
/// </summary>
public static class AppIdentity
{
    /// <summary>用户平时用的那个名字。</summary>
    public const string DefaultAppId = "HeliumTodo";

    /// <summary>改名的环境变量。只在自检/探针/绿色版并存时用，界面上不暴露。</summary>
    public const string AppIdVariable = "HELIUM_APP_ID";

    /// <summary>
    /// 把环境变量里读到的值变成能用的 app id：空、全空白一律回落到默认值；
    /// 首尾空白去掉（命令行里 <c>set X= a </c> 这种写法很常见）。
    /// </summary>
    public static string Resolve(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultAppId;
        }

        return configured.Trim();
    }

    /// <summary>从当前进程的环境里取。找不到就用默认值。</summary>
    public static string ResolveFromEnvironment() =>
        Resolve(Environment.GetEnvironmentVariable(AppIdVariable));
}
