// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Services;

namespace HeliumTodo.Domain.Notifications;

/// <summary>
/// 提醒的"什么时候该响"这件事，单独抽成一个纯函数。
///
/// 为什么不写在服务里：这里全是时间比较，最容易写错、也最容易测。
/// 抽出来之后可以拿一堆"现在几点、日程几点、提前多少"的组合直接打表测，
/// 不用起窗口、不用等真的到点。
///
/// 规则：
///   · 只看 <see cref="ScheduleItem.ReminderMinutes"/> / <see cref="TodoItem.ReminderMinutes"/> 非空的；
///   · 提醒时刻 = 基准时刻 - 提前分钟数；
///   · "到点了"的判定是「提醒时刻落在 [now - 容差, now] 这个区间内」——
///     写成区间而不是相等，是因为定时器是每 30 秒扫一次，
///     如果程序正好在 08:00:00.4 那一刻没醒着，等号判断就永远错过了。
/// </summary>
public static class ReminderPlanner
{
    /// <summary>
    /// 收集此刻该提醒的条目。
    /// </summary>
    /// <param name="schedules">所有日程（含重复规则）。</param>
    /// <param name="todos">所有待办。</param>
    /// <param name="now">现在时刻。</param>
    /// <param name="tolerance">
    /// 容忍的时间窗。定时器 30 秒扫一次，这里给 1~2 分钟，
    /// 就算某次扫描被卡住也不会漏掉。
    /// </param>
    /// <param name="alreadyFired">已经弹过的键（会被跳过）。可为 null。</param>
    public static IReadOnlyList<ReminderItem> CollectDue(
        IEnumerable<ScheduleItem> schedules,
        IEnumerable<TodoItem> todos,
        DateTime now,
        TimeSpan tolerance,
        ISet<string>? alreadyFired = null)
    {
        var windowStart = now - tolerance;
        var result = new List<ReminderItem>();

        foreach (var schedule in schedules)
        {
            if (schedule.ReminderMinutes is not { } lead) continue;
            if (schedule.Status == ScheduleStatus.Cancelled) continue;

            // 重复日程：只有当"今天"这一天确实会发生时才算。
            var today = DateOnly.FromDateTime(now);
            if (!string.IsNullOrWhiteSpace(schedule.RecurrenceRule) &&
                !RecurrenceExpander.OccursOn(schedule.Start, schedule.RecurrenceRule, today))
            {
                continue;
            }

            // 非重复日程只有在它自己那天才提醒
            if (string.IsNullOrWhiteSpace(schedule.RecurrenceRule) &&
                DateOnly.FromDateTime(schedule.Start) != today)
            {
                continue;
            }

            // 把开始时间挪到今天（重复日程的 Start 可能是很久以前那一天）
            var startToday = today.ToDateTime(TimeOnly.FromTimeSpan(schedule.Start.TimeOfDay));
            var remindAt = startToday.AddMinutes(-lead);

            if (remindAt < windowStart || remindAt > now) continue;

            var item = new ReminderItem
            {
                Kind = ReminderSourceKind.Schedule,
                SourceId = schedule.Id,
                Title = schedule.Title,
                Message = BuildScheduleMessage(schedule, startToday, lead),
                Anchor = startToday,
                LeadMinutes = lead,
                RemindAt = remindAt,
            };

            if (alreadyFired?.Contains(item.Key) == true) continue;
            result.Add(item);
        }

        foreach (var todo in todos)
        {
            if (todo.ReminderMinutes is not { } lead) continue;
            if (todo.IsCompleted) continue;

            // 基准时刻：有截止时间用截止时间，否则用计划开始时间
            if (todo.DueAt is not { } anchor && todo.PlannedStart is not { } planned) continue;
            var baseTime = todo.DueAt ?? todo.PlannedStart!.Value;

            var remindAt = baseTime.AddMinutes(-lead);
            if (remindAt < windowStart || remindAt > now) continue;

            var item = new ReminderItem
            {
                Kind = ReminderSourceKind.Todo,
                SourceId = todo.Id,
                Title = todo.Title,
                Message = BuildTodoMessage(todo, baseTime, lead),
                Anchor = baseTime,
                LeadMinutes = lead,
                RemindAt = remindAt,
            };

            if (alreadyFired?.Contains(item.Key) == true) continue;
            result.Add(item);
        }

        // 按提醒时刻排序：同一批里先到点的先弹
        return result.OrderBy(r => r.RemindAt).ToList();
    }

    private static string BuildScheduleMessage(ScheduleItem schedule, DateTime start, int lead)
    {
        var when = $"{start:HH:mm}–{start.Add(schedule.Duration):HH:mm}";
        var prefix = lead <= 0 ? "现在开始" : $"{ReminderPresets.Describe(lead)}开始";
        var place = string.IsNullOrWhiteSpace(schedule.Notes) ? string.Empty : $" · {schedule.Notes}";
        return $"{when} {prefix}{place}";
    }

    private static string BuildTodoMessage(TodoItem todo, DateTime anchor, int lead)
    {
        var isDue = todo.DueAt is not null;
        var label = isDue ? "截止" : "计划开始";
        var leadText = lead <= 0 ? "就在现在" : ReminderPresets.Describe(lead);
        return $"{label}时间 {anchor:HH:mm} · {leadText}";
    }
}