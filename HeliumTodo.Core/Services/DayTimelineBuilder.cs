using HeliumTodo.Core.Models;

namespace HeliumTodo.Core.Services;

/// <summary>
/// 每日工作流构建器 —— 整个「可视信息流」的大脑。
///
/// 它做三件事：
///   1. 把日程 / 待办 / 专注 三种数据统一变成时间轴上的矩形块（<see cref="TimelineEntry"/>）。
///   2. 检测冲突（<see cref="Conflict"/>）。
///   3. 给重叠的块分配列号（Lane），实现「错位展示」。
///
/// 这里全是纯计算，不碰数据库也不碰界面，所以可以放心写单元测试。
/// </summary>
public static class DayTimelineBuilder
{
    /// <summary>一天之中「塞得太满」的判定线：超过 12 小时就算过载。</summary>
    public static readonly TimeSpan OverloadThreshold = TimeSpan.FromHours(12);

    /// <summary>
    /// 构建某一天的时间轴。
    /// </summary>
    /// <param name="date">要构建的日期。</param>
    /// <param name="schedules">全部日程（会自动按重复规则筛选出这一天该出现的）。</param>
    /// <param name="todos">全部待办（只取安排了时间或当天截止的）。</param>
    /// <param name="focusSessions">全部专注记录。</param>
    public static DayTimeline Build(
        DateOnly date,
        IEnumerable<ScheduleItem> schedules,
        IEnumerable<TodoItem> todos,
        IEnumerable<FocusSession> focusSessions)
    {
        var entries = new List<TimelineEntry>();

        CollectSchedules(date, schedules, entries);
        CollectTodos(date, todos, entries);
        CollectFocusSessions(date, focusSessions, entries);

        // 去掉重复条目（同一条待办被「日程」和「待办」各表示了一次）
        RemoveDuplicateTodoEntries(entries, schedules);

        // 先按开始时间排好，后面的冲突检测和分列都依赖这个顺序
        entries = entries.OrderBy(e => e.Start).ThenBy(e => e.End).ToList();

        AssignLanes(entries);
        var conflicts = DetectConflicts(entries, todos, date);
        var busy = CalculateBusyDuration(entries);

        return new DayTimeline
        {
            Date = date,
            Entries = entries,
            Conflicts = conflicts,
            BusyDuration = busy,
        };
    }

    // ─────────────────────────────────────────────────────────────
    // 第一步：把三种数据收集成统一的时间轴条目
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 去掉「同一件事被表示两次」的条目。
    ///
    /// 什么情况会重复？
    ///   给一个待办安排了时间（PlannedStart），同时又存在一条指向这个待办的日程
    ///   （ScheduleItem.LinkedTodoId == 待办Id）。
    ///   这时时间轴上同一个时段会冒出两个块，看起来像两条安排，其实是同一件事。
    ///
    /// 保留哪一个？
    ///   保留「待办」那一条（紫色块），因为它是待办的本体；把多余的日程去掉。
    /// </summary>
    private static void RemoveDuplicateTodoEntries(
        List<TimelineEntry> entries,
        IEnumerable<ScheduleItem> schedules)
    {
        // 第 1 步：时间轴上已经作为「待办」出现的那些待办 Id
        var plannedTodoIds = entries
            .Where(e => e.Kind == TimelineEntryKind.PlannedTodo)
            .Select(e => e.SourceId)
            .ToHashSet();

        if (plannedTodoIds.Count == 0) return;

        // 第 2 步：哪些日程是在替这些待办「代班」——它们的 Id 应该从时间轴上拿掉
        var redundantScheduleIds = schedules
            .Where(s => s.LinkedTodoId.HasValue && plannedTodoIds.Contains(s.LinkedTodoId.Value))
            .Select(s => s.Id)
            .ToHashSet();

        if (redundantScheduleIds.Count == 0) return;

        // 第 3 步：删掉
        entries.RemoveAll(e =>
            e.Kind == TimelineEntryKind.Schedule && redundantScheduleIds.Contains(e.SourceId));
    }

    private static void CollectSchedules(DateOnly date, IEnumerable<ScheduleItem> schedules, List<TimelineEntry> entries)
    {
        foreach (var s in schedules)
        {
            if (!s.IsActive) continue;
            if (!RecurrenceExpander.OccursOn(s.Start, s.RecurrenceRule, date)) continue;

            // 重复日程：把开始时间换成「目标日期 + 原来的时刻」
            var startTime = s.Start.TimeOfDay;
            var endTime = startTime + s.Duration;

            // 跨过午夜的部分裁掉，免得时间轴画出 25:00 这种奇怪的东西
            if (startTime >= TimeSpan.FromHours(24)) continue;
            if (endTime > TimeSpan.FromHours(24)) endTime = TimeSpan.FromHours(24);

            entries.Add(new TimelineEntry
            {
                SourceId = s.Id,
                Kind = TimelineEntryKind.Schedule,
                Title = s.Title,
                Notes = s.Notes,
                Priority = s.Priority,
                Start = startTime,
                End = endTime,
            });
        }
    }

