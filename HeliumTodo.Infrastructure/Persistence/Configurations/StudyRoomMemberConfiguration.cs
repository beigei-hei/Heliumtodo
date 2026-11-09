// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HeliumTodo.Infrastructure.Persistence.Configurations;

/// <summary>自习室成员表。</summary>
public sealed class StudyRoomMemberConfiguration : IEntityTypeConfiguration<StudyRoomMember>
{
    public void Configure(EntityTypeBuilder<StudyRoomMember> entity)
    {
        entity.ToTable("StudyRoomMembers");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.DisplayName).HasMaxLength(50);
        entity.Property(e => e.PeerId).HasMaxLength(100);
        entity.Property(e => e.JoinedAt).HasColumnType("timestamp without time zone");
        entity.Property(e => e.FocusedToday).HasColumnType("interval");

        entity.HasIndex(e => e.StudyRoomId);
    }
}