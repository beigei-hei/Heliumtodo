// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeliumTodo.Domain.Networking;

/// <summary>消息类型。用字符串而不是数字，抓包 / 看日志时一眼能认出来。</summary>
public static class PeerMessageKinds
{
    /// <summary>连上之后先自报家门。</summary>
    public const string Hello = "hello";

    /// <summary>请求加入房间（客户端 → 房主）。</summary>
    public const string Join = "join";

    /// <summary>房主确认加入（房主 → 客户端），附带当前成员名单。</summary>
    public const string Welcome = "welcome";

    /// <summary>有人开始 / 结束专注，或者剩余时间变了。</summary>
    public const string Status = "status";

    /// <summary>某人上线或下线。</summary>
    public const string Presence = "presence";

    /// <summary>房间里的聊天。</summary>
    public const string Chat = "chat";

    /// <summary>要断开了。</summary>
    public const string Leave = "leave";

    /// <summary>心跳：问对方还在不在（参考 EasyTier，空闲连接要主动探活）。</summary>
    public const string Ping = "ping";

    /// <summary>心跳回应，带原样回传的时间戳用来算延迟。</summary>
    public const string Pong = "pong";

    /// <summary>房主下发的成员地址表（谁在哪、延迟多少）。</summary>
    public const string Peers = "peers";

    /// <summary>所有已知类型（解析时用来校验）。</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        Hello, Join, Welcome, Status, Presence, Chat, Leave, Ping, Pong, Peers,
    };

    /// <summary>是不是认识的类型。</summary>
    public static bool IsKnown(string? kind) => kind is not null && All.Contains(kind);
}

/// <summary>
/// 联机时在线路上流动的一条消息。
///
/// 为什么所有字段都放在一个类里，而不是每种消息一个类型？
///   这里的消息种类少、字段也少，用一个"宽"的记录配上 <see cref="Kind"/>
///   足够用了。拆成七八个子类反而要多写一堆多态序列化代码，
///   对这个规模的项目来说不划算。
/// </summary>
public sealed record PeerMessage
{
    /// <summary>消息类型，见 <see cref="PeerMessageKinds"/>。</summary>
    public string Kind { get; init; } = PeerMessageKinds.Hello;

    /// <summary>房间码（Hello 时可以为空）。</summary>
    public string RoomCode { get; init; } = "";

    /// <summary>发消息那个节点的 id。</summary>
    public string PeerId { get; init; } = "";

    /// <summary>显示名。</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>是否正在专注。</summary>
    public bool IsFocusing { get; init; }

    /// <summary>正在专注什么（没在专注时为 null）。</summary>
    public string? FocusTitle { get; init; }

    /// <summary>本次专注还剩多少秒。</summary>
    public int? RemainingSeconds { get; init; }

    /// <summary>这个人现在在不在线（Presence 消息用）。</summary>
    public bool IsOnline { get; init; } = true;

    /// <summary>聊天内容（Chat 消息用）。</summary>
    public string? Text { get; init; }

    /// <summary>发出去的时间（Unix 毫秒）。</summary>
    public long SentAtUnixMs { get; init; }

    /// <summary>
    /// 自己在哪个端口上等别人连（Hello / Join 时带上）。
    ///
    /// 为什么要专门带这个：TCP 出去的连接源端口是系统随机分配的，
    /// 跟本机的监听端口不是一个数。别人想反过来连我，必须知道我监听在哪。
    /// （EasyTier 里同样是靠节点互报监听地址来组网的。）
    /// </summary>
    public int? ListenPort { get; init; }

    /// <summary>
    /// 我对外（经过 NAT / 端口映射后）的地址，形如 "1.2.3.4:47470"。
    ///
    /// 对应 EasyTier 的 --mapped-listeners：本机监听的是一个端口，
    /// 但别人应该连的是路由器映射出去的那个。填了就优先用它。
    /// </summary>
    public string? AdvertisedEndpoint { get; init; }

    /// <summary>这一跳的往返延迟（毫秒，Pong 时带回）。</summary>
    public int? RoundTripMs { get; init; }

    /// <summary>成员地址表（Peers 消息用）。</summary>
    public List<PeerAddressInfo>? PeerList { get; init; }

    /// <summary>当前时间戳。</summary>
    public static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// 成员地址表里的一条：某个人可以从哪个地址连上、延迟多少。
///
/// 有了它，客户端就不只知道"房主在哪"，还知道"其余人在哪" ——
/// 这是 EasyTier 那种节点互相直连的基础。本项目的消息仍然走房主转发，
/// 但地址表已经交给了每个客户端（界面能看到，将来要做直连也不用改协议）。
/// </summary>
public sealed record PeerAddressInfo
{
    /// <summary>对方的节点 id。</summary>
    public string PeerId { get; init; } = "";

    /// <summary>对方的显示名。</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>对方的地址（host:port，IPv6 带方括号）。</summary>
    public string Endpoint { get; init; } = "";

