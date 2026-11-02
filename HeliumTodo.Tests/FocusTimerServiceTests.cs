using HeliumTodo.Core.Services;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>专注计时器的测试。</summary>
public class FocusTimerServiceTests
{
    [Fact]
    public void 初始状态是空闲()
    {
        var timer = new FocusTimerService();

        Assert.Equal(FocusPhase.Idle, timer.Phase);
        Assert.Equal(TimeSpan.Zero, timer.Elapsed);
        Assert.False(timer.IsActive);
    }

    [Fact]
    public void 开始专注后进入专注状态()
    {
        var timer = new FocusTimerService();
        timer.Start(TimeSpan.FromMinutes(25), "写实验报告");

        Assert.Equal(FocusPhase.Focusing, timer.Phase);
        Assert.True(timer.IsActive);
        Assert.Equal("写实验报告", timer.Title);
        Assert.Equal(TimeSpan.FromMinutes(25), timer.Remaining);
    }

    [Fact]
    public void 只有正在专注时才累计时间()
    {
        var timer = new FocusTimerService();
        timer.Start(TimeSpan.FromMinutes(25));

        timer.Tick(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Elapsed);

        // 暂停之后 Tick 不再累计
        timer.Pause();
        timer.Tick(TimeSpan.FromMinutes(3));
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Elapsed);

        // 继续之后重新累计
        timer.Resume();
        timer.Tick(TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.FromMinutes(7), timer.Elapsed);
    }

    [Fact]
    public void 暂停算一次打扰()
    {
        var timer = new FocusTimerService();
        timer.Start(TimeSpan.FromMinutes(25));

        Assert.Equal(0, timer.InterruptionCount);
        timer.Pause();
        Assert.Equal(1, timer.InterruptionCount);
    }

    [Fact]
    public void 到时间会触发完成事件并累计今日统计()
    {
        var timer = new FocusTimerService();
        FocusCompletedEventArgs? completed = null;
        timer.FocusCompleted += (_, e) => completed = e;

        timer.Start(TimeSpan.FromMinutes(25), "复习");
        timer.Tick(TimeSpan.FromMinutes(25));

        Assert.NotNull(completed);
        Assert.True(completed!.Completed);
        Assert.Equal("复习", completed.Title);
        Assert.Equal(TimeSpan.FromMinutes(25), completed.ActualDuration);

        // 回到空闲，今日统计累加
        Assert.Equal(FocusPhase.Idle, timer.Phase);
        Assert.Equal(TimeSpan.FromMinutes(25), timer.TodayTotal);
        Assert.Equal(1, timer.TodaySessionCount);
    }

    [Fact]
    public void 超出计划时间也不会超过总时长()
    {
        var timer = new FocusTimerService();
        timer.Start(TimeSpan.FromMinutes(25));

        timer.Tick(TimeSpan.FromMinutes(40));

        Assert.Equal(TimeSpan.FromMinutes(25), timer.Elapsed);
        Assert.Equal(TimeSpan.Zero, timer.Remaining);
        Assert.Equal(1.0, timer.Progress, 3);
    }

    [Fact]
    public void 进度按比例计算()
    {
        var timer = new FocusTimerService();
        timer.Start(TimeSpan.FromMinutes(20));

        timer.Tick(TimeSpan.FromMinutes(5));
        Assert.Equal(0.25, timer.Progress, 3);
    }

    [Fact]
    public void 放弃不计入统计()
    {
        var timer = new FocusTimerService();
        timer.Start(TimeSpan.FromMinutes(25), "不想做了");
        timer.Tick(TimeSpan.FromMinutes(10));

        var result = timer.Abandon();

        Assert.NotNull(result);
        Assert.False(result!.Completed);
        Assert.Equal(TimeSpan.FromMinutes(10), result.ActualDuration);

        Assert.Equal(FocusPhase.Idle, timer.Phase);
        Assert.Equal(TimeSpan.Zero, timer.TodayTotal);   // 放弃的时长不算
        Assert.Equal(0, timer.TodaySessionCount);
    }

    [Fact]
    public void 休息阶段不累计专注时间()
    {
        var timer = new FocusTimerService();
        timer.StartBreak(TimeSpan.FromMinutes(5));

        Assert.Equal(FocusPhase.Break, timer.Phase);
        timer.Tick(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.Zero, timer.Elapsed);   // 休息不计入专注
    }

    [Fact]
    public void 状态变化会通知界面()
    {
        var timer = new FocusTimerService();
        var count = 0;
        timer.StateChanged += (_, _) => count++;

        timer.Start(TimeSpan.FromMinutes(25));
        Assert.Equal(1, count);

        timer.Tick(TimeSpan.FromMinutes(1));
        Assert.Equal(2, count);

        timer.Pause();
        Assert.Equal(3, count);
    }

    [Fact]
    public void 时长为零时用默认的25分钟兜底()
    {
        var timer = new FocusTimerService();
        timer.Start(TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromMinutes(25), timer.PlannedDuration);
    }
}
