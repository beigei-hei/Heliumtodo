// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Application.Abstractions;
using HeliumTodo.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;

namespace HeliumTodo.Desktop.Services;

/// <summary>
/// 后台提醒扫描。
///
/// 为什么要有它（而不是"关掉主界面就结束"）：
///   用户把窗口关进托盘之后，程序还得继续跑 —— 否则提醒就断了。
///   这个服务就是"后台进程在做的必要的事"：每 30 秒看一次，
///   有没有日程/待办到了该提醒的点。
///
/// 为什么每 30 秒而不是每分钟：
///   提前量是分钟级的，但定时器难免会被系统拖慢几十秒；
///   30 秒扫一次，配合 Core 层 2 分钟的容忍窗，就不会漏。
/// </summary>
public sealed class ReminderService : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);

    /// <summary>容忍窗：比扫描间隔宽，卡一次也补得回来。</summary>
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(2);

    private readonly IServiceProvider _services;
    private readonly INotificationService _notifications;

    /// <summary>已经弹过的键，防止同一个提醒重复弹。</summary>
    private readonly HashSet<string> _fired = new(StringComparer.Ordinal);

    private readonly object _gate = new();
    private Timer? _timer;
    private bool _scanning;

    /// <summary>
    /// 诊断日志钩子。程序是 WinExe，后台发生的事没有别的地方可看 ——
    /// 出问题时"提醒到底有没有算出来"全靠这几行。
    /// </summary>
    public Action<string>? Trace;

    public ReminderService(IServiceProvider services, INotificationService notifications)
    {
        _services = services;
        _notifications = notifications;
    }

    /// <summary>已经弹出过多少条（自检用）。</summary>
    public int FiredCount { get; private set; }

    /// <summary>上一次扫描的时间（自检用）。</summary>
    public DateTime? LastScanAt { get; private set; }

    public void Start()
    {
        lock (_gate)
        {
            _timer ??= new Timer(_ => _ = ScanAsync(), null, TimeSpan.Zero, ScanInterval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>
    /// 立刻扫一遍。返回这一轮弹了几条。
    /// 自检直接调这个，就不用等 30 秒。
    /// </summary>
    public async Task<int> ScanAsync()
    {
        // 上一轮还没跑完就跳过：数据库读慢的时候别排队堆起来
        if (Interlocked.Exchange(ref _scanning, true))
        {
            return 0;
        }

        try
        {
            var schedules = await _services.GetRequiredService<ScheduleRepository>().GetAllAsync();
            var todos = await _services.GetRequiredService<TodoRepository>().GetAllAsync();

            var now = DateTime.Now;
            IReadOnlyList<ReminderItem> due;
            lock (_gate)
            {
                due = ReminderPlanner.CollectDue(schedules, todos, now, Tolerance, _fired);
            }

            foreach (var item in due)
            {
                _notifications.Notify(new NotificationMessage
                {
                    Title = item.Title,
                    Message = item.Message,
                    Tag = $"{item.Kind}",
                    Kind = item.Kind,
                    SourceId = item.SourceId,
                });

                lock (_gate)
                {
                    _fired.Add(item.Key);
                }

                FiredCount++;
                Trace?.Invoke($"[提醒] 弹出「{item.Title}」：{item.Message}");
            }

            LastScanAt = now;
            if (due.Count > 0)
            {
                Trace?.Invoke($"[提醒] 本轮扫到 {due.Count} 条");
            }
            return due.Count;
        }
        catch (Exception ex)
        {
            // 后台定时器里抛异常会静默带走整个进程，这里必须兜住
            System.Diagnostics.Debug.WriteLine($"[提醒] 扫描失败：{ex.Message}");
            return 0;
        }
        finally
        {
            Volatile.Write(ref _scanning, false);
        }
    }

    public void Dispose() => Stop();
}