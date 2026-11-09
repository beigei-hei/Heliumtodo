// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Notifications;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 提醒调度（ReminderPlanner）和提前量文案的测试。
///
/// 这部分全是"几点几分该不该响"的判断，错一分钟就是"提醒没来"或者"半夜里响了"，
/// 所以逐条打表测：给定 now、给定时段、给定提前量，看结果对不对。
/// 时间全部用字面量构造，不依赖真正的时间，跑起来是确定的。
/// </summary>
public class ReminderPlannerTests
{
    private static readonly DateTime Noon = new(2026, 3, 10, 12, 0, 0);

    private static ScheduleItem Schedule(DateTime start, int minutes, int? lead, string rule = "")
        => new()
        {
            Id = 1,
            Title = "高等数学",
            Start = start,
            Duration = TimeSpan.FromMinutes(minutes),
            ReminderMinutes = lead,
            RecurrenceRule = rule,
        };

    private static TodoItem Todo(DateTime? due, DateTime? planned, int? lead, bool done = false)
        => new()
        {
            Id = 2,
            Title = "交作业",
            DueAt = due,
            PlannedStart = planned,
            ReminderMinutes = lead,
            IsCompleted = done,
        };

    // ───────────────────────── 日程 ─────────────────────────

    [Fact(DisplayName = "开始时提醒：正好到点会响")]
    public void AtStartFiresExactlyOnTime()
    {
        var schedule = Schedule(Noon, 60, ReminderPresets.AtStart);

        var due = ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(2));

        var item = Assert.Single(due);
        Assert.Equal(ReminderSourceKind.Schedule, item.Kind);
        Assert.Equal(Noon, item.RemindAt);
    }

    [Fact(DisplayName = "提前十分钟：九点五十响，九点五十一还在窗口内")]
    public void TenMinutesEarlyFiresAtNineFifty()
    {
        var schedule = Schedule(Noon, 60, 10);
        var tenTo = Noon.AddMinutes(-10);

        // 到点那一刻
        Assert.Single(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), tenTo, TimeSpan.FromMinutes(2)));

        // 晚 1 分钟（定时器扫慢了），2 分钟容差内仍然算到点
        Assert.Single(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), tenTo.AddMinutes(1), TimeSpan.FromMinutes(2)));

        // 晚 5 分钟：已经超出容差，不再重复提醒
        Assert.Empty(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), tenTo.AddMinutes(5), TimeSpan.FromMinutes(2)));
    }

    [Fact(DisplayName = "提前两小时：十点就响")]
    public void TwoHoursEarlyFiresAtTen()
    {
        var schedule = Schedule(Noon, 60, 120);
        var due = ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon.AddHours(-2), TimeSpan.FromMinutes(1));

        Assert.Single(due);
    }

    [Fact(DisplayName = "没设提醒的日程不响")]
    public void NoLeadMeansNoReminder()
    {
        var schedule = Schedule(Noon, 60, null);

        Assert.Empty(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(5)));
    }

    [Fact(DisplayName = "已取消的日程不响")]
    public void CancelledScheduleStaysSilent()
    {
        var schedule = Schedule(Noon, 60, ReminderPresets.AtStart);
        schedule.Status = ScheduleStatus.Cancelled;

        Assert.Empty(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(2)));
    }

    [Fact(DisplayName = "别的日子的日程不响")]
    public void OtherDayScheduleStaysSilent()
    {
        var schedule = Schedule(Noon.AddDays(1), 60, ReminderPresets.AtStart);

        Assert.Empty(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(2)));
    }

    [Fact(DisplayName = "重复日程：发生的那天响，不发生的日子不响")]
    public void RecurringScheduleOnlyFiresOnMatchingDays()
    {
        // 2026-03-10 是星期二。规则里 0=周日、1=周一、2=周二。
        var schedule = Schedule(new DateTime(2026, 1, 6, 12, 0, 0), 60, ReminderPresets.AtStart, "weekly:2");

        Assert.Single(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(2)));

        // 星期三不响
        Assert.Empty(ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon.AddDays(1), TimeSpan.FromMinutes(2)));
    }

    [Fact(DisplayName = "重复日程按今天的时刻算提醒，不按创建那天")]
    public void RecurringScheduleUsesTodayClock()
    {
        // 这条日程是 1 月 6 日建的，但每天重复；今天应该按 12:00 提醒
        var schedule = Schedule(new DateTime(2026, 1, 6, 12, 0, 0), 30, 10, "daily");

        var due = ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon.AddMinutes(-10), TimeSpan.FromMinutes(1));

        var item = Assert.Single(due);
        Assert.Equal(new DateTime(2026, 3, 10, 12, 0, 0), item.Anchor);
    }

    // ───────────────────────── 待办 ─────────────────────────

    [Fact(DisplayName = "待办按截止时间提前响")]
    public void TodoUsesDueTime()
    {
        var todo = Todo(Noon, null, 30);

        var due = ReminderPlanner.CollectDue(
            Array.Empty<ScheduleItem>(), new[] { todo }, Noon.AddMinutes(-30), TimeSpan.FromMinutes(1));

        var item = Assert.Single(due);
        Assert.Equal(ReminderSourceKind.Todo, item.Kind);
        Assert.Equal(Noon, item.Anchor);
    }

    [Fact(DisplayName = "待办没截止时间时用计划开始时间")]
    public void TodoFallsBackToPlannedStart()
    {
        var todo = Todo(null, Noon, ReminderPresets.AtStart);

        Assert.Single(ReminderPlanner.CollectDue(
            Array.Empty<ScheduleItem>(), new[] { todo }, Noon, TimeSpan.FromMinutes(1)));
    }

    [Fact(DisplayName = "待办既没截止也没计划就不响")]
    public void TodoWithoutAnchorStaysSilent()
    {
        var todo = Todo(null, null, ReminderPresets.AtStart);

        Assert.Empty(ReminderPlanner.CollectDue(
            Array.Empty<ScheduleItem>(), new[] { todo }, Noon, TimeSpan.FromMinutes(5)));
    }

    [Fact(DisplayName = "已完成的待办不响")]
    public void CompletedTodoStaysSilent()
    {
        var todo = Todo(Noon, null, ReminderPresets.AtStart, done: true);

        Assert.Empty(ReminderPlanner.CollectDue(
            Array.Empty<ScheduleItem>(), new[] { todo }, Noon, TimeSpan.FromMinutes(2)));
    }

    // ───────────────────────── 去重 ─────────────────────────

    [Fact(DisplayName = "已经弹过的键会被跳过")]
    public void AlreadyFiredIsSkipped()
    {
        var schedule = Schedule(Noon, 60, ReminderPresets.AtStart);
        var fired = new HashSet<string>();

        var first = ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(1), fired);
        var item = Assert.Single(first);
        fired.Add(item.Key);

        var second = ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(1), fired);

        Assert.Empty(second);
    }

    [Fact(DisplayName = "同一条重复日程第二天还会再提醒一次")]
    public void RecurringScheduleFiresAgainNextDay()
    {
        var schedule = Schedule(Noon, 60, ReminderPresets.AtStart, "daily");
        var fired = new HashSet<string>();

        var today = ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), Noon, TimeSpan.FromMinutes(1), fired);
        fired.Add(Assert.Single(today).Key);

        var tomorrowNoon = Noon.AddDays(1);
        var tomorrow = ReminderPlanner.CollectDue(
            new[] { schedule }, Array.Empty<TodoItem>(), tomorrowNoon, TimeSpan.FromMinutes(1), fired);

        Assert.Single(tomorrow);
    }

    [Fact(DisplayName = "同一批提醒按时间先后排序")]
    public void ResultsAreOrderedByRemindTime()
    {
        var late = Schedule(Noon.AddMinutes(30), 30, ReminderPresets.AtStart);
        late.Id = 3;
        var early = Schedule(Noon, 30, ReminderPresets.AtStart);
        early.Id = 4;

        var now = Noon.AddMinutes(30);
        var due = ReminderPlanner.CollectDue(
            new[] { late, early }, Array.Empty<TodoItem>(), now, TimeSpan.FromHours(1));

        Assert.Equal(2, due.Count);
        Assert.True(due[0].RemindAt <= due[1].RemindAt);
    }
}

