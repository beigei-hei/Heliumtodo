// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Notifications;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 提示条自动消失的时长规则。
///
/// 为什么要测这个？
///   提示条以前会一直挂着 —— 用户改完一个设置去改下一个，上次的提示还压在
///   功能区上方，看上去像"一直没存成功"。现在要求 3 秒后自己收起来。
///   时长这个数字同时被界面（排定时器）和自检（判断该不该消失）用到，
///   两边各写一个 3000 迟早会不一致，所以钉在 Core 里。
/// </summary>
public class NoticePolicyTests
{
    [Fact]
    public void 提示条应当显示三秒()
    {
        Assert.Equal(3000, NoticePolicy.DisplayMilliseconds);
    }

    [Fact]
    public void 不到三秒不该消失()
    {
        // 2.9 秒还在看 —— 提前收掉会让用户"咦怎么没了"。
        Assert.False(NoticePolicy.ShouldExpire(0));
        Assert.False(NoticePolicy.ShouldExpire(1500));
        Assert.False(NoticePolicy.ShouldExpire(2999));
    }

    [Fact]
    public void 满三秒就消失()
    {
        Assert.True(NoticePolicy.ShouldExpire(3000));
        Assert.True(NoticePolicy.ShouldExpire(3001));
        Assert.True(NoticePolicy.ShouldExpire(60_000));
    }
}
