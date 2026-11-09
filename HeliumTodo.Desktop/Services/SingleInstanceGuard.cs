// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;

namespace HeliumTodo.Desktop.Services;

/// <summary>
/// 单实例守卫：保证同一时间只有一个 Helium Todo 在跑，并让"第二个实例"负责把第一个叫回前台。
///
/// 为什么需要它：
///   这个程序关闭主界面后是留在后台跑的（进程不退出、提醒继续扫）。
///   那么用户想再看界面时，**最自然的动作就是再双击一次程序图标**。
///   如果没有这层保护，第二次启动会开出一个全新的进程、第二份数据库连接、
///   第二个托盘图标 —— 用户会以为"开了两个"，后台那个还占着文件。
///   有了它：第二个实例发现"已经有人在跑"，就把"请把窗口显示出来"这个消息
///   发给第一个实例，然后自己立刻退出。用户看到的是一次干净的复现窗口。
///
/// 它同时是托盘失效时的退路：
///   万一托盘图标因为系统原因加不上（错误码 5 之类的），窗口藏起来之后靠它也能叫回来。
///   所以 App 里"关闭 = 藏到后台"这条规则可以无条件执行，不必依赖托盘是否成功。
///
/// 实现用的是"命名互斥量 + 命名事件"，都是进程间通信里最基础的两个原语，
/// 不需要网络、不需要管道、也不需要管理员权限。
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _waiter;

    public SingleInstanceGuard(string appId)
    {
        // Local\ 前缀表示"只在当前登录会话内有效"：
        // 同一台机器上另一个人（或另一个远程会话）可以各自跑一份，互不干扰。
        _mutex = new Mutex(initiallyOwned: true, $"Local\\{appId}.lock", out var createdNew);
        IsFirstInstance = createdNew;

        if (!createdNew)
        {
            // 不是第一个 —— 那个互斥量不属于我们，别去释放别人的
            _mutex.Dispose();
            _mutex = new Mutex(initiallyOwned: false, $"Local\\{appId}.lock");
        }

        _showEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            name: $"Local\\{appId}.show");
    }

    /// <summary>是不是第一个（也就是"正在跑的那个"）。</summary>
    public bool IsFirstInstance { get; }

    /// <summary>
    /// 让已在运行的那个实例把窗口显示出来。第二个实例调完就可以退出了。
    /// </summary>
    public void RequestShow() => _showEvent.Set();

    /// <summary>
    /// 开始监听"有人要我们显示窗口"。回调发生在后台线程上，
    /// 由调用方自己切到界面线程（Avalonia 的 Dispatcher）。
    /// </summary>
    public void Listen(Action onShowRequested)
    {
        if (!IsFirstInstance || _waiter is not null) return;

        _waiter = new Thread(() =>
        {
            while (!_cts.IsCancellationRequested)
            {
                // 500ms 醒一次，是为了能及时响应退出请求；
                // 用 WaitHandle.WaitAny 也可以，但这里不值得为省一次唤醒引入一个额外句柄。
                if (!_showEvent.WaitOne(500)) continue;

                try
                {
                    onShowRequested();
                }
                catch (Exception ex)
                {
                    // 后台线程抛异常会静默带走进程，必须兜住
                    Debug.WriteLine($"[单实例] 显示窗口失败：{ex.Message}");
                }
            }
        })
        {
            IsBackground = true,
            Name = "helium-single-instance",
        };

        _waiter.Start();
    }

    public void Dispose()
    {
        _cts.Cancel();

        // 不等线程退出：它最多 500ms 就自己看到取消标记了，
        // 而这里是在退出流程里跑的，不该为了收尾把用户卡住半秒。
        _showEvent.Dispose();
        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 释放失败说明我们并不持有它，无所谓
            }
        }

        _mutex.Dispose();
        _cts.Dispose();
    }
}