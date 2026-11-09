// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeliumTodo.Infrastructure.Persistence.Configurations;

/// <summary>自习室消息表。</summary>
public sealed class StudyRoomMessageConfiguration : IEntityTypeConfiguration<StudyRoomMessage>
{
    public void Configure(EntityTypeBuilder<StudyRoomMessage> entity)
    {
        entity.ToTable("StudyRoomMessages");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.SenderName).HasMaxLength(50);
        entity.Property(e => e.Content).HasMaxLength(4000);
        entity.Property(e => e.SentAt).HasColumnType("timestamp without time zone");

        entity.HasIndex(e => new { e.StudyRoomId, e.SentAt });
    }
}