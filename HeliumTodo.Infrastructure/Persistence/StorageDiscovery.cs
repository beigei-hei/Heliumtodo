// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Net.Sockets;
using System.Text.RegularExpressions;
using HeliumTodo.Application.Configuration;
using Npgsql;

namespace HeliumTodo.Infrastructure.Persistence;

/// <summary>
/// 本机 PostgreSQL 的自动发现：用户不该为了"连上数据库"去手改 config.json。
///
/// 只做本机（回环地址）探测，按下面的顺序找：
///   端口：配置里的端口 → 5432（官方安装）→ 55432（本项目便携版）
///   用户：配置里的用户 → postgres → 当前 Windows 用户
/// 连接一律用空密码 / 配置里的密码，Timeout 压到 2 秒；端口先用 400ms 的 TCP 预检，
/// 没人监听的端口不会拖慢启动。
///
/// 找到可用连接后，如果目标库不存在会尝试用同一账号 CREATE DATABASE
/// （只有有权限时才会成功，失败就继续找下一个候选）。
/// 同时从 `SHOW data_directory` 反推 pg_ctl.exe 的位置，写回配置，
/// 下次启动即使服务没开也能自动拉起。
///
/// 边界：不猜远程主机、不试密码字典、不改已存在库里的任何数据。
/// </summary>
public static partial class StorageDiscovery
{
    private const int TcpProbeTimeoutMs = 400;
    private const int ConnectTimeoutSeconds = 2;

    private static readonly int[] WellKnownPorts = [5432, 55432];

    /// <summary>找一台能连上的本机 PostgreSQL；成功时 discovered 可直接写回配置。</summary>
    public static bool TryDiscover(StorageSettings configured, out StorageSettings discovered, out string reason)
    {
        discovered = configured;
        reason = string.Empty;

        var host = NormalizeHost(configured.Host);
        if (host is null)
        {
            reason = "只自动检测本机数据库（localhost / 127.0.0.1 / ::1）";
            return false;
        }

        var database = string.IsNullOrWhiteSpace(configured.Database)
            ? "helium_todo"
            : configured.Database.Trim();

        var ports = new[] { configured.Port }
            .Concat(WellKnownPorts)
            .Where(p => p is > 0 and < 65536)
            .Distinct()
            .ToArray();

        var users = new[] { configured.Username, "postgres", Environment.UserName }
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var reachable = ports.Where(p => IsListening(host, p)).ToArray();
        if (reachable.Length == 0)
        {
            reason = $"{host} 的 {string.Join(" / ", ports)} 端口都没有 PostgreSQL 在监听";
            return false;
        }

        foreach (var port in reachable)
        {
            foreach (var user in users)
            {
                if (!TryOpen(host, port, user, configured.Password, database, out var error))
                {
                    // 库不存在时（3D000）再走一次"建库后重试"；其它错误（密码错/没权限）直接换候选。
                    if (!IsMissingDatabase(error)
                        || !TryCreateDatabase(host, port, user, configured.Password, database)
                        || !TryOpen(host, port, user, configured.Password, database, out error))
                    {
                        continue;
                    }
                }

                var found = Copy(configured, host, port, user, database);
                var (pgCtl, dataDirectory) = FindPgTooling(host, port, user, configured.Password, configured);
                if (pgCtl is not null)
                {
                    found.PgCtlPath = pgCtl;
                }

                if (dataDirectory is not null)
                {
                    found.PgDataDirectory = dataDirectory;
                }

                discovered = found;
                reason = $"{host}:{port} / {database}（用户 {user}）";
                return true;
            }
        }

        reason = $"{host} 的 {string.Join(" / ", reachable)} 端口能连上，但没有免密可用的账号或数据库";
        return false;
    }

    /// <summary>只接受本机地址，避免把"扫描局域网"这种事带进启动路径。</summary>
    public static string? NormalizeHost(string host)
    {
        var trimmed = host.Trim();
        if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("127.0.0.1", StringComparison.Ordinal)
            || trimmed.Equals("::1", StringComparison.Ordinal))
        {
            return "127.0.0.1";
        }

        return null;
    }

    private static StorageSettings Copy(
        StorageSettings source, string host, int port, string user, string database)
    {
        return new StorageSettings
        {
            Provider = "postgres",
            Host = host,
            Port = port,
            Database = database,
            Username = user,
            Password = source.Password,
            SslMode = source.SslMode,
            CommandTimeoutSeconds = source.CommandTimeoutSeconds,
            PgCtlPath = source.PgCtlPath,
            PgDataDirectory = source.PgDataDirectory,
        };
    }

    private static bool IsListening(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            return task.Wait(TcpProbeTimeoutMs) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryOpen(
        string host, int port, string user, string password, string database, out Exception? error)
    {
        error = null;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Username = user,
                Password = password ?? string.Empty,
                Database = database,
                Timeout = ConnectTimeoutSeconds,
                Pooling = false,
            };

            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private static bool IsMissingDatabase(Exception? error)
        => error is PostgresException { SqlState: "3D000" };

    private static bool TryCreateDatabase(
        string host, int port, string user, string password, string database)
    {
        if (!SafeDatabaseName().IsMatch(database))
        {
            return false;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Username = user,
                Password = password ?? string.Empty,
                Database = "postgres",
                Timeout = ConnectTimeoutSeconds,
                Pooling = false,
            };

            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();

            using (var check = connection.CreateCommand())
            {
                check.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
                check.Parameters.AddWithValue("name", database);
                if (check.ExecuteScalar() is not null)
                {
                    return true;
                }
            }

            using var create = connection.CreateCommand();
            // 库名已用 SafeDatabaseName 校验，不能参数化 CREATE DATABASE 的标识符。
            create.CommandText = $"CREATE DATABASE \"{database}\" ENCODING 'UTF8'";
            create.ExecuteNonQuery();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>从正在运行的实例反推 pg_ctl.exe / 数据目录，供下次冷启动自动拉起。</summary>
    private static (string? PgCtl, string? DataDirectory) FindPgTooling(
        string host, int port, string user, string password, StorageSettings configured)
    {
        string? dataDirectory = null;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Username = user,
                Password = password ?? string.Empty,
                Database = "postgres",
                Timeout = ConnectTimeoutSeconds,
                Pooling = false,
            };

            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SHOW data_directory";
            dataDirectory = command.ExecuteScalar() as string;
        }
        catch (Exception)
        {
            // 拿不到数据目录不影响"能连上数据库"这件事，只是少一个下次自动拉起的线索。
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured.PgCtlPath))
        {
            candidates.Add(configured.PgCtlPath);
        }

        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            var parent = Directory.GetParent(dataDirectory)?.FullName;
            if (parent is not null)
            {
                candidates.Add(Path.Combine(parent, "pgsql", "bin", "pg_ctl.exe"));
                candidates.Add(Path.Combine(parent, "bin", "pg_ctl.exe"));
                candidates.Add(Path.Combine(parent, "..", "bin", "pg_ctl.exe"));
            }

            candidates.Add(Path.Combine(dataDirectory, "..", "bin", "pg_ctl.exe"));
        }

        foreach (var candidate in candidates)
        {
            try
            {
                var full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                {
                    return (full, string.IsNullOrWhiteSpace(dataDirectory) ? configured.PgDataDirectory : dataDirectory);
                }
            }
            catch (Exception)
            {
                // 路径非法就跳过。
            }
        }

        return (null, string.IsNullOrWhiteSpace(dataDirectory) ? configured.PgDataDirectory : dataDirectory);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,62}$")]
    private static partial Regex SafeDatabaseName();
}
