// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Models;

/// <summary>
/// 自习室房间 —— 扩展功能。
///
/// 【当前状态：本地占位实现】
/// 房间数据先存在本地 SQLite 里，单人也能创建、进入、查看。
/// 多人联机（无中心服务器方案）的接口已经预留好（见 <see cref="Abstractions.IPeerTransport"/>），
/// 但具体实现标记为「暂缓」，不影响现在的编译和运行。
///
/// 无中心服务器（P2P）思路备忘（以后实现时按这个来）：
///   1. 房主创建房间 → 生成一个房间码（例如 6 位字符）。
///   2. 想加入的人输入房间码 → 通过局域网广播（UDP 组播）或手动填 IP 找到房主。
///   3. 成员之间直接点对点连接，同步「谁在专注、还剩多久」，不经过任何服务器。
/// </summary>
public class StudyRoom
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>房间码：其他人用这个码加入。例如 "H2K9QX"。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>房间名称，例如「期末冲刺自习室」。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>房主昵称。</summary>
    public string HostName { get; set; } = "我";

    /// <summary>是否只允许本地单人使用（联机暂缓期间为 true）。</summary>
    public bool IsLocalOnly { get; set; } = true;

    /// <summary>默认的专注时长（进房间后一键开始，用这个时长）。</summary>
    public TimeSpan DefaultFocusDuration { get; set; } = TimeSpan.FromMinutes(45);

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 离开房间的时间。null 表示还「在房间里」。
    ///
    /// 为什么需要这个字段：房间是存在本地数据库里的，光把内存里的
    /// CurrentRoom 置空，下次打开自习室页又会把最近那个房间载回来 ——
    /// 表现就是「明明退出了，重进还显示在房间内部」。
    /// 记下离开时间，载入时只认没关过的房间就不会跑回去。
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>房间里的成员（本地占位：目前只有自己）。</summary>
    public List<StudyRoomMember> Members { get; set; } = new();

    /// <summary>生成一个新的房间码（6 位，去掉容易看错的 0/O/1/I）。</summary>
    public static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        var chars = new char[6];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[random.Next(alphabet.Length)];
        }
        return new string(chars);
    }
}

/// <summary>
/// 自习室成员。当前只在本地记录自己；联机后每个人一条。
/// </summary>
public class StudyRoomMember
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属房间 Id。</summary>
    public int StudyRoomId { get; set; }

    /// <summary>显示昵称。</summary>
    public string DisplayName { get; set; } = "我";

    /// <summary>在无中心服务器方案里，这是该成员的节点标识（本地先用机器名占位）。</summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>是否在线（联机后由心跳决定；本地模式恒为 true）。</summary>
    public bool IsOnline { get; set; } = true;

    /// <summary>加入时间。</summary>
    public DateTime JoinedAt { get; set; } = DateTime.Now;

    /// <summary>当前累计专注时长。</summary>
    public TimeSpan FocusedToday { get; set; }

    /// <summary>是否正在专注中（用于在成员列表里显示小绿灯）。</summary>
    public bool IsFocusing { get; set; }
}

/// <summary>
/// 自习室里的聊天 / 状态消息（本地占位：只存在本地数据库）。
/// 联机以后这些消息会通过 <see cref="Abstractions.IPeerTransport"/> 在成员之间直接传递。
/// </summary>
public class StudyRoomMessage
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属房间 Id。</summary>
    public int StudyRoomId { get; set; }

    /// <summary>发送者昵称。</summary>
    public string SenderName { get; set; } = "我";

    /// <summary>内容。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>发送时间。</summary>
    public DateTime SentAt { get; set; } = DateTime.Now;

    /// <summary>是否是系统提示（例如「某某开始了专注」）。</summary>
    public bool IsSystemMessage { get; set; }
}
