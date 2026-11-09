// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Application;

/// <summary>
/// 应用层程序集标记。
///
/// 为什么现在是个空类：
///   P0 只做工程拆分（DEVELOPMENT_SPEC.md §3.1），先让依赖方向可以被编译器和
///   架构测试检查；真正的用例（UseCases）、契约（Contracts）与配置
///   （Configuration）在 P1 从旧 Core 原样迁入，本阶段不改变任何行为。
/// </summary>
internal static class ApplicationAssembly
{
}