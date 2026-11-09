# Helium Todo

> **许可证**：GPL-3.0-only（[`LICENSE`](LICENSE)，第三方组件见 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)）。
> 源码：本仓库（发布时填写公开仓库地址）。本项目不联网、不上传数据。

一个**本地优先**的「日程 + 专注 + 待办」桌面工具：C# 14 / .NET 10，Avalonia 12 界面，
数据存在本机 PostgreSQL 17 里（也可以用便携版，不装服务）。

## 现在是什么状态

当前是 **0.5.0-alpha 的 P3 主题骨架**快照：

- 七个项目已按目标分层改名：`HeliumTodo.Domain` / `Application` / `Infrastructure` /
  `Desktop` / `Tests` / `SelfTest` / `Verification`。
- 版本号统一为 `0.5.0-alpha`，新增 GPL-3.0 许可证与逐文件 SPDX 头。
- 插件宿主已移除，`IHeliumPlugin` 收进 `#if HELIUM_PLUGINS`。
- 主存储已切到 **PostgreSQL 17**：EF Core 10 + Npgsql，只用 Migration 建表；
  业务时间是墙上时钟、审计时间是 `timestamptz`。
- 备份升到 **v2**（`manifest.json` + 每表一份 JSONL，带 SHA256），支持 Replace / Merge；
  0.4 的 SQLite 备份只识别、不自动转换。

界面已挂上 Material.Avalonia 3.21 / Avalonia 12.1.5：标准控件走 Material，强调色支持任意 `#RRGGBB`；尚未替换的 FluentAvalonia 控件在 P4 一并换掉，届时删除 FA 依赖。

## 准备数据库

需要本机有 PostgreSQL 17，并且存在一个可连接的库。便携版（免安装、免管理员）示例：

```powershell
# 解压 postgresql-17.x-windows-x64-binaries.zip 后
.\pgsql\bin\initdb.exe -D .\data -U postgres -A trust --encoding=UTF8 --locale=C
.\pgsql\bin\pg_ctl.exe -D .\data -o "-p 55432" start
.\pgsql\bin\createdb.exe -h 127.0.0.1 -p 55432 -U postgres helium_todo
$env:HELIUM_DB = "Host=127.0.0.1;Port=55432;Database=helium_todo;Username=postgres"
```

默认连接是 `localhost:5432/helium_todo`（用户 `helium`）；用 `HELIUM_DB` 覆盖整条连接串，
或用 `HELIUM_DB_PASSWORD` 只覆盖密码。仓库里的 `infra/docker-compose.yml` 也可以直接拉起一个实例。

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

`--check` 共十六项（含 `--db-check`），另有 `--shell` 截图模式；完整命令见 [`AGENTS.md`](AGENTS.md)。

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