// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 数据库初始化：迁移到最新 + （首次运行时）灌入示例数据。
/// 程序启动时调用一次。
///
/// 0.5.0 起建库/升级**只能**走 EF Migrations（规格 §7.2）：
/// `EnsureCreated()`、`PRAGMA table_info`、裸 `ALTER TABLE` 全部删除。
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
        ArgumentNullException.ThrowIfNull(db);

        db.Database.Migrate();

        RefuseDowngrade(db);

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

    /// <summary>
    /// 数据库里已经应用了本程序不认识的迁移 —— 说明用户用新版本写过数据，
    /// 又启动了旧版本。继续跑会用旧模型读新表，轻则报错，重则写坏数据。
    /// </summary>
    private static void RefuseDowngrade(HeliumDbContext db)
    {
        var local = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var unknown = db.Database.GetAppliedMigrations()
            .Where(m => !local.Contains(m))
            .ToList();

        if (unknown.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "数据库结构比当前程序新（已应用未知迁移：" + string.Join("、", unknown) +
            "）。请升级程序，不要用旧版本继续写这个库。");
    }
}