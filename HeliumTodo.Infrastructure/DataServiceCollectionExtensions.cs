// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HeliumTodo.Infrastructure;

/// <summary>
/// 数据层的启动配置。
/// 界面层只需要调用 <see cref="AddHeliumData"/>，不用关心 EF Core 的细节。
/// </summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// 把数据库服务注册进依赖注入容器。
    /// </summary>
    /// <param name="services">DI 容器。</param>
    /// <param name="connectionString">PostgreSQL 连接串（已由 <c>PostgresConnection.Resolve</c> 组装）。</param>
    /// <param name="enableSensitiveDataLogging">
    /// 是否在日志里打印参数值（只在 Debug 打开，发布版要关掉，避免把隐私写进日志）。
    /// </param>
    public static IServiceCollection AddHeliumData(
        this IServiceCollection services,
        string connectionString,
        bool enableSensitiveDataLogging = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // 用「工厂」的方式注册：每次操作开一个新的 DbContext，用完就扔。
        // 这是 EF Core 在桌面程序里的推荐做法（DbContext 不是线程安全的，不能长期共用）。
        services.AddDbContextFactory<HeliumDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // 迁移失败时不要无限重试：桌面程序启动阶段，连不上就应该给用户看错误。
                npgsql.CommandTimeout(30);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
            });

            if (enableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }

            // 桌面程序不需要挂起跟踪查询，关掉能省点内存
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        });

        return services;
    }
}