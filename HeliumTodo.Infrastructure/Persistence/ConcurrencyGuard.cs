// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure.Persistence;

/// <summary>
/// 把 EF 的并发异常翻译成界面能直接展示的异常。
/// </summary>
public static class ConcurrencyGuard
{
    /// <summary>
    /// 保存修改；xmin 对不上时抛 <see cref="ConcurrencyConflictException"/>。
    /// </summary>
    public static async Task SaveGuardedAsync(this DbContext db, CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }
}