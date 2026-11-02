using HeliumTodo.Core.Models;

namespace HeliumTodo.Data;

/// <summary>
/// 首次运行时的示例数据。
///
/// 为什么要预置数据？
///   新装的程序如果一片空白，很难看出「每日工作流」到底能干什么。
///   这里故意安排了 <b>两处时间重叠</b>，这样一打开就能看到错位展示和冲突提醒。
///   只要数据库里有数据，就不会再插入（见 <see cref="DatabaseInitializer"/>）。
/// </summary>
public static class SeedData
{
    /// <summary>把示例数据加进数据库。</summary>
    public static void Apply(HeliumDbContext db)
    {
        var today = DateTime.Today;

        // 找到「本周一」，让示例日程落在本周内，看起来更像真的课表
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        // ───────────────────────── 待办 ─────────────────────────
        var todos = new List<TodoItem>
        {
            new()
            {
                Title = "写数据结构实验报告",
                Notes = "实验三是二叉树的遍历，重点写清楚递归思路。",
                Priority = Priority.Urgent,
                EstimatedDuration = TimeSpan.FromMinutes(90),
                DueAt = today.AddDays(1).AddHours(9),           // 明天上午 9 点截止
                Category = "学习",
                CreatedAt = today.AddHours(8),
            },
            new()
            {
                Title = "复习英语六级词汇",
                Notes = "背 List 13 – List 15。",
                Priority = Priority.High,
                EstimatedDuration = TimeSpan.FromMinutes(45),
                DueAt = today.AddDays(2).AddHours(22),
                Category = "学习",
                CreatedAt = today.AddHours(8).AddMinutes(5),
            },
            new()
            {
                Title = "整理线性代数笔记",
                Priority = Priority.Normal,
                EstimatedDuration = TimeSpan.FromMinutes(60),
                Category = "学习",
                CreatedAt = today.AddHours(9),
            },
            new()
            {
                Title = "去快递站取包裹",
                Priority = Priority.Low,
                EstimatedDuration = TimeSpan.FromMinutes(20),
                DueAt = today.AddHours(20),
                Category = "生活",
                CreatedAt = today.AddHours(10),
            },
            new()
            {
                Title = "给室友带早饭",
                Priority = Priority.Normal,
                EstimatedDuration = TimeSpan.FromMinutes(15),
                IsCompleted = true,
                CompletedAt = today.AddHours(7).AddMinutes(30),
                Category = "生活",
                CreatedAt = today.AddHours(7),
            },
        };
        db.Todos.AddRange(todos);

        // ───────────────────────── 日程 ─────────────────────────
        var schedules = new List<ScheduleItem>
        {
            // 每周一三五：高等数学（08:00 – 09:40）
            new()
            {
                Title = "高等数学",
                Notes = "三教 305",
                Start = monday.AddHours(8),
                Duration = TimeSpan.FromMinutes(100),
                Priority = Priority.High,
                Block = TimeBlock.Morning,
                RecurrenceRule = "weekly:1,3,5",
            },

            // 每周一三五：算法设计（10:00 – 11:40）
            new()
            {
                Title = "算法设计与分析",
                Notes = "实验楼 B201",
                Start = monday.AddHours(10),
                Duration = TimeSpan.FromMinutes(100),
                Priority = Priority.High,
                Block = TimeBlock.Morning,
                RecurrenceRule = "weekly:1,3,5",
            },

            // 每周一：社团例会（11:00 – 12:00）
            // ★ 故意和上面的算法课重叠 40 分钟 —— 用来演示「错位展示 + 冲突提醒」
            new()
            {
                Title = "ACM 社团例会",
                Notes = "大学生活动中心 208",
                Start = monday.AddHours(11),
                Duration = TimeSpan.FromHours(1),
                Priority = Priority.Normal,
                Block = TimeBlock.Morning,
                RecurrenceRule = "weekly:1",
            },

            // 每周二四：大学英语（14:00 – 15:40）
            new()
            {
                Title = "大学英语",
                Notes = "一教 102",
                Start = monday.AddDays(1).AddHours(14),
                Duration = TimeSpan.FromMinutes(100),
                Priority = Priority.Normal,
                Block = TimeBlock.Afternoon,
                RecurrenceRule = "weekly:2,4",
            },

            // 每周二：线性代数（15:00 – 16:40）
            // ★ 和英语课重叠 40 分钟，第二处冲突
            new()
            {
                Title = "线性代数",
                Notes = "三教 210",
                Start = monday.AddDays(1).AddHours(15),
                Duration = TimeSpan.FromMinutes(100),
                Priority = Priority.High,
                Block = TimeBlock.Afternoon,
                RecurrenceRule = "weekly:2",
            },

            // 每周三：体育课（16:00 – 17:30）
            new()
            {
                Title = "体育（羽毛球）",
                Notes = "体育馆",
                Start = monday.AddDays(2).AddHours(16),
                Duration = TimeSpan.FromMinutes(90),
                Priority = Priority.Low,
                Block = TimeBlock.Afternoon,
                RecurrenceRule = "weekly:3",
            },

            // 今晚：自习（19:00 – 21:00），单次
            new()
            {
                Title = "晚自习",
                Notes = "图书馆四楼",
                Start = today.AddHours(19),
                Duration = TimeSpan.FromHours(2),
                Priority = Priority.Normal,
                Block = TimeBlock.Evening,
                RecurrenceRule = string.Empty,
            },

            // 今早：小组讨论（09:00 – 10:30），单次
            // ★ 今天本来就有课的话，这里会再产生一处重叠，让「今日工作流」一打开就有内容
            new()
            {
                Title = "课程项目小组讨论",
                Notes = "线上 · 腾讯会议",
                Start = today.AddHours(9),
                Duration = TimeSpan.FromMinutes(90),
                Priority = Priority.High,
                Block = TimeBlock.Morning,
                RecurrenceRule = string.Empty,
            },
        };
        db.Schedules.AddRange(schedules);

        // 保存一次，让上面插入的数据拿到数据库分配的 Id
        db.SaveChanges();

        // ─────────────────── 待办 → 时间轴 的联动示例 ───────────────────
        // 给「复习英语六级词汇」安排今晚 21:00 做 45 分钟。
        //
        // 关键点：**只设置 PlannedStart，不要再额外插一条 ScheduleItem**。
        //   DayTimelineBuilder 会自己把「有 PlannedStart 的待办」当成
        //   TimelineEntryKind.PlannedTodo 放进时间轴（紫色的那种块）。
        //   如果这里再插一条日程，时间轴上同一个时段就会出现两个块 —— 那是重复显示，不是联动。
        var vocab = todos.First(t => t.Title == "复习英语六级词汇");
        vocab.PlannedStart = today.AddHours(21);

        db.SaveChanges();

        // ───────────────────────── 专注 ─────────────────────────
        var focusSessions = new List<FocusSession>
        {
            // 已完成的一次专注，关联到待办上（演示「专注 → 待办累计时长」）
            new()
            {
                Title = "写数据结构实验报告",
                TodoId = todos.First(t => t.Title == "写数据结构实验报告").Id,
                PlannedStart = today.AddHours(14),
                PlannedDuration = TimeSpan.FromMinutes(50),
                StartedAt = today.AddHours(14),
                EndedAt = today.AddHours(14).AddMinutes(52),
                Status = FocusSessionStatus.Completed,
                InterruptionCount = 1,
                Notes = "效率还不错",
            },

            // 已完成的又一次专注
            new()
            {
                Title = "复习英语六级词汇",
                TodoId = vocab.Id,
                PlannedStart = today.AddHours(16),
                PlannedDuration = TimeSpan.FromMinutes(25),
                StartedAt = today.AddHours(16),
                EndedAt = today.AddHours(16).AddMinutes(25),
                Status = FocusSessionStatus.Completed,
            },

            // ★ 预订的专注时长：今晚 19:30 – 20:15（45 分钟）
            //   它和「晚自习 19:00 – 21:00」重叠 —— 演示「预订专注撞上日程」这类冲突
            new()
            {
                Title = "预订：专心做完实验报告",
                TodoId = todos.First(t => t.Title == "写数据结构实验报告").Id,
                PlannedStart = today.AddHours(19).AddMinutes(30),
                PlannedDuration = TimeSpan.FromMinutes(45),
                Status = FocusSessionStatus.Reserved,
                Notes = "提前预订的专注时段",
            },
        };
        db.FocusSessions.AddRange(focusSessions);

        db.SaveChanges();

        // ───────────────────────── 自习室 ─────────────────────────
        var room = new StudyRoom
        {
            Code = "STUDY1",
            Name = "期末冲刺自习室",
            HostName = "我",
            DefaultFocusDuration = TimeSpan.FromMinutes(45),
            IsLocalOnly = true,
            CreatedAt = today.AddHours(8),

            // 示例房间标记成"已经离开过"。
            // 否则第一次装好程序打开自习室页，会莫名其妙发现自己已经在房间里 ——
            // 示例数据是拿来看的，不是替用户做决定的。
            ClosedAt = today.AddHours(10),
        };
        room.Members.Add(new StudyRoomMember
        {
            DisplayName = "我",
            PeerId = "local",
            IsOnline = true,
            JoinedAt = today.AddHours(8),
            FocusedToday = TimeSpan.FromMinutes(77),
        });
        db.StudyRooms.Add(room);

        db.SaveChanges();
    }
}
