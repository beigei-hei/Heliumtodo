// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HeliumTodo.Tests;

/// <summary>
/// 给需要真库的测试开一个一次性的 PostgreSQL 数据库。
///
/// 为什么不打桩：0.5.0 的数据层全是 PostgreSQL 方言（timestamp without time zone、
/// interval、setval、ON CONFLICT），拿内存库替身测出来的"绿"没有意义。
/// 所以这里真的 CREATE DATABASE，测完 DROP DATABASE。
///
/// 连接串从 `HELIUM_TEST_POSTGRES` 读（指向管理库，通常是 `...Database=postgres`）；
/// 没设置或者连不上时**跳过**，而不是让整个测试套件红掉 ——
/// 开发机上没有 PostgreSQL 的人也要能跑完剩下的 190 多项检查。
/// </summary>
internal sealed class PostgresTestDatabase : IDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseName;

    /// <summary>非 null 表示本类测试应整体跳过；内容是给人看的原因。</summary>
    public string? SkipReason { get; }

    /// <summary>指向本次测试专用数据库的连接串（未创建时为空串）。</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public PostgresTestDatabase(string prefix)
    {
        _adminConnectionString = string.Empty;
        _databaseName = string.Empty;

        var source = Environment.GetEnvironmentVariable("HELIUM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(source))
        {
            SkipReason = "跳过：未设置 HELIUM_TEST_POSTGRES（需要一台可连接的 PostgreSQL 17）";
            return;
        }

        try
        {
            var admin = new NpgsqlConnectionStringBuilder(source)
            {
                // 管理连接不池化：CREATE/DROP DATABASE 不能被池里的旧连接挡住
                Pooling = false,
                Timeout = 5,
            };

            // 库名只能是小写字母/数字/下划线，且 PostgreSQL 标识符上限 63 字节
            var suffix = Guid.NewGuid().ToString("N")[..12];
            _databaseName = $"helium_test_{prefix}_{suffix}".ToLowerInvariant();
            if (_databaseName.Length > 63)
            {
                _databaseName = _databaseName[..63];
            }

            _adminConnectionString = admin.ConnectionString;
            ConnectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString)
            {
                Database = _databaseName,
                // 测试连接也不池化：Dispose 之后 DROP DATABASE 才能立刻成功
                Pooling = false,
            }.ConnectionString;

            ExecuteAdmin($"CREATE DATABASE \"{_databaseName}\"");
        }
        catch (Exception ex)
        {
            SkipReason = $"跳过：无法连接 PostgreSQL（{ex.Message}）";
            ConnectionString = string.Empty;
        }
    }

    /// <summary>建库并建表；测试在拿到连接串之后自己决定要不要灌数据。</summary>
    public void CreateSchema()
    {
        if (SkipReason is not null)
        {
            return;
        }

        using var db = new HeliumDbContext(
            new DbContextOptionsBuilder<HeliumDbContext>()
                .UseNpgsql(ConnectionString)
                .Options);

        DatabaseInitializer.Initialize(db, seedIfEmpty: false);
    }

    public void Dispose()
    {
        if (SkipReason is not null || string.IsNullOrEmpty(_databaseName))
        {
            return;
        }

        try
        {
            // WITH (FORCE) 会踢掉残留连接；PG 13+ 支持，17 没问题
            ExecuteAdmin($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
        }
        catch
        {
            // 测试清理失败不该盖住测试本身的失败信息
        }
    }

    private void ExecuteAdmin(string sql)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}