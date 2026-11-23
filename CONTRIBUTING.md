# 参与贡献（CONTRIBUTING）

感谢你愿意花时间改进 Helium Todo。开始之前请先读：

- [`AGENTS.md`](AGENTS.md)：不许破的硬规则（分层、界面、动效、测试）。
- [`DEVELOPMENT_SPEC.md`](DEVELOPMENT_SPEC.md)：开发流程与质量门禁。
- [`DEVELOPMENT_SPEC.md`](DEVELOPMENT_SPEC.md)：0.5.0 的目标技术栈与里程碑。

## 许可证与 DCO

本项目以 **GPL-3.0-only** 发布。提交代码即表示你同意以同一许可证分发你的贡献。

我们**不要求 CLA**（版权转让），但**要求每个提交带 DCO 签名**，用来证明你有权提交这段代码：

```text
Signed-off-by: Your Name <you@example.com>
```

用 `git commit -s` 自动生成这一行。名字与邮箱必须是真实的、可联系的。
如果提交来自你的雇主，请确认雇主允许你以 GPL-3.0-only 贡献这段代码。

新增源文件时必须在文件头写两行 SPDX 标识（`tools/check-license-headers.py` 会检查）：

```csharp
// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only
```

```xml
<!--
  SPDX-FileCopyrightText: 2025 Helium Todo contributors
  SPDX-License-Identifier: GPL-3.0-only
-->
```

新增依赖前先核对许可证与 GPL-3.0 的兼容性，并更新
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) 与 `LICENSES/` 里对应的原文。

## 开发环境

- .NET SDK 10.0.x（见 `global.json`）。
- Windows 10/11（当前界面层依赖 Win32 窗口样式与 DWM 动画）。
- 不需要 PostgreSQL 也能构建当前 P0 快照；数据库仍为 SQLite。

## 提交前必须跑通

```powershell
dotnet build HeliumTodo.slnx -c Debug
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --shell
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --layout-check
pwsh tools\check-file-integrity.ps1
python tools\check-license-headers.py
```

自检必须用 `HELIUM_CONFIG_DIR` 指到临时目录，绝不碰 `%APPDATA%\Helium Todo` 的真实数据。
退出条件：构建 **0 警告 0 错误**、SelfTest **190/190**、十五项 `--check` 全绿、
`git status --short` 为空。

## 提交信息

备注好修改的文档，代码以及提交目的（别只有一个修复了bug就行）。
界面、逻辑、文档尽量分开提交，方便回滚。
