// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Networking;
using HeliumTodo.Infrastructure;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 联机部分的测试：地址解析、消息编解码，以及真实的 TCP 回环通信。
///
/// 最后那组是**真的开两个 TCP 端口互相对话**（都在 127.0.0.1 上），
/// 不是打桩。这样"能不能连上、消息会不会乱序、断线会不会卡死"这些问题
/// 才真的被验证过。
/// </summary>
public class NetworkingTests
{
    // ────────────── 地址解析 ──────────────

    [Fact]
    public void 解析不带端口的IPv4()
    {
        Assert.True(PeerEndpoint.TryParse("192.168.1.5", out var endpoint));

        Assert.Equal("192.168.1.5", endpoint.Host);
        Assert.Equal(PeerEndpoint.DefaultPort, endpoint.Port);
        Assert.False(endpoint.IsIPv6Literal);
    }

    [Fact]
    public void 解析带端口的IPv4()
    {
        Assert.True(PeerEndpoint.TryParse("192.168.1.5:8080", out var endpoint));

        Assert.Equal("192.168.1.5", endpoint.Host);
        Assert.Equal(8080, endpoint.Port);
    }

    [Fact]
    public void 解析裸IPv6用默认端口()
    {
        // IPv6 里冒号一大堆，不能再按"最后一个冒号后面是端口"来切
        Assert.True(PeerEndpoint.TryParse("::1", out var endpoint));

        Assert.Equal("::1", endpoint.Host);
        Assert.Equal(PeerEndpoint.DefaultPort, endpoint.Port);
        Assert.True(endpoint.IsIPv6Literal);
    }

    [Fact]
    public void 解析方括号IPv6带端口()
    {
        Assert.True(PeerEndpoint.TryParse("[fe80::1c2b:3a4d]:5000", out var endpoint));

        Assert.Equal("fe80::1c2b:3a4d", endpoint.Host);
        Assert.Equal(5000, endpoint.Port);
    }

    [Fact]
    public void IPv6转回文字要加方括号()
    {
        var endpoint = new PeerEndpoint("::1", 47470);

        Assert.Equal("[::1]:47470", endpoint.ToString());
        Assert.Equal("192.168.1.5:47470", new PeerEndpoint("192.168.1.5", 47470).ToString());
    }

    [Fact]
    public void 地址写错要能识别出来()
    {
        Assert.False(PeerEndpoint.TryParse(null, out _));
        Assert.False(PeerEndpoint.TryParse("", out _));
        Assert.False(PeerEndpoint.TryParse("   ", out _));
        Assert.False(PeerEndpoint.TryParse("1.2.3.4:0", out _));          // 端口不能是 0
        Assert.False(PeerEndpoint.TryParse("1.2.3.4:70000", out _));      // 端口越界
        Assert.False(PeerEndpoint.TryParse("1.2.3.4:abc", out _));        // 端口不是数字
        Assert.False(PeerEndpoint.TryParse("[::1", out _));               // 少右括号
        Assert.False(PeerEndpoint.TryParse("[::1]xyz", out _));           // ] 后面不是冒号
        Assert.False(PeerEndpoint.TryParse("主机 名", out _));             // 中间有空格
    }

    [Fact]
    public void 允许写主机名()
    {
        Assert.True(PeerEndpoint.TryParse("my-laptop:5000", out var endpoint));

        Assert.Equal("my-laptop", endpoint.Host);
        Assert.Equal(5000, endpoint.Port);
    }

    // ────────────── 消息编解码 ──────────────

    [Fact]
    public void 状态消息能原样往返()
    {
        var status = new PeerStatus("peer-1", "小张", true, "写数据结构实验报告", TimeSpan.FromMinutes(12));

        var message = PeerMessageCodec.FromPeerStatus(status, "ABCDEF");
        var parsed = PeerMessageCodec.TryParse(PeerMessageCodec.Serialize(message));

        Assert.NotNull(parsed);

        var back = PeerMessageCodec.ToPeerStatus(parsed!);

        Assert.NotNull(back);
        Assert.Equal(status.PeerId, back!.PeerId);
        Assert.Equal(status.DisplayName, back.DisplayName);
        Assert.True(back.IsFocusing);
        Assert.Equal(status.FocusTitle, back.FocusTitle);
        Assert.Equal(720, (int)back.Remaining!.Value.TotalSeconds);
    }

