// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeliumTodo.Infrastructure.Persistence.Configurations;

/// <summary>自习室表。</summary>
public sealed class StudyRoomConfiguration : IEntityTypeConfiguration<StudyRoom>
{
    public void Configure(EntityTypeBuilder<StudyRoom> entity)
    {
        entity.ToTable("StudyRooms");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Code).IsRequired().HasMaxLength(32);
        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.HostName).HasMaxLength(50);
        entity.Property(e => e.DefaultFocusDuration).HasColumnType("interval");

        entity.Property(e => e.CreatedAt)
              .HasColumnType("timestamptz")
              .HasConversion(
                  v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                  v => v.ToLocalTime());

        // 离开房间的时间（null = 还在房间里）
        entity.Property(e => e.ClosedAt).HasColumnType("timestamp without time zone");

        entity.HasIndex(e => e.Code).IsUnique();

        // 一个房间有多个成员；删房间时成员一起删掉
        entity.HasMany(e => e.Members)
              .WithOne()
              .HasForeignKey(m => m.StudyRoomId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}