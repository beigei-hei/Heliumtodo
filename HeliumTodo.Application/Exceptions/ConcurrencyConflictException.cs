// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Application.Exceptions;

/// <summary>
/// 两个窗口同时修改同一行时抛出的冲突。
///
/// 数据层用 PostgreSQL 的 xmin 做并发令牌：后提交的一方会拿到
/// <c>DbUpdateConcurrencyException</c>，Infrastructure 把它翻译成本异常，
/// 界面层只需要把 <see cref="Exception.Message"/> 放进提示条。
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message = "数据已被其它窗口修改，请刷新后重试。")
        : base(message)
    {
    }

    public ConcurrencyConflictException(Exception inner, string message = "数据已被其它窗口修改，请刷新后重试。")
        : base(message, inner)
    {
    }
}