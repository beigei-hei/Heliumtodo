// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 数据库上下文 —— 可以理解成「程序里的数据库」。
///
/// 每一个 <see cref="DbSet{T}"/> 属性对应数据库里的一张表。
/// 表结构不写在这里，而是分散在 <c>Persistence/Configurations</c> 的
/// <see cref="IEntityTypeConfiguration{T}"/> 里，一张表一个文件。
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

        // 迁移文件也放在 Infrastructure 程序集里，所以从本程序集扫描配置类。
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HeliumDbContext).Assembly);
    }
}