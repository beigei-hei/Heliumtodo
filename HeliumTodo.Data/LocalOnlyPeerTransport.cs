using HeliumTodo.Core.Plugins;

namespace HeliumTodo.Data;

/// <summary>
/// 一个「不联网」的网络传输实现。
///
/// 它的存在是为了让自习室的代码现在就能编译、能跑：
/// 界面和业务逻辑都以为自己在跟一个网络层打交道，
/// 实际上所有调用都被这个类静静地忽略掉了。
///
/// 以后实现真正的无中心服务器方案时：
///   1. 新写一个类，例如 <c>LanUdpPeerTransport</c>，实现同一个 <see cref="IPeerTransport"/> 接口；
///   2. 在依赖注入里把注册的这一行换掉；
///   3. 其它代码一行都不用改。
/// </summary>
public sealed class LocalOnlyPeerTransport : IPeerTransport
{
    /// <summary>本地节点的 Id：用机器名 + 随机数，保证每台机器不一样。</summary>
    public string LocalPeerId { get; } = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..16];

    /// <summary>本地模式永远不会连上别人。</summary>
    public bool IsConnected => false;

    public event EventHandler<PeerStatus>? PeerStatusReceived;
    public event EventHandler<PeerPresence>? PresenceChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // 本地模式：什么都不用做
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task JoinRoomAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        // 【暂缓】真正的联机实现会在这里开始监听 / 广播。
        // 现在直接返回，不产生任何网络行为。
        return Task.CompletedTask;
    }

    public Task BroadcastStatusAsync(PeerStatus status, CancellationToken cancellationToken = default)
    {
        // 本地模式：只把状态回抛给自己，方便界面统一处理（成员列表照样能更新）
        PeerStatusReceived?.Invoke(this, status);
        return Task.CompletedTask;
    }

    /// <summary>供测试或以后联机实现使用：手动模拟「收到了别人的状态」。</summary>
    public void SimulateIncomingStatus(PeerStatus status) => PeerStatusReceived?.Invoke(this, status);

    /// <summary>供测试或以后联机实现使用：手动模拟「有人上下线」。</summary>
    public void SimulatePresence(PeerPresence presence) => PresenceChanged?.Invoke(this, presence);
    // ────────────── 联机能力：这个实现一个都不支持 ──────────────

    /// <summary>纯本地模式不能联机。</summary>
    public bool SupportsNetworking => false;

    public string LocalAddressText => "（本地模式）";

    public int Port => 0;

    /// <summary>本地模式永远不会有连接状态变化，但事件还是留着，界面订阅了也不会出错。</summary>
    public event EventHandler? ConnectionChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<PeerChatMessage>? ChatReceived
    {
        add { }
        remove { }
    }

    /// <summary>本地模式没有别的成员，地址表永远是空的。</summary>
    public IReadOnlyList<HeliumTodo.Core.Networking.PeerAddressInfo> Peers => Array.Empty<HeliumTodo.Core.Networking.PeerAddressInfo>();

    public event EventHandler? PeersChanged
    {
        add { }
        remove { }
    }

    /// <summary>本地模式没有连接，也就没有延迟可测。</summary>
    public int RoundTripMs => 0;

    /// <summary>本地模式不需要对外地址。</summary>
    public string? AdvertisedEndpoint { get; set; }

    /// <summary>本地模式没有房主的概念。</summary>
    public bool IsHost => false;

    public void BecomeHost(string roomCode)
    {
        // 本地模式没有"房主"这回事
    }

    public void LeaveRoom()
    {
    }

    /// <summary>本地模式连不上任何地址，直接返回 false 让界面给出提示。</summary>
    public Task<bool> ConnectAsync(string host, int? port = null, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task SendChatAsync(string text, CancellationToken cancellationToken = default)
    {
        // 本地模式：聊天记录只在本地，不往外发
        return Task.CompletedTask;
    }
}
