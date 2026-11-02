using HeliumTodo.Core.Configuration;
using HeliumTodo.Core.Models;
using HeliumTodo.Core.Plugins;

namespace HeliumTodo.Core.Services;

/// <summary>
/// 自习室服务 —— 扩展功能。
///
/// 【当前状态】
///   · 本地模式：创建房间、进房间、成员列表、房间内聊天，全部可用（数据存在本地 SQLite）。
///   · 局域网联机（无中心服务器）：房子主开在房主机器上，别人填 IP 连过来。
///     默认<b>关闭</b>，要用户在设置里打开才监听端口（<see cref="LanEnabled"/>）。
///     不做 NAT 穿透、加密、TUN —— 只在同一个局域网里用。
///     传输层是接口（<see cref="IPeerTransport"/>），想换实现不用改这里的代码。
/// </summary>
public sealed class StudyRoomService
{
    private readonly IPeerTransport _transport;
    private readonly ConfigService? _config;

    /// <summary>当前所在的房间（不在任何房间时为 null）。</summary>
    public StudyRoom? CurrentRoom { get; private set; }

    /// <summary>
    /// 现在能不能联机：传输层支持 <b>并且</b> 用户打开了开关。
    ///
    /// 建房、加入房间、连地址都问这一个属性，所以"联机默认关闭"
    /// 只要在这里判断一次就够了，不用每个入口各写一遍。
    /// </summary>
    public bool IsOnlineModeAvailable => SupportsNetworking && LanEnabled;

    /// <summary>注入的传输层本身支不支持联网（不管用户开没开）。</summary>
    public bool SupportsNetworking => _transport.SupportsNetworking;

    /// <summary>
    /// 用户在设置里有没有打开「局域网联机」。
    ///
    /// 没注入配置时按"打开"算：单元测试直接 new 一个服务来试逻辑，
    /// 不该被配置文件牵着走。
    /// </summary>
    public bool LanEnabled => _config?.Current.EnableNetworkStudyRoom ?? true;

    /// <summary>本机地址（联机时显示给用户抄给同学）。</summary>
    public string LocalAddressText => _transport.LocalAddressText;

    /// <summary>本机监听端口。</summary>
    public int LocalPort => _transport.Port;

    /// <summary>当前是否真的连着别人。</summary>
    public bool IsConnected => _transport.IsConnected;

    /// <summary>已知的成员地址表（谁在哪、延迟多少）。</summary>
    public IReadOnlyList<HeliumTodo.Core.Networking.PeerAddressInfo> Peers => _transport.Peers;

    /// <summary>到房主的往返延迟（毫秒）。0 表示还没测出来。</summary>
    public int RoundTripMs => _transport.RoundTripMs;

    /// <summary>对外地址（NAT 映射后用），对应 EasyTier 的 mapped listener。</summary>
    public string? AdvertisedEndpoint
    {
        get => _transport.AdvertisedEndpoint;
        set => _transport.AdvertisedEndpoint = value;
    }

    /// <summary>
    /// 这个房间是不是我建的（我是房主）。
    ///
    /// 界面据此决定「本机地址」那块要不要显示：只有房主才需要把自己的
    /// 地址发给别人，客户端是"我要连别人"，显示自己的地址反而没用。
    /// </summary>
    public bool IsHost => _transport.IsHost;

    /// <summary>房间状态变化时触发（成员加入、有人开始专注等）。</summary>
    public event EventHandler<StudyRoom>? RoomChanged;

    /// <summary>收到房间内消息时触发。</summary>
    public event EventHandler<StudyRoomMessage>? MessageReceived;

    public StudyRoomService(IPeerTransport transport, ConfigService? config = null)
    {
        _transport = transport;
        _config = config;

        _transport.PeerStatusReceived += OnPeerStatusReceived;
        _transport.PresenceChanged += OnPresenceChanged;
        _transport.ChatReceived += OnPeerChatReceived;
        _transport.PeersChanged += OnPeersChanged;
    }

    /// <summary>成员地址表 / 延迟有变化（界面刷新用）。</summary>
    public event EventHandler? PeersChanged;

