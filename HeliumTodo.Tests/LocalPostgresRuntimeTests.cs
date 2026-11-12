// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Application.Configuration;
using HeliumTodo.Infrastructure.Persistence;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 便携 PostgreSQL 启动器的边界：宁可返回「没配置 / 找不到」，也不要去启动错误的路径。
/// </summary>
public class LocalPostgresRuntimeTests
{
    // 没配置 pg_ctl 时不能启动任何进程，提示里必须带配置项名字。
    [Fact]
    public void 未配置路径时给出明确原因()
    {
        var storage = new StorageSettings();

        var started = LocalPostgresRuntime.TryStart(storage, Path.GetTempPath(), out var reason);

        Assert.False(started);
        Assert.Contains("PgCtlPath", reason);
    }

    // 路径写错时要直接说找不到，不能等到 Process.Start 才抛异常。
    [Fact]
    public void pgctl不存在时不启动进程()
    {
        var storage = new StorageSettings
        {
            PgCtlPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "pg_ctl.exe"),
            PgDataDirectory = Path.GetTempPath(),
        };

        var started = LocalPostgresRuntime.TryStart(storage, Path.GetTempPath(), out var reason);

        Assert.False(started);
        Assert.Contains("找不到 pg_ctl.exe", reason);
    }
}
