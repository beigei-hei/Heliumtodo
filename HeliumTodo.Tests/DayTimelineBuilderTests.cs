// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>
/// 每日工作流算法测试。
///
/// 为什么先测这个？
///   因为「错位展示」和「冲突检测」是整个软件最核心、最容易算错的地方，
///   而且它不依赖界面，最适合用测试保护。
///   写完测试再改算法，就不怕把功能改坏。
/// </summary>
public class DayTimelineBuilderTests
{
    // 用固定日期，避免测试结果随「今天是星期几」变化
    private static readonly DateOnly Monday = new(2025, 1, 6);   // 2025-01-06 是星期一
    private static readonly DateOnly Tuesday = new(2025, 1, 7);  // 星期二

    /// <summary>造一条单次日程的快捷方法。</summary>
    private static ScheduleItem MakeSchedule(
        string title,
        DateOnly date,
        double startHour,
        double durationHours,
        Priority priority = Priority.Normal,
        string rule = "",
        ScheduleStatus status = ScheduleStatus.Planned)
    {
        return new ScheduleItem
        {
            Id = Math.Abs(title.GetHashCode() % 10000),
            Title = title,
            Start = date.ToDateTime(TimeOnly.MinValue).AddHours(startHour),
            Duration = TimeSpan.FromHours(durationHours),
            Priority = priority,
            RecurrenceRule = rule,
            Status = status,
        };
    }

    private static DayTimeline Build(DateOnly date, params ScheduleItem[] schedules) =>
        DayTimelineBuilder.Build(date, schedules, Array.Empty<TodoItem>(), Array.Empty<FocusSession>());

    // ─────────────────────────────────────────────────────────
    // 1. 基础：不重叠的日程各自占满整行
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 不重叠的日程_每条各占一列()
    {
        var timeline = Build(Monday,
            MakeSchedule("数学", Monday, 8, 1),
            MakeSchedule("英语", Monday, 10, 1));

        Assert.Equal(2, timeline.Entries.Count);
        Assert.All(timeline.Entries, e =>
        {
            Assert.Equal(0, e.Lane);
            Assert.Equal(1, e.LaneCount);
            Assert.False(e.IsOverlapping);
        });
        Assert.Empty(timeline.Conflicts);
    }

    // ─────────────────────────────────────────────────────────
    // 2. 错位展示：重叠的日程分到不同列
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 两条重叠的日程_分别错位到第0列和第1列()
    {
        var timeline = Build(Monday,
            MakeSchedule("算法课", Monday, 10, 1),      // 10:00 – 11:00
            MakeSchedule("社团例会", Monday, 10.5, 1)); // 10:30 – 11:30

        Assert.Equal(2, timeline.Entries.Count);
        Assert.All(timeline.Entries, e => Assert.Equal(2, e.LaneCount));

        // 先开始的排左边（Lane 0），后开始的排右边（Lane 1）
        var first = timeline.Entries.Single(e => e.Title == "算法课");
        var second = timeline.Entries.Single(e => e.Title == "社团例会");
        Assert.Equal(0, first.Lane);
        Assert.Equal(1, second.Lane);

        // 宽度各占一半，左边距分别是 0 和 50%
        Assert.Equal(0.0, first.LeftRatio, 3);
        Assert.Equal(0.5, first.WidthRatio, 3);
        Assert.Equal(0.5, second.LeftRatio, 3);
    }

    [Fact]
    public void 连环重叠的条目属于同一组_共用最少的列数()
    {
        // A: 08:00–10:00, B: 09:00–11:00, C: 10:00–12:00
        //   · A 和 B 重叠 60 分钟
        //   · B 和 C 重叠 60 分钟
        //   · A 和 C 只是「10:00 首尾相接」，不算重叠
        // 三者通过 B 连成一串（同一个重叠组），但任意时刻最多同时有 2 条，
        // 所以最少只要 2 列就摆得下。列数取「组内最大并发数」而不是「组内条目数」。
        var timeline = Build(Monday,
            MakeSchedule("A", Monday, 8, 2),
            MakeSchedule("B", Monday, 9, 2),
            MakeSchedule("C", Monday, 10, 2));

        Assert.All(timeline.Entries, e => Assert.Equal(2, e.LaneCount));

        // A 在列 0；B 和 A 重叠所以去列 1；
        // C 从 10:00 开始，此时 A 已经结束，所以 C 回到列 0 复用空位
        Assert.Equal(0, timeline.Entries.Single(e => e.Title == "A").Lane);
        Assert.Equal(1, timeline.Entries.Single(e => e.Title == "B").Lane);
        Assert.Equal(0, timeline.Entries.Single(e => e.Title == "C").Lane);
    }

