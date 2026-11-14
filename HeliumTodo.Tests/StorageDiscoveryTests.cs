// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Application.Configuration;
using HeliumTodo.Infrastructure.Persistence;
using Xunit;

namespace HeliumTodo.Tests;

public sealed class StorageDiscoveryTests
{
    // 为什么值得测：用户不该为了连上数据库去手改 config.json；
    // 即使配置里的用户名/端口过时，自动检测也要能找到本机可用的那组参数。
    [Fact]
    public void 用户名写错时自动检测能找到可用的本机数据库()
    {
        using var pg = new PostgresTestDatabase("storage_discovery");
        if (pg.SkipReason is not null)
        {
            return;
        }

        var admin = new Npgsql.NpgsqlConnectionStringBuilder(pg.ConnectionString);
        var configured = new StorageSettings
        {
            Provider = "postgres",
            Host = admin.Host ?? "127.0.0.1",
            Port = admin.Port,
            Database = admin.Database ?? "helium_todo",
            Username = "definitely_missing_role",
            Password = string.Empty,
        };

        var ok = StorageDiscovery.TryDiscover(configured, out var found, out var reason);

        Assert.True(ok, reason);
        Assert.Equal(admin.Username, found.Username);
        Assert.Equal(configured.Port, found.Port);
        Assert.Equal(configured.Database, found.Database);
        Assert.False(string.IsNullOrWhiteSpace(found.PgDataDirectory));
    }

    // 为什么值得测：自动检测是启动路径，只允许碰本机回环地址，不能变成局域网扫描器。
    [Fact]
    public void 非本机地址不会被自动检测()
    {
        var configured = new StorageSettings
        {
            Provider = "postgres",
            Host = "192.168.1.10",
            Port = 5432,
            Database = "helium_todo",
            Username = "postgres",
            Password = string.Empty,
        };

        var ok = StorageDiscovery.TryDiscover(configured, out _, out var reason);

        Assert.False(ok);
        Assert.Contains("本机", reason);
    }
}
