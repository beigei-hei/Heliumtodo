// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Application.Ipc;

/// <summary>
/// 进程间通信的预留接口。
///
/// 现在的状态：**只有契约，没有实现**。
/// 之所以先留出来，是因为「和别的软件互通数据」这件事一旦要做，
/// 最难改的不是通道怎么开，而是"外面的人该按什么格式问我们、我们怎么答" ——
/// 所以先把这份契约定下来，将来接命名管道 / 本地套接字时只需换一个实现。
///
/// 通道的具体形态（管道名、端口、权限）属于实现细节，不写进接口。
/// </summary>
public interface IIpcChannel : IAsyncDisposable
{
    /// <summary>通道是否已经在监听。</summary>
    bool IsRunning { get; }

    /// <summary>对外公布的服务标识 —— 别的程序靠它找到我们。</summary>
    string Endpoint { get; }

    /// <summary>启动监听。</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>停止监听。</summary>
    Task StopAsync();

    /// <summary>主动向对端发一条请求（对端是"我们连出去"的那个程序）。</summary>
    Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken = default);

    /// <summary>收到别的地方发来的请求时触发。</summary>
    event EventHandler<IpcRequest>? RequestReceived;
}

/// <summary>
/// 什么都不做的通道实现。
///
/// 这是现在的默认实现：程序正常跑，但对外没有任何入口。
/// 将来要做真正的互通时，写一个 <c>NamedPipeIpcChannel</c> 换来这一行注册即可，
/// 调用它的一方不需要改任何代码。
/// </summary>
public sealed class NullIpcChannel : IIpcChannel
{
    public bool IsRunning => false;

    public string Endpoint => string.Empty;

    public event EventHandler<IpcRequest>? RequestReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _ = RequestReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;

    public Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(IpcResponse.Failure("没有可用的进程间通道。"));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}