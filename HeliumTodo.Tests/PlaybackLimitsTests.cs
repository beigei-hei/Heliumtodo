// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Media;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 动态壁纸播放限制的测试。
///
/// 为什么要测这个？
///   用户的原话是「动图支持mp4并在实际渲染时将帧率锁定在30帧以下」，
///   而"不超过 30 帧/秒"这件事没有任何界面能看出来 ——
///   播放快了只是风扇响一点、电量掉得快一点，谁也不会去数帧。
///   所以只能用测试把它钉住：间隔 20 ms 的 GIF、16 ms 的 60 帧 MP4，
///   都必须被抬到 34 ms。
/// </summary>
public class PlaybackLimitsTests
{
    [Fact]
    public void 帧率上限应是30帧每秒()
    {
        Assert.Equal(30, PlaybackLimits.MaxFramesPerSecond);

        // 34 ms 的反算：1000 / 34 = 29.4 帧/秒，确实在 30 以下。
        // 取 33 就不行了（30.3 帧/秒），这条断言防的就是有人"顺手改成 33"。
        Assert.Equal(34, PlaybackLimits.MinFrameIntervalMs);
        Assert.True(1000.0 / PlaybackLimits.MinFrameIntervalMs < PlaybackLimits.MaxFramesPerSecond);
    }

    [Fact]
    public void 过快的帧间隔应被抬到下限()
    {
        // GIF 常见的 20 ms、60 帧视频的 16 ms，都会被抬到 34 ms
        Assert.Equal(34, PlaybackLimits.ClampFrameDelay(20));
        Assert.Equal(34, PlaybackLimits.ClampFrameDelay(16));
        Assert.Equal(34, PlaybackLimits.ClampFrameDelay(33));
    }

    [Fact]
    public void 正常范围内的间隔应原样保留()
    {
        // 34 ms 本身（正好 29.4 帧/秒）不该被改动
        Assert.Equal(34, PlaybackLimits.ClampFrameDelay(34));
        Assert.Equal(67, PlaybackLimits.ClampFrameDelay(67));
        Assert.Equal(100, PlaybackLimits.ClampFrameDelay(100));
        Assert.Equal(2000, PlaybackLimits.ClampFrameDelay(2000));
    }

    [Fact]
    public void 零或负的间隔应换成兜底值()
    {
        // 有些 GIF 的帧延迟字段就是 0，浏览器约定按 100 ms 处理
        Assert.Equal(100, PlaybackLimits.ClampFrameDelay(0));
        Assert.Equal(100, PlaybackLimits.ClampFrameDelay(-5));
    }

    [Fact]
    public void 过长的间隔应被压到上限()
    {
        // 一帧停一分钟就是"卡死"了，压到 10 秒
        Assert.Equal(10_000, PlaybackLimits.ClampFrameDelay(60_000));
        Assert.Equal(10_000, PlaybackLimits.ClampFrameDelay(10_000));
    }
}