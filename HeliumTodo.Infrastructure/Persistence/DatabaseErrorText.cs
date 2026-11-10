// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Net.Sockets;
using System.Text.RegularExpressions;
using Npgsql;

namespace HeliumTodo.Infrastructure.Persistence;

/// <summary>
/// 把数据库异常翻译成用户能照着做的中文。
///
/// 为什么不直接把 <c>ex.Message</c> 甩到状态栏：Npgsql 的原文是
/// "The maximum number of retries (3) was exceeded…"，用户看到只会以为程序坏了，
/// 实际上九成是「PostgreSQL 没启动」或「端口/用户名不对」。
/// </summary>
public static class DatabaseErrorText
{
    /// <summary>
    /// 生成一句短提示（状态栏 / 提示条用），同时把关键参数带出来。
    /// </summary>
    /// <param name="ex">原始异常，可以为 null（例如启动探测直接给了原因）。</param>
    /// <param name="maskedConnection">已打码的连接串，可为 null。</param>
    public static string Describe(Exception? ex, string? maskedConnection = null)
    {
        var where = string.IsNullOrWhiteSpace(maskedConnection)
            ? string.Empty
            : $"（{maskedConnection}）";

        if (Find<SocketException>(ex) is not null)
        {
            return $"连不上 PostgreSQL{where}。请确认数据库服务已启动、端口正确；"
                   + "可到「设置 → 数据与文件 → 数据库连接」修改后保存重启。";
        }

        if (Find<PostgresException>(ex) is { } pg)
        {
            return pg.SqlState switch
            {
                "28000" => $"PostgreSQL 里没有用户「{RoleName(pg)}」{where}。请到「设置 → 数据与文件 → 数据库连接」改成已存在的用户名（本机常见 postgres）后保存重启。",
                "28P01" => $"PostgreSQL 拒绝了用户名或密码{where}。请检查「设置 → 数据与文件 → 数据库连接」。",
                "3D000" => $"数据库不存在{where}。请先创建它（createdb），或在设置里换一个库名。",
                "42P01" => "数据库里缺少表结构：迁移没有应用成功。请确认程序有建表权限后重启。",
                "57P03" => "PostgreSQL 还在启动中，请等几秒再重试。",
                "53300" => "PostgreSQL 连接数已满，请关闭其它占用连接的程序后重试。",
                _ => $"PostgreSQL 报错（{pg.SqlState}）：{Trim(pg.MessageText)}",
            };
        }

        if (Find<TimeoutException>(ex) is not null
            || (ex?.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return $"连接 PostgreSQL 超时{where}。请检查主机/端口和防火墙。";
        }

        if (ex is null)
        {
            return $"数据库不可用{where}。请到「设置 → 数据与文件 → 数据库连接」检查后重启。";
        }

        return $"数据库操作失败：{Trim(ex.Message)}";
    }

    /// <summary>
    /// 启动探测失败时的提示（探测本身只给一句原因，不带异常对象）。
    /// </summary>
    public static string DescribeProbeFailure(string reason, string? maskedConnection)
    {
        var where = string.IsNullOrWhiteSpace(maskedConnection)
            ? string.Empty
            : $"（{maskedConnection}）";

        return $"连不上 PostgreSQL{where}：{reason}。"
               + "请在「设置 → 数据与文件 → 数据库连接」检查主机、端口、用户名和密码后保存重启。";
    }

    /// <summary>
    /// 状态栏用的一句话：只保留「哪类问题」，细节留给提示条和设置页。
    /// </summary>
    public static string Brief(Exception? ex)
    {
        if (Find<SocketException>(ex) is not null) return "连不上数据库";
        if (Find<PostgresException>(ex) is { } pg)
        {
            return pg.SqlState switch
            {
                "28000" => $"用户名「{RoleName(pg)}」不存在",
                "28P01" => "用户名或密码不正确",
                "3D000" => "数据库不存在",
                "42P01" => "表结构缺失（迁移未应用）",
                "57P03" => "数据库正在启动",
                _ => $"数据库报错（{pg.SqlState}）",
            };
        }
        if (Find<TimeoutException>(ex) is not null) return "连接超时";
        return Trim(ex?.Message ?? "未知错误", 60);
    }

    /// <summary>从 "role \"helium\" does not exist" 里取出角色名。</summary>
    private static string RoleName(PostgresException pg)
    {
        var match = Regex.Match(pg.MessageText ?? string.Empty, "role \"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : "（未知）";
    }

    private static T? Find<T>(Exception? ex) where T : Exception
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is T match) return match;
        }

        return null;
    }

    private static string Trim(string text, int max = 140)
    {
        var singleLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= max ? singleLine : singleLine[..max] + "…";
    }
}
