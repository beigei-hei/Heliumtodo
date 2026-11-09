// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Abstractions;

namespace HeliumTodo.Infrastructure.Platform;

/// <summary>
/// 生产环境时钟：只读系统时间，不缓存，避免长时间运行后时间漂移。
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}