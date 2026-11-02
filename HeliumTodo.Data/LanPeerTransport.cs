using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using HeliumTodo.Core.Networking;
using HeliumTodo.Core.Plugins;

namespace HeliumTodo.Data;

/// <summary>
/// 局域网联机用的传输层实现（**无中心服务器**）。
///
/// 【拓扑：星形，由房主转发】
///   自习室没有服务器，最省事的做法是让"建房的人"临时当一下转发站：
///
///        客户端 A ──┐
///        客户端 B ──┼──→ 房主 ──→ 把消息转给其它所有人
///        客户端 C ──┘
///
///   房主本身也是一个普通节点（他也在这间自习室里、也在专注），
///   只是额外承担转发。这跟"中心服务器"不是一回事：
///   房主关掉窗口，这间房就散了，没有任何数据留在第三方机器上。
///
/// 【地址：IPv4 与 IPv6 双栈】
///   监听用 <see cref="IPAddress.IPv6Any"/> + <c>DualMode = true</c>，
///   这样一个监听端口同时收 IPv4 和 IPv6 的连接。
///   用户手动填地址时两种写法都能识别（见 <see cref="PeerEndpoint"/>）。
///
/// 【发现：UDP 广播 + 手动填 IP】
///   房主每 3 秒往局域网广播一条"我这有间自习室叫什么、房间码多少、端口多少"，
///   其它人收到就显示在"发现的房间"列表里。
///   广播只在同一网段内有效（路由器不转发广播），跨网段 / 跨设备热点
///   就只能手动填 IP —— 两种方式并存，这也是题目里要求"可手动填 IP"的原因。
/// </summary>
public sealed class LanPeerTransport : IPeerTransport, IAsyncDisposable
{
    /// <summary>把状态打包发给业务层时用的 id。</summary>
    private readonly string _localPeerId =
        $"{Environment.MachineName}-{Guid.NewGuid():N}"[..24];

    private readonly int _port;
    private readonly string _displayName;

    /// <summary>监听器（房主用）。没建房时不监听。</summary>
    private TcpListener? _listener;

    private CancellationTokenSource? _cts;

    /// <summary>连到房主的那条连接（客户端用）。</summary>
    private PeerSession? _hostSession;

    /// <summary>连到我的那些连接（房主用）。</summary>
    private readonly List<PeerSession> _clientSessions = new();

    /// <summary>保护上面两个字段的锁（网络回调在别的线程上跑）。</summary>
    private readonly object _sessionsLock = new();

    /// <summary>UDP 广播 / 发现用的套接字。</summary>
    private UdpClient? _udp;

    /// <summary>发现的其它自习室（房间码 → 信息）。</summary>
    private readonly ConcurrentDictionary<string, DiscoveredRoom> _discovered = new();

    /// <summary>定时广播、清理过期发现的循环。</summary>
    private Task? _announceLoop;

    /// <summary>心跳 / 保活 / 测延迟的循环。</summary>
    private Task? _heartbeatLoop;

    /// <summary>到房主的往返延迟（毫秒）。0 表示还没测出来。</summary>
    private int _roundTripMs;

    /// <summary>对外的地址（NAT 映射后用），对应 EasyTier 的 mapped listener。</summary>
    private string? _advertisedEndpoint;

    /// <summary>房主下发给客户端的成员地址表缓存。</summary>
    private volatile List<PeerAddressInfo> _peerListCache = new();

    /// <summary>当前房间码（不在房间时为空）。</summary>
    private string _roomCode = "";

    /// <summary>本机是不是房主。</summary>
    private bool _isHost;

    // ────────────── IPeerTransport ──────────────

    public string LocalPeerId => _localPeerId;

    /// <summary>这个实现支持真联机。</summary>
    public bool SupportsNetworking => true;

    public bool IsConnected
    {
        get
        {
            lock (_sessionsLock)
            {
                return _hostSession is not null || _clientSessions.Count > 0;
            }
        }
    }

    /// <summary>出错时的提示（界面显示用）。</summary>
    public string? LastError { get; private set; }

    /// <summary>本机监听的端口。</summary>
    public int Port => _port;

    /// <summary>本机地址的文字形式（界面显示"把这串发给同学"）。</summary>
    public string LocalAddressText
    {
        get
        {
            var addresses = GetLocalAddresses();

            if (addresses.Count == 0) return "（没找到可用网卡）";

            // 网卡多的时候地址会长得吓人（虚拟网卡尤其多），最多显示 4 个。
            // IPv4 在前是因为同一个路由器下它最常用。
            if (addresses.Count > 4)
            {
                addresses = addresses.Take(4).ToList();
            }

            return string.Join(" / ", addresses);
        }
    }

