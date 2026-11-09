// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using HeliumTodo.Domain.Models;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 分层铁律的机器检查：Domain 必须能被纯控制台测试，不能把界面/数据库拖进来。
/// 光靠人眼看 using 迟早会漏，所以这里直接查程序集引用。
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(ScheduleItem).Assembly;

    // 为什么值得测：Domain 一旦引用 Avalonia，无头测试就会要求图形栈，CI 直接挂
    [Fact]
    public void Domain程序集不引用界面框架()
    {
        var names = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();

        Assert.DoesNotContain(names, n => n.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.StartsWith("FluentAvalonia", StringComparison.OrdinalIgnoreCase));
    }

    // 为什么值得测：PostgreSQL 的连接类型漏进 Domain 后，换存储就要改领域规则
    [Fact]
    public void Domain程序集不引用数据库驱动()
    {
        var names = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();

        Assert.DoesNotContain(names, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.StartsWith("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase));
    }

    // 为什么值得测：依赖方向反了（Domain→Application/Infrastructure）编译期不一定报错
    [Fact]
    public void Domain只依赖基础类库()
    {
        var names = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();

        Assert.DoesNotContain("HeliumTodo.Application", names);
        Assert.DoesNotContain("HeliumTodo.Infrastructure", names);
    }
}