    /// <summary>到对方的往返延迟（毫秒）；0 表示还没测出来。</summary>
    public int RoundTripMs { get; init; }
}

/// <summary>
/// 消息的序列化 / 反序列化。
///
/// 线路上用的是「一行一条 JSON」。选它而不是二进制，理由：
///   · 出问题时直接看日志就能读懂，不用写解析工具
///   · 用 \n 分隔，读一行就是一条消息，流式处理很简单
/// 代价是体积比二进制大一点 —— 但这只是传递"谁在专注"这类小消息，
/// 一条几百字节，完全无所谓。
/// </summary>
public static class PeerMessageCodec
{
    /// <summary>
    /// 单条消息的长度上限。
    ///
    /// 设这个上限是为了防住恶意 / 出错的对端：如果它一直不发换行符，
    /// 读缓冲会无限涨，最后把内存吃光。超过上限就当作坏消息，断开连接。
    /// </summary>
    public const int MaxLineBytes = 64 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        // 属性名用驼峰，和配置文件（AppConfig）保持一致的风格
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // 不把 null 写进去：消息里大部分字段都是可空的，
        // 全写上的话一条消息会有一半是 "xxx": null
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>序列化成一行 JSON（不含换行符；调用方负责加分隔）。</summary>
    public static string Serialize(PeerMessage message)
    {
        var json = JsonSerializer.Serialize(message, Options);

        // JsonSerializer 默认会把字符转义，所以 json 里不会出现真正的换行。
        // 这里再保险一下：万一哪天有人关了转义，换行会直接把协议搞乱。
        return json.Replace("\r", "").Replace("\n", " ");
    }

    /// <summary>从一行 JSON 解析出消息；认不出来就返回 null（不抛异常）。</summary>
    public static PeerMessage? TryParse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        if (line.Length > MaxLineBytes) return null;

        PeerMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<PeerMessage>(line, Options);
        }
        catch (JsonException)
        {
            // 对端发来的东西不可信，坏了就当没收到，不能让一条坏消息弄崩整个应用
            return null;
        }

        if (message is null) return null;

        // 类型必须认识，否则后面的逻辑没法处理
        if (!PeerMessageKinds.IsKnown(message.Kind)) return null;

        // 除了聊天，其余消息都必须带 peerId，否则不知道是谁
        if (message.Kind != PeerMessageKinds.Chat && string.IsNullOrWhiteSpace(message.PeerId)) return null;

        return message;
    }

    // ────────────── 和 Core 里已有的类型互相转换 ──────────────

    /// <summary>把一条状态消息转成 <see cref="PeerStatus"/>（业务层只认这个）。</summary>
    public static PeerStatus? ToPeerStatus(PeerMessage message)
    {
        if (message.Kind != PeerMessageKinds.Status && message.Kind != PeerMessageKinds.Hello) return null;

        if (string.IsNullOrWhiteSpace(message.PeerId)) return null;

        TimeSpan? remaining = message.RemainingSeconds is { } seconds
            ? TimeSpan.FromSeconds(Math.Max(0, seconds))
            : null;

        return new PeerStatus(
            message.PeerId,
            message.DisplayName,
            message.IsFocusing,
            message.FocusTitle,
            remaining);
    }

    /// <summary>把业务层的状态打包成一条消息。</summary>
    public static PeerMessage FromPeerStatus(PeerStatus status, string roomCode, string? kind = null) => new()
    {
        Kind = kind ?? PeerMessageKinds.Status,
        RoomCode = roomCode,
        PeerId = status.PeerId,
        DisplayName = status.DisplayName,
        IsFocusing = status.IsFocusing,
        FocusTitle = status.FocusTitle,
        RemainingSeconds = status.Remaining is { } remaining ? (int)Math.Max(0, remaining.TotalSeconds) : null,
        SentAtUnixMs = PeerMessage.NowUnixMs(),
    };

    /// <summary>把成员地址表打包成一条消息（房主 → 客户端）。</summary>
    public static PeerMessage FromPeerList(string roomCode, string peerId, IEnumerable<PeerAddressInfo> peers) => new()
    {
        Kind = PeerMessageKinds.Peers,
        RoomCode = roomCode,
        PeerId = peerId,
        PeerList = peers.ToList(),
        SentAtUnixMs = PeerMessage.NowUnixMs(),
    };

    /// <summary>把上线 / 下线打包成一条消息。</summary>
    public static PeerMessage FromPeerPresence(PeerPresence presence, string roomCode) => new()
    {
        Kind = PeerMessageKinds.Presence,
        RoomCode = roomCode,
        PeerId = presence.PeerId,
        DisplayName = presence.DisplayName,
        IsOnline = presence.IsOnline,
        SentAtUnixMs = PeerMessage.NowUnixMs(),
    };

    /// <summary>从在线状态消息里取出 <see cref="PeerPresence"/>。</summary>
    public static PeerPresence? ToPeerPresence(PeerMessage message)
    {
        if (message.Kind != PeerMessageKinds.Presence) return null;

        if (string.IsNullOrWhiteSpace(message.PeerId)) return null;

        return new PeerPresence(message.PeerId, message.DisplayName, message.IsOnline);
    }
}
