namespace HeliumTodo.Core.Lifecycle;

/// <summary>
/// 一次"重启自己"尝试的结果。
/// </summary>
public enum RestartOutcome
{
    /// <summary>新进程已经起来了，本进程随后退出。</summary>
    Started,

    /// <summary>
    /// 当前进程挂在调试器下面，不能自己重启。
    ///
    /// 为什么这种情况必须单独拿出来：
    ///   调试器启动的是它自己管的那个子进程。我们从里面再 <c>Process.Start</c> 一个，
    ///   新进程不受调试器管，于是调试会话在旧进程退出时就结束了，新进程却还在跑 ——
    ///   两个进程都抓着 <c>bin\Debug\net10.0\HeliumTodo.App.exe</c>，
    ///   下一次编译就是 <c>MSB3027 文件正由另一进程使用</c>，而用户在界面上
    ///   既找不到那个新实例（它可能已经藏进托盘），也不知道该杀谁。
    /// </summary>
    BlockedByDebugger,

    /// <summary>没重启成（拿不到 exe 路径、或起新进程抛了异常）。</summary>
    Failed,
}

/// <summary>
/// 决定"这次重启该不该真的起新进程"。抽出来是为了能在单元测试里断言，
/// 不用真的挂一个调试器。
/// </summary>
public static class RestartPolicy
{
    /// <summary>
    /// 调试器在场时一律不自动重启，交给调试器自己的「重新启动」。
    /// </summary>
    public static RestartOutcome Decide(bool debuggerAttached)
        => debuggerAttached ? RestartOutcome.BlockedByDebugger : RestartOutcome.Started;
}
