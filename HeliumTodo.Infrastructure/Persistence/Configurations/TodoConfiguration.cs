// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeliumTodo.Infrastructure.Persistence.Configurations;

/// <summary>
/// 待办表。
///
/// 时间列分两种：
///   · 截止 / 计划 / 完成 —— 业务墙钟，`timestamp without time zone`；
///   · 创建时间 —— 审计时间，`timestamptz`，写库前换成 UTC，读出来再换回本地。
/// 两者混用是 0.5.0 最容易踩的坑（规格 §7.3）。
/// </summary>
public sealed class TodoConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> entity)
    {
        entity.ToTable("Todos");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Notes).HasMaxLength(4000);
        entity.Property(e => e.Category).HasMaxLength(50);

        entity.Property(e => e.DueAt).HasColumnType("timestamp without time zone");
        entity.Property(e => e.PlannedStart).HasColumnType("timestamp without time zone");
        entity.Property(e => e.CompletedAt).HasColumnType("timestamp without time zone");
        entity.Property(e => e.EstimatedDuration).HasColumnType("interval");
        entity.Property(e => e.FocusedDuration).HasColumnType("interval");

        entity.Property(e => e.Priority).HasConversion<int>();

        entity.Property(e => e.CreatedAt)
              .HasColumnType("timestamptz")
              .HasConversion(
                  v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                  v => v.ToLocalTime());

        entity.HasIndex(e => e.DueAt);
        entity.HasIndex(e => new { e.IsCompleted, e.Priority });
        entity.HasIndex(e => e.PlannedStart);
    }
}