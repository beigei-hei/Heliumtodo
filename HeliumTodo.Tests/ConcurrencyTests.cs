// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Application.Exceptions;
using HeliumTodo.Domain.Models;
using HeliumTodo.Infrastructure;
using HeliumTodo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeliumTodo.Tests;

public sealed class ConcurrencyTests
{
    // 为什么值得测：两个窗口同时改同一行时，后提交的一方必须被 xmin 拦住；
    // 如果这里静默成功，用户在前一个窗口的修改就被悄悄覆盖了。
    [Fact]
    public async Task 两个上下文改同一行时后提交的一方拿到冲突()
    {
        using var pg = new PostgresTestDatabase("concurrency");
        if (pg.SkipReason is { } reason)
        {
            // 没配 HELIUM_TEST_POSTGRES 时按仓库惯例静默通过（其余真库用例同样处理）。
            return;
        }

        var options = new DbContextOptionsBuilder<HeliumDbContext>()
            .UseNpgsql(pg.ConnectionString)
            .Options;

        await using (var seed = new HeliumDbContext(options))
        {
            DatabaseInitializer.Initialize(seed, seedIfEmpty: false);
            seed.Todos.Add(new TodoItem
            {
                Title = "并发基线",
                CreatedAt = DateTime.UtcNow,
                Category = "测试",
            });
            await seed.SaveChangesAsync();
        }

        await using var first = new HeliumDbContext(options);
        await using var second = new HeliumDbContext(options);

        var firstCopy = await first.Todos.SingleAsync();
        var secondCopy = await second.Todos.SingleAsync();

        firstCopy.Title = "窗口 A 改的";
        await first.SaveChangesAsync();

        secondCopy.Title = "窗口 B 改的";
        var conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => second.SaveGuardedAsync());

        Assert.Contains("其它窗口", conflict.Message);
    }
}