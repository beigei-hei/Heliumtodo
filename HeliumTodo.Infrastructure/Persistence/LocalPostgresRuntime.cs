// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using HeliumTodo.Application.Configuration;

namespace HeliumTodo.Infrastructure.Persistence;

/// <summary>
/// 便携 PostgreSQL 的启动器。
///
/// 0.5.0-alpha 交付后用户反复遇到「数据库没起来」：便携 PostgreSQL 不会开机自启，
/// 应用只能报连接超时。这里在启动前做一次“能不能连”的检查，连不上就按配置的
/// pg_ctl.exe + data 目录把服务器拉起来，然后再让 EF 去连。
///
/// 只处理本机（回环地址）；远程数据库和系统服务（PostgreSQL 安装器注册的服务）
/// 不归这里管，启动失败仍由调用方给出中文提示。
/// </summary>
public static class LocalPostgresRuntime
{
    public const string PgCtlVariable = "HELIUM_PG_CTL";
    public const string PgDataVariable = "HELIUM_PG_DATA";

    /// <summary>
    /// 尝试启动本机 PostgreSQL。
    /// 返回 true 表示启动命令执行成功且端口已可连接；
    /// 返回 false 时 <paramref name="reason"/> 说明是“没配置”还是“启动失败”。
    /// </summary>
    public static bool TryStart(StorageSettings storage, out string reason)
    {
        var pgCtl = ResolvePath(storage.PgCtlPath, PgCtlVariable);
        var dataDirectory = ResolvePath(storage.PgDataDirectory, PgDataVariable);

        if (string.IsNullOrWhiteSpace(pgCtl) || string.IsNullOrWhiteSpace(dataDirectory))
        {
            reason = "未配置本机数据库启动路径（PgCtlPath / PgDataDirectory）";
            return false;
        }

        if (!File.Exists(pgCtl))
        {
            reason = $"找不到 pg_ctl.exe：{pgCtl}";
            return false;
        }

        if (!Directory.Exists(dataDirectory) || !File.Exists(Path.Combine(dataDirectory, "PG_VERSION")))
        {
            reason = $"data 目录不像 PostgreSQL 数据目录：{dataDirectory}";
            return false;
        }

        try
        {
            var logPath = Path.Combine(Path.GetDirectoryName(dataDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? dataDirectory, "server-start.log");
            var startInfo = new ProcessStartInfo(pgCtl)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                // 不接管道：postgres 会继承句柄，ReadToEnd 会一直等服务器退出（实测挂死）。
                WorkingDirectory = Path.GetDirectoryName(pgCtl) ?? AppContext.BaseDirectory,
            };
            startInfo.ArgumentList.Add("start");
            startInfo.ArgumentList.Add("-D");
            startInfo.ArgumentList.Add(dataDirectory);
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add(logPath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add($"-p {storage.Port}");
            startInfo.ArgumentList.Add("-w");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("20");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                reason = "无法启动 pg_ctl 进程";
                return false;
            }

            if (!process.WaitForExit(25_000))
            {
                reason = "pg_ctl 启动超时（25 秒）";
                return false;
            }

            if (process.ExitCode != 0)
            {
                reason = $"pg_ctl 返回 {process.ExitCode}（详见 {logPath}）";
                return false;
            }

            for (var i = 0; i < 10; i++)
            {
                if (PostgresConnection.Probe(storage, 800) is null)
                {
                    reason = string.Empty;
                    return true;
                }

                Thread.Sleep(300);
            }

            reason = "pg_ctl 已返回，但端口仍连不上";
            return false;
        }
        catch (Exception ex)
        {
            reason = $"启动失败：{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static string? ResolvePath(string? configured, string variable)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment.Trim();
    }


}