    [Fact]
    public void 三条完全同时重叠_需要三列()
    {
        // 三条时间完全一样，这才是真正需要 3 列的情况
        var timeline = Build(Monday,
            MakeSchedule("甲", Monday, 9, 2),
            MakeSchedule("乙", Monday, 9, 2),
            MakeSchedule("丙", Monday, 9, 2));

        Assert.All(timeline.Entries, e => Assert.Equal(3, e.LaneCount));

        var lanes = timeline.Entries.Select(e => e.Lane).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 0, 1, 2 }, lanes);
    }

    [Fact]
    public void 首尾相接不算重叠()
    {
        // 09:00 结束 和 09:00 开始，不应该算冲突
        var timeline = Build(Monday,
            MakeSchedule("第一节", Monday, 8, 1),
            MakeSchedule("第二节", Monday, 9, 1));

        Assert.All(timeline.Entries, e => Assert.Equal(1, e.LaneCount));
        Assert.Empty(timeline.Conflicts);
    }

    // ─────────────────────────────────────────────────────────
    // 3. 冲突检测
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 日程重叠会产生严重冲突_并算出重叠时长()
    {
        var timeline = Build(Monday,
            MakeSchedule("高等数学", Monday, 8, 1),        // 08:00 – 09:00
            MakeSchedule("算法设计", Monday, 8.5, 1));     // 08:30 – 09:30

        var conflict = Assert.Single(timeline.Conflicts);
        Assert.Equal(ConflictKind.ScheduleOverlap, conflict.Kind);
        Assert.Equal(ConflictSeverity.Error, conflict.Severity);

        // 重叠 30 分钟（08:30 – 09:00）
        Assert.Equal(30, (int)Math.Round(conflict.Overlap.TotalMinutes));
        Assert.Contains("高等数学", conflict.Message);
        Assert.Contains("算法设计", conflict.Message);
    }

    [Fact]
    public void 预订的专注撞上日程_算专注类冲突()
    {
        var schedule = MakeSchedule("晚自习", Monday, 19, 2);   // 19:00 – 21:00

        var focus = new FocusSession
        {
            Id = 1,
            Title = "预订：写实验报告",
            PlannedStart = Monday.ToDateTime(TimeOnly.MinValue).AddHours(19.5),   // 19:30
            PlannedDuration = TimeSpan.FromMinutes(45),
            Status = FocusSessionStatus.Reserved,
        };

        var timeline = DayTimelineBuilder.Build(
            Monday, new[] { schedule }, Array.Empty<TodoItem>(), new[] { focus });

        // 专注时段应该出现在时间轴上
        var focusEntry = timeline.Entries.Single(e => e.Kind == TimelineEntryKind.FocusReservation);
        Assert.Equal(19.5, focusEntry.Start.TotalHours, 3);

        // 并且报出「专注与日程重叠」
        var conflict = timeline.Conflicts.FirstOrDefault(c => c.Kind == ConflictKind.FocusOverlapsSchedule);
        Assert.NotNull(conflict);
        Assert.Equal(ConflictSeverity.Warning, conflict!.Severity);

        // 专注时段 19:30–20:15 完全落在晚自习 19:00–21:00 里面，
        // 所以重叠时长就是专注本身的长度 45 分钟
        Assert.Equal(45, (int)Math.Round(conflict.Overlap.TotalMinutes));
        Assert.Equal(TimeSpan.FromMinutes(19 * 60 + 30), conflict.Start);
        Assert.Equal(TimeSpan.FromMinutes(20 * 60 + 15), conflict.End);
    }

    [Fact]
    public void 待办截止时间落在忙碌时段_会被提醒()
    {
        var schedule = MakeSchedule("高等数学", Monday, 8, 2);   // 08:00 – 10:00

        var todo = new TodoItem
        {
            Id = 42,
            Title = "交实验报告",
            DueAt = Monday.ToDateTime(TimeOnly.MinValue).AddHours(9),   // 09:00 截止，正好在上课中间
            EstimatedDuration = TimeSpan.FromMinutes(30),
        };

        var timeline = DayTimelineBuilder.Build(
            Monday, new[] { schedule }, new[] { todo }, Array.Empty<FocusSession>());

        var conflict = timeline.Conflicts.FirstOrDefault(c => c.Kind == ConflictKind.TodoDueInBusyTime);
        Assert.NotNull(conflict);
        Assert.Contains("交实验报告", conflict!.Message);
    }

    [Fact]
    public void 一天安排超过阈值_会提示过载()
    {
        var timeline = Build(Monday,
            MakeSchedule("上午", Monday, 6, 6),     // 06:00 – 12:00
            MakeSchedule("下午", Monday, 12, 7));   // 12:00 – 19:00，合计 13 小时

        Assert.Contains(timeline.Conflicts, c => c.Kind == ConflictKind.OverloadedDay);
    }

    // ─────────────────────────────────────────────────────────
    // 4. 忙碌时长：重叠部分不能重复计算
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 重叠的日程_忙碌时长只算一次()
    {
        // 两个各 1 小时、重叠 30 分钟 → 共 1.5 小时，而不是 2 小时
        var timeline = Build(Monday,
            MakeSchedule("A", Monday, 8, 1),      // 08:00 – 09:00
            MakeSchedule("B", Monday, 8.5, 1));   // 08:30 – 09:30

        Assert.Equal(90, (int)Math.Round(timeline.BusyDuration.TotalMinutes));
    }

    [Fact]
    public void 忙碌时长应包含专注时段()
    {
        var focus = new FocusSession
        {
            Id = 1,
            PlannedStart = Monday.ToDateTime(TimeOnly.MinValue).AddHours(14),
            PlannedDuration = TimeSpan.FromMinutes(50),
            Status = FocusSessionStatus.Reserved,
        };

        var timeline = DayTimelineBuilder.Build(
            Monday, Array.Empty<ScheduleItem>(), Array.Empty<TodoItem>(), new[] { focus });

        Assert.Equal(50, (int)Math.Round(timeline.BusyDuration.TotalMinutes));
    }

    // ─────────────────────────────────────────────────────────
    // 5. 重复日程
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 每周一三五的课_只在周一三五出现()
    {
        var course = MakeSchedule("高等数学", Monday, 8, 1, rule: "weekly:1,3,5");

        // 周一：出现
        Assert.Single(Build(Monday, course).Entries);

        // 周二：不出现
        Assert.Empty(Build(Tuesday, course).Entries);

        // 周三 / 周五：出现
        Assert.Single(Build(Monday.AddDays(2), course).Entries);
        Assert.Single(Build(Monday.AddDays(4), course).Entries);

        // 周日：不出现
        Assert.Empty(Build(Monday.AddDays(6), course).Entries);
    }

    [Fact]
    public void 每天重复的日程_天天出现()
    {
        var daily = MakeSchedule("晨跑", Monday, 7, 0.5, rule: "daily");

        for (int i = 0; i < 7; i++)
        {
            Assert.Single(Build(Monday.AddDays(i), daily).Entries);
        }
    }

    [Fact]
    public void 重复日程不能早于首次开始日期()
    {
        var course = MakeSchedule("高等数学", Monday, 8, 1, rule: "weekly:1");

        // 上周一不该出现（因为首次开始是本周一）
        Assert.Empty(Build(Monday.AddDays(-7), course).Entries);
    }

    [Fact]
    public void 已取消的日程不参与时间轴和冲突()
    {
        var timeline = Build(Monday,
            MakeSchedule("正常课", Monday, 8, 1),
            MakeSchedule("取消的课", Monday, 8.5, 1, status: ScheduleStatus.Cancelled));

        Assert.Single(timeline.Entries);
        Assert.Empty(timeline.Conflicts);
    }

    // ─────────────────────────────────────────────────────────
    // 5.5 同一件事不要显示两次
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 待办和指向它的日程_在时间轴上只出现一次()
    {
        // 场景：给待办「复习词汇」安排了 21:00，同时数据库里还留着一条
        // 指向它的日程（LinkedTodoId）。如果不处理，时间轴会画出两个块。
        var todo = new TodoItem
        {
            Id = 7,
            Title = "复习词汇",
            EstimatedDuration = TimeSpan.FromMinutes(45),
            PlannedStart = Monday.ToDateTime(TimeOnly.MinValue).AddHours(21),
        };

        var linkedSchedule = MakeSchedule("复习词汇", Monday, 21, 0.75);
        linkedSchedule.Id = 99;
        linkedSchedule.LinkedTodoId = todo.Id;   // 关键：指向同一个待办

        var timeline = DayTimelineBuilder.Build(
            Monday, new[] { linkedSchedule }, new[] { todo }, Array.Empty<FocusSession>());

        // 只应该剩一条，而且是「待办」那一条（紫色块）
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(TimelineEntryKind.PlannedTodo, entry.Kind);
        Assert.Equal(todo.Id, entry.SourceId);
    }

    [Fact]
    public void 普通日程不会被误删()
    {
        // 没有 LinkedTodoId 的日程是独立安排，必须保留
        var todo = new TodoItem
        {
            Id = 7,
            Title = "复习词汇",
            PlannedStart = Monday.ToDateTime(TimeOnly.MinValue).AddHours(21),
        };

        var timeline = DayTimelineBuilder.Build(
            Monday,
            new[] { MakeSchedule("晚自习", Monday, 19, 2) },
            new[] { todo },
            Array.Empty<FocusSession>());

        Assert.Equal(2, timeline.Entries.Count);
        Assert.Contains(timeline.Entries, e => e.Kind == TimelineEntryKind.Schedule);
        Assert.Contains(timeline.Entries, e => e.Kind == TimelineEntryKind.PlannedTodo);
    }

    // ─────────────────────────────────────────────────────────
    // 6. 待办进入时间轴
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 安排了时间的待办会出现在时间轴上()
    {
        var todo = new TodoItem
        {
            Id = 7,
            Title = "复习词汇",
            Priority = Priority.High,
            EstimatedDuration = TimeSpan.FromMinutes(45),
            PlannedStart = Monday.ToDateTime(TimeOnly.MinValue).AddHours(21),
        };

        var timeline = DayTimelineBuilder.Build(
            Monday, Array.Empty<ScheduleItem>(), new[] { todo }, Array.Empty<FocusSession>());

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(TimelineEntryKind.PlannedTodo, entry.Kind);
        Assert.Equal(Priority.High, entry.Priority);
        Assert.Equal(21, (int)entry.Start.TotalHours);
        Assert.Equal(45, (int)Math.Round(entry.Duration.TotalMinutes));
    }

    [Fact]
    public void 已完成的待办不出现在时间轴上()
    {
        var todo = new TodoItem
        {
            Id = 7,
            Title = "已经做完了",
            IsCompleted = true,
            PlannedStart = Monday.ToDateTime(TimeOnly.MinValue).AddHours(21),
        };

        var timeline = DayTimelineBuilder.Build(
            Monday, Array.Empty<ScheduleItem>(), new[] { todo }, Array.Empty<FocusSession>());

        Assert.Empty(timeline.Entries);
    }

    // ─────────────────────────────────────────────────────────
    // 7. 优先级排序与空闲时段
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 按优先级排序_紧急的排最前()
    {
        var timeline = Build(Monday,
            MakeSchedule("低", Monday, 8, 1, Priority.Low),
            MakeSchedule("紧急", Monday, 10, 1, Priority.Urgent),
            MakeSchedule("普通", Monday, 12, 1, Priority.Normal));

        var ordered = timeline.ByPriority.Select(e => e.Title).ToList();
        Assert.Equal(new[] { "紧急", "普通", "低" }, ordered);
    }

    [Fact]
    public void 空闲时段能正确算出来()
    {
        var timeline = Build(Monday,
            MakeSchedule("上午课", Monday, 8, 2),     // 08:00 – 10:00
            MakeSchedule("下午课", Monday, 14, 2));   // 14:00 – 16:00

        // 从 00:00 起找，最短 30 分钟
        var slots = DayTimelineBuilder.FindFreeSlots(timeline.Entries, TimeSpan.Zero, TimeSpan.FromMinutes(30));

        // 应该有：00:00–08:00、10:00–14:00、16:00–24:00
        Assert.Equal(3, slots.Count);
        Assert.Equal(TimeSpan.Zero, slots[0].Start);
        Assert.Equal(TimeSpan.FromHours(8), slots[0].End);
        Assert.Equal(TimeSpan.FromHours(10), slots[1].Start);
        Assert.Equal(TimeSpan.FromHours(14), slots[1].End);
    }

    [Fact]
    public void 从当前时刻开始找空闲_会跳过已经过去的空档()
    {
        var timeline = Build(Monday,
            MakeSchedule("上午课", Monday, 8, 2),     // 08:00 – 10:00
            MakeSchedule("下午课", Monday, 14, 2));   // 14:00 – 16:00

        // 「现在」是 12:00
        var slots = DayTimelineBuilder.FindFreeSlots(
            timeline.Entries, TimeSpan.FromHours(12), TimeSpan.FromMinutes(30));

        Assert.Equal(2, slots.Count);
        Assert.Equal(TimeSpan.FromHours(12), slots[0].Start);   // 12:00–14:00
        Assert.Equal(TimeSpan.FromHours(16), slots[1].Start);   // 16:00–24:00
    }

    // ─────────────────────────────────────────────────────────
    // 8. 概要文字
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void 概要文字应包含条目数和冲突数()
    {
        var timeline = Build(Monday,
            MakeSchedule("A", Monday, 8, 1),
            MakeSchedule("B", Monday, 8.5, 1));

        Assert.Contains("2 项安排", timeline.SummaryText);
        Assert.Contains("1 处冲突", timeline.SummaryText);
        Assert.True(timeline.HasCriticalConflict);
    }

    [Fact]
    public void 没有任何安排时概要不会崩()
    {
        var timeline = Build(Monday);

        Assert.Empty(timeline.Entries);
        Assert.Equal(TimeSpan.Zero, timeline.BusyDuration);
        Assert.Contains("0 项安排", timeline.SummaryText);
        Assert.Contains("无冲突", timeline.SummaryText);
        Assert.False(timeline.HasCriticalConflict);
    }
}