    /// <summary>是不是房主。</summary>
    public bool IsHost => _isHost;

    /// <summary>到房主的往返延迟（毫秒）。</summary>
    public int RoundTripMs => _roundTripMs;

    /// <summary>对外地址（填了就优先告诉别人这个）。</summary>
    public string? AdvertisedEndpoint
    {
        get => _advertisedEndpoint;
        set => _advertisedEndpoint = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 成员地址表。
    ///
    /// 房主自己算：它手上有所有客户端连接；客户端用房主下发的 Peers 消息。
    /// 两边都以"别人的地址"为口径，不含自己 —— 界面上"我"是单独一块。
    /// </summary>
    public IReadOnlyList<PeerAddressInfo> Peers
    {
        get
        {
            if (!_isHost) return _peerListCache;

            var list = new List<PeerAddressInfo>();

            lock (_sessionsLock)
            {
                foreach (var session in _clientSessions)
                {
                    if (string.IsNullOrEmpty(session.PeerId)) continue;

                    list.Add(new PeerAddressInfo
                    {
                        PeerId = session.PeerId,
                        DisplayName = session.DisplayName,
                        Endpoint = DescribeEndpoint(session),
                        RoundTripMs = session.RoundTripMs,
                    });
                }
            }

            return list;
        }
    }

    /// <summary>地址表 / 延迟变化。</summary>
    public event EventHandler? PeersChanged;

    /// <summary>
    /// 把一个 session 描述成 "host:port"。
    ///
    /// 优先用对端自报的对外地址（NAT 场景），其次用它自报的监听端口
    /// 配它的来源 IP。都对不上就只给个 IP —— 至少界面上能看见人。
    /// </summary>
    private string DescribeEndpoint(PeerSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.AdvertisedEndpoint))
        {
            return session.AdvertisedEndpoint;
        }

        if (session.ListenPort is { } port && port > 0 && !string.IsNullOrEmpty(session.RemoteAddress))
        {
            return new PeerEndpoint(session.RemoteAddress, port).ToString();
        }