    private static void CollectTodos(DateOnly date, IEnumerable<TodoItem> todos, List<TimelineEntry> entries)
    {
        foreach (var t in todos)
        {
            if (t.IsCompleted) continue;
            if (!t.PlannedStart.HasValue) continue;
            if (DateOnly.FromDateTime(t.PlannedStart.Value) != date) continue;

            var startTime = t.PlannedStart.Value.TimeOfDay;
            var endTime = startTime + t.EstimatedDuration;
            if (startTime >= TimeSpan.FromHours(24)) continue;
            if (endTime > TimeSpan.FromHours(24)) endTime = TimeSpan.FromHours(24);

            entries.Add(new TimelineEntry
            {
                SourceId = t.Id,
                Kind = TimelineEntryKind.PlannedTodo,
                Title = t.Title,
                Notes = t.Category,
                Priority = t.Priority,
                Start = startTime,
                End = endTime,
            });
        }
    }

    private static void CollectFocusSessions(DateOnly date, IEnumerable<FocusSession> sessions, List<TimelineEntry> entries)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        foreach (var f in sessions)
        {
            // 已放弃的专注不算进时间轴
            if (f.Status == FocusSessionStatus.Abandoned) continue;

            // 正在进行中的专注：用「实际开始时间 + 计划时长」来画
            var rawStart = f.StartedAt ?? f.PlannedStart;
            var rawEnd = f.EndedAt ?? (f.StartedAt.HasValue
                ? f.StartedAt.Value + f.PlannedDuration
                : f.PlannedStart + f.PlannedDuration);

            // 和这一天没有交集就跳过
            if (rawEnd <= dayStart || rawStart >= dayEnd) continue;

            // 裁到当天范围内
            var clampedStart = rawStart < dayStart ? dayStart : rawStart;
            var clampedEnd = rawEnd > dayEnd ? dayEnd : rawEnd;
            if (clampedEnd <= clampedStart) continue;

            entries.Add(new TimelineEntry
            {
                SourceId = f.Id,
                Kind = f.IsRunning ? TimelineEntryKind.RunningFocus : TimelineEntryKind.FocusReservation,
                Title = string.IsNullOrWhiteSpace(f.Title) ? "专注时段" : f.Title,
                Notes = f.IsStudyRoomSession ? $"自习室 · {f.StudyRoomCode}" : null,
                Priority = Priority.High, // 专注时段一律按高优先级显示
                Start = clampedStart.TimeOfDay,
                End = clampedEnd.TimeOfDay,
            });
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 第二步：错位展示 —— 给重叠的条目分列
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 用「并查集」把互相重叠的条目分到同一组，然后在组内用贪心算法分配列号。
    ///
    /// 为什么要分组？
    ///   一组重叠的条目共用同样的列数，界面才能把它们等宽并排。
    ///   例如 A(08:00-09:00)、B(08:30-10:00)、C(09:00-11:00) 三个连环重叠，
    ///   它们属于同一组，一共需要 2 列，各自宽度就是 50%。
    /// </summary>
    public static void AssignLanes(List<TimelineEntry> entries)
    {
        if (entries.Count == 0) return;

        var n = entries.Count;
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;

        // 并查集：找根节点
        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]]; // 路径压缩，让后续查找更快
                x = parent[x];
            }
            return x;
        }

        // 合并两个集合
        void Union(int a, int b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) parent[rb] = ra;
        }

        // 两两检查是否重叠，重叠就合并成一组
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (Overlaps(entries[i], entries[j]))
                {
                    Union(i, j);
                }
            }
        }

        // 按根节点分组
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            var root = Find(i);
            if (!groups.TryGetValue(root, out var list))
            {
                list = new List<int>();
                groups[root] = list;
            }
            list.Add(i);
        }

        // 每组内部贪心分列
        foreach (var group in groups.Values)
        {
            // 同一组里所有条目共用的列数 = 组内最大并发数
            var laneEnds = new List<TimeSpan>();          // 每列当前占用到几点
            var laneOfEntry = new Dictionary<int, int>();

            // 按开始时间排，先到的先占列（这样列号看起来更稳定）
            foreach (var index in group.OrderBy(i => entries[i].Start).ThenBy(i => entries[i].End))
            {
                var entry = entries[index];
                var placed = -1;

                // 找第一列：这一列上一个条目已经结束了
                for (int lane = 0; lane < laneEnds.Count; lane++)
                {
                    if (laneEnds[lane] <= entry.Start)
                    {
                        placed = lane;
                        break;
                    }
                }

                // 没有空列就新开一列
                if (placed < 0)
                {
                    placed = laneEnds.Count;
                    laneEnds.Add(TimeSpan.Zero);
                }

                laneEnds[placed] = entry.End;
                laneOfEntry[index] = placed;
            }

            var laneCount = Math.Max(1, laneEnds.Count);
            foreach (var (index, lane) in laneOfEntry)
            {
                entries[index].Lane = lane;
                entries[index].LaneCount = laneCount;
            }
        }
    }

    /// <summary>
    /// 两个时间段是否重叠。
    /// 用「严格小于」而不是「小于等于」：一个 09:00 结束、另一个 09:00 开始，不算冲突。
    /// </summary>
    public static bool Overlaps(TimelineEntry a, TimelineEntry b) =>
        a.Start < b.End && b.Start < a.End;

    // ─────────────────────────────────────────────────────────────
    // 第三步：冲突检测
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 检测这一天的所有冲突。
    /// 共 5 类，按严重程度从高到低排在结果里。
    /// </summary>
    public static List<Conflict> DetectConflicts(
        List<TimelineEntry> entries,
        IEnumerable<TodoItem> allTodos,
        DateOnly date)
    {
        var conflicts = new List<Conflict>();

        // ---- 冲突 1：两条日程时间重叠 ----
        for (int i = 0; i < entries.Count; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                var a = entries[i];
                var b = entries[j];
                if (!Overlaps(a, b)) continue;

                var overlapStart = a.Start > b.Start ? a.Start : b.Start;
                var overlapEnd = a.End < b.End ? a.End : b.End;

                // 专注时段和日程撞车，单独归一类（用户更关心「我预订的专注被占了」）
                var involvesFocus = a.Kind is TimelineEntryKind.FocusReservation or TimelineEntryKind.RunningFocus
                                 || b.Kind is TimelineEntryKind.FocusReservation or TimelineEntryKind.RunningFocus;

                var involvesTodo = a.Kind == TimelineEntryKind.PlannedTodo
                                || b.Kind == TimelineEntryKind.PlannedTodo;

                var kind = involvesFocus
                    ? ConflictKind.FocusOverlapsSchedule
                    : involvesTodo
                        ? ConflictKind.PlannedTodoOverlapsSchedule
                        : ConflictKind.ScheduleOverlap;

                // 两条普通日程硬重叠最严重；带上专注或待办则算警告
                var severity = kind == ConflictKind.ScheduleOverlap
                    ? ConflictSeverity.Error
                    : ConflictSeverity.Warning;

                conflicts.Add(new Conflict
                {
                    Kind = kind,
                    Severity = severity,
                    Message = BuildOverlapMessage(a, b, overlapEnd - overlapStart),
                    InvolvedSourceIds = new[] { a.SourceId, b.SourceId },
                    Start = overlapStart,
                    End = overlapEnd,
                });
            }
        }

        // ---- 冲突 2：截止时间落在忙碌时段里 ----
        conflicts.AddRange(DetectDeadlineConflicts(entries, allTodos, date));

        // ---- 冲突 3：一天安排太满 ----
        var busy = CalculateBusyDuration(entries);
        if (busy > OverloadThreshold)
        {
            conflicts.Add(new Conflict
            {
                Kind = ConflictKind.OverloadedDay,
                Severity = ConflictSeverity.Warning,
                Message = $"今天安排了 {(int)busy.TotalHours} 小时 {(busy.Minutes)} 分，几乎没有休息时间，建议留出空档。",
                InvolvedSourceIds = Array.Empty<int>(),
                Start = TimeSpan.Zero,
                End = TimeSpan.FromHours(24),
            });
        }

        // 严重的排前面，界面第一条就是最需要处理的
        return conflicts
            .OrderByDescending(c => c.Severity)
            .ThenBy(c => c.Start ?? TimeSpan.Zero)
            .ToList();
    }

    private static string BuildOverlapMessage(TimelineEntry a, TimelineEntry b, TimeSpan overlap)
    {
        var minutes = (int)Math.Round(overlap.TotalMinutes);
        var relation = a.Kind switch
        {
            TimelineEntryKind.FocusReservation or TimelineEntryKind.RunningFocus => "的专注时段与",
            TimelineEntryKind.PlannedTodo => "的计划时间与",
            _ => " 与",
        };

        return a.Kind == TimelineEntryKind.Schedule && b.Kind == TimelineEntryKind.Schedule
            ? $"「{a.Title}」与「{b.Title}」时间重叠 {minutes} 分钟"
            : $"「{a.Title}」{relation}「{b.Title}」重叠 {minutes} 分钟";
    }

    /// <summary>
    /// 找出「截止时间卡在忙碌时段里」的待办。
    ///
    /// 场景：待办「交实验报告」截止 14:00，但 13:30–15:00 有一节课。
    /// 这条待办实际做不完，所以要提醒。
    /// </summary>
    private static IEnumerable<Conflict> DetectDeadlineConflicts(
        List<TimelineEntry> entries,
        IEnumerable<TodoItem> allTodos,
        DateOnly date)
    {
        var scheduledTodoIds = entries
            .Where(e => e.Kind == TimelineEntryKind.PlannedTodo)
            .Select(e => e.SourceId)
            .ToHashSet();

        foreach (var todo in allTodos)
        {
            if (todo.IsCompleted) continue;
            if (!todo.DueAt.HasValue) continue;
            if (DateOnly.FromDateTime(todo.DueAt.Value) != date) continue;

            // 已经单独安排了时间的待办，重叠问题在上面第 1 类里已经报过了
            if (scheduledTodoIds.Contains(todo.Id)) continue;

            var due = todo.DueAt.Value.TimeOfDay;

            // 找出截止时刻所在的日程（截止点正好落在某段时间中间）
            var blocking = entries.FirstOrDefault(e =>
                e.Kind == TimelineEntryKind.Schedule &&
                e.Start <= due && due < e.End);

            if (blocking is null) continue;

            yield return new Conflict
            {
                Kind = ConflictKind.TodoDueInBusyTime,
                Severity = ConflictSeverity.Warning,
                Message = $"「{todo.Title}」截止时间 {FormatTime(due)} 落在「{blocking.Title}」期间" +
                          $"（{blocking.TimeRangeText}），可能来不及完成。",
                InvolvedSourceIds = new[] { todo.Id, blocking.SourceId },
                Start = blocking.Start,
                End = blocking.End,
            };
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 工具方法
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 算出这一天真正被占用的时长。
    /// 注意要去掉重叠部分 —— 两段 1 小时重叠 30 分钟，总共只占 1.5 小时，不是 2 小时。
    /// </summary>
    public static TimeSpan CalculateBusyDuration(List<TimelineEntry> entries)
    {
        if (entries.Count == 0) return TimeSpan.Zero;

        var sorted = entries.OrderBy(e => e.Start).ToList();
        var total = TimeSpan.Zero;
        var cursorStart = sorted[0].Start;
        var cursorEnd = sorted[0].End;

        for (int i = 1; i < sorted.Count; i++)
        {
            var e = sorted[i];
            if (e.Start <= cursorEnd)
            {
                // 和当前区间重叠或相接：把结束时间往后推（合并）
                if (e.End > cursorEnd) cursorEnd = e.End;
            }
            else
            {
                // 断开了：结算当前区间，开始新区间
                total += cursorEnd - cursorStart;
                cursorStart = e.Start;
                cursorEnd = e.End;
            }
        }

        total += cursorEnd - cursorStart;
        return total;
    }

    /// <summary>
    /// 找出这一天里的空闲时段（可以拿来推荐「什么时候适合专心做某件事」）。
    /// </summary>
    /// <param name="from">只考虑这个时刻之后（例如「现在」）。</param>
    /// <param name="minLength">太短的碎片不要。</param>
    public static List<(TimeSpan Start, TimeSpan End)> FindFreeSlots(
        List<TimelineEntry> entries,
        TimeSpan from,
        TimeSpan minLength)
    {
        var result = new List<(TimeSpan, TimeSpan)>();
        var cursor = from;
        var dayEnd = TimeSpan.FromHours(24);

        foreach (var e in entries.Where(x => x.Kind == TimelineEntryKind.Schedule)
                                 .OrderBy(x => x.Start))
        {
            if (e.End <= cursor) continue;          // 已经过去的，跳过
            if (e.Start > cursor)
            {
                var gap = e.Start - cursor;
                if (gap >= minLength) result.Add((cursor, e.Start));
            }
            if (e.End > cursor) cursor = e.End;
            if (cursor >= dayEnd) break;
        }

        if (dayEnd - cursor >= minLength) result.Add((cursor, dayEnd));
        return result;
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 24 ? "24:00" : $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
}
