// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliumTodo.Application.Services;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;
using HeliumTodo.Infrastructure;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 「自习室」页面 —— 扩展功能。
///
/// 【当前实现程度】
///   ✅ 本地自习室：创建房间、房间码、成员列表、聊天、在房间里开始专注
///   ✅ 和专注模块打通：房间里的专注时长会计入「专注」页的统计
///   ⏸ 多人联机（无中心服务器方案）：其实已经修了，但是受限于开发人数（一个人+一只肥鱼）还没测。
///
/// 【为什么暂缓，以及以后怎么做】
///   无中心服务器（P2P）听起来简单，实际要处理不少麻烦事：
///     · 局域网里怎么找到对方？→ UDP 组播广播房间码
///     · 不在同一个局域网怎么办？→ 需要 NAT 穿透（打洞）或中继，难度陡增
///     · 大家的电脑时间不一样怎么办？→ 用「剩余秒数」而不是绝对时间同步
///     · 有人掉线了怎么办？→ 心跳检测 + 自动剔除
///   这些都不难，但要花时间调。首版先把接口留好（IPeerTransport），
///   业务逻辑全部走接口，以后补上真正的 P2P 实现时，这个页面一行都不用改。
///   （其实一直在想，能不能使用easytier去代替）
/// </summary>
public sealed partial class StudyRoomViewModel : ViewModelBase
{
    private readonly StudyRoomService _roomService;
    private readonly StudyRoomRepository _roomRepo;
    private readonly FocusTimerService _focusTimer;
    private readonly AppState _state;

    public StudyRoomViewModel(IServiceProvider services)
    {
        _roomService = (StudyRoomService)services.GetService(typeof(StudyRoomService))!;
        _roomRepo = (StudyRoomRepository)services.GetService(typeof(StudyRoomRepository))!;
        _focusTimer = (FocusTimerService)services.GetService(typeof(FocusTimerService))!;
        _state = (AppState)services.GetService(typeof(AppState))!;

        _roomName = "期末冲刺自习室";
        _joinCode = string.Empty;
        _focusMinutes = _state.Config.StudyRoomFocusMinutes;

        // 房间里的消息和状态变化都要显示出来
        _roomService.MessageReceived += (_, message) =>
        {
            // 消息可能来自后台线程，切回界面线程再更新集合
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Messages.Add(message));
        };