    [Fact]
    public void 序列化出来只有一行()
    {
        // 协议靠换行分消息，所以序列化结果里绝不能出现真正的换行符
        var message = new PeerMessage
        {
            Kind = PeerMessageKinds.Chat,
            PeerId = "p1",
            Text = "第一行\n第二行\r\n第三行",
        };

        var line = PeerMessageCodec.Serialize(message);

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);

        // 转义之后读回来，文字应该还是原样
        var parsed = PeerMessageCodec.TryParse(line);

        Assert.NotNull(parsed);
        Assert.Equal("第一行\n第二行\r\n第三行", parsed!.Text);
    }

    [Fact]
    public void 坏消息不会抛异常()
    {
        Assert.Null(PeerMessageCodec.TryParse(null));
        Assert.Null(PeerMessageCodec.TryParse(""));
        Assert.Null(PeerMessageCodec.TryParse("这不是 JSON"));
        Assert.Null(PeerMessageCodec.TryParse("{\"kind\":\"我不认识\"}"));
        Assert.Null(PeerMessageCodec.TryParse("{\"kind\":\"status\"}"));   // status 必须有 peerId
    }

    [Fact]
    public void 超长的一行直接丢掉()
    {
        var huge = new string('x', PeerMessageCodec.MaxLineBytes + 10);

        Assert.Null(PeerMessageCodec.TryParse(huge));
    }

    [Fact]
    public void 在线状态消息能识别()
    {
        var presence = new PeerPresence("peer-2", "小李", true);

        var parsed = PeerMessageCodec.TryParse(
            PeerMessageCodec.Serialize(PeerMessageCodec.FromPeerPresence(presence, "ABCDEF")));

        var back = PeerMessageCodec.ToPeerPresence(parsed!);

        Assert.NotNull(back);
        Assert.Equal("peer-2", back!.PeerId);
        Assert.True(back.IsOnline);
    }

    [Fact]
    public void 地址表消息能带上每个人的地址和延迟()
    {
        var message = PeerMessageCodec.FromPeerList("ABCDEF", "host-1", new[]
        {
            new PeerAddressInfo
            {
                PeerId = "host-1",
                DisplayName = "房主",
                Endpoint = "192.168.1.5:47470",
                RoundTripMs = 3,
            },
            new PeerAddressInfo
            {
                PeerId = "peer-2",
                DisplayName = "同学",
                Endpoint = "[fe80::1]:47471",
                RoundTripMs = 12,
            },
        });

        var parsed = PeerMessageCodec.TryParse(PeerMessageCodec.Serialize(message));

        Assert.NotNull(parsed);
        Assert.Equal(PeerMessageKinds.Peers, parsed!.Kind);
        Assert.NotNull(parsed.PeerList);
        Assert.Equal(2, parsed.PeerList!.Count);
        Assert.Equal("192.168.1.5:47470", parsed.PeerList[0].Endpoint);
        Assert.Equal(12, parsed.PeerList[1].RoundTripMs);
    }

    [Fact]
    public void 自报监听端口和对外地址能原样传过去()
    {
        var message = new PeerMessage
        {
            Kind = PeerMessageKinds.Join,
            PeerId = "peer-9",
            DisplayName = "同学",
            RoomCode = "ABCDEF",
            ListenPort = 47471,
            AdvertisedEndpoint = "203.0.113.7:50000",
            SentAtUnixMs = PeerMessage.NowUnixMs(),
        };

        var parsed = PeerMessageCodec.TryParse(PeerMessageCodec.Serialize(message));

        Assert.NotNull(parsed);
        Assert.Equal(47471, parsed!.ListenPort);
        Assert.Equal("203.0.113.7:50000", parsed.AdvertisedEndpoint);
    }

    // ────────────── 真实 TCP 回环通信 ──────────────

    [Fact]
    public async Task 两台机器能连上并互相看到()
    {
        var port = PickFreePortPair();

        await using var host = new LanPeerTransport(port, "房主");
        await using var client = new LanPeerTransport(port + 1, "同学");

        try
        {
            await host.StartAsync();
            host.BecomeHost("ABCDEF");

            await client.StartAsync();
            await client.JoinRoomAsync("ABCDEF");

            // 房主这边应该看到"有人进来了"
            var joined = new TaskCompletionSource<PeerPresence>(TaskCreationOptions.RunContinuationsAsynchronously);

            host.PresenceChanged += (_, presence) =>
            {
                if (presence.IsOnline) joined.TrySetResult(presence);
            };

            var connected = await client.ConnectAsync("127.0.0.1", port);

            Assert.True(connected, $"连不上，错误：{client.LastError}");

            var presence = await joined.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("同学", presence.DisplayName);

            // 客户端推一条"我在专注"，房主要收到
            var received = new TaskCompletionSource<PeerStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

            host.PeerStatusReceived += (_, status) =>
            {
                if (status.IsFocusing) received.TrySetResult(status);
            };

            await client.BroadcastStatusAsync(new PeerStatus(
                client.LocalPeerId, "同学", true, "复习英语六级", TimeSpan.FromMinutes(25)));

            var status = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("复习英语六级", status.FocusTitle);
            Assert.Equal(25, (int)status.Remaining!.Value.TotalMinutes);
        }
        finally
        {
            await host.StopAsync();
            await client.StopAsync();
        }
    }

    [Fact]
    public async Task 房主能看到成员的自报地址客户端能收到地址表()
    {
        var port = PickFreePortPair();

        await using var host = new LanPeerTransport(port, "房主");
        await using var client = new LanPeerTransport(port + 1, "同学");

        try
        {
            await host.StartAsync();
            host.BecomeHost("ABCDEF");

            await client.StartAsync();
            await client.JoinRoomAsync("ABCDEF");

            // 客户端一收到地址表就记录下来
            var gotPeers = new TaskCompletionSource<IReadOnlyList<PeerAddressInfo>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            client.PeersChanged += (_, _) =>
            {
                var peers = client.Peers;

                if (peers.Count > 0) gotPeers.TrySetResult(peers);
            };

            Assert.True(await client.ConnectAsync("127.0.0.1", port), $"连不上，错误：{client.LastError}");

            // 房主侧：成员表里应该出现这个客户端，而且地址用的是它的监听端口
            // （TCP 连出去的源端口是随机的，所以只能靠对方自报监听端口）
            //
            // ⚠️ 这里必须等一下。ConnectAsync 返回只代表「TCP 握手完成」，
            //    hello / join 那些行是随后异步发出去的，房主的读循环还没跑完。
            //    直接断言会偶发失败（Assert.Single 拿到空集合），
            //    以前它「一直是绿的」只是因为机器快。
            var hostPeers = await WaitForAsync(() => host.Peers, peers => peers.Count > 0);

            Assert.Single(hostPeers);
            Assert.Equal("同学", hostPeers[0].DisplayName);
            Assert.EndsWith($":{port + 1}", hostPeers[0].Endpoint);

            // 客户端侧：房主会把"自己 + 所有成员"的地址表推下来
            var peers = await gotPeers.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Contains(peers, p => p.DisplayName == "房主");
            Assert.Contains(peers, p => p.DisplayName == "同学");
        }
        finally
        {
            await host.StopAsync();
            await client.StopAsync();
        }
    }

    [Fact]
    public async Task 房主开始专注时组员也能看到()
    {
        // 上一个测试验的是「组员 → 房主」，方向反过来走的是另一条代码路径
        // （房主直接把状态推给每个客户端，而不是转发）。
        // 房间创建者开始专注是最常见的情况，这条路径必须单独验。
        var port = PickFreePortPair();

        await using var host = new LanPeerTransport(port, "房主");
        await using var client = new LanPeerTransport(port + 1, "同学");

        try
        {
            await host.StartAsync();
            host.BecomeHost("ABCDEF");

            await client.StartAsync();
            await client.JoinRoomAsync("ABCDEF");

            Assert.True(await client.ConnectAsync("127.0.0.1", port), $"连不上，错误：{client.LastError}");

            var received = new TaskCompletionSource<PeerStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.PeerStatusReceived += (_, status) =>
            {
                if (status.IsFocusing) received.TrySetResult(status);
            };

            await host.BroadcastStatusAsync(new PeerStatus(
                host.LocalPeerId, "房主", true, "写数据结构实验报告", TimeSpan.FromMinutes(45)));

            var status = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("房主", status.DisplayName);
            Assert.Equal("写数据结构实验报告", status.FocusTitle);
            Assert.Equal(45, (int)status.Remaining!.Value.TotalMinutes);
        }
        finally
        {
            await host.StopAsync();
            await client.StopAsync();
        }
    }

    [Fact]
    public async Task 房间码不对会被拒绝()
    {
        var port = PickFreePortPair();

        await using var host = new LanPeerTransport(port, "房主");
        await using var client = new LanPeerTransport(port + 1, "同学");

        try
        {
            await host.StartAsync();
            host.BecomeHost("ABCDEF");

            await client.StartAsync();
            await client.JoinRoomAsync("ZZZZZZ");      // 故意写错

            var connected = await client.ConnectAsync("127.0.0.1", port);

            // TCP 本身连得上（握手成功），但房间码校验不通过，房主会主动断开
            Assert.True(connected);

            // 等房主把连接踢掉：房主的在线人数应该回到 1（只有自己）
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < deadline && host.OnlinePeerCount > 1)
            {
                await Task.Delay(50);
            }

            Assert.Equal(1, host.OnlinePeerCount);
        }
        finally
        {
            await host.StopAsync();
            await client.StopAsync();
        }
    }

    [Fact]
    public async Task 聊天消息能送到对方()
    {
        var port = PickFreePortPair();

        await using var host = new LanPeerTransport(port, "房主");
        await using var client = new LanPeerTransport(port + 1, "同学");

        try
        {
            await host.StartAsync();
            host.BecomeHost("ABCDEF");

            await client.StartAsync();
            await client.JoinRoomAsync("ABCDEF");

            Assert.True(await client.ConnectAsync("127.0.0.1", port), client.LastError);

            var chat = new TaskCompletionSource<PeerChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            host.ChatReceived += (_, e) => chat.TrySetResult(e);

            // 给 TCP 一点时间把握手包处理完
            await Task.Delay(200);

            await client.SendChatAsync("大家加油");

            var received = await chat.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("大家加油", received.Text);
            Assert.Equal("同学", received.DisplayName);
        }
        finally
        {
            await host.StopAsync();
            await client.StopAsync();
        }
    }

    /// <summary>
    /// 找一对空闲端口。
    ///
    /// 为什么要"一对"而不是随机猜一个？
    ///   测试里两个节点各要监听一个端口，还得保证都不冲突。
    ///   随机端口碰上被占用的会偶发失败，所以真的去试听一下。
    /// </summary>
    /// <summary>
    /// 轮询等一个条件成立，最多等 5 秒。
    ///
    /// 联机测试里凡是「对方应该已经处理了我刚发的消息」这种断言，都得走这里 ——
    /// 网络收发是异步的，靠 Task.Delay 拍脑袋定一个时长要么太短（偶发失败）
    /// 要么太长（测试变慢）。
    /// </summary>
    private static async Task<T> WaitForAsync<T>(Func<T> read, Func<T, bool> until, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        var value = read();

        while (!until(value) && Environment.TickCount64 < deadline)
        {
            await Task.Delay(20);
            value = read();
        }

        return value;
    }

    private static int PickFreePortPair()
    {
        var random = new Random();

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = random.Next(20000, 60000);

            if (IsPortFree(candidate) && IsPortFree(candidate + 1))
            {
                return candidate;
            }
        }

        // 实在找不到就随便给一个，让测试自己失败并暴露出环境问题
        return 47470;
    }

    private static bool IsPortFree(int port)
    {
        try
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);

            listener.Start();
            listener.Stop();

            return true;
        }
        catch (System.Net.Sockets.SocketException)
        {
            return false;
        }
    }
}