        return session.RemoteAddress;
    }

    /// <summary>当前在线成员数（含自己）。</summary>
    public int OnlinePeerCount
    {
        get
        {
            lock (_sessionsLock)
            {
                // 自己算一个，加上每条连接对应的人
                return 1 + (_hostSession is not null ? 1 : 0) + _clientSessions.Count;
            }
        }
    }

    public event EventHandler<PeerStatus>? PeerStatusReceived;

    public event EventHandler<PeerPresence>? PresenceChanged;

    /// <summary>发现的房间列表变化时触发。</summary>
    public event EventHandler? DiscoveryChanged;

    /// <summary>连接状态变化时触发（界面据此刷新按钮可用性）。</summary>
    public event EventHandler? ConnectionChanged;

    public LanPeerTransport(int port = PeerEndpoint.DefaultPort, string? displayName = null)
    {
        _port = port;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? Environment.MachineName : displayName.Trim();
    }

    /// <summary>启动：开始监听 + 开始收发广播。</summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        StartListener();
        StartUdp();

        _announceLoop = Task.Run(() => AnnounceLoopAsync(_cts.Token));
        _heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(_cts.Token));

        return Task.CompletedTask;
    }

    /// <summary>停止：断开所有连接、关掉监听和广播。</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {

        var cts = _cts;

        _cts = null;

        if (cts is not null)
        {
            try
            {
                await cts.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
                // 已经取消过了，忽略
            }
        }

        if (_announceLoop is not null)
        {
            try
            {
                await _announceLoop;
            }
            catch (OperationCanceledException)
            {
                // 正常退出
            }

            _announceLoop = null;
        }

        CloseAllSessions();

        _listener?.Stop();
        _listener = null;

        _udp?.Dispose();
        _udp = null;

        _discovered.Clear();

        RaiseConnectionChanged();
    }

    /// <summary>
    /// 建房：成为房主，开始接受别人连进来。
    ///
    /// 这里只是记住房间码 —— 别人连进来时会带着房间码，对不上就拒绝。
    /// </summary>
    public async Task JoinRoomAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken);

        _roomCode = string.IsNullOrWhiteSpace(roomCode) ? "" : roomCode.Trim().ToUpperInvariant();

        // 客户端模式下 JoinRoom 只记房间码，真正的连接由 ConnectAsync 发起
        RaiseConnectionChanged();
    }

    /// <summary>把自己标成房主（建房时调用）。</summary>
    public void BecomeHost(string roomCode)
    {
        _roomCode = string.IsNullOrWhiteSpace(roomCode) ? "" : roomCode.Trim().ToUpperInvariant();
        _isHost = true;

        RaiseConnectionChanged();
    }

    /// <summary>退出房间（断开所有连接，但保留监听）。</summary>
    public void LeaveRoom()
    {
        CloseAllSessions();

        _roomCode = "";
        _isHost = false;

        RaiseConnectionChanged();
    }

    /// <summary>把状态广播出去。</summary>
    public async Task BroadcastStatusAsync(PeerStatus status, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return;

        var message = PeerMessageCodec.FromPeerStatus(status, _roomCode);

        await SendToEveryPeerAsync(message, excludePeerId: status.PeerId, cancellationToken);
    }

    /// <summary>发一条聊天消息。</summary>
    public async Task SendChatAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(text)) return;

        var message = new PeerMessage
        {
            Kind = PeerMessageKinds.Chat,
            RoomCode = _roomCode,
            PeerId = _localPeerId,
            DisplayName = _displayName,
            Text = text.Trim(),
            SentAtUnixMs = PeerMessage.NowUnixMs(),
        };

        await SendToEveryPeerAsync(message, excludePeerId: null, cancellationToken);
    }

    /// <summary>收到别人聊天时触发（业务层挂这个事件）。</summary>
    public event EventHandler<PeerChatMessage>? ChatReceived;

    /// <summary>
    /// 手动连接到某个地址（局域网里手动填 IP 就是走这里）。
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int? port = null, CancellationToken cancellationToken = default)
    {
        if (!PeerEndpoint.TryParse(port is { } p ? $"{host}:{p}" : host, out var endpoint))
        {
            LastError = $"地址看不懂：{host}";
            RaiseConnectionChanged();
            return false;
        }

        await StartAsync(cancellationToken);

        try
        {
            var client = new TcpClient();

            // DualMode：允许用同一个 TcpClient 连 IPv4 和 IPv6 地址，
            // 不用先判断用户填的是哪种
            client.Client.DualMode = true;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(TimeSpan.FromSeconds(6));

            await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token);

            var session = new PeerSession(client, endpoint);

            // 先自报家门，再说"我要加入这个房间"
            await session.SendAsync(new PeerMessage
            {
                Kind = PeerMessageKinds.Hello,
                PeerId = _localPeerId,
                DisplayName = _displayName,
                RoomCode = _roomCode,
                ListenPort = _port,
                AdvertisedEndpoint = _advertisedEndpoint,
                SentAtUnixMs = PeerMessage.NowUnixMs(),
            }, cancellationToken);

            await session.SendAsync(new PeerMessage
            {
                Kind = PeerMessageKinds.Join,
                PeerId = _localPeerId,
                DisplayName = _displayName,
                RoomCode = _roomCode,
                ListenPort = _port,
                AdvertisedEndpoint = _advertisedEndpoint,
                SentAtUnixMs = PeerMessage.NowUnixMs(),
            }, cancellationToken);

            lock (_sessionsLock)
            {
                _hostSession = session;
            }

            LastError = null;

            _ = Task.Run(() => ReadSessionAsync(session, isHostSideSession: false));

            RaiseConnectionChanged();
            return true;
        }
        catch (OperationCanceledException)
        {
            LastError = $"连接 {endpoint} 超时";
        }
        catch (SocketException ex)
        {
            LastError = $"连不上 {endpoint}（{ex.SocketErrorCode}）";
        }
        catch (Exception ex)
        {
            LastError = $"连接 {endpoint} 失败：{ex.Message}";
        }

        RaiseConnectionChanged();
        return false;
    }

    /// <summary>当前发现的房间（同一网段广播收到的）。</summary>
    public IReadOnlyList<DiscoveredRoom> DiscoveredRooms =>
        _discovered.Values.OrderByDescending(r => r.LastSeenUtc).ToList();

    // ────────────── 监听 ──────────────

    private void StartListener()
    {
        try
        {
            // 双栈监听：IPv6Any + DualMode 之后，IPv4 的连接也会被这个监听器收下
            var listener = new TcpListener(IPAddress.IPv6Any, _port);

            listener.Server.DualMode = true;

            // 允许端口复用：老进程刚退出时端口可能还在 TIME_WAIT 里
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            listener.Start();

            _listener = listener;
        }
        catch (SocketException)
        {
            // 有些系统 / 环境不放行 IPv6，那就退回只监听 IPv4
            try
            {
                var fallback = new TcpListener(IPAddress.Any, _port);

                fallback.Start();

                _listener = fallback;
            }
            catch (SocketException ex)
            {
                LastError = $"端口 {_port} 被占用（{ex.SocketErrorCode}），联机功能不可用";
                _listener = null;
                return;
            }
        }

        _ = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        var listener = _listener;
        var token = _cts?.Token ?? CancellationToken.None;


        if (listener is null) return;

        while (!token.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;

            try
            {

                // 同步 accept 包在线程池里跑：AcceptTcpClientAsync 在这台机器上
                // 会「连接已经进来但 await 一直不返回」，同步版本没这个问题。
                client = await Task.Run(listener.AcceptTcpClient, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // 单次接受失败不影响之后的接受，继续等下一个
                continue;
            }

            // ⚠️ 千万别在这里对「已经接受」的连接再设 DualMode。
            //    以前有这段代码，结果是 new PeerSession(...) 里的
            //    client.GetStream() 抛 InvalidOperationException
            //    「The operation is not allowed on non-connected sockets」；
            //    异常又被外层 Task.Run 吞掉，表现成
            //    「客户端显示已连接、房主那边毫无反应」。
            //    连接早就建立了，改 DualMode 没有任何意义。

            var session = new PeerSession(client, null);


            lock (_sessionsLock)
            {
                _clientSessions.Add(session);
            }

            _ = Task.Run(() => ReadSessionAsync(session, isHostSideSession: true));
        }
    }

    // ────────────── 收消息 ──────────────

    private async Task ReadSessionAsync(PeerSession session, bool isHostSideSession)
    {
        try
        {
            while (true)
            {
                var line = await session.ReadLineAsync();

                if (line is null) break;      // 对端关闭了

                var message = PeerMessageCodec.TryParse(line);

                if (message is null) continue;   // 坏消息直接跳过

                // 收到任何一条消息就算"他还活着"（心跳超时判断靠它）
                session.LastSeenUtc = DateTime.UtcNow;

                await HandleMessageAsync(session, message, isHostSideSession);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // 连接断了，走下面的清理
        }
        finally
        {
            await DropSessionAsync(session, isHostSideSession);
        }
    }

    private async Task HandleMessageAsync(PeerSession session, PeerMessage message, bool isHostSideSession)
    {
        switch (message.Kind)
        {
            case PeerMessageKinds.Hello:
                session.PeerId = message.PeerId;
                session.DisplayName = message.DisplayName;
                session.ListenPort = message.ListenPort;
                session.AdvertisedEndpoint = message.AdvertisedEndpoint;

                // 告诉业务层"有人来了"
                RaisePresence(new PeerPresence(message.PeerId, message.DisplayName, true));

                // Hello 里也带了"我正在专注什么"，一并交给业务层，
                // 这样刚连上就能看到对方的状态，不用等对方主动推一次
                if (PeerMessageCodec.ToPeerStatus(message) is { } helloStatus)
                {
                    PeerStatusReceived?.Invoke(this, helloStatus);
                }

                break;

            case PeerMessageKinds.Join:
                session.PeerId = message.PeerId;
                session.DisplayName = message.DisplayName;
                session.ListenPort = message.ListenPort;
                session.AdvertisedEndpoint = message.AdvertisedEndpoint;

                if (isHostSideSession && !string.IsNullOrEmpty(_roomCode)
                    && !string.Equals(message.RoomCode, _roomCode, StringComparison.OrdinalIgnoreCase))
                {
                    // 房间码对不上，礼貌地拒绝
                    await session.SendAsync(new PeerMessage
                    {
                        Kind = PeerMessageKinds.Leave,
                        PeerId = _localPeerId,
                        Text = "房间码不对",
                        SentAtUnixMs = PeerMessage.NowUnixMs(),
                    }, CancellationToken.None);

                    session.Close();
                    return;
                }

                RaisePresence(new PeerPresence(message.PeerId, message.DisplayName, true));
                RaiseConnectionChanged();

                // 房主把自己的状态回给对方，让对方立刻看到"房主在专注什么"
                if (isHostSideSession)
                {
                    await session.SendAsync(new PeerMessage
                    {
                        Kind = PeerMessageKinds.Hello,
                        PeerId = _localPeerId,
                        DisplayName = _displayName,
                        RoomCode = _roomCode,
                        ListenPort = _port,
                        AdvertisedEndpoint = _advertisedEndpoint,
                        SentAtUnixMs = PeerMessage.NowUnixMs(),
                    }, CancellationToken.None);

                    // 再把当前成员地址表推给他，并广播给所有人（新成员进来自报地址了）
                    await SendPeerListAsync(session);
                    await BroadcastPeerListAsync(CancellationToken.None);
                }

                break;

            case PeerMessageKinds.Ping:
                // 原样回传时间戳，对面拿 (现在 - 发出去的时刻) 就是这一跳的延迟
                await session.SendAsync(new PeerMessage
                {
                    Kind = PeerMessageKinds.Pong,
                    PeerId = _localPeerId,
                    DisplayName = _displayName,
                    RoomCode = _roomCode,
                    SentAtUnixMs = message.SentAtUnixMs,
                }, CancellationToken.None);

                break;

            case PeerMessageKinds.Pong:
                session.RoundTripMs = (int)Math.Clamp(PeerMessage.NowUnixMs() - message.SentAtUnixMs, 0, 60_000);

                // 客户端只关心"到房主多少毫秒"；房主侧记最近一次测到的值
                if (_hostSession is not null && ReferenceEquals(session, _hostSession))
                {
                    _roundTripMs = session.RoundTripMs;
                }

                PeersChanged?.Invoke(this, EventArgs.Empty);

                break;

            case PeerMessageKinds.Peers:
                // 房主下发的成员地址表 —— 客户端存下来给界面看
                _peerListCache = message.PeerList ?? new List<PeerAddressInfo>();

                PeersChanged?.Invoke(this, EventArgs.Empty);

                break;

            case PeerMessageKinds.Status:
            {
                var status = PeerMessageCodec.ToPeerStatus(message);

                if (status is not null)
                {
                    PeerStatusReceived?.Invoke(this, status);
                }

                // 房主负责转发给其它客户端
                if (isHostSideSession)
                {
                    await RelayToOtherClientsAsync(message, exceptSession: session);
                }

                break;
            }

            case PeerMessageKinds.Presence:
            {
                var presence = PeerMessageCodec.ToPeerPresence(message);

                if (presence is not null)
                {
                    RaisePresence(presence);
                }

                if (isHostSideSession)
                {
                    await RelayToOtherClientsAsync(message, exceptSession: session);
                }

                break;
            }

            case PeerMessageKinds.Chat:
                ChatReceived?.Invoke(this, new PeerChatMessage(
                    message.PeerId,
                    string.IsNullOrWhiteSpace(message.DisplayName) ? "同学" : message.DisplayName,
                    message.Text ?? ""));

                if (isHostSideSession)
                {
                    await RelayToOtherClientsAsync(message, exceptSession: session);
                }

                break;

            case PeerMessageKinds.Leave:
                session.Close();
                break;
        }
    }

    private async Task RelayToOtherClientsAsync(PeerMessage message, PeerSession exceptSession)
    {
        List<PeerSession> targets;

        lock (_sessionsLock)
        {
            targets = _clientSessions.Where(s => !ReferenceEquals(s, exceptSession)).ToList();
        }

        foreach (var target in targets)
        {
            try
            {
                await target.SendAsync(message, CancellationToken.None);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // 这条连接坏了，交给它的读循环去清理
            }
        }
    }

    private async Task SendToEveryPeerAsync(PeerMessage message, string? excludePeerId, CancellationToken cancellationToken)
    {
        List<PeerSession> targets;

        lock (_sessionsLock)
        {
            targets = _clientSessions.ToList();

            if (_hostSession is not null)
            {
                targets.Add(_hostSession);
            }
        }

        foreach (var target in targets)
        {
            if (excludePeerId is not null && string.Equals(target.PeerId, excludePeerId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                await target.SendAsync(message, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // 忽略，读循环会负责清理
            }
        }
    }

    private Task DropSessionAsync(PeerSession session, bool isHostSideSession)
    {
        var removed = false;

        lock (_sessionsLock)
        {
            if (ReferenceEquals(_hostSession, session))
            {
                _hostSession = null;
                removed = true;
            }

            if (_clientSessions.Remove(session))
            {
                removed = true;
            }
        }

        session.Close();

        if (removed && !string.IsNullOrEmpty(session.PeerId))
        {
            RaisePresence(new PeerPresence(session.PeerId, session.DisplayName, false));
        }

        RaiseConnectionChanged();

        // 成员表变了：房主要重新推地址表，界面也要刷新"成员/延迟"
        if (removed)
        {
            PeersChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    private void CloseAllSessions()
    {
        List<PeerSession> all;

        lock (_sessionsLock)
        {
            all = _clientSessions.ToList();

            if (_hostSession is not null)
            {
                all.Add(_hostSession);
            }

            _clientSessions.Clear();
            _hostSession = null;
        }

        foreach (var session in all)
        {
            session.Close();
        }
    }

    // ────────────── UDP 广播 / 发现 ──────────────

    private void StartUdp()
    {
        try
        {
            var udp = new UdpClient();

            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, _port));

            _udp = udp;

            _ = Task.Run(() => UdpReceiveLoopAsync(udp));
        }
        catch (SocketException ex)
        {
            // 广播不是必须的：坏了就只剩"手动填 IP"这一条路，仍然可用
            LastError = $"广播端口 {_port} 不可用（{ex.SocketErrorCode}），只能手动填 IP";
            _udp = null;
        }
    }

    private async Task UdpReceiveLoopAsync(UdpClient udp)
    {
        var token = _cts?.Token ?? CancellationToken.None;

        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult result;

            try
            {
                result = await udp.ReceiveAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            try
            {
                var text = Encoding.UTF8.GetString(result.Buffer);

                if (!text.StartsWith(Announcement.Prefix, StringComparison.Ordinal)) continue;

                var announcement = Announcement.TryParse(text[Announcement.Prefix.Length..]);

                if (announcement is null) continue;

                // 自己的广播自己也会收到，跳过（不然"发现的房间"里会出现自己）
                if (string.Equals(announcement.PeerId, _localPeerId, StringComparison.Ordinal)) continue;

                _discovered[announcement.RoomCode] = new DiscoveredRoom(
                    announcement.RoomCode,
                    announcement.RoomName,
                    announcement.HostName,
                    announcement.PeerId,
                    // 广播包里的端口才是对方真正的监听端口
                    new PeerEndpoint(result.RemoteEndPoint.Address.ToString(), announcement.Port),
                    DateTime.UtcNow);

                DiscoveryChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // 广播内容不可信，坏了就丢掉
            }
        }
    }

    private async Task AnnounceLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // 只有房主才广播"我这有房间"
                if (_isHost && !string.IsNullOrEmpty(_roomCode) && _udp is not null)
                {
                    var payload = Announcement.Prefix + new Announcement(
                        _localPeerId,
                        _displayName,
                        _roomCode,
                        $"{_displayName} 的自习室",
                        _port).ToJson();

                    var bytes = Encoding.UTF8.GetBytes(payload);

                    foreach (var broadcast in GetBroadcastAddresses())
                    {
                        try
                        {
                            await _udp.SendAsync(bytes, new IPEndPoint(broadcast, _port), token);
                        }
                        catch (SocketException)
                        {
                            // 某个网卡发不出去就算了，其它网卡继续
                        }
                    }
                }

                // 顺手清掉超过 10 秒没再听到的房间
                var deadline = DateTime.UtcNow.AddSeconds(-10);

                foreach (var pair in _discovered.ToArray())
                {
                    if (pair.Value.LastSeenUtc < deadline && _discovered.TryRemove(pair.Key, out _))
                    {
                        DiscoveryChanged?.Invoke(this, EventArgs.Empty);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // 广播循环不能因为一次异常就整个死掉
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <summary>本机所有可用网卡的 IPv4 地址。</summary>
    private static List<string> GetLocalAddresses()
    {
        var list = new List<string>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;

                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

                foreach (var info in nic.GetIPProperties().UnicastAddresses)
                {
                    var address = info.Address;

                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        list.Add(address.ToString());
                        continue;
                    }

                    // IPv6 也列出来 —— 用户明确要求支持 IPv6 直连。
                    // 但链路本地地址（fe80:: 开头）要跳过：
                    // 它必须带上网卡编号（fe80::1%12）才能用，
                    // 而地址解析那边不接受 % 后缀，贴给同学也连不上。
                    // 同一网段内用 IPv4 就够，跨网段才需要 IPv6。
                    if (address.AddressFamily == AddressFamily.InterNetworkV6 &&
                        address.IsIPv6LinkLocal == false &&
                        address.IsIPv6Multicast == false)
                    {
                        list.Add($"[{address}]");
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
            // 拿不到网卡信息（权限 / 驱动问题），返回空列表即可
        }

        return list;
    }

    /// <summary>
    /// 算出要往哪些广播地址发。
    ///
    /// 这里除了 255.255.255.255（受限广播）之外，还会按每块网卡的
    /// 子网掩码算出"定向广播地址"（例如 192.168.1.255）。
    /// 原因：Windows 上受限广播经常被防火墙拦掉，定向广播成功率更高。
    /// </summary>
    private static List<IPAddress> GetBroadcastAddresses()
    {
        var list = new List<IPAddress> { IPAddress.Broadcast };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;

                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

                foreach (var info in nic.GetIPProperties().UnicastAddresses)
                {
                    var address = info.Address;

                    if (address.AddressFamily != AddressFamily.InterNetwork) continue;

                    var mask = info.IPv4Mask;

                    if (mask is null || mask.Equals(IPAddress.Any)) continue;

                    var addressBytes = address.GetAddressBytes();
                    var maskBytes = mask.GetAddressBytes();

                    if (addressBytes.Length != 4 || maskBytes.Length != 4) continue;

                    var broadcastBytes = new byte[4];

                    for (var i = 0; i < 4; i++)
                    {
                        broadcastBytes[i] = (byte)(addressBytes[i] | (maskBytes[i] ^ 0xFF));
                    }

                    var computed = new IPAddress(broadcastBytes);

                    if (!list.Contains(computed))
                    {
                        list.Add(computed);
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
            // 忽略，只发受限广播
        }

        return list;
    }

    // ────────────── 心跳 / 保活 / 测延迟（参考 EasyTier）──────────────

    /// <summary>心跳间隔。局域网上 4 秒一次完全无压力，延迟数字也比较及时。</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(4);

    /// <summary>
    /// 多久没收到任何消息就判定这条连接死了。
    ///
    /// 为什么要这个：拔网线、断电、休眠，TCP 都不会立刻告诉我们对端没了，
    /// 那条连接会一直躺在列表里，成员面板上会显示一个"幽灵"。EasyTier 里
    /// 同样靠心跳 + 超时来清理这种节点。
    /// </summary>
    private static readonly TimeSpan PeerIdleTimeout = TimeSpan.FromSeconds(15);

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(HeartbeatInterval, token);

                var targets = new List<(PeerSession Session, bool IsHostSide)>();

                lock (_sessionsLock)
                {
                    foreach (var session in _clientSessions) targets.Add((session, true));

                    if (_hostSession is not null) targets.Add((_hostSession, false));
                }

                if (targets.Count == 0) continue;

                var stale = new List<(PeerSession Session, bool IsHostSide)>();

                foreach (var (session, isHostSide) in targets)
                {
                    if (DateTime.UtcNow - session.LastSeenUtc > PeerIdleTimeout)
                    {
                        stale.Add((session, isHostSide));
                        continue;
                    }

                    try
                    {
                        await session.SendAsync(new PeerMessage
                        {
                            Kind = PeerMessageKinds.Ping,
                            PeerId = _localPeerId,
                            DisplayName = _displayName,
                            RoomCode = _roomCode,
                            SentAtUnixMs = PeerMessage.NowUnixMs(),
                        }, token);
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                    {
                        stale.Add((session, isHostSide));
                    }
                }

                foreach (var (session, isHostSide) in stale)
                {
                    await DropSessionAsync(session, isHostSide);
                }

                // 房主顺手把刷新过的延迟推给所有人（界面上的"延迟 12 毫秒"就是它）
                if (_isHost)
                {
                    await BroadcastPeerListAsync(token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
    }

    /// <summary>把成员地址表推给某一个连接。</summary>
    private async Task SendPeerListAsync(PeerSession session)
    {
        try
        {
            await session.SendAsync(BuildPeerListMessage(), CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // 对方已经走了，让读循环去清理
        }
    }

    /// <summary>把成员地址表推给所有客户端。</summary>
    private async Task BroadcastPeerListAsync(CancellationToken token)
    {
        List<PeerSession> clients;

        lock (_sessionsLock)
        {
            clients = _clientSessions.ToList();
        }

        if (clients.Count == 0) return;

        var message = BuildPeerListMessage();

        foreach (var session in clients)
        {
            try
            {
                await session.SendAsync(message, token);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // 同上，交给读循环
            }
        }
    }

    /// <summary>
    /// 组一条地址表消息：自己 + 所有客户端。
    ///
    /// 为什么要带自己：客户端知道"房主在哪"很关键（断线要重连、
    /// 界面要显示延迟），而房主自己的那份 Peers 是不含自己的，
    /// 所以在这里显式补上。
    /// </summary>
    private PeerMessage BuildPeerListMessage()
    {
        var peers = new List<PeerAddressInfo>
        {
            new()
            {
                PeerId = _localPeerId,
                DisplayName = _displayName,
                Endpoint = _advertisedEndpoint ?? BuildLocalEndpoint(),
                RoundTripMs = _roundTripMs,
            },
        };

        peers.AddRange(Peers);

        return PeerMessageCodec.FromPeerList(_roomCode, _localPeerId, peers);
    }

    /// <summary>把本机第一个可用地址拼成 "host:port"，给地址表兜底用。</summary>
    private string BuildLocalEndpoint()
    {
        var addresses = GetLocalAddresses();

        return addresses.Count == 0
            ? new PeerEndpoint("127.0.0.1", _port).ToString()
            : new PeerEndpoint(addresses[0], _port).ToString();
    }

    private void RaisePresence(PeerPresence presence) => PresenceChanged?.Invoke(this, presence);

    private void RaiseConnectionChanged() => ConnectionChanged?.Invoke(this, EventArgs.Empty);

    public async ValueTask DisposeAsync() => await StopAsync();

    // ────────────── 辅助类型 ──────────────

    /// <summary>一条 TCP 连接。读写各用一把锁，保证不会有两段消息交错写在一起。</summary>
    private sealed class PeerSession
    {
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public string PeerId { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public PeerEndpoint Remote { get; }

        /// <summary>对方监听的端口（Hello / Join 里自报的）。</summary>
        public int? ListenPort { get; set; }

        /// <summary>对方对外公布的地址（NAT 映射后的）。</summary>
        public string? AdvertisedEndpoint { get; set; }

        /// <summary>这条连接上最近一次收到消息的时间（探活用）。</summary>
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

        /// <summary>这条连接的往返延迟（毫秒）。</summary>
        public int RoundTripMs { get; set; }

        /// <summary>对方的 IP（房主侧用它拼"连我"的地址）。</summary>
        public string RemoteAddress { get; }

        public PeerSession(TcpClient client, PeerEndpoint? remote)
        {
            _client = client;
            Remote = remote ?? default;

            // 房主侧没有 Remote（是对方连进来的），地址得从套接字上取。
            // IPv4 映射到 IPv6 的那种地址（::ffff:192.168.1.5）要还原成 IPv4，
            // 否则拼出来的地址既长又不好看，用户还得照着输。
            RemoteAddress = client.Client.RemoteEndPoint is IPEndPoint remotePoint
                ? (remotePoint.Address.IsIPv4MappedToIPv6
                    ? remotePoint.Address.MapToIPv4().ToString()
                    : remotePoint.Address.ToString())
                : "";

            var stream = client.GetStream();

            _reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        }

        public async Task<string?> ReadLineAsync()
        {
            // StreamReader.ReadLineAsync 自带换行切分，正好对上"一行一条消息"的协议
            return await _reader.ReadLineAsync().ConfigureAwait(false);
        }

        public async Task SendAsync(PeerMessage message, CancellationToken cancellationToken)
        {
            var line = PeerMessageCodec.Serialize(message);

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Close()
        {
            try
            {
                _client.Close();
            }
            catch (Exception)
            {
                // 已经关了
            }
        }
    }

    /// <summary>广播包的内容。</summary>
    private sealed record Announcement(string PeerId, string HostName, string RoomCode, string RoomName, int Port)
    {
        public const string Prefix = "HELIUM-ROOM ";

        public string ToJson() => System.Text.Json.JsonSerializer.Serialize(
            this,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            });

        public static Announcement? TryParse(string json)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<Announcement>(
                    json,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    });
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }
}

/// <summary>通过广播发现的一个自习室。</summary>
public sealed record DiscoveredRoom(
    string RoomCode,
    string RoomName,
    string HostName,
    string PeerId,
    PeerEndpoint Endpoint,
    DateTime LastSeenUtc);

