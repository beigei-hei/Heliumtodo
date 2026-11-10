// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeliumTodo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// xmin 并发令牌不建真实列：PostgreSQL 的 xmin 是系统列，本来就在每一行上。
    /// EF 生成器会产出 AddColumn("xmin")，但 Npgsql 的 SQL 生成器按系统列名把它整条跳过
    /// （见 NpgsqlMigrationsSqlGenerator 的 SystemColumnNames），所以这里显式留空，
    /// 避免迁移文件读起来像"要加一列"。
    /// </summary>
    public partial class XminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}