// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Lifecycle;
using Xunit;

namespace HeliumTodo.Tests;

public class AppIdentityTests
{
    // 用户没设环境变量时，app id 必须就是那个老名字 —— 否则升级之后
    // 老实例（用旧名字占着互斥量）和新实例会各建一个，变成两个后台进程。
    [Fact]
    public void 没设环境变量时用默认名字()
    {
        Assert.Equal("HeliumTodo", AppIdentity.Resolve(null));
        Assert.Equal("HeliumTodo", AppIdentity.Resolve(""));
        Assert.Equal("HeliumTodo", AppIdentity.Resolve("   "));
    }

    // 探针靠这个做隔离：设了就必须原样生效，否则两份实例又会抢同一个互斥量。
    [Fact]
    public void 设了环境变量就用它并去掉首尾空白()
    {
        Assert.Equal("HeliumTodoProbe", AppIdentity.Resolve("HeliumTodoProbe"));
        Assert.Equal("HeliumTodoProbe", AppIdentity.Resolve("  HeliumTodoProbe \t"));
    }

    // 名字里允许带用户目录之类的内容，不能被截断或改写（只做 strip）。
    [Fact]
    public void 不会改写名字里的其他字符()
    {
        Assert.Equal("Helium-Todo_2", AppIdentity.Resolve("Helium-Todo_2"));
    }
}
