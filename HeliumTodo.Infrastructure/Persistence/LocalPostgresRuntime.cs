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
    /// <paramref name="logDirectory"/> 必须是当前用户可写的目录（应用配置目录），
    /// pg_ctl 的日志写在这里，避免数据目录所在盘只读或权限受限。
    /// </summary>
    public static bool TryStart(StorageSettings storage, string logDirectory, out string reason)
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

        // 先自己试写一次：postgres 启动第一件事是创建 postmaster.pid，不可写时它只给
        // 一句 “could not create lock file … Permission denied”，用户看不出是权限问题。
        try
        {
            var probe = Path.Combine(dataDirectory, $".helium-write-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            reason = $"数据目录不可写：{dataDirectory}（{ex.GetType().Name}: {ex.Message}）";
            return false;
        }

        try
        {
            // 日志写到应用配置目录：数据目录可能在只读盘/受控目录里，pg_ctl 打不开就会
            // 直接以 “could not open log file … Permission denied” 退出（发布包实测）。
            var logPath = Path.Combine(
                string.IsNullOrWhiteSpace(logDirectory) ? AppContext.BaseDirectory : logDirectory,
                "server-start.log");
            var startInfo = new ProcessStartInfo(pgCtl)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                // pg_ctl 与它拉起的 postgres 会继承父进程的 stdout/stderr。若父进程输出
                // 接的是管道（dotnet ... | tee、CI 采集），只要 postgres 不退出，管道就
                // 不会 EOF，调用方会一直等下去。这里把两个流都接到自己的管道上，让
                // postgres 继承的是我们这根（随进程释放关闭），而不是调用方的。
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

            // 把 pg_ctl 的 stderr 读出来：启动失败时真正的原因（端口占用、pid 文件、
            // 数据目录被锁）都在它里面，只报退出码等于让用户猜。
            // 注意不能同步 ReadToEnd：postgres 会继承这个句柄，句柄不关就一直等。
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            string? errorText = null;
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                reason = "无法启动 pg_ctl 进程";
                return false;
            }

            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(25_000))
            {
                reason = "pg_ctl 启动超时（25 秒）";
                return false;
            }

            if (errorTask.Wait(500))
            {
                errorText = errorTask.Result.Trim();
            }

            if (process.ExitCode != 0)
            {
                // 刚停库时数据目录可能还在收尾，立刻再启一次往往就好了。
                // 每次重试都重新接 stderr，失败时把最后一次的真实原因带回去。
                var started = false;
                for (var attempt = 1; attempt <= 2 && !started; attempt++)
                {
                    Thread.Sleep(1500 * attempt);

                    using var retry = Process.Start(startInfo);
                    if (retry is null)
                    {
                        break;
                    }

                    var retryErrorTask = retry.StandardError.ReadToEndAsync();
                    started = retry.WaitForExit(25_000) && retry.ExitCode == 0;

                    if (!started && retryErrorTask.Wait(500))
                    {
                        errorText = retryErrorTask.Result.Trim();
                    }
                }

                if (!started)
                {
                    var detail = string.IsNullOrWhiteSpace(errorText) ? string.Empty : $"：{errorText}";
                    reason = $"pg_ctl 返回 {process.ExitCode}（详见 {logPath}）{detail}";
                    return false;
                }
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