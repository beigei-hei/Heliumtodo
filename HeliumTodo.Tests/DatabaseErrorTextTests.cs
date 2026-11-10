// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Net.Sockets;
using HeliumTodo.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 数据库异常翻译成中文提示的用例。
///
/// 为什么值得测：用户截图里看到的是英文重试原文，只会以为程序坏了；
/// 这几条分支分别对应「没启动」「密码错」「库不存在」「超时」，
/// 翻错一条就会把人引到错误的排查方向。
/// </summary>
public class DatabaseErrorTextTests
{
    [Fact]
    public void 连接被拒绝时提示检查服务与端口()
    {
        var text = DatabaseErrorText.Describe(
            new NpgsqlException("outer", new SocketException((int)SocketError.ConnectionRefused)),
            "Host=127.0.0.1;Port=5432;Password=***");

        Assert.Contains("连不上 PostgreSQL", text);
        Assert.Contains("服务已启动", text);
        Assert.Contains("Port=5432", text);
    }

    [Fact]
    public void 密码错误时提示检查用户名密码()
    {
        var pg = new PostgresException("password authentication failed", "ERROR", "ERROR", "28P01");
        var text = DatabaseErrorText.Describe(new NpgsqlException("outer", pg));

        Assert.Contains("用户名或密码", text);
    }

    [Fact]
    public void 数据库不存在时提示先创建()
    {
        var pg = new PostgresException("database does not exist", "ERROR", "ERROR", "3D000");
        var text = DatabaseErrorText.Describe(pg);

        Assert.Contains("数据库不存在", text);
        Assert.Contains("createdb", text);
    }

    [Fact]
    public void 超时提示检查主机与防火墙()
    {
        var text = DatabaseErrorText.Describe(new TimeoutException("timeout while connecting"));

        Assert.Contains("超时", text);
        Assert.Contains("防火墙", text);
    }

    [Fact]
    public void 其它异常保留一行原文不吞掉()
    {
        var text = DatabaseErrorText.Describe(new InvalidOperationException("boom\r\nsecond line"));

        Assert.Contains("数据库操作失败", text);
        Assert.Contains("boom", text);
        Assert.DoesNotContain("\r", text);
        Assert.DoesNotContain("\n", text);
    }
}
