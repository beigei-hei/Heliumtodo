# Helium Todo

> **许可证**：GPL-3.0-only（[`LICENSE`](LICENSE)，第三方组件见 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)）。
> 源码：本仓库（发布时填写公开仓库地址）。本项目不联网、不上传数据。

一个**本地优先**的「日程 + 专注 + 待办」桌面工具：C# 14 / .NET 10，Avalonia 12 界面，
数据存在本机 PostgreSQL 17 里（也可以用便携版，不装服务）。

## 现在是什么状态

当前是 **0.5.0-alpha 的 P6 文案与性能门禁完成**快照：

- 七个项目已按目标分层改名：`HeliumTodo.Domain` / `Application` / `Infrastructure` /
  `Desktop` / `Tests` / `SelfTest` / `Verification`。
- 版本号统一为 `0.5.0-alpha`，新增 GPL-3.0 许可证与逐文件 SPDX 头。
- 插件宿主已移除，`IHeliumPlugin` 收进 `#if HELIUM_PLUGINS`。
- 主存储已切到 **PostgreSQL 17**：EF Core 10 + Npgsql，只用 Migration 建表；
  业务时间是墙上时钟、审计时间是 `timestamptz`。
- 备份升到 **v2**（`manifest.json` + 每表一份 JSONL，带 SHA256），支持 Replace / Merge；
  0.4 的 SQLite 备份只识别、不自动转换。

界面使用 **FluentAvaloniaUI 3.1.0** + Avalonia 12.1.5（0.5.0 曾整体切到 Material.Avalonia，因小尺寸按钮与下拉框观感回退，见 `PROGRESS.md` 2026-11-10 热修 2）；设置页折叠分组和两条提示条仍是自绘控件（`Desktop/Controls/SettingsExpander.cs`），强调色支持任意 `#RRGGBB`。外壳交互（拖拽 / 双击最大化 / 窗口按钮 / 8 个抓手 / 回车发送 / 滚动同步）改由 `Xaml.Behaviors.Avalonia` 12.1.5 的 `Behavior<T>` 接管，code-behind 只剩装配。

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

**连不上时不用手改配置**：应用启动时会依次尝试「配置里的端口 → 5432 → 55432」和
「配置里的用户 → postgres → 当前 Windows 用户」，找到可免密登录的本机实例后自动写回
`config.json`（并从运行实例反推 `pg_ctl` 与数据目录，下次冷启动自动拉起）。设置页
「数据与文件 → 数据库连接」也有「自动检测」按钮，只回填表单，确认后再「保存并重启」。
自动识别只碰本机回环地址，不猜密码、不建角色；远程库或密码错误仍按提示处理。

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

`--check` 共十九项（含 `--db-check`、`--theme-check`、`--behavior-check`、`--copy-check`），另有 `--shell` 截图模式；完整命令见 [`AGENTS.md`](AGENTS.md)。

发布包：

```powershell
pwsh tools\publish.ps1
```

脚本做自包含 `win-x64` **单文件**发布（`PublishSingleFile`，原生库自解压），把 `LICENSE`、
`THIRD-PARTY-NOTICES.md`、`LICENSES/`、`SOURCE.txt`（写明 commit）和 VC++ 运行库
（`vcruntime140*.dll` / `msvcp140.dll`）一起塞进 `dist\HeliumTodo-0.5.0-win-x64.zip`，最后打印 SHA256。
已经 publish 过、只想重打包时加 `-SkipPublish`。

单文件版首次启动会把原生库解压到 `%TEMP%\.net`；如果该目录被策略禁止写入（报
“Failed to extract embedded files … error code: 5”），把 `DOTNET_BUNDLE_EXTRACT_BASE_DIR`
指到一个可写目录即可。

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