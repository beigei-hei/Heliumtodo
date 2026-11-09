// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

#if HELIUM_PLUGINS
using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;
using HeliumTodo.Domain.Theming;

namespace HeliumTodo.Application.Plugins;

/// <summary>
/// 插件能力的「说明书」。
/// 宿主（主程序）通过这个接口把能力交给插件，插件不用自己去 new 任何东西。
///
/// 【0.5.0-alpha 起暂时禁用】
///   插件规范（可暴露的接口类型、权限模型、版本兼容策略）尚未成熟，
///   宿主不再加载任何外部 dll，见 DEVELOPMENT_SPEC.md §9。
///   这个文件只在显式定义 HELIUM_PLUGINS 时才参与编译，默认不定义。
/// </summary>
[Obsolete("0.5.0-alpha 起暂时禁用，见 DEVELOPMENT_SPEC.md §9")]
public interface IHeliumHostContext
{
    /// <summary>程序版本（插件可以用来判断宿主是否够新）。</summary>
    string AppVersion { get; }

    /// <summary>主题注册表：插件可以往里加自己的主题。</summary>
    ThemeRegistry Themes { get; }

    /// <summary>读配置。</summary>
    T GetConfig<T>(string key, T defaultValue);

    /// <summary>写配置（会自动保存到磁盘上的 JSON 文件）。</summary>
    void SetConfig<T>(string key, T value);

    /// <summary>往日志里写一行（调试时用，会输出到「调试输出」窗口）。</summary>
    void Log(string message);

    /// <summary>
    /// 往左侧导航栏里加一个页面。
    /// 参数是「图标字符 + 标题 + 返回页面内容的工厂方法」。
    /// </summary>
    void RegisterPage(string icon, string title, Func<object> pageFactory);

    // ── 下面是事件钩子：插件可以「挂」在这些时机上做点事 ──

    /// <summary>待办被创建时。</summary>
    event EventHandler<TodoItem>? TodoCreated;

    /// <summary>待办被完成时。</summary>
    event EventHandler<TodoItem>? TodoCompleted;

    /// <summary>一次专注结束时。</summary>
    event EventHandler<FocusSession>? FocusCompleted;

    /// <summary>某天的时间轴重新计算完成时。</summary>
    event EventHandler<DayTimeline>? TimelineRebuilt;
}

/// <summary>
/// 一个插件。
///
/// 写一个插件只需要三步：
///   1. 新建一个类库，引用 HeliumTodo.Application；
///   2. 写一个类实现 <see cref="IHeliumPlugin"/>；
///   3. 编译出的 dll 丢进程序的 plugins 文件夹，启动时会自动加载。
///
/// 最小例子：
/// <code>
/// public sealed class MyPlugin : IHeliumPlugin
/// {
///     public string Id =&gt; "com.example.myplugin";
///     public string Name =&gt; "我的插件";
///     public string Version =&gt; "1.0.0";
///
///     public void Initialize(IHeliumHostContext host)
///     {
///         host.Log("我的插件启动了！");
///     }
/// }
/// </code>
/// </summary>
[Obsolete("0.5.0-alpha 起暂时禁用，见 DEVELOPMENT_SPEC.md §9")]
public interface IHeliumPlugin
{
    /// <summary>唯一 Id（建议用 反向域名.名字 的写法，避免和别人的插件撞名）。</summary>
    string Id { get; }

    /// <summary>显示名称。</summary>
    string Name { get; }

    /// <summary>版本号。</summary>
    string Version { get; }

    /// <summary>作者，可以留空。</summary>
    string Author => string.Empty;

    /// <summary>一句话说明这个插件做什么。</summary>
    string Description => string.Empty;

    /// <summary>
    /// 插件加载时调用一次。
    /// 在这里注册主题、注册页面、挂事件钩子。
    /// </summary>
    void Initialize(IHeliumHostContext host);

    /// <summary>程序退出时调用（用来存盘、断开连接）。</summary>
    void Shutdown() { }
}
#endif