// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HeliumTodo.Infrastructure.Persistence;

/// <summary>
/// 只在设计时（`dotnet ef migrations add` / `dotnet ef database update`）使用。
///
/// 运行期由 `AddHeliumData` 注入真实连接串；这里只是给 EF 工具一个能构造
/// DbContext 的入口。连接串优先读 `HELIUM_DB`，否则落回本机便携 PostgreSQL。
/// 这两种情况下模型完全一致，生成的 Migration 不受连接串影响。
/// </summary>
public sealed class HeliumDbContextFactory : IDesignTimeDbContextFactory<HeliumDbContext>
{
    public HeliumDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HELIUM_DB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=127.0.0.1;Port=55432;Database=helium_todo;Username=postgres";
        }

        var options = new DbContextOptionsBuilder<HeliumDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new HeliumDbContext(options);
    }
}