        _roomService.RoomChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateFromRoom);
        };

        // 地址表 / 延迟变化（心跳每 4 秒会推一次新的延迟）。
        // 只刷新这几个文案，不整体重排界面 —— 免得每 4 秒闪一下。
        _roomService.PeersChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(LatencyText));
                OnPropertyChanged(nameof(PeersText));
                OnPropertyChanged(nameof(OnlineStatusText));
            });
        };

        // 设置页里开关联机之后，这一页的文案和按钮要马上跟着变，不用等切页面
        _state.SettingsChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshNetworkState);

            // 关掉联机就顺手断开：让端口继续开着和"已关闭"自相矛盾。
            // 走命令而不是直接调服务，是为了把房间也标记成已离开（否则页面还显示在房间里）。
            if (!_roomService.LanEnabled && CurrentRoom is not null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = LeaveRoomAsync());
            }
        };
    }

    /// <summary>联机开关变了以后，把这些跟联机有关的属性都通知一遍。</summary>
    private void RefreshNetworkState()
    {
        OnPropertyChanged(nameof(IsLanEnabled));
        OnPropertyChanged(nameof(CanGoOnline));
        OnPropertyChanged(nameof(IsLanDisabled));
        OnPropertyChanged(nameof(OnlineStatusText));
    }

    // ─────────────────────────────────────────────────────────
    // 房间状态
    // ─────────────────────────────────────────────────────────

    /// <summary>当前所在房间（没进房间时为 null）。</summary>
    [ObservableProperty] private StudyRoom? _currentRoom;

    /// <summary>是不是已经在房间里了。</summary>
    [ObservableProperty] private bool _isInRoom;

    /// <summary>房间名（创建时填）。</summary>
    [ObservableProperty] private string _roomName = string.Empty;

    /// <summary>要加入的房间码。</summary>
    [ObservableProperty] private string _joinCode = string.Empty;

    /// <summary>房间里的成员。</summary>
    public ObservableCollection<StudyRoomMember> Members { get; } = new();

    /// <summary>聊天 / 系统消息。</summary>
    public ObservableCollection<StudyRoomMessage> Messages { get; } = new();

    /// <summary>发消息的输入框。</summary>
    [ObservableProperty] private string _newMessage = string.Empty;

    /// <summary>提示 / 说明文字。</summary>
    [ObservableProperty] private string _noticeText = string.Empty;

    /// <summary>房间码（界面上大写显示，方便念给别人）。</summary>
    public string RoomCode => CurrentRoom?.Code ?? "------";

    /// <summary>房间名。</summary>
    public string CurrentRoomName => CurrentRoom?.Name ?? "还没有加入自习室";

    /// <summary>成员数量文字。</summary>
    public string MemberCountText => $"{Members.Count} 人在线";

    /// <summary>自己在房间里的累计专注时长。</summary>
    [ObservableProperty] private string _myFocusTodayText = "0 分钟";

    // ─────────────────────────────────────────────────────────
    // 房间内的专注
    // ─────────────────────────────────────────────────────────

    /// <summary>房间内专注的时长（分钟）。</summary>
    [ObservableProperty] private int _focusMinutes = 45;

    /// <summary>房间内专注的标题。</summary>
    [ObservableProperty] private string _focusTitle = "和大家一起专注";

    /// <summary>房间内的专注是否正在进行。</summary>
    [ObservableProperty] private bool _isRoomFocusRunning;

    /// <summary>房间内的计时显示。</summary>
    [ObservableProperty] private string _roomClockText = "45:00";

    /// <summary>房间内计时进度。</summary>
    [ObservableProperty] private double _roomClockProgress;

    private readonly Avalonia.Threading.DispatcherTimer _roomTimer =
        new() { Interval = TimeSpan.FromMilliseconds(500) };

    private DateTime _lastRoomTick;

    // ─────────────────────────────────────────────────────────
    // 联机
    // ─────────────────────────────────────────────────────────

    /// <summary>这个传输层实现支不支持联机（纯本地占位实现返回 false）。</summary>
    public bool IsOnlineAvailable => _roomService.SupportsNetworking;

    /// <summary>用户有没有在设置里打开「局域网联机」（默认关闭）。</summary>
    public bool IsLanEnabled => _roomService.LanEnabled;

    /// <summary>支持联机、而且用户打开了 —— 这才显示"填 IP 连接"那一块。</summary>
    public bool CanGoOnline => IsOnlineAvailable && IsLanEnabled;

    /// <summary>支持联机但用户关着，界面上给一句"去哪儿打开"。</summary>
    public bool IsLanDisabled => IsOnlineAvailable && !IsLanEnabled;

    /// <summary>联机状态说明，会随开关和连接情况变化。</summary>
    public string OnlineStatusText
    {
        get
        {
            if (!IsOnlineAvailable) return "当前是本地模式，没法联机。";

            if (!IsLanEnabled)
            {
                return "局域网联机默认关闭：到「设置 → 自习室」打开「局域网联机」后，才能和同学一起自习。";
            }

            return _roomService.IsConnected
                ? $"已连接 · 共 {_roomService.LocalPort} 端口在监听"
                : "局域网联机已就绪：填写对方IP或对方填写你的IP";
        }
    }

    /// <summary>
    /// 是不是房主（这个房间是我建的）。
    ///
    /// 只有房主需要把「本机地址」发给别人；客户端是自己去连别人，
    /// 把自己的地址显示在最显眼的位置只会让人误以为要发给对方。
    /// </summary>
    public bool IsHost => _roomService.IsHost;

    /// <summary>
    /// 要不要显示「把这一串告诉同学」那块。
    ///
    /// 界面现在直接绑 <see cref="IsHost"/>（那块在"房间内"卡片里，
    /// 能显示出来就说明已经在房间里了），这个属性留给真机调试看状态用。
    /// </summary>
    public bool ShowHostAddress => IsOnlineAvailable && IsHost;

    /// <summary>到房主的延迟，形如「延迟 12 毫秒」。还没测出来就提示稍等。</summary>
    public string LatencyText
    {
        get
        {
            if (!_roomService.IsConnected) return "";

            var rtt = _roomService.RoundTripMs;

            return rtt <= 0 ? "正在测量延迟…" : $"延迟 {rtt} 毫秒";
        }
    }

    /// <summary>成员地址表（房主下发的，含每个人在哪、延迟多少）。</summary>
    public string PeersText
    {
        get
        {
            var peers = _roomService.Peers;

            if (peers.Count == 0) return "";

            var lines = peers.Select(p =>
            {
                var latency = p.RoundTripMs > 0 ? $"{p.RoundTripMs} ms" : "未测";

                return string.IsNullOrEmpty(p.Endpoint)
                    ? $"{p.DisplayName}（{latency}）"
                    : $"{p.DisplayName} · {p.Endpoint}（{latency}）";
            });

            return string.Join("\n", lines);
        }
    }

    /// <summary>本机地址，显示出来给同学抄。</summary>
    public string LocalAddressText => _roomService.LocalAddressText;

    /// <summary>本机端口。</summary>
    public string LocalPortText => _roomService.LocalPort > 0
        ? _roomService.LocalPort.ToString()
        : "未启动";

    /// <summary>要连接的对方 IP（IPv4 / IPv6 都行）。</summary>
    [ObservableProperty]
    private string _joinHost = "";

    /// <summary>
    /// 手动填地址连过去。
    ///
    /// 为什么不只看"发现的房间"列表？
    ///   UDP 广播只在同一网段有效，路由器不转发广播。
    ///   跨网段、连手机热点、开着防火墙的情况都得手填地址。
    /// </summary>
    [RelayCommand]
    private async Task ConnectByAddressAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinHost))
        {
            NoticeText = "填写IP地址，例如 192.168.1.5 或 [fe80::1]:47470";
            return;
        }

        if (!CanGoOnline)
        {
            NoticeText = "连不上啦( ; ω ; )，前往设置-自习室将局域网联机打开捏";
            return;
        }

        NoticeText = "正在连接…";

        var ok = await _roomService.JoinRemoteRoomAsync(
            JoinCode,
            _state.Config.DisplayName,
            JoinHost);

        NoticeText = ok ? "连接完成，可以让对方开始专注了。" : "连接失败，请检查地址和房间码。";

        UpdateFromRoom();
        OnPropertyChanged(nameof(OnlineStatusText));
    }

    // ─────────────────────────────────────────────────────────
    // 命令
    // ─────────────────────────────────────────────────────────

    /// <summary>创建一个自习室。</summary>
    [RelayCommand]
    private async Task CreateRoomAsync()
    {
        var room = _roomService.CreateRoom(
            RoomName,
            _state.Config.DisplayName,
            TimeSpan.FromMinutes(Math.Max(1, FocusMinutes)));

        // 存到本地数据库，下次打开还能看到
        var saved = await _roomRepo.SaveAsync(room);
        CurrentRoom = saved;

        UpdateFromRoom();
    }

    /// <summary>用房间码加入（联机暂缓期间会给出明确提示）。</summary>
    [RelayCommand]
    private async Task JoinRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinCode))
        {
            NoticeText = "请输入房间码。";
            return;
        }

        // 先看看本地有没有这个房间（本地创建过的可以「回到」房间）
        var local = await _roomRepo.GetByCodeAsync(JoinCode.Trim().ToUpperInvariant());

        if (local is not null)
        {
            // 之前离开过的房间可以「回到」：把离开时间清掉，才会重新算房间里
            if (local.ClosedAt is not null)
            {
                local.ClosedAt = null;
                local = await _roomRepo.SaveAsync(local);
            }

            CurrentRoom = local;
            UpdateFromRoom();
            NoticeText = $"已回到本地房间「{local.Name}」。";
            return;
        }

        // 本地没有：这就是联机场景
        if (!CanGoOnline)
        {
            NoticeText = IsLanDisabled
                ? "本地找不到这个房间。联机已关闭，请先到设置-自习室下打开“局域网联机”。"
                : "本地找不到这个房间，当前为本地模式，无法联机。";
            return;
        }

        _roomService.JoinRoom(JoinCode, _state.Config.DisplayName);

        NoticeText = string.IsNullOrWhiteSpace(JoinHost)
            ? $"本地没有这个房间。请先输入房主的 IP 地址，再尝试「连接」。"
            : "正在连接…";
    }

    /// <summary>离开房间。</summary>
    [RelayCommand]
    private async Task LeaveRoomAsync()
    {
        var leaving = CurrentRoom;

        await _roomService.LeaveRoomAsync();

        // 离开这件事要写进数据库。只清内存的话，下次打开自习室页
        // 又会被「载入最近的房间」拉回去 —— 就是"退出后重进还在房间里"。
        if (leaving is not null)
        {
            leaving.ClosedAt = DateTime.Now;
            await _roomRepo.SaveAsync(leaving);
        }

        CurrentRoom = null;
        Messages.Clear();
        Members.Clear();
        UpdateFromRoom();
        NoticeText = "已离开自习室。";
    }

    /// <summary>在房间里开始专注。</summary>
    [RelayCommand]
    private async Task StartRoomFocusAsync()
    {
        if (CurrentRoom is null)
        {
            NoticeText = "先创建或加入一个自习室吧。";
            return;
        }

        var minutes = Math.Max(1, FocusMinutes);
        var title = string.IsNullOrWhiteSpace(FocusTitle) ? "和大家一起专注" : FocusTitle;

        await _roomService.StartFocusAsync(title, TimeSpan.FromMinutes(minutes));

        // 同时启动全局的专注计时器 —— 这样「专注」页的统计也会算上这一段时间
        _focusTimer.Start(TimeSpan.FromMinutes(minutes), title);

        _lastRoomTick = DateTime.Now;
        _roomTimer.Start();

        IsRoomFocusRunning = true;
        UpdateFromRoom();
    }

    /// <summary>结束房间里的专注。</summary>
    [RelayCommand]
    private async Task EndRoomFocusAsync()
    {
        _roomTimer.Stop();

        var actual = _focusTimer.Elapsed;

        // 结束全局计时器（用 FinishNow 的逻辑，会把记录写进数据库）
        // 这里直接调用 Abandon 会丢掉记录，所以用 SkipBreak 回到空闲后再手工保存
        if (_focusTimer.Phase != FocusPhase.Idle)
        {
            await _roomService.EndFocusAsync(actual);
            _focusTimer.SkipBreak();
        }

        IsRoomFocusRunning = false;
        RoomClockText = FormatClock(TimeSpan.FromMinutes(Math.Max(1, FocusMinutes)));
        RoomClockProgress = 0;

        _state.NotifyChanged(AppDataType.Focus);
        UpdateFromRoom();
    }

    /// <summary>发一条消息。</summary>
    [RelayCommand]
    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(NewMessage)) return;

        _roomService.SendMessage(NewMessage.Trim(), _state.Config.DisplayName);
        NewMessage = string.Empty;
    }

    /// <summary>
    /// 模拟一个队友加入 —— 只是为了让界面看起来不那么空。
    /// 真正的联机实现之后，这个方法会被真实的网络事件取代。
    /// </summary>
    [RelayCommand]
    private async Task AddDemoMemberAsync()
    {
        if (CurrentRoom is null)
        {
            NoticeText = "先创建一个自习室。";
            return;
        }

        var names = new[] { "小张", "小李", "小王", "阿伟", "小陈" };
        var existing = CurrentRoom.Members.Select(m => m.DisplayName).ToHashSet();
        var name = names.FirstOrDefault(n => !existing.Contains(n));

        if (name is null)
        {
            NoticeText = "示例队友已经加满了。";
            return;
        }

        CurrentRoom.Members.Add(new StudyRoomMember
        {
            StudyRoomId = CurrentRoom.Id,
            DisplayName = name,
            PeerId = $"demo-{Guid.NewGuid():N}"[..12],
            IsOnline = true,
            JoinedAt = DateTime.Now,
            IsFocusing = Random.Shared.Next(2) == 0,
            FocusedToday = TimeSpan.FromMinutes(Random.Shared.Next(20, 180)),
        });

        await _roomRepo.SaveAsync(CurrentRoom);

        Messages.Add(new StudyRoomMessage
        {
            StudyRoomId = CurrentRoom.Id,
            SenderName = "系统",
            Content = $"「{name}」加入了自习室。",
            SentAt = DateTime.Now,
            IsSystemMessage = true,
        });

        UpdateFromRoom();
    }

    // ─────────────────────────────────────────────────────────
    // 加载
    // ─────────────────────────────────────────────────────────

    public override async Task OnNavigatedToAsync()
    {
        // 房间计时器
        _roomTimer.Tick -= OnRoomTick;
        _roomTimer.Tick += OnRoomTick;

        // 把本地还开着的房间载入，方便接着用。
        // 用 GetOpenAsync 而不是 GetAllAsync：用户点过「离开」的房间带上离开时间，
        // 不该在重进页面时又被拉回房间里。
        var rooms = await _roomRepo.GetOpenAsync();
        var latest = rooms.FirstOrDefault();

        if (CurrentRoom is null && latest is not null)
        {
            CurrentRoom = latest;

            var messages = await _roomRepo.GetMessagesAsync(latest.Id);
            Messages.Clear();
            foreach (var message in messages.TakeLast(50))
            {
                Messages.Add(message);
            }
        }

        UpdateFromRoom();
    }

    private void OnRoomTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var delta = now - _lastRoomTick;
        _lastRoomTick = now;

        if (delta > TimeSpan.FromSeconds(5)) delta = TimeSpan.FromMilliseconds(500);

        _focusTimer.Tick(delta);

        RoomClockText = FormatClock(_focusTimer.Remaining);
        RoomClockProgress = _focusTimer.Progress;

        // 计时结束
        if (_focusTimer.Phase == FocusPhase.Idle)
        {
            _roomTimer.Stop();
            IsRoomFocusRunning = false;
            _ = EndRoomFocusAsync();
        }
    }

    /// <summary>把服务层的房间状态搬到界面上。</summary>
    private void UpdateFromRoom()
    {
        var room = CurrentRoom;

        IsInRoom = room is not null;
        Members.Clear();

        if (room is not null)
        {
            foreach (var member in room.Members)
            {
                Members.Add(member);
            }

            var me = room.Members.FirstOrDefault();
            MyFocusTodayText = DashboardViewModel.FormatDuration(me?.FocusedToday ?? TimeSpan.Zero);
        }

        OnPropertyChanged(nameof(RoomCode));
        OnPropertyChanged(nameof(CurrentRoomName));
        OnPropertyChanged(nameof(MemberCountText));
        OnPropertyChanged(nameof(IsOnlineAvailable));
        OnPropertyChanged(nameof(OnlineStatusText));
        OnPropertyChanged(nameof(LocalAddressText));
        OnPropertyChanged(nameof(LocalPortText));
        OnPropertyChanged(nameof(IsHost));
        OnPropertyChanged(nameof(ShowHostAddress));
        OnPropertyChanged(nameof(LatencyText));
        OnPropertyChanged(nameof(PeersText));
    }

    private static string FormatClock(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
}
