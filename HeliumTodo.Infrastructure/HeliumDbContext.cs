// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 数据库上下文 —— 可以理解成「程序里的数据库」。
///
/// 每一个 <see cref="DbSet{T}"/> 属性对应数据库里的一张表。
/// 想加新表：加一个 DbSet 属性 + 在 <see cref="OnModelCreating"/> 里配置一下就行。
///
/// 用什么数据库由构造参数决定（见 <c>DbContextFactory</c>），
/// 所以以后想换成别的数据库，这里一行都不用改。
/// </summary>
public class HeliumDbContext : DbContext
{
    public HeliumDbContext(DbContextOptions<HeliumDbContext> options) : base(options)
    {
    }

    /// <summary>日程表。</summary>
    public DbSet<ScheduleItem> Schedules => Set<ScheduleItem>();

    /// <summary>待办表。</summary>
    public DbSet<TodoItem> Todos => Set<TodoItem>();

    /// <summary>专注记录表。</summary>
    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();

    /// <summary>自习室表。</summary>
    public DbSet<StudyRoom> StudyRooms => Set<StudyRoom>();

    /// <summary>自习室成员表。</summary>
    public DbSet<StudyRoomMember> StudyRoomMembers => Set<StudyRoomMember>();

    /// <summary>自习室消息表。</summary>
    public DbSet<StudyRoomMessage> StudyRoomMessages => Set<StudyRoomMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ───────────── 日程 ─────────────
        modelBuilder.Entity<ScheduleItem>(entity =>
        {
            entity.ToTable("Schedules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.RecurrenceRule).HasMaxLength(100).HasDefaultValue(string.Empty);

            entity.HasIndex(e => e.Start);
            entity.HasIndex(e => e.Status);
        });

        // ───────────── 待办 ─────────────
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.ToTable("Todos");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(50);

            entity.HasIndex(e => e.IsCompleted);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.DueAt);
        });

        // ───────────── 专注记录 ─────────────
        modelBuilder.Entity<FocusSession>(entity =>
        {
            entity.ToTable("FocusSessions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.StudyRoomCode).HasMaxLength(20);

            // 专注记录可以关联一个待办；待办删掉时把外键置空（保留历史记录）
            entity.HasOne(e => e.Todo)
                  .WithMany()
                  .HasForeignKey(e => e.TodoId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.PlannedStart);
            entity.HasIndex(e => e.Status);
        });

        // ───────────── 自习室 ─────────────
        modelBuilder.Entity<StudyRoom>(entity =>
        {
            entity.ToTable("StudyRooms");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.HostName).HasMaxLength(50);

            entity.HasIndex(e => e.Code).IsUnique();

            // 离开房间的时间（null = 还在房间里）
            entity.Property(e => e.ClosedAt);

            // 一个房间有多个成员；删房间时成员一起删掉
            entity.HasMany(e => e.Members)
                  .WithOne()
                  .HasForeignKey(m => m.StudyRoomId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudyRoomMember>(entity =>
        {
            entity.ToTable("StudyRoomMembers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DisplayName).HasMaxLength(50);
            entity.Property(e => e.PeerId).HasMaxLength(100);
        });

        modelBuilder.Entity<StudyRoomMessage>(entity =>
        {
            entity.ToTable("StudyRoomMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SenderName).HasMaxLength(50);
            entity.Property(e => e.Content).HasMaxLength(2000);

            entity.HasIndex(e => e.StudyRoomId);
        });
    }

    /// <summary>
    /// 第一次创建数据库时建表。
    ///
    /// 说明：这里故意用「确保创建」而不是 EF 的迁移（Migrations）。
    /// 原因是首版还在快速改结构，迁移文件会一直冲突；
    /// 等结构稳定了再换成迁移也不迟（那时只要把这段换成 Migrate()）。
    /// </summary>
    public void EnsureDatabaseReady()
    {
        Database.EnsureCreated();
        EnsureColumns();
    }

    /// <summary>
    /// 给已经存在的老库补上后加的列。
    ///
    /// <c>EnsureCreated()</c> 只在「库/表还不存在」时建表，已经存在的库不会跟着
    /// 模型变 —— 所以新加的列得自己补，否则老用户那里查询会报"没有这一列"。
    /// 等结构稳定改用 Migrate() 之后，这个方法就可以删掉。
    /// </summary>
    private void EnsureColumns()
    {
        // 这里刻意写成字面量 SQL（不用插值字符串）：
        // 表名列名都是编译期常量，字面量既没有注入面，也不会触发 EF1002 警告。

        // ① 自习室「离开时间」（0.1.0 之前没有这个字段）
        if (!ColumnExists("StudyRooms", "ClosedAt"))
        {
            Database.ExecuteSqlRaw("ALTER TABLE \"StudyRooms\" ADD COLUMN \"ClosedAt\" TEXT NULL");

            // 升级之前根本没有"离开房间"这个概念，库里的旧房间都是些点了离开
            // 却没能记住的残留。不处理的话，用户升级后第一次打开自习室页
            // 依旧会看到自己在房间里。所以一次性全部标成已离开。
            Database.ExecuteSqlRaw(
                "UPDATE \"StudyRooms\" SET \"ClosedAt\" = datetime('now','localtime') WHERE \"ClosedAt\" IS NULL");
        }

        // ② 日程 / 待办的提醒提前量（0.1.0 之后加的）
        //    默认 NULL = 不提醒：老数据不该在用户没要求的情况下突然开始弹通知。
        AddNullableIntColumn("Schedules", "ReminderMinutes");
        AddNullableIntColumn("Todos", "ReminderMinutes");
    }

    /// <summary>缺这一列就补一个可空整数列（已有则什么都不做）。</summary>
    private void AddNullableIntColumn(string table, string column)
    {
        if (ColumnExists(table, column))
        {
            return;
        }

        // 表名/列名是编译期常量，所以这里用字面量写法；不同表的 SQL 只能各写一条。
        var sql = table switch
        {
            "Schedules" => "ALTER TABLE \"Schedules\" ADD COLUMN \"ReminderMinutes\" INTEGER NULL",
            "Todos" => "ALTER TABLE \"Todos\" ADD COLUMN \"ReminderMinutes\" INTEGER NULL",
            _ => null,
        };

        if (sql is null)
        {
            // 走到这里说明有人给这个方法传了没登记的表名 —— 属于编码错误，
            // 与其悄悄跳过，不如在调试输出里留个痕迹。
            System.Diagnostics.Debug.WriteLine($"[数据库] 没有为表 {table} 登记补列语句");
            return;
        }

        Database.ExecuteSqlRaw(sql);
    }

    private bool ColumnExists(string table, string column)
    {
        var connection = Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;

        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                // table_info 的第 2 列是列名
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }
}