/// <summary>
/// 提前量的文字与换算。
/// </summary>
public class ReminderPresetsTests
{
    [Fact(DisplayName = "提前量的文案")]
    public void DescribeCoversAllShapes()
    {
        Assert.Equal("不提醒", ReminderPresets.Describe(null));
        Assert.Equal("开始时", ReminderPresets.Describe(0));
        Assert.Equal("提前 5 分钟", ReminderPresets.Describe(5));
        Assert.Equal("提前 1 小时", ReminderPresets.Describe(60));
        Assert.Equal("提前 1 小时 30 分钟", ReminderPresets.Describe(90));
        Assert.Equal("提前 2 小时", ReminderPresets.Describe(120));
    }

    [Fact(DisplayName = "小时和分钟合成分钟数")]
    public void ComposeAddsUp()
    {
        Assert.Equal(90, ReminderPresets.Compose(1, 30));
        Assert.Equal(120, ReminderPresets.Compose(0, 120));
        Assert.Equal(0, ReminderPresets.Compose(0, 0));
    }

    [Fact(DisplayName = "分钟数能拆回小时和分钟")]
    public void SplitIsInverseOfCompose()
    {
        var (hours, minutes) = ReminderPresets.Split(150);
        Assert.Equal(2, hours);
        Assert.Equal(30, minutes);
        Assert.Equal(150, ReminderPresets.Compose(hours, minutes));
    }

    [Fact(DisplayName = "自定义值不会超过上限")]
    public void ComposeClampsToMaximum()
    {
        Assert.Equal(ReminderPresets.MaxMinutes, ReminderPresets.Compose(999, 999));
    }

    [Fact(DisplayName = "固定选项里认得出来哪些是预设")]
    public void IsPresetWorks()
    {
        Assert.True(ReminderPresets.IsPreset(null));
        Assert.True(ReminderPresets.IsPreset(10));
        Assert.False(ReminderPresets.IsPreset(7));
    }
}