// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 自习室的数据操作。
/// 【说明】联机暂缓期间，房间只存在本地数据库里，不会有网络行为。
/// </summary>
public sealed class StudyRoomRepository
{
    private readonly IDbContextFactory<HeliumDbContext> _factory;

    public StudyRoomRepository(IDbContextFactory<HeliumDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>取全部房间（含成员），最近创建的在前。</summary>
    public async Task<List<StudyRoom>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.StudyRooms
                       .AsNoTracking()
                       .Include(r => r.Members)
                       .OrderByDescending(r => r.CreatedAt)
                       .ToListAsync(ct);
    }

    /// <summary>
    /// 取「还开着」的房间（没离开过的），最近的在前。
    ///
    /// 打开自习室页时用这个：用户点了「离开」，那条记录就带上离开时间，
    /// 不该再被载回来。
    /// </summary>
    public async Task<List<StudyRoom>> GetOpenAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.StudyRooms
                       .AsNoTracking()
                       .Include(r => r.Members)
                       .Where(r => r.ClosedAt == null)
                       .OrderByDescending(r => r.CreatedAt)
                       .ToListAsync(ct);
    }

    /// <summary>按房间码查找（用于「输入房间码加入」）。</summary>
    public async Task<StudyRoom?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.StudyRooms
                       .AsNoTracking()
                       .Include(r => r.Members)
                       .FirstOrDefaultAsync(r => r.Code == code, ct);
    }

    /// <summary>保存一个房间（新增或更新都行）。</summary>
    public async Task<StudyRoom> SaveAsync(StudyRoom room, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        if (room.Id == 0)
        {
            db.StudyRooms.Add(room);
        }
        else
        {
            db.StudyRooms.Update(room);
        }

        await db.SaveChangesAsync(ct);
        return room;
    }

    /// <summary>删除房间（成员会一起删掉）。</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var found = await db.StudyRooms.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (found is null) return;

        db.StudyRooms.Remove(found);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>取某个房间的聊天记录，最旧的在前。</summary>
    public async Task<List<StudyRoomMessage>> GetMessagesAsync(int studyRoomId, int take = 200, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.StudyRoomMessages
                       .AsNoTracking()
                       .Where(m => m.StudyRoomId == studyRoomId)
                       .OrderBy(m => m.SentAt)
                       .Take(take)
                       .ToListAsync(ct);
    }

    /// <summary>保存一条消息。</summary>
    public async Task AddMessageAsync(StudyRoomMessage message, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.StudyRoomMessages.Add(message);
        await db.SaveChangesAsync(ct);
    }
}
