// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Lifecycle;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 「要不要自己重启」这条判断的测试。
///
/// 为什么要测这个？
///   这不是一道数学题，是一条**踩过坑的产品规则**：
///   在 Visual Studio 里调试时点「确认」重启，我们从调试器管的子进程里
///   再 Process.Start 一个 —— 新进程不受调试器管，调试会话在旧进程退出时就结束了，
///   新进程却还占着 bin\Debug\net10.0 下的 exe 和 dll。
///   下一次编译立刻 MSB3027「文件正由另一进程使用」，而用户在界面上既找不到
///   那个实例（它已经藏进托盘），也不知道该杀谁。
///   所以规则要钉死：调试器在场 → 一律不自己重启，交给调试器的「重新启动」。
/// </summary>
public class RestartPolicyTests
{
    [Fact]
    public void 调试器在场时不能自动重启()
    {
        // 这就是用户在 VS 里遇到的那个场景
        var outcome = RestartPolicy.Decide(debuggerAttached: true);

        Assert.Equal(RestartOutcome.BlockedByDebugger, outcome);
    }

    [Fact]
    public void 没有调试器时应当起新进程()
    {
        var outcome = RestartPolicy.Decide(debuggerAttached: false);

        Assert.Equal(RestartOutcome.Started, outcome);
    }

    [Fact]
    public void 结果里要有失败这一档()
    {
        // 第三种结果（Failed）不由 Decide 产生，而是 App.TryRestart 里
        // 「拿不到 exe 路径 / 起进程抛异常」时的返回值。这里只是把它钉在枚举里，
        // 防止以后有人合并枚举值时把"没重启成"和"在调试器里"混成同一个状态 ——
        // 两者给用户的提示完全不同（一个要去找 VS 的重新启动，一个要手动重开）。
        Assert.NotEqual(RestartOutcome.BlockedByDebugger, RestartOutcome.Failed);
        Assert.NotEqual(RestartOutcome.Started, RestartOutcome.Failed);
    }
}
