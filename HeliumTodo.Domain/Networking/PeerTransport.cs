// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Networking;

/// <summary>
/// 网络传输接口 —— 为「自习室多人联机（无中心服务器方案）」预留的接入点。
///
/// 【当前状态：已实现局域网直连】
/// 现在有本地占位实现（<c>LocalOnlyPeerTransport</c>）和真正的局域网实现
/// （<c>LanPeerTransport</c>，房主监听、客户端直连），界面和业务逻辑都只依赖这个接口。
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
    IReadOnlyList<PeerAddressInfo> Peers { get; }

    /// <summary>地址表或延迟变化时触发。</summary>
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