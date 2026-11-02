using HeliumTodo.Core.Models;
using HeliumTodo.Core.Services;
using HeliumTodo.Core.Theming;

namespace HeliumTodo.Core.Plugins;

/// <summary>
/// 插件能力的「说明书」。
/// 宿主（主程序）通过这个接口把能力交给插件，插件不用自己去 new 任何东西。
///
/// 【扩展点 2：插件】
/// 从现在开始，任何新功能都可以先做成插件试试，
/// 不用改主程序的界面代码 —— 这对毕设「易扩展」这一点是加分项。
/// </summary>
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
///   1. 新建一个类库，引用 HeliumTodo.Core；
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

/// <summary>
/// 网络传输接口 —— 为「自习室多人联机（无中心服务器方案）」预留的接入点。
///
/// 【当前状态：暂缓实现】
/// 现在只有一个不联网的本地实现（<c>LocalOnlyPeerTransport</c>），
/// 界面和业务逻辑都已经按这个接口写好了，
/// 以后补上真正的 P2P 实现（局域网 UDP 发现 + 直连）时，
/// 只要换一个实现类，其它代码都不用动。
/// </summary>
public interface IPeerTransport
{
    /// <summary>本节点的唯一标识。</summary>
    string LocalPeerId { get; }

    /// <summary>是否已连接到其它成员（本地模式恒为 false）。</summary>
    bool IsConnected { get; }

    /// <summary>启动（联机时在这里开始监听 / 广播）。</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>停止。</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>加入某个房间。</summary>
    Task JoinRoomAsync(string roomCode, CancellationToken cancellationToken = default);

    /// <summary>把「我正在专注」的状态广播出去。</summary>
    Task BroadcastStatusAsync(PeerStatus status, CancellationToken cancellationToken = default);

    /// <summary>收到其它成员状态时触发。</summary>
    event EventHandler<PeerStatus>? PeerStatusReceived;

    /// <summary>成员上下线时触发。</summary>
    event EventHandler<PeerPresence>? PresenceChanged;

    // ────────────── 以下是「真联机」才用得上的能力 ──────────────
    //
    // 为什么加在同一个接口里，而不是另开一个接口？
    //   因为界面和业务层只认识 IPeerTransport 这一个类型，
    //   多一个接口就多一处强转和判断。不支持的实现（比如纯本地那个）
    //   只要把 SupportsNetworking 返回 false，其余成员给个空实现就行。

    /// <summary>这个实现到底能不能联机（纯本地实现返回 false）。</summary>
    bool SupportsNetworking { get; }

    /// <summary>本机地址的文字形式，界面显示给用户抄给同学。</summary>
    string LocalAddressText { get; }

    /// <summary>监听 / 连接的端口。</summary>
    int Port { get; }

    /// <summary>连接状态变化（连上 / 断开）时触发。</summary>
    event EventHandler? ConnectionChanged;

    /// <summary>收到别人的聊天消息时触发。</summary>
    event EventHandler<PeerChatMessage>? ChatReceived;

    /// <summary>把自己标成房主（建房时调用），之后才接受别人连进来。</summary>
    void BecomeHost(string roomCode);

    /// <summary>本机是不是房主（这个房间是不是自己建的）。</summary>
    bool IsHost { get; }

    /// <summary>退出房间，断开所有连接。</summary>
    void LeaveRoom();

    /// <summary>连到某个地址（手动填 IP 就是走这里）。</summary>
    Task<bool> ConnectAsync(string host, int? port = null, CancellationToken cancellationToken = default);

    /// <summary>把一条聊天消息发给房间里所有人。</summary>
    Task SendChatAsync(string text, CancellationToken cancellationToken = default);

    // ────────────── 以下是「参考 EasyTier 做的连接质量 / 组网」能力 ──────────────
    //
    // 这几项不是必须的（纯本地实现给空值就行），加进来是为了让界面
    // 能显示"延迟多少""都有谁在哪个地址"，也方便以后做直连。

    /// <summary>已知的成员地址表（房主自己 + 各客户端）。</summary>
    IReadOnlyList<HeliumTodo.Core.Networking.PeerAddressInfo> Peers { get; }    /// <summary>地址表或延迟变化时触发。</summary>
    event EventHandler? PeersChanged;

    /// <summary>到房主（房主则是到最近回应的成员）的往返延迟，毫秒；0 表示还没测出来。</summary>
    int RoundTripMs { get; }

    /// <summary>
    /// 对外的地址（NAT / 端口映射后的样子），形如 "1.2.3.4:47470"。
    /// 对应 EasyTier 的 --mapped-listeners：填了之后告诉别人连这个地址。
    /// </summary>
    string? AdvertisedEndpoint { get; set; }
}

/// <summary>同步给别人的状态：我在专注什么、还剩多久。</summary>
public sealed record PeerStatus(
    string PeerId,
    string DisplayName,
    bool IsFocusing,
    string? FocusTitle,
    TimeSpan? Remaining);

/// <summary>成员上下线信息。</summary>
public sealed record PeerPresence(string PeerId, string DisplayName, bool IsOnline);

/// <summary>收到的一条房间内聊天消息。</summary>
public sealed record PeerChatMessage(string PeerId, string DisplayName, string Text);
