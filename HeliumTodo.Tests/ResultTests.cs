// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Application.Contracts;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>用例返回值必须能区分"业务失败"和"成功但值为空"。</summary>
public class ResultTests
{
    // 为什么值得测：失败时调用方若读了 Value 会拿到 default，必须靠 IsSuccess 拦住
    [Fact]
    public void 失败结果不携带值且保留原因()
    {
        var result = Result<int>.Fail("时段冲突");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Equal("时段冲突", result.Error);
    }

    // 为什么值得测：成功路径不能残留错误信息，否则界面会闪一条红字
    [Fact]
    public void 成功结果携带值且没有错误()
    {
        var result = Result<string>.Ok("完成");

        Assert.True(result.IsSuccess);
        Assert.Equal("完成", result.Value);
        Assert.Null(result.Error);
    }

    // 为什么值得测：无返回值用例也要能报告失败原因
    [Fact]
    public void 无返回值结果同样能表达失败()
    {
        var failed = Result.Fail("数据库不可用");
        var ok = Result.Ok();

        Assert.False(failed.IsSuccess);
        Assert.Equal("数据库不可用", failed.Error);
        Assert.True(ok.IsSuccess);
        Assert.Null(ok.Error);
    }
}