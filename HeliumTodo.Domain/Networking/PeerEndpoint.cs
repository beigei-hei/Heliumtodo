// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Net;
using System.Net.Sockets;

namespace HeliumTodo.Domain.Networking;

/// <summary>
/// 一个对端地址（主机 + 端口）。
///
/// 为什么专门做一个类型？
///   联机时要处理三种写法，规则还不一样：
///     · 192.168.1.5         ← IPv4，省掉端口
///     · 192.168.1.5:47470   ← IPv4 + 端口
///     · fe80::1c2b:3a4d     ← IPv6，冒号一大堆
///     · [fe80::1c2b:3a4d]:47470  ← IPv6 + 端口，必须加方括号
///   如果不把解析单独抽出来，这段判断会散落在界面层里，
///   而且 IPv6 的方括号规则特别容易写错。抽出来就能直接写单元测试。
/// </summary>
public readonly record struct PeerEndpoint(string Host, int Port)
{
    /// <summary>
    /// 默认端口。
    ///
    /// 随便挑的 47470，选它的原因只是"大于 1024（不需要管理员权限）"
    /// 而且不撞常见服务（3306 MySQL / 5432 Postgres / 8080 HTTP 之类）。
    /// </summary>
    public const int DefaultPort = 47470;

    /// <summary>主机部分是不是一个 IPv6 字面量（加方括号时要用）。</summary>
    public bool IsIPv6Literal =>
        IPAddress.TryParse(Host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6;

    /// <summary>
    /// 把用户输入的文字解析成地址。
    ///
    /// 解析规则：
    ///   · "1.2.3.4"            → 1.2.3.4:默认端口
    ///   · "1.2.3.4:5000"       → 1.2.3.4:5000
    ///   · "::1"                → ::1:默认端口（整个都是地址，没有端口）
    ///   · "[::1]"              → ::1:默认端口
    ///   · "[::1]:5000"         → ::1:5000
    ///   · "my-laptop:5000"     → 也允许，交给系统解析主机名
    /// </summary>
    public static bool TryParse(string? text, out PeerEndpoint endpoint, int defaultPort = DefaultPort)
    {
        endpoint = default;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();

        if (value.Length == 0) return false;

        string host;
        var port = defaultPort;

        if (value[0] == '[')
        {
            // ── 方括号写法：一定是 IPv6（或者带方括号的主机名），端口在 ] 后面 ──
            var close = value.IndexOf(']');

            if (close < 0) return false;                  // 少了右括号，写错了

            host = value[1..close];

            var tail = value[(close + 1)..];

            if (tail.Length > 0)
            {
                if (tail[0] != ':') return false;         // ] 后面只能是 ":端口"

                if (!TryParsePort(tail[1..], out port)) return false;
            }
        }
        else
        {
            var firstColon = value.IndexOf(':');

            if (firstColon < 0)
            {
                // 没有冒号：要么是 IPv4，要么是主机名，要么是不带端口的 IPv6（不可能，IPv6 一定有冒号）
                host = value;
            }
            else if (value.IndexOf(':', firstColon + 1) < 0)
            {
                // 只有一个冒号：拆成"主机 + 端口"。
                // 注意这里可能是 IPv4，也可能是主机名，也可能用户写错了 —— 交给下面统一校验。
                host = value[..firstColon];

                if (!TryParsePort(value[(firstColon + 1)..], out port)) return false;
            }
            else
            {
                // 多个冒号又没加方括号：只能是裸 IPv6，端口用默认值
                host = value;
            }
        }

        host = host.Trim();

        if (host.Length == 0) return false;

        // 主机必须是"能解析的 IP"或者"看起来像主机名"的字符串。
        // 这里不做 DNS 查询 —— 那要联网、会卡、还可能失败，留到真正连接时再说。
        if (!LooksLikeHost(host)) return false;

        endpoint = new PeerEndpoint(host, port);
        return true;
    }

    /// <summary>端口必须能解析成 1~65535 的整数。</summary>
    private static bool TryParsePort(string text, out int port)
    {
        port = 0;

        var value = text.Trim();

        if (value.Length == 0) return false;

        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }

        if (!int.TryParse(value, out var parsed)) return false;

        if (parsed is < 1 or > 65535) return false;

        port = parsed;
        return true;
    }

    /// <summary>主机部分是否合法（IP 字面量，或者由字母数字、点、横线组成的主机名）。</summary>
    private static bool LooksLikeHost(string host)
    {
        if (IPAddress.TryParse(host, out _)) return true;

        // 主机名：只允许常见字符，避免把 "1.2.3.4 后面还有一堆东西" 这种当成合法输入
        foreach (var c in host)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '-' && c != '_') return false;
        }

        return true;
    }

    /// <summary>
    /// 显示用：IPv6 一定要加方括号，否则 "<c>::1:47470</c>" 会被读成别的意思。
    /// </summary>
    public override string ToString() => IsIPv6Literal ? $"[{Host}]:{Port}" : $"{Host}:{Port}";
}
