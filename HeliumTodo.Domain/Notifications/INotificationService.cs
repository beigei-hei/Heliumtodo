// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Notifications;

/// <summary>
/// 系统通知（Windows 上是"通知中心"里那条）。
///
/// 为什么要抽接口：通知是唯一强依赖平台的东西。
/// Core 层只认识这个接口，具体怎么弹由各平台自己实现 ——
/// 现在只有 Windows 实现（<c>WindowsToastNotifier</c>），
/// 没有实现时用 <see cref="NullNotificationService"/> 兜底，程序照样能跑。
/// </summary>
public interface INotificationService
{
    /// <summary>这台机器支不支持弹系统通知。</summary>
    bool IsSupported { get; }

    /// <summary>弹一条。不支持时静默忽略（不要抛异常，调用方是后台定时器）。</summary>
    void Notify(NotificationMessage message);

    /// <summary>
    /// 用户点了通知本体时触发（用于把窗口拉起来并跳到对应页面）。
    /// 平台不支持时永远不触发。
    /// </summary>
    event EventHandler<NotificationActivatedEventArgs>? Activated;
}

/// <summary>要弹出去的内容。</summary>
public sealed record NotificationMessage
{
    /// <summary>标题。</summary>
    public required string Title { get; init; }

    /// <summary>正文。</summary>
    public required string Message { get; init; }

    /// <summary>
    /// 归并标签：同标签的通知在通知中心里会合并成一条，
    /// 避免"十条待办提醒堆成十行"。同一个来源用同一个标签。
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>点开时要跳转的提示（来源类型 + Id），由界面层解释。</summary>
    public ReminderSourceKind? Kind { get; init; }

    /// <summary>点开时要跳转的条目 Id。</summary>
    public int? SourceId { get; init; }
}

/// <summary>用户点了通知。</summary>
public sealed class NotificationActivatedEventArgs : EventArgs
{
    public NotificationActivatedEventArgs(ReminderSourceKind? kind, int? sourceId)
    {
        Kind = kind;
        SourceId = sourceId;
    }

    public ReminderSourceKind? Kind { get; }

    public int? SourceId { get; }
}

/// <summary>
/// 什么都不做的实现。
///
/// 用途：① 非 Windows 平台；② 渲染自检/单元测试里不想真弹通知；
/// ③ 用户把提醒全关掉时。
/// </summary>
public sealed class NullNotificationService : INotificationService
{
    public bool IsSupported => false;

    public event EventHandler<NotificationActivatedEventArgs>? Activated;

    public void Notify(NotificationMessage message)
    {
        // 刻意什么都不做：没有通知能力的平台上，提醒服务仍然要能正常跑完一轮。
        _ = message;
        _ = Activated;
    }
}