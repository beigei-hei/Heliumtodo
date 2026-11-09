// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeliumTodo.Infrastructure.Persistence.Configurations;

/// <summary>专注记录表。</summary>
public sealed class FocusSessionConfiguration : IEntityTypeConfiguration<FocusSession>
{
    public void Configure(EntityTypeBuilder<FocusSession> entity)
    {
        entity.ToTable("FocusSessions");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Title).HasMaxLength(200);
        entity.Property(e => e.Notes).HasMaxLength(4000);
        entity.Property(e => e.StudyRoomCode).HasMaxLength(32);

        entity.Property(e => e.PlannedStart).HasColumnType("timestamp without time zone");
        entity.Property(e => e.StartedAt).HasColumnType("timestamp without time zone");
        entity.Property(e => e.EndedAt).HasColumnType("timestamp without time zone");
        entity.Property(e => e.PlannedDuration).HasColumnType("interval");

        entity.Property(e => e.Status).HasConversion<int>();

        // 专注记录可以关联一个待办；待办删掉时把外键置空（保留历史记录）
        entity.HasOne(e => e.Todo)
              .WithMany()
              .HasForeignKey(e => e.TodoId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.PlannedStart);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.StartedAt);
    }
}