# Helium Todo

> **许可证**：GPL-3.0-only（[`LICENSE`](LICENSE)，第三方组件见 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)）。
> 源码：本仓库（发布时填写公开仓库地址）。本项目不联网、不上传数据。

一个**本地优先**的「日程 + 专注 + 待办」桌面工具：C# 14 / .NET 10，Avalonia 12 界面，
数据默认存在本机 SQLite 文件里。

## 现在是什么状态

当前是 **0.5.0-alpha 的 P0 工程基建**快照：

- 七个项目已按目标分层改名：`HeliumTodo.Domain` / `Application` / `Infrastructure` /
  `Desktop` / `Tests` / `SelfTest` / `Verification`。
- 版本号统一为 `0.5.0-alpha`，新增 GPL-3.0 许可证与逐文件 SPDX 头。
- 插件宿主已移除，`IHeliumPlugin` 收进 `#if HELIUM_PLUGINS`。

目标技术栈（Material.Avalonia、PostgreSQL、EF Core 10）尚未切换：本机处于离线环境，
NuGet 与 PostgreSQL 都装不上，P1 之后的迁移计划见 [`DEVELOPMENT_SPEC.md`](DEVELOPMENT_SPEC.md)。
真实技术栈与已知偏差以 [`TECH_STACK.md`](TECH_STACK.md) 和 [`PROGRESS.md`](PROGRESS.md) 为准。

## 构建与运行

```powershell
dotnet build HeliumTodo.slnx -c Debug
dotnet HeliumTodo.Desktop\bin\Debug\net10.0\HeliumTodo.Desktop.dll
```

自检与界面校验：

```powershell
$env:HELIUM_CONFIG_DIR = "$PWD\.tmp-build\sandbox-config"
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --layout-check
```

`--check` 共十五项，另有 `--shell` 截图模式；完整命令见 [`AGENTS.md`](AGENTS.md)。

## 文档

| 文档 | 内容 |
| --- | --- |
| [`DEVELOPMENT_SPEC.md`](DEVELOPMENT_SPEC.md) | 0.5.0 目标规格、里程碑与验收标准 |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | 现有架构约定与踩坑记录 |
| [`DEVELOPMENT_PLAYBOOK.md`](DEVELOPMENT_PLAYBOOK.md) | 开发流程与质量门禁 |
| [`AGENTS.md`](AGENTS.md) | 给 AI 与人类的硬规则 |
| [`PROGRESS.md`](PROGRESS.md) | 逐轮进度（历史小节不改） |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | 贡献流程与 DCO |

## 许可证

本项目以 **GNU General Public License v3.0 only** 发布，不是 `or-later`。
你可以自由运行、研究、修改与再分发，但再分发时必须保持同一许可证并提供源码。
第三方依赖的许可证与版权行集中在 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)，
原文在 [`LICENSES/`](LICENSES/)。