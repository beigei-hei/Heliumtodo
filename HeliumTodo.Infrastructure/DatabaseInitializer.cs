// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 数据库初始化：建表 + （首次运行时）灌入示例数据。
/// 程序启动时调用一次。
///
/// 注意：备份已经不在这里了。
/// 为了「导出方式统一」，备份现在是 <see cref="DataBackupService"/> 负责的 .zip 包，
/// 启动时的自动备份也走同一条路（见 App 的启动流程）。
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// 确保数据库可用。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="seedIfEmpty">数据库是空的时候是否插入示例数据。</param>
    /// <returns>是否插入了示例数据。</returns>
    public static bool Initialize(HeliumDbContext db, bool seedIfEmpty = true)
    {
        // 建表（如果还没建的话）
        db.EnsureDatabaseReady();

        if (!seedIfEmpty)
        {
            return false;
        }

        // 三条主数据全都是空的，才认为是「第一次运行」
        var isEmpty = !db.Schedules.Any() && !db.Todos.Any() && !db.FocusSessions.Any();
        if (!isEmpty)
        {
            return false;
        }

        SeedData.Apply(db);
        return true;
    }
}