    private void OnPeersChanged(object? sender, EventArgs e) => PeersChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 创建一个新房间。房间码是随机生成的，联机后别人用它加入。
    /// </summary>
    public StudyRoom CreateRoom(string name, string hostName, TimeSpan? defaultFocusDuration = null)
    {
        var room = new StudyRoom
        {
            Code = StudyRoom.GenerateCode(),
            Name = string.IsNullOrWhiteSpace(name) ? "我的自习室" : name,
            HostName = string.IsNullOrWhiteSpace(hostName) ? "我" : hostName,
            DefaultFocusDuration = defaultFocusDuration ?? TimeSpan.FromMinutes(45),
            IsLocalOnly = true,        // 联机暂缓期间，房间是纯本地的
            CreatedAt = DateTime.Now,
        };

        room.Members.Add(new StudyRoomMember
        {
            DisplayName = room.HostName,
            PeerId = _transport.LocalPeerId,
            IsOnline = true,
            JoinedAt = DateTime.Now,
        });

        CurrentRoom = room;

        // 联机模式下，建房的人顺便当房主：开始接受别人连进来
        if (IsOnlineModeAvailable)
        {
            _transport.BecomeHost(room.Code);
        }

        RoomChanged?.Invoke(this, room);

        AddSystemMessage($"房间「{room.Name}」已创建，房间码 {room.Code}。");
        return room;
    }

    /// <summary>
    /// 进入一个已存在的房间。
    ///
    /// 联机模式下还需要给出对方地址，见 <see cref="JoinRemoteRoomAsync"/>。
    /// 这个方法保留下来是为了兼容"只想按房间码在局域网里找一找"的场景。
    /// </summary>
    public bool JoinRoom(string roomCode, string displayName)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return false;

        _ = _transport.JoinRoomAsync(roomCode.Trim().ToUpperInvariant());

        if (!IsOnlineModeAvailable)
        {
            AddSystemMessage("当前是本地模式，没法连到别人的房间。");
            return false;
        }

