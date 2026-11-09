// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Abstractions;

/// <summary>
/// 时钟抽象：领域规则里凡是"现在几点"都必须经过它，测试才能把时间钉死。
/// 直接用 <see cref="DateTimeOffset.UtcNow"/> 会让"跨天 / 到点提醒"这类测试变成看运气。
/// </summary>
public interface IClock
{
    /// <summary>当前 UTC 时间。</summary>
    DateTimeOffset UtcNow { get; }
}