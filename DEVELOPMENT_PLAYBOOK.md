# Helium Todo · 开发流程手册

> 这份文档回答一个问题：**在这个仓库里改代码，按什么流程走才算"做完"**。
>
> 它同时是 **Skills for Real Engineers 技能集**的采用方案：哪些用、怎么用、哪些不用、为什么不用。
> 目标读者：接手这个项目的下一个人（或下一个 AI Agent）。
>
> 版本 **0.3.5-alpha**　·　建立于 2026-11-02　·　配套 [`AGENTS.md`](AGENTS.md)（给 AI 的硬规则）

---

## 目录

1. [每轮开发固定七步](#每轮开发固定七步)
2. [采用的技能](#采用的技能)
3. [降级使用的技能](#降级使用的技能)
4. [明确排除的技能](#明确排除的技能)
5. [质量门禁](#质量门禁)
6. [写作与文档规范](#写作与文档规范)
7. [交接清单](#交接清单)
8. [已知缺口与建议](#已知缺口与建议)

---

## 每轮开发固定七步

不管是"加一个功能"还是"修一个 bug"，都走这条线。规模小的可以合并步骤，但**不能跳过 ⑤ 和 ⑥**。

| 步骤 | 做什么 | 用哪个技能 | 产出 |
| --- | --- | --- | --- |
| ① 立题 | 把用户的一句话拆成"可验收的行为"：改哪里、怎么判断改好了 | `to-spec`（降级：写进对话的计划段） | 一段验收标准（3–6 条） |
| ② 设计 | 判断要不要新模块、新接口放哪层；术语先对齐 | `codebase-design` / `domain-modeling` | 涉及文件的清单 + 分层理由 |
| ③ 验证先行 | **先想清楚怎么证明它对**：单测？无头渲染？像素统计？ | `tdd` | 失败的测试或自检断言 |
| ④ 实现 | 一次只让一件事变绿 | `tdd` | 代码 + 测试 |
| ⑤ 排障 | 红了就按"先复现、再二分、最后才猜"的顺序查 | `diagnosing-bugs` | 根因一句话 + 修法 |
| ⑥ 收尾 | 跑完质量门禁（见第 5 节），同步文档 | `code-review` | 全绿的输出 |
| ⑦ 交付 | 截图存档、写 PROGRESS、报数字 | `writing-for-agents` | 用户能看懂的一段总结 |

**一条硬经验**：③ 不能省。这个项目历史上最贵的几次返工，都是"改完看着挺对 → 用户说不对 → 猜原因 → 改了别的地方"。凡是能从无头渲染里量出来的，一律量（本项目用像素统计代替肉眼，见第 5 节）。

---

## 采用的技能

以下 8 个技能**默认启用**：满足"触发条件"时自动使用，不需要用户点名。

### 1. `tdd` — 先红后绿

- **触发**：新增功能、修 bug、改算法。
- **在本项目落地**：`HeliumTodo.Tests`（xunit）+ 自研 `HeliumTodo.SelfTest` 运行器（`dotnet test` 在本机沙箱起不来，见 `PROJECT_OVERVIEW.md`「为什么需要自研的 SelfTest 运行器」）。
- **写法约定**：测试方法名用**中文**（`认不出来的值应回落到默认而不是抛异常`），方法上方一行中文注释说明"为什么值得测"。
- **跳过条件**：纯 XAML 排版。这类没有逻辑可测，改用 `--layout-check` / `--appearance-check` 的断言 + 像素统计。

### 2. `diagnosing-bugs` — 疑难 bug 与性能回归

- **触发**：用户说"diagnose"/"调试一下"，或报"很慢"/"有时候会…"/"复现不了"。
- **铁律**：先建立**反馈回路**（能一句话复现的脚本或命令），再谈猜测；每次只改一个变量。
- **本项目实例**：构建慢的诊断（`--clp:PerformanceSummary` + 计时，结论见 `PROGRESS.md` 2026-11-02 那节）；`MSB3027 文件被锁定` 是靠 `Get-CimInstance Win32_Process` 找到孤儿进程；QML/中文路径问题靠四路对照实验定位。
- **反面教材（写进 PROGRESS 的）**：`AcceptLoopAsync` 里任务被丢弃导致异常完全静默，症状与根因毫不相干 → 后来所有"静默失败"都先怀疑"异常被吞了"。

### 3. `research` — 查事实，不靠记忆

- **触发**：要写平台 API、第三方库行为、格式规范。
- **铁律**：**以本机安装的 SDK / 包为准**，不看博客。引用时写清文件路径与行号。
- **本项目实例**：Media Foundation 的 5 个 GUID 与接口虚表顺序全部从 `E:\Windows Kits\10\Include\10.0.26100.0\um\mfidl.h` 等头文件核对（记忆里的 `IMFSample` GUID 是错的）；FluentAvalonia 有没有某个属性，查 `C:/Users/17121/.nuget/packages/fluentavaloniaui/3.1.0/lib/net10.0/FluentAvalonia.xml` 的 XML 注释。
- **产出**：结论写进 `PROGRESS.md` 的踩坑表或 `ARCHITECTURE.md`，不要只留在对话里。

### 4. `prototype` — 一次性实验，不进主干

- **触发**：拿不准的行为（某个 API 到底会不会接受这个参数、某个渲染后端到底加载了什么）。
- **约定**：脚本放 `tools/probe-*.ps1`，跑完**删除临时产物**（`output/`、`_probe-*`），只保留结论与可复用的探针本身。
- **本项目实例**：`tools/probe-render-backend.ps1`（四种后端到底加载了哪些 DLL）、`tools/probe-memory.ps1`（工作集 / 私有字节 / 私有工作集）。

### 5. `domain-modeling` — 术语与领域模型

- **触发**：出现"同一个东西三种叫法"，或要写 ADR。
- **本项目约定**：术语以 `ARCHITECTURE.md` 与代码里的类型名为准（`ScheduleItem` / `TodoItem` / `FocusSession` / `StudyRoom` / `wall_time` 层面的"时间块"）。
- **缺口**：还没有 `GLOSSARY.md` 与 `docs/adr/`（见第 8 节）。在补上之前，术语争议写进 `ARCHITECTURE.md` 的对应小节。

### 6. `codebase-design` — 深模块

- **触发**：准备拆文件 / 抽接口 / 判断"这坨逻辑该放哪一层"。
- **铁律（本项目已定型）**：
  - `HeliumTodo.Core` **不许引用 Avalonia、EF Core**（换了界面框架、换了数据库，它一行不用改）。
  - 纯逻辑（可测的算术、规则）优先放进 `Core`，界面只做装配。实例：`TimelineMetrics`（时间轴左侧刻度宽 46 + 间隙 8）、`PlaybackLimits`（30 帧/秒）、`RestartPolicy`（调试器里不许自己重启）都属于"抽出来才好测"。
  - 界面层的重活抽到 `HeliumTodo.App/Windowing/`、`Services/`，`MainWindow.axaml.cs` 只做装配。

### 7. `code-review` — 收尾审查

- **触发**：每轮交付前。
- **fixed point**：自 2026-11-02 起本仓库已进 Git，直接用它原生形式 —— `git diff main...HEAD`（`dev` 分支上干活，`main` 是发布线）或 `git diff <上一轮的提交>`。
- **另加一道尺子**：`tools/check-file-integrity.ps1` 的指纹差异仍然要跑 —— 它覆盖所有源码与文档，能抓到 Git 看不见的临时物残留。
- **两个轴自查**：**规范**（是否符合本手册第 5–6 节与 `ARCHITECTURE.md` 的约定）、**需求**（是否逐条对上用户原话）。

### 8. `writing-for-agents` — 写给人也写给 Agent 的文档

- **触发**：写 `AGENTS.md`、写交接文档、改 `PROGRESS.md` 的结构。
- **约定**：短句、命令可复制、路径写全、结论先行；不要写"我尝试了…"的过程（过程进 PROGRESS，结论进 ARCHITECTURE）。

---

## 降级使用的技能

这些技能**思路照用**，但它们的载体在本项目里不存在（没有 issue tracker、没有 Git 远端），或者必须有人在旁边。

| 技能 | 为什么降级 | 本项目替代做法 |
| --- | --- | --- |
| `to-spec` | 它把 spec 发布到 issue tracker | 写进对话里的一段「验收标准」+ `PROGRESS.md` 的小节标题 |
| `to-tickets` | 同上，且本项目是单人单线开发 | 用 `todo_write` 的任务清单 |
| `triage` / `wayfinder` | 需要 issue tracker 的状态机 | 用 `PROGRESS.md` 的「待办 / 已知缺口」两节 |
| `grill-me` / `grilling` / `loop-me` | 是对**人**的连续追问 | 用户明确说"grill 我"时才用 |
| `retro` | 面向团队复盘 | 每轮结束时我在总结里写"这次的教训"一条 |
| `handoff` / `claude-handoff` | 面向另一个 Agent 接力 | 本项目用 `PROGRESS.md` + 本手册 + `AGENTS.md` 三件套交接 |
| `teach` | 教学场景 | 用户问"为什么"时口头解释即可 |
| `implement` / `implement-spec` | 依赖 spec/ticket 体系 | 等价于本手册的前七步 |
| `ask-matt` | 技能路由器 | 用本手册第 2–4 节代替 |

---

## 明确排除的技能

**Agent 无法使用，或与本项目技术栈无关**。列出来是为了以后不用重新讨论一遍。

| 技能 | 排除理由 |
| --- | --- |
| `migrate-to-shoehorn` | TypeScript 专用（把 `as` 换成 shoehorn）。本项目是 C#。 |
| `setup-ts-deep-modules` | TypeScript + dependency-cruiser。 |
| `setup-pre-commit` | Husky / lint-staged / Prettier，都是 JS 生态；.NET 侧的等价物是"构建 + SelfTest + 指纹基线"，已在第 5 节手工执行。 |
| `git-guardrails-claude-code` | 需要 Claude Code 的 hooks 机制与 Git 仓库，两者都没有。 |
| `pr` | 没有远端仓库。 |
| `scaffold-exercises` | 教学题库脚手架，与产品无关。 |
| `design-taste-frontend` | 面向 Web 落地页（真实浏览器 + 截图循环）。本项目是 Avalonia 桌面。 |
| `impeccable` | 同上，Web / 移动端界面设计；Avalonia 的 Fluent 2 约束由 `UI_REDESIGN_BRIEF.md` 承载。 |
| `wizard` | 生成"只给人做"的 bash 置备向导；本项目没有这类一次性置备环节（依赖用 NuGet 还原，Qt 那套在 `cpp/`，不在本仓库）。 |
| `chief-of-staff` | 多子 Agent 长期编排；本项目是单线开发，引入它会破坏"一个改动一个上下文"的简单性。 |
| `software-factory` | 设计"软件工厂"（任务 + 服务接入）的概念层工具，与单体桌面应用无关。 |
| `writing-beats` / `writing-shape` / `writing-fragments` | 文章写作法，与工程文档无关。 |
| `to-questionnaire` | 把决策外包给别人填表；本项目决策都由用户当场定。 |
| `wait-what` | "上一条没讲清，重讲一遍"的口语化纠错流程，与开发流程无关。 |
| `office` / `office-*` 系列 | Office 文档处理；本项目交付物只有 .md 与 .png（将来若要出 Word/Excel 再启用）。 |
| `diagnose-windows-sandbox-acl` | 只用于 DSH 沙箱 ACL 异常排障，与产品代码无关（本会话文件策略是 danger-full-access，基本用不到）。 |

---

## 质量门禁

每轮交付前**全部**必须绿。任何一项红了就不算做完，不许"先交再修"。

```powershell
# ① 编译：0 警告 0 错误（当前基线）
dotnet build HeliumTodo.slnx -c Debug

# ② 单元测试：168 个用例，约 2.8 秒
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll

# ③ 界面渲染：6 个页面 + 外壳，输出到 output/render/
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --shell

# ④ 十二项行为自检（每项都要 exit=0 且 fails=0）
$rc = "HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll"
foreach ($c in "--shell-check","--refresh-check","--room-check","--backup-check",
               "--folder-check","--timeline-check","--reminder-check","--window-check",
               "--restart-prompt-check","--notice-check","--appearance-check","--layout-check") {
  dotnet $rc $c
}

# ⑤ 源文件完整性：166 个文件的 SHA256 指纹
pwsh tools\check-file-integrity.ps1
```

| 自检 | 管什么 |
| --- | --- |
| `--shell` | 七个页面能不能无头渲染出来（编译过 ≠ 画得对） |
| `--shell-check` | 外壳 + 导航切换 |
| `--refresh-check` | 三大功能互相联动（改待办 → 时间轴/概览跟着变） |
| `--room-check` | 自习室进出与成员同步 |
| `--backup-check` | .zip 备份导出 / 导入（`Replace` 与 `Merge`） |
| `--folder-check` | 「打开文件夹」多级降级启动 |
| `--timeline-check` | 时间轴：24 小时布局、冲突红框、横向缩进 |
| `--reminder-check` | 提醒收集（窗口、去重、重复日程） |
| `--window-check` | 窗口装饰、圆角、最大化动画的纯算术 |
| `--restart-prompt-check` | 改渲染后端 → 确认弹窗 → 保存/取消/调试器分支（17 项） |
| `--notice-check` | 提示条占一行、不压内容（5 项） |
| `--appearance-check` | 明暗模式、卡片玻璃浓度、壁纸灰蒙版（14 项） |
| `--layout-check` | 六页分区一致：左 12 / 顶 64 / 卡片间无漏底色 / 标题 24px（6 页 × 6 项） |

**视觉验证一律用像素统计**：本项目的验证 Agent 不能看图，所以"看着挺好"不算证据。套路是——渲染 PNG → 用带 Pillow 的 python（`C:/Users/17121/.dsh/dsh-runtimes/dsh-primary-runtime/dependencies/python/python.exe`，系统 `python` 没有 PIL）取关键坐标的像素值，与期望颜色比对。历史上靠这个抓出过"卡片直角没生效""提示条压住内容""设置页十张小卡"三个视觉 bug。

---

## 写作与文档规范

- **编码**：所有 `.md` 是**无 BOM 的 UTF-8**；所有含中文的 `.ps1` 是**带 BOM 的 UTF-8**（否则 PowerShell 报"字符串缺少终止符"）。
- **改完 XAML 文案后**：跑 `python tools/gen_ui_text_map.py` 重生成 `UI_TEXT_MAP.md`（界面文字 → 代码行号）。
- **改完功能后**同步这四处：
  1. `PROGRESS.md` —— 追加一节（现象 / 根因 / 修法 / 验证数字 / 教训）；
  2. `ARCHITECTURE.md` —— 约定进对应小节（§19 是界面细节约定，§18 是运行时画像）；
  3. `TECH_STACK.md` —— 依赖或质量基线变了才改（测试数、自检数、内存三数字）；
  4. `tools/check-file-integrity.ps1 -Update` —— 指纹基线随文件变化更新，然后**不带参数再跑一次**确认一致。
- **版本号唯一源头**：`Directory.Build.props`（`Version` / `AssemblyVersion` / `FileVersion`）。改版本时**不要动 `PROGRESS.md` 的历史小节**——那里的旧版本号是当时的事实。
- **禁止**：临时脚本（`tools/_*.py`）、`output/`、`_probe-*` 提交/遗留在仓库里；结束前删掉。

---

## 交接清单

接手第一天，按这个顺序读、按这个顺序跑。

**读（约 40 分钟）**
1. 本文件（流程）→ 2. [`AGENTS.md`](AGENTS.md)（硬规则）→ 3. [`PROJECT_OVERVIEW.md`](PROJECT_OVERVIEW.md)（文件树与依赖）→ 4. [`ARCHITECTURE.md`](ARCHITECTURE.md) 的 §19（界面约定，最容易踩坑的地方）→ 5. [`PROGRESS.md`](PROGRESS.md) 最后三节（最近在做什么）。

**跑（约 3 分钟）**
```powershell
dotnet build HeliumTodo.slnx -c Debug                                  # 应为 0 警告 0 错误
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll   # 应为 186/186
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --shell
pwsh tools\check-file-integrity.ps1                                    # 应为 158 文件一致
```
四步全绿 = 环境没问题，可以开始改东西。

**第一次改代码时注意**
- 用 `HeliumTodo.slnx`（不是 `.sln`），SDK 必须 10.0.401+（见 `global.json`）。
- 只改界面：改 `HeliumTodo.App/Views/*.axaml` + 对应 ViewModel；**不要顺手改 `Core`**。
- 只改算法：改 `Core` → 先在 `HeliumTodo.Tests` 里加红测试。
- 任何"看起来是样式问题"的界面 bug，先怀疑**主题字典优先级**与**控件 FINAL 属性**（`ARCHITECTURE.md` §19.6 / §19.4）。

---

## 已知缺口与建议

按投入产出排：

1. **版本管理已就位**（2026-11-02）。`main` = 发布线、`dev` = 开发线，标签打版本（当前 `v0.3.5-alpha`）。`.gitignore` 挡掉了 `bin/`、`obj/`、`dist/`、`Data/`、`output/`、`_probe-*`、`.vs/`、`.idea/`；`.gitattributes` 统一按 LF 存。约定：每轮一个提交，消息写"做了什么 + 为什么"；交付前 `git status` 必须是干净的（临时物不许混进来）。仓库外的整包备份放 `D:\代码\dsh project\HeliumTodo-backups\`（`git archive` 导出的源码 zip）。
2. **`GLOSSARY.md`**（`domain-modeling` 的标准产物）。目前术语散在 `ARCHITECTURE.md` 里；建议把"日程 / 待办 / 专注 / 时间块 / 冲突 / 车道(lane)"等词各写一句定义。
3. **`docs/adr/`**：重大决策（软件渲染为默认、Media Foundation 而非 ffmpeg、`Core` 不许引用 Avalonia）值得各留一份 ADR。现在它们只写在 `PROGRESS.md` 的叙述里。
4. **构建提速**（已诊断、待实施）：验证回路用 `--no-build`、把 12 次启动合并成单进程 `--all-checks`。实测 12 项自检串行 `--no-build` 约 57.5 秒，带构建检查 70 秒以上；单进程可省掉重复的 Avalonia 初始化。
5. **C++ 移植工程**（`cpp/`）不在本工作区。本手册只覆盖 C# 版本；`PORTING_TO_CPP.md` 与 `cpp/PORTING_PROGRESS.md` 是那边的独立流程。

---

*本手册随项目演进更新；与代码冲突时以代码和 `ARCHITECTURE.md` 为准，并顺手修正本文件。*