        AddSystemMessage($"准备加入房间 {roomCode.Trim().ToUpperInvariant()}，请填写房主地址后点「连接」。");
        return false;
    }

    /// <summary>
    /// 联机加入：连到指定地址，然后进入房间。
    ///
    /// 参数里的 host 就是用户手动填的 IP（IPv4 或 IPv6 都行）。
    /// </summary>
    public async Task<bool> JoinRemoteRoomAsync(
        string roomCode,
        string displayName,
        string host,
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsOnlineModeAvailable)
        {
            AddSystemMessage("当前是本地模式，没法联机。");
            return false;
        }

        var code = (roomCode ?? "").Trim().ToUpperInvariant();

        if (code.Length == 0)
        {
            AddSystemMessage("请先填房间码。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            AddSystemMessage("请先填对方的 IP 地址。");
            return false;
        }

        await _transport.JoinRoomAsync(code, cancellationToken);

        var connected = await _transport.ConnectAsync(host.Trim(), port, cancellationToken);

        if (!connected)
        {
            AddSystemMessage("没连上，检查一下地址、房间码，以及对方的防火墙是否放行了这个端口。");
            return false;
        }

        // 连接成功：先搭一个本地房间对象，成员列表会被对方发来的上线消息填充
        CurrentRoom = new StudyRoom
        {
            Code = code,
            Name = $"{displayName} 的自习室",
            HostName = host.Trim(),
            DefaultFocusDuration = TimeSpan.FromMinutes(45),
            IsLocalOnly = false,
            CreatedAt = DateTime.Now,
        };

        CurrentRoom.Members.Add(new StudyRoomMember
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "我" : displayName,
            PeerId = _transport.LocalPeerId,
            IsOnline = true,
            JoinedAt = DateTime.Now,
        });

        AddSystemMessage($"已连上 {host.Trim()}，房间码 {code}。");

        RoomChanged?.Invoke(this, CurrentRoom);
        return true;
    }

    /// <summary>离开当前房间。</summary>
    public async Task LeaveRoomAsync()
    {
        if (CurrentRoom is null) return;

        AddSystemMessage("已离开房间。");
        CurrentRoom = null;

        _transport.LeaveRoom();
        await _transport.StopAsync();
        RoomChanged?.Invoke(this, new StudyRoom { Code = string.Empty, Name = string.Empty });
    }

    /// <summary>
    /// 在房间里开始一次专注。
    /// 返回 true 表示可以开始计时；联机模式下还会把状态广播给其他成员。
    /// </summary>
    public async Task<bool> StartFocusAsync(string title, TimeSpan duration)
    {
        if (CurrentRoom is null) return false;

        var me = CurrentRoom.Members.FirstOrDefault();
        if (me is not null) me.IsFocusing = true;

        AddSystemMessage($"「{(me?.DisplayName ?? "我")}」开始了 {duration.TotalMinutes:0} 分钟的专注：{title}");

        await _transport.BroadcastStatusAsync(new PeerStatus(
            _transport.LocalPeerId,
            me?.DisplayName ?? "我",
            IsFocusing: true,
            FocusTitle: title,
            Remaining: duration));

        RoomChanged?.Invoke(this, CurrentRoom);
        return true;
    }

    /// <summary>结束专注，更新成员状态。</summary>
    public async Task EndFocusAsync(TimeSpan actualDuration)
    {
        if (CurrentRoom is null) return;

        var me = CurrentRoom.Members.FirstOrDefault();
        if (me is not null)
        {
            me.IsFocusing = false;
            me.FocusedToday += actualDuration;
        }

        AddSystemMessage($"专注结束，本次 {actualDuration.TotalMinutes:0} 分钟。");

        await _transport.BroadcastStatusAsync(new PeerStatus(
            _transport.LocalPeerId,
            me?.DisplayName ?? "我",
            IsFocusing: false,
            FocusTitle: null,
            Remaining: null));

        RoomChanged?.Invoke(this, CurrentRoom);
    }

    /// <summary>在房间里发一条消息。</summary>
    public void SendMessage(string content, string senderName = "我")
    {
        if (CurrentRoom is null || string.IsNullOrWhiteSpace(content)) return;

        var message = new StudyRoomMessage
        {
            StudyRoomId = CurrentRoom.Id,
            SenderName = senderName,
            Content = content.Trim(),
            SentAt = DateTime.Now,
            IsSystemMessage = false,
        };

        MessageReceived?.Invoke(this, message);

        // 联机模式下同时发给房间里其它人
        if (IsOnlineModeAvailable && _transport.IsConnected)
        {
            _ = _transport.SendChatAsync(message.Content);
        }
    }

    /// <summary>往聊天区加一条系统提示。</summary>
    private void AddSystemMessage(string content)
    {
        MessageReceived?.Invoke(this, new StudyRoomMessage
        {
            StudyRoomId = CurrentRoom?.Id ?? 0,
            SenderName = "系统",
            Content = content,
            SentAt = DateTime.Now,
            IsSystemMessage = true,
        });
    }

    private void OnPeerStatusReceived(object? sender, PeerStatus status)
    {
        if (CurrentRoom is null) return;

        var member = CurrentRoom.Members.FirstOrDefault(m => m.PeerId == status.PeerId);
        if (member is not null)
        {
            member.IsFocusing = status.IsFocusing;
        }

        RoomChanged?.Invoke(this, CurrentRoom);
    }

    private void OnPresenceChanged(object? sender, PeerPresence presence)
    {
        if (CurrentRoom is null) return;

        // 自己的上线消息不用管（成员列表里已经有自己了）
        if (string.Equals(presence.PeerId, _transport.LocalPeerId, StringComparison.Ordinal)) return;

        var member = CurrentRoom.Members.FirstOrDefault(m => m.PeerId == presence.PeerId);

        if (member is null)
        {
            // 第一次见到这个人：加进成员列表
            CurrentRoom.Members.Add(new StudyRoomMember
            {
                DisplayName = string.IsNullOrWhiteSpace(presence.DisplayName) ? "同学" : presence.DisplayName,
                PeerId = presence.PeerId,
                IsOnline = presence.IsOnline,
                JoinedAt = DateTime.Now,
            });

            if (presence.IsOnline)
            {
                AddSystemMessage($"「{(string.IsNullOrWhiteSpace(presence.DisplayName) ? "同学" : presence.DisplayName)}」进来了。");
            }
        }
        else
        {
            member.IsOnline = presence.IsOnline;

            if (!presence.IsOnline)
            {
                AddSystemMessage($"「{member.DisplayName}」离开了。");
            }
        }

        RoomChanged?.Invoke(this, CurrentRoom);
    }

    /// <summary>收到别人发来的聊天消息。</summary>
    private void OnPeerChatReceived(object? sender, PeerChatMessage message)
    {
        if (CurrentRoom is null) return;

        if (string.IsNullOrWhiteSpace(message.Text)) return;

        MessageReceived?.Invoke(this, new StudyRoomMessage
        {
            StudyRoomId = CurrentRoom.Id,
            SenderName = string.IsNullOrWhiteSpace(message.DisplayName) ? "同学" : message.DisplayName,
            Content = message.Text,
            SentAt = DateTime.Now,
            IsSystemMessage = false,
        });
    }
}
