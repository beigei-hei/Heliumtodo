using HeliumTodo.Application.Abstractions;
// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only


namespace HeliumTodo.Desktop.Services;

/// <summary>
/// <see cref="INotificationService"/> 的 Windows 实现。
///
/// 走的是托盘图标的气泡通知（<c>Shell_NotifyIcon</c> + <c>NIF_INFO</c>）：
/// 从 Windows 10 开始，系统会把它统一转成「操作中心 / 通知中心」里的那条通知，
/// 和别的应用弹出的通知排在一起 —— 这就是"调用系统接口"的意思，
/// 不是自己在窗口里画一个假的弹窗。
///
/// 为什么不用 WinRT 的 toast（<c>ToastNotificationManager</c>）：
///   那条路要求先注册 AppUserModelID 和开始菜单快捷方式，否则通知不显示；
///   而且需要额外的 WinRT 互操作包。对一个本地小工具来说代价太大。
/// </summary>
public sealed class WindowsToastNotifier : INotificationService
{
    private readonly TrayService? _tray;

    public WindowsToastNotifier(TrayService? tray)
    {
        _tray = tray;
    }

    public bool IsSupported => _tray is { IsRunning: true };

    /// <summary>
    /// 目前没有接"点通知跳转" —— 托盘气泡本身不回报点击。
    /// 事件先留着，将来换成真正的 toast 就能用上。
    /// </summary>
    public event EventHandler<NotificationActivatedEventArgs>? Activated;

    public void Notify(NotificationMessage message)
    {
        if (_tray is null || !_tray.IsRunning)
        {
            return;
        }

        _tray.ShowBalloon(message.Title, message.Message);
        _ = Activated;
    }
}