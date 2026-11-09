// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Application.Configuration;
using Npgsql;

namespace HeliumTodo.Infrastructure.Persistence;

/// <summary>
/// 把 <see cref="StorageSettings"/> 变成 Npgsql 连接串，并负责日志脱敏。
///
/// 覆盖顺序（规格 §7.1）：
///   <c>HELIUM_DB</c>（完整连接串）&gt; <c>HELIUM_DB_PASSWORD</c>（只覆盖密码）&gt; <c>config.json</c>。
/// </summary>
public static class PostgresConnection
{
    /// <summary>完整连接串的环境变量名。</summary>
    public const string FullConnectionVariable = "HELIUM_DB";

    /// <summary>根据配置和环境变量组装连接串。</summary>
    public static string Resolve(StorageSettings storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var full = Environment.GetEnvironmentVariable(FullConnectionVariable);
        if (!string.IsNullOrWhiteSpace(full))
        {
            return full;
        }

        if (!string.Equals(storage.Provider, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            // 不悄悄回退 SQLite：配置写错了要立刻说清楚，否则用户会以为数据还在，
            // 实际却连到了一个空的 SQLite 文件。
            throw new InvalidOperationException(
                $"不支持的存储提供程序「{storage.Provider}」；0.5.0 只支持 postgres。");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = storage.Host,
            Port = storage.Port,
            Database = storage.Database,
            Username = storage.Username,
            Password = storage.Password,
            SslMode = Enum.TryParse<SslMode>(storage.SslMode, ignoreCase: true, out var sslMode)
                ? sslMode
                : SslMode.Prefer,
            CommandTimeout = storage.CommandTimeoutSeconds,
            MaxPoolSize = 20,
            MinPoolSize = 2,
            NoResetOnClose = false,
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// 打日志前把 <c>Password=...</c> 换成 <c>***</c>。
    /// 不用正则去猜，直接让 Npgsql 的 builder 解析再回写。
    /// </summary>
    public static string Mask(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Password = "***",
            };
            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            // 连接串都解析不了的时候，宁可整串不打印，也不冒险泄露密码。
            return "<无法解析的连接串>";
        }
    }
}