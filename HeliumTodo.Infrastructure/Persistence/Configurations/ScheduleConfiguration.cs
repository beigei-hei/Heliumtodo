// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeliumTodo.Infrastructure.Persistence.Configurations;

/// <summary>日程表：开始/结束是「墙上几点」，必须存 timestamp without time zone。</summary>
public sealed class ScheduleConfiguration : IEntityTypeConfiguration<ScheduleItem>
{
    public void Configure(EntityTypeBuilder<ScheduleItem> entity)
    {
        entity.ToTable("Schedules");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Notes).HasMaxLength(4000);
        entity.Property(e => e.RecurrenceRule).HasMaxLength(100).HasDefaultValue(string.Empty);

        // 本地墙钟：Npgsql 6+ 默认把 DateTime 当 timestamptz 并按 UTC 换算，
        // 不显式写这一行，日程会整体平移 8 小时（规格 §7.3 / 坑 F4）。
        entity.Property(e => e.Start).HasColumnType("timestamp without time zone");
        entity.Property(e => e.Duration).HasColumnType("interval");

        entity.Property(e => e.Priority).HasConversion<int>();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.Block).HasConversion<int>();

        entity.HasIndex(e => e.Start);
        entity.HasIndex(e => e.Status);
    }
}