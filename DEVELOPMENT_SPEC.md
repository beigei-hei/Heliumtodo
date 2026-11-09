# Helium Todo 开发规格 · 0.5.0-alpha（技术栈重写版）

> **文档版本**：0.5.0-alpha（本文档定义的目标版本）
> **当前基线**：0.3.5-alpha（仓库里跑着的这一版，代码未按本文档改动）
> **状态**：**只写规格，不进入实施**。任何一行代码都不因本文档而修改。
> **适用读者**：接手这个项目的开发者，以及被要求"按文档干活"的 AI 代理。
> **与其它文档的关系**：本文档**取代** `TECH_STACK.md` 与 `ARCHITECTURE.md` 中
> "技术栈 / 分层 / 编码规范 / 生命周期"四类内容的效力；`ARCHITECTURE.md` 的
> 历史决策记录、`PROGRESS.md` 的历史小节、`UI_TEXT_MAP.md` 的界面文字
> 三份仍然有效，且**不许重写**（历史数字就是当时的事实）。

---

## 0. 一页纸摘要

| 维度 | 0.3.5-alpha（现状） | 0.5.0-alpha（本规格） |
| --- | --- | --- |
| 运行时 | .NET 10（`net10.0`） | .NET 10（`net10.0`），C# 14 |
| UI 框架 | Avalonia 12.1.2 | **Avalonia 12.1.5** |
| UI 主题库 | FluentAvaloniaUI 3.1.0（FA 前缀控件 147 处） | **Material.Avalonia 3.21.0**（移除 FluentAvaloniaUI） |
| 数据存储 | EF Core 8.0.11 + SQLite 单文件 | **EF Core 10.0.14 + Npgsql 10.0.3 + PostgreSQL 17** |
| 建库方式 | `EnsureCreated()` + 手工 `ALTER TABLE` | **EF Core Migrations**（`Database.Migrate()`，禁止 `EnsureCreated`） |
| 交互逻辑 | code-behind 事件（17 个 `On*`，10 处 XAML 事件绑定） | **Avalonia.Xaml.Behaviors**（附实测状态与降级预案，见 §8） |
| MVVM | CommunityToolkit.Mvvm 8.4.0，但 ViewModel 里仍有服务定位器 | 同库，**强制构造注入**，ViewModel 不许碰 Avalonia 控件类型 |
| 插件接口 | `IHeliumPlugin` + `PluginHost` 已接线 7 处 | **暂时禁用**（保留源码存档，不注册、不加载，见 §9） |
| 外壳风格 | 无边框 + 自绘标题栏 + 8 抓手 + 图标轨 56↔188 | **完全保留**（数值清单见 §6） |
| 版本号 | 0.3.5-alpha | **0.5.0-alpha** |
| 许可 | 未声明 | **GPL-3.0-only**（SPDX 头 + LICENSE + THIRD-PARTY-NOTICES，见 §13） |

**两条不可协商的优先级**（用户明确要求）：

1. **程序运行生命周期的完整性**优先于任何视觉/架构上的漂亮。（§10）
2. **功能实现的完整性**优先于重构速度：0.3.5-alpha 能做的事，0.5.0-alpha 一件都不许少。（§11）

---

## 1. 版本与里程碑

### 1.1 版本号

- 唯一来源：`Directory.Build.props` 的 `<Version>` / `<AssemblyVersion>` / `<FileVersion>`。
- 目标值：`0.5.0-alpha` / `0.5.0.0` / `0.5.0.0`。
- **实施时**（不是现在）需要同步的文件，一个都不能漏：

| 位置 | 内容 |
| --- | --- |
| `Directory.Build.props:22-24` | `Version` / `AssemblyVersion` / `FileVersion` |
| `HeliumTodo.Core/VersionInfo.cs` | 常量与 XML 注释里的示例版本 |
| `HeliumTodo.App/ViewModels/SettingsViewModel.cs` | 「关于」卡里的版本文字 |
| `HeliumTodo.App/ViewModels/ShellViewModel.cs` | 标题栏版本文字 |
| `HeliumTodo.Core/Backup/BackupManifest.cs` | 备份清单里的 `AppVersion` 默认值 |
| `HeliumTodo.App/Windowing/WindowChrome.cs` | 类注释里的版本示例 |
| `HeliumTodo.Tests/VersionInfoTests.cs` | 断言里的期望版本 |
| 文档头 | `ARCHITECTURE.md:17/:1093`、`TECH_STACK.md:3`、`PROJECT_OVERVIEW.md:5`、`DEVELOPMENT_PLAYBOOK.md:8`、`PROGRESS.md:6` |

> **禁令**：`PROGRESS.md` 里"0.3.3-alpha 那一节"之类的**历史小节**一律不动。
> 改版本号时把历史数字一起改掉，文档就从"记录"变成"谎言"。

### 1.2 里程碑（只列计划，不实施）

| 里程碑 | 内容 | 出口条件 |
| --- | --- | --- |
| M0 | 文档落地（本文档 + 索引） | 本文档通过评审；不改代码 |
| P0 | 工程基建：改版本号、加 LICENSE/SPDX、去 FluentAvalonia、目录重排、门禁脚本路径同步 | 构建 0 警告 0 错误；15 项自检全绿 |
| P1 | Domain + Application 重写（含 190 个测试迁移） | `SelfTest` ≥190 全绿 |
| P2 | Infrastructure 换 PostgreSQL + Migrations + 备份格式 v2 | `--db-check` 全绿；备份往返一致 |
| P3 | Material 主题骨架（明暗 / 强调色 / 卡片令牌） | `--theme-check` 全绿；截图归档 |
| P4 | 控件替换（逐页，FA → Material） | 七页视觉验收 + `--layout-check` 全绿 |
| P5 | Behaviors 落地（外壳回归） | `--behavior-check` 全绿；DWM 动画不回退 |
| P6 | 验证工具升级（性能预算、真机探针） | `--perf-check` / `--anim-check` 全绿 |
| P7 | 打包与文档收尾 | 可分发 zip；`git status` 干净 |

---

## 2. 固定的技术栈

### 2.1 依赖清单（写死，不许自行升降）

| 分类 | 包 | 版本 | 为什么是这个版本 |
| --- | --- | --- | --- |
| 运行时 | `.NET SDK` | 10.0.x | `global.json` 已钉 10.0.401；FluentAvalonia 3 曾强制 `net10.0`，去掉后仍保持（新代码用好 C# 14） |
| 语言 | `C#` | 14（`LangVersion` 跟随 SDK） | `field` 关键字、集合表达式等能显著减少样板 |
| UI | `Avalonia` 系列 | **12.1.5** | 去掉 FluentAvalonia 后不再被 `DataGrid 12.1.2` 卡住（详见坑 D3）；12.1.5 是当前最新稳定 |
| 主题 | `Material.Avalonia` | **3.21.0** | 依赖 `Avalonia 12.1.1`（`>=` 语义），12.1.5 可用；3.21.0 是该库唯一支持 Avalonia 12 的稳定线 |
| 主题（对话框） | `Material.Avalonia.Dialogs` | 3.21.0 | 有对话框需求时用库内实现，不自己写 Window |
| 图标 | `Material.Icons.Avalonia` | 3.0.2 | 「控件优先」：图标也优先用库内资源；许可证 Apache-2.0 |
| MVVM | `CommunityToolkit.Mvvm` | 8.4.0 | 源生成器省样板；不要换成 Prism/ReactiveUI（学习成本与收益不成比例） |
| 数据 | `Microsoft.EntityFrameworkCore` | **10.0.14** | 与 .NET 10 对齐的 LTS 线 |
| 数据（PG） | `Npgsql.EntityFrameworkCore.PostgreSQL` | **10.0.3** | 与 EF Core 10 对齐；8.x 只配 EF 8 |
| 数据（设计期） | `Microsoft.EntityFrameworkCore.Design` | 10.0.14 | `dotnet ef migrations` 必需，仅 `PrivateAssets=all` |
| 数据（驱动） | `Npgsql` | 10.0.3（传递） | 不直接引用，除非要用 `COPY` 批量导入 |
| 位图 | `SkiaSharp` | 3.119.4 | **必须**与 Avalonia 传递依赖同版（坑 D4） |
| 交互 | `Avalonia.Xaml.Behaviors` | **见 §8** | 当前 NuGet 无 12.x；有两条路（源码构建 / 适配层），P0 必须先验证 |
| DI / 日志 | `Microsoft.Extensions.DependencyInjection` / `.Logging` | 10.0.x | 跟随 .NET 10 |
| 测试 | `xunit` 2.9.2 + `Microsoft.NET.Test.Sdk` | 17.11.1+ | 沿用；`dotnet test` 在本机沙箱跑不起来，靠 `SelfTest` 反射执行 |

### 2.2 明确禁止（栈层面）

- ❌ 不引入第二套 UI 主题库（FluentAvalonia / Semi.Avalonia / Ursa 一律不加）。
- ❌ 不引入 ORM 之外的 SQL 层（Dapper / 手写 SqlCommand）——**除**PG 批量导入这一处受控例外。
- ❌ 不引入 Redis / 消息队列 / 微服务：这是单机桌面应用，PG 只是存储。
- ❌ 不引入 WinUI / WPF / WinForms 混编（托盘、通知、壁纸全部走现有 P/Invoke 或 Media Foundation）。
- ❌ 不引入 `unsafe`（项目 `AllowUnsafeBlocks=false`，探针脚本里的 `/unsafe` 只影响脚本自己的 `Add-Type`）。
- ❌ 不用 `dotnet test` 作为门禁（沙箱里 testhost 起不来，见坑 H1）。

### 2.3 `Directory.Packages.props` 的改动（P0 一次做完）

| 动作 | 包 | 新版本 |
| --- | --- | --- |
| 删除 | `FluentAvaloniaUI` | — |
| 删除 | `Microsoft.EntityFrameworkCore.Sqlite` | — |
| 升级 | `Avalonia` / `.Desktop` / `.Themes.Fluent` / `.Fonts.Inter` / `.HarfBuzz` / `.Headless` | 12.1.5 |
| 降级/删除 | `Avalonia.Controls.DataGrid`、`.ColorPicker` | 若不用就删；要用则取 12.1.5 存在的最高版 |
| 新增 | `Material.Avalonia` | 3.21.0 |
| 新增（可选） | `Material.Avalonia.Dialogs`、`Material.Icons.Avalonia` | 3.21.0 / 3.0.2 |
| 新增 | `Microsoft.EntityFrameworkCore` / `.Design` | 10.0.14 |
| 新增 | `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 |
| 新增 | `Avalonia.Xaml.Behaviors` | 见 §8（本地包 12.1.5-* 或适配层） |
| 保持 | `CommunityToolkit.Mvvm`、`SkiaSharp`、xunit、`Microsoft.NET.Test.Sdk` | 8.4.0 / 3.119.4 / 2.9.2 / 17.11.1 |
| 保持 | `CentralPackageTransitivePinningEnabled` | **必须 `true`**（坑 G1） |

同时把 `Directory.Build.props` 的 `<LangVersion>` 从 `12.0` 提到 `14`（.NET 10 的默认语言版本），
`<AnalysisLevel>` 设 `latest-recommended`，`TreatWarningsAsErrors` 按 §14.3 分阶段开启。

---

## 3. 分层架构与命名空间

### 3.1 决策：项目拆七个（一次机械改名）

| 新项目 | 层 | 只允许依赖 | 对应旧项目 |
| --- | --- | --- | --- |
| `HeliumTodo.Domain` | 领域 | 无（纯 BCL） | `HeliumTodo.Core` 的 Models / Services / Theming 规则 / Windowing 规则 |
| `HeliumTodo.Application` | 应用 | Domain | `HeliumTodo.Core` 的 Configuration、Backup 契约、Notifications 契约、Ipc 契约 + 原 App 里可测的编排逻辑 |
| `HeliumTodo.Infrastructure` | 基础设施 | Domain、Application | `HeliumTodo.Data`（EF Core + Npgsql + 备份实现） |
| `HeliumTodo.Desktop` | 表现 | Domain、Application、Infrastructure（仅 DI 组合根） | `HeliumTodo.App` |
| `HeliumTodo.Tests` | 测试 | 前三个 | `HeliumTodo.Tests` |
| `HeliumTodo.SelfTest` | 测试运行器 | Tests | `HeliumTodo.SelfTest` |
| `HeliumTodo.Verification` | 验证（无头渲染/截图/性能） | Desktop + 前三 | `HeliumTodo.RenderCheck` |

**依赖方向铁律**：`Desktop → Application → Domain`，`Infrastructure → Application → Domain`。
`Domain` 不许引用 Avalonia、EF Core、Npgsql、System.Windows.Forms。
`Application` 不许引用 Avalonia 控件（`Avalonia.Base` 的类型也不许，除了 `Thickness` 这种纯值类型被明令豁免）。

> **为什么不用"Core/Data/App"继续凑**：`Core` 现在同时装着领域模型、应用编排、
> 甚至 `Ipc`/`Networking` 契约，边界已经糊了；`RenderCheck` 这名字也没说明它到底验什么。
> 改名是一次性的机械成本（见 §3.3），换来的是一条能被机器检查的依赖线。

### 3.2 目录契约（每层内部固定）

```
HeliumTodo.Domain/
├── Entities/            实体（原 Models：ScheduleItem / TodoItem / FocusSession / StudyRoom …）
├── ValueObjects/        wall_time、TimeRange、LaneLayout、PromptPreset …
├── Enums/
├── Services/            领域服务（RecurrenceExpander / DayTimelineBuilder / Conflict / FocusTimer）
├── Policies/            纯规则（WindowTransitionPolicy / RailMetrics / PlaybackLimits / NoticePolicy）
└── Abstractions/        领域内接口（仓储接口 I*Repository、时钟 IClock）

HeliumTodo.Application/
├── UseCases/            每文件一个用例（CreateTodo / ExpandDay / ExportBackup / ImportBackup …）
├── Contracts/           DTO、请求/响应、结果类型（Result<T> 或 *Result 记录）
├── Abstractions/        平台接口（IFolderLauncher / INotificationService / IWallpaperSource / IIpcChannel）
└── Configuration/       AppConfig、ConfigService、迁移器

HeliumTodo.Infrastructure/
├── Persistence/         HeliumDbContext、实体配置 IEntityTypeConfiguration<T>、迁移 Migrations/
├── Repositories/        仓储实现
├── Backup/              备份 v2（JSONL + manifest）
├── Notifications/       托盘气泡 / Windows 通知实现
└── Platform/            文件、单实例、Media Foundation、P/Invoke

HeliumTodo.Desktop/
├── Behaviors/           行为（拖动窗口、双击最大化、回车发送、抓手改大小、滚动联动）
├── Views/               每页一个 View（.axaml + 极小 code-behind）
├── ViewModels/          每页一个 ViewModel
├── Controls/            仅放"库内没有"的自绘控件（登记在 CUSTOM_CONTROLS.md）
├── Themes/              Material 主题装配 + 令牌（DesignTokens）
├── Styles/              控件样式
├── Services/            视图服务（对话框、导航、主题应用、壁纸、渲染后端）
└── Composition/         DI 组合根（App.axaml.cs 里的注册全部搬到这里）
```

### 3.3 改名涉及的机械改动（P0 一次做完，不许拖到后面）

| 位置 | 数量 | 改法 |
| --- | --- | --- |
| `tools/probe-*.ps1` 里的 `..\HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe` | 16 个脚本 | 替换成 `..\HeliumTodo.Desktop\bin\Debug\net10.0\HeliumTodo.Desktop.exe` |
| `HeliumTodo.RenderCheck` 的调用命令 | 6 份文档 + `AGENTS.md` + `DEVELOPMENT_PLAYBOOK.md` | 改成 `HeliumTodo.Verification` |
| `tools/file-hashes.json` | 177 条 | 改名后 `-Update` 重建（不要手改） |
| `HeliumTodo.slnx` | 1 处 | 项目列表替换 |
| `UI_TEXT_MAP.md` 的生成脚本 `tools/gen_ui_text_map.py` | 路径常量 | 改成 `HeliumTodo.Desktop/Views` |

> 除上表之外，**任何**"顺手把某段代码挪个位置"的改动都不许混进 P0；P0 必须是一次可 `git revert` 的纯改名。

---

## 4. MVVM 规范

### 4.1 硬规则

1. ViewModel 里**不许**出现 `Avalonia.Controls.*`（`Window`/`Button`/`TextBlock` 一律不许）。
   需要弹窗 → 注入 `IDialogService`；需要打开文件夹 → `IFolderLauncher`；需要通知 → `INotificationService`。
2. 服务定位器退出历史舞台：`services.GetService(typeof(PluginHost))` 这种写法在 0.5.0 里**禁止**。
   ViewModel 一律构造函数注入（`CommunityToolkit.Mvvm` 的 `ObservableObject` 不动）。
   > 当前 7 个 ViewModel 里有 5 个在构造里 `(PluginHost)services.GetService(...)!`（注意那个 `!`），
   > 插件禁用后这些行直接删除，**不要**改成 `IServiceProvider` 继续用。
3. `async void` 只允许出现在**事件处理器**里，且必须整段 `try/catch` + 写日志。
   `[RelayCommand]` 生成的 `IAsyncRelayCommand` 天然安全，优先用它。
4. 命令并发：默认 `AllowConcurrentExecutions = false`；确需并发时显式注明理由。
5. `[ObservableProperty]` 字段名 → `Foo`；`partial void OnFooChanged(...)` 只允许**一个**实现（多个实现声明 = `CS0757`）。
6. 派生属性用 `[NotifyPropertyChangedFor(nameof(Bar))]`，不许在 setter 里手写 `OnPropertyChanged` 链。
7. 集合用 `ObservableCollection<T>` / `ReadOnlyObservableCollection<T>`；大批量刷新用 `SuspendNotifications`（Avalonia 12 提供）或整表替换，不许循环 `Add`。
8. 每个 View 的 `DataContext` 类型用 `x:DataType` 写死（编译绑定），禁止裸绑定。
9. 设计时数据：`d:DataContext="{x:Static vm:DesignData.TodoPage}"`，不许 `if (Design.IsDesignMode) new ServiceProvider()`。
10. 导航：`INavigationService`（`ShellViewModel` 持有当前页），View 不进 ViewModel。

### 4.2 反面示例（现状里真实存在的写法，0.5.0 起禁止）

```csharp
// ❌ 服务定位器 + 空引用断言
_pluginHost = (PluginHost)services.GetService(typeof(PluginHost))!;

// ❌ async void 无 try/catch（曾经直接把进程带走）
private async void OnFocusCompleted(object? sender, EventArgs e) { ... }

// ❌ ViewModel 里直接开窗口
var dialog = new ConfirmWindow();
await dialog.ShowDialog(owner);
```

---

## 5. 控件优先策略（"能不画就不画"）

### 5.1 优先级

1. **Material.Avalonia 自带控件**（`Card`、`Expander`、`ColorZone`、`SnackbarHost`、`SideSheet`、`FloatingButton`、`NavigationDrawer`、`Badge`、`Clock`、`CircleClockPicker`、`IconButton` …）。
2. **Avalonia 官方控件**（`ColorPicker`/`ColorView`、`AutoCompleteBox`、`DataGrid`、`TreeView`、`TabControl` …）。
3. **组合现有控件**（用 `Grid` + 样式拼，不加新控件类）。
4. **自绘控件**——仅当 1–3 都做不到。必须同时满足：
   - 在 `CUSTOM_CONTROLS.md` 登记一行：类名 / 为什么库内没有 / 证据（链接或截图）/ 替代方案被否决的理由。
   - 继承 `Control`（或 `TemplatedControl`），逻辑走属性 + 渲染，**不许**在 `Render` 里读 `DataContext`。
   - 有对应的 `--*-check` 断言（颜色/尺寸/位置至少一项可量化）。

### 5.2 现有自绘/FA 控件的去向

| 现有 | 0.5.0 去向 | 说明 |
| --- | --- | --- |
| `FASettingsExpander` + `FASettingsExpanderItem`（40 + 107 处） | **Material `Expander` + 自建行模板** | FA 模板写死 `#B3FFFFFF` 两层底色（坑 B1），Material `Expander` 无此问题；行模板用 `Grid` + 现有 `Border` 令牌 |
| `FAInfoBar`（2 处） | **Material `Snackbar`**（或保留现有自绘 `NoticeBar`） | 提示条必须"占位不浮层 + 3 秒自动消失"（§6.6），`Snackbar` 自带超时 |
| `Border.card` + `CardShadow` + `RadiusCard=18` | **保留自研令牌**，只换背景/阴影来源 | Material 自带的圆角与阴影是"预设档位"（没有 18 px 这一档），我们的卡片必须显式覆盖；`Card` 自带的 `Elevation` 达不到原观感 |
| `ColorPickerControls.cs`（自绘调色盘） | **Avalonia `ColorPicker`/`ColorView`** | 自绘的 RGB↔HSV↔Hex 三方同步本来就该交给官方控件 |
| `TimelineGrid.cs`（24 小时网格） | **保留自绘**（登记） | 无库内等价物：24 条小时线 + 半时线 + 刻度的像素级对齐没有现成控件 |
| `ClockControls.cs`（专注表盘） | **Material `Clock` / `CircleClockPicker`** | 先试库内；若动效或尺寸不达标再自绘并登记 |
| `WidthObserverPanel.cs` | **保留**（内部工具） | 是为了拿可用宽度做时间轴布局，不是视觉控件 |
| `ReminderPicker` | **Material `ComboBox` + 数字框组合** | 现在也只是组合 |
| `Segoe Fluent Icons` 字形按钮（图标轨） | **先保留**；可选换 `Material.Icons.Avalonia` | 图标字体只在运行期引用系统字体，不打包（GPL 与再分发无关，见 §13） |

### 5.3 保留的"库内没有"清单（初版）

`TimelineGrid`、`WidthObserverPanel`、`IntroVeil`（一层遮罩 Border，不算控件）、
`ResizeGrips` 的 8 个边框 `Border`（Material 没有无边框窗口抓手）。

---

## 6. 保留的界面风格（不可回退清单）

> 用户明确要求："重构前的 UI 风格，无边框界面保持不变"，
> 并且"采用 material avalonia 时保留所有自大修 UI 布局以来的所有变动"。
> 下面每一条都是**已交付并验收过**的，0.5.0 不许"顺手改回旧样"。

### 6.1 外壳

| 项 | 值/做法 |
| --- | --- |
| 窗口装饰 | `WindowDecorations="None"`（窗口矩形 = 客户区） |
| 圆角 | `Win32Properties.WindowCornerPreference="Round"`，不画自绘描边（DWM 自己画 2px） |
| 抓手 | 8 个 `Border`（四边 6 px、四角 10 px），`Tag` 为 `North/South/West/East/NorthWest/…` |
| 标题栏 | 自绘，48 px；品牌方块 + 版本文字 + 最小化/最大化/关闭三个字形按钮 |
| 图标轨 | 收起 56 px / 展开 188 px，260 ms `CubicEaseOut`，`ClipToBounds` |
| 图标轨条目 | 收起态 44×44；展开态宽度跟随轨道（`Width="NaN"` + `Stretch`），名称用 `Opacity` 淡入，**不许**给每个按钮各挂一条宽度过渡 |
| 打磨 | `WS_CAPTION` 样式位必须在（DWM 才有最小化/最大化过渡）；`DwmSetWindowAttribute(DWMWA_TRANSITIONS_FORCEDISABLED, FALSE)` |

### 6.2 页面分区（六页完全一致）

| 项 | 值 |
| --- | --- |
| 页面根 | `<Grid RowDefinitions="Auto,*" Margin="12,4,12,12">` |
| 标题行 | `MinHeight="40" Margin="4,0,4,20"`，标题字号 `pageHeader=20`、说明 `pageCaption=14`、指标 `pageMetric=20` |
| 卡片 | 一页**只有一张** `Border Classes="card"`，圆角 `RadiusCard=18`，内容再套一层 `CornerRadius=18 ClipToBounds` 且 `Padding=8` |
| 卡片顶边 / 左边缘 | 64 / 12（由 `--layout-check` 断言） |
| 模块名位置 | 在卡片**外面**，与卡片左边缘差 4 px |

### 6.3 卡片与玻璃

| 项 | 值 |
| --- | --- |
| 卡片底色 | `CardSurfaceBrush`：亮 `#FFFFFFFF`、暗 `#1C1C1C`；alpha = `CardOpacity%×255/100` |
| 默认不透明度 | 100%；开壁纸自动 82% |
| 阴影 | `0 8 24 0 #14000000`（亮）/ `0 8 24 0 #40000000`（暗） |
| 资源位置 | **必须在 `ThemeDictionaries` 之外**（坑 B2） |
| 壁纸蒙版 | 一层，颜色随明暗；暗色强度 ×1.6，上限 0.95 |

### 6.4 壁纸

- `Image` 名为 `WallpaperImage`，静态图**先缩放到窗口像素**（`WallpaperScaler`，`UniformToFill` 居中裁切）再 `Stretch="Fill"`；
- 动图（GIF/APNG）与 MP4 贴原图，`Stretch="UniformToFill"`；
- 动图解码预算：长边 ≤1280、总帧字节 ≤96 MB；播放上限 30 fps；
- 窗口尺寸变化时 160 ms 防抖后重算缩放位图。

### 6.5 动效

| 场景 | 做法 |
| --- | --- |
| 打开窗口 | 一层 `IntroVeil` 遮罩改画刷 Alpha 淡出（180 ms，至少 5 帧，单帧步进 ≤0.2）；**不许**改 `Window.Opacity`，**不许**缩放内容 |
| 最小化 / 最大化 / 还原 | 交给 DWM（`WS_CAPTION`），自己不写动画 |
| 图标轨展开 | 全窗口只有 `NavRail.Width` 一条属性在动 |
| 窗口被最小化 | 立刻 `Settle()`（清 `RenderTransform`、遮罩收起、`Opacity=1`） |
| 交互降质 | 动画/拖拽期间：壁纸插值降 `LowQuality`、卡片阴影关掉；时长 ≥250 ms 才暂停动图壁纸 |
| 帧判据 | `P95 < 2×P50` 且 `最慢 < 4×P50`（`WindowTransitionPolicy`） |

### 6.6 提示条

- 位置：叠在功能区上方（`ZIndex` 高于卡片），不占布局；
- 颜色：跟随明暗；
- 时长：3 秒自动消失（`NoticePolicy.DisplayMilliseconds`），可手动关闭；
- 背景不透明（避免透出底下内容，坑 B3）。

### 6.7 设置页

十个分组，标题**逐字照抄**（见 §12）：
`外观`、`背景壁纸`、`专注`、`自控模块`、`每日工作流`、`自习室`、`插件`（禁用后改为只读说明）、`数据与文件`、`底层设置`、`关于`。

---

## 7. 数据层：PostgreSQL

### 7.1 连接与配置

`config.json`（可分离配置，沿用现有 `ConfigService`）：

```jsonc
{
  "Storage": {
    "Provider": "postgres",
    "Host": "localhost",
    "Port": 5432,
    "Database": "helium_todo",
    "Username": "helium",
    "Password": "",           // 生产环境建议用环境变量 HELIUM_DB_PASSWORD 覆盖
    "SslMode": "Prefer",
    "CommandTimeoutSeconds": 30
  }
}
```

- 环境变量覆盖顺序：`HELIUM_DB`（完整连接串）> `HELIUM_DB_PASSWORD` > `config.json`。
- 连接串**不许**写进日志（打日志前把 `Password=` 段替换成 `***`）。
- 连接池：`Maximum Pool Size=20`、`Minimum Pool Size=2`，`No Reset On Close=false`。

### 7.2 建库：用 Migrations，禁止 EnsureCreated

```csharp
// Infrastructure/Persistence/DatabaseBootstrapper.cs
await using var db = await factory.CreateDbContextAsync(ct);
await db.Database.MigrateAsync(ct);          // 唯一合法建库/升级方式
```

- 迁移文件由 `dotnet ef migrations add <Name> -p HeliumTodo.Infrastructure -s HeliumTodo.Desktop` 生成，进仓库。
- 启动时打印当前迁移版本；检测到"数据库版本 > 程序版本"时**拒绝启动**并提示（防降级写坏数据）。
- `EnsureColumns()` / `PRAGMA table_info` / 裸 `ALTER TABLE` 全部删除。

### 7.3 映射约定（逐条写进 `IEntityTypeConfiguration<T>`）

| C# 类型 | PG 类型 | 规则 |
| --- | --- | --- |
| `int`（主键） | `integer` + identity | `UseIdentityByDefaultColumn()`；导入后 `setval(pg_get_serial_sequence('"Schedules"','Id'), max("Id"))` |
| `string` | `text` | 明确 `MaxLength` 的给 `varchar(n)`；标题 200、正文 4000、代码 32 |
| `bool` | `boolean` | 不用 0/1 |
| 枚举 | `integer` | `HasConversion<int>()`（与旧库数值保持一致，见坑 F5） |
| `TimeSpan` | `interval` | 专注时长、提醒偏移 |
| **本地墙钟时间** `DateTime`（计划开始/结束、截止、提醒基准） | **`timestamp without time zone`** | 产品的语义就是"墙上几点"，**不许**存成 UTC |
| 审计时间（`CreatedAt`/`UpdatedAt`） | `timestamptz`（UTC） | 只在审计里用，不参与业务比较 |
| `DateTime?` | 同类型可空 | NULL 表示未设置 |
| 并发 | `xmin`（`UseXminAsConcurrencyToken()`） | P4 起启用；冲突时抛 `DbUpdateConcurrencyException`，界面提示"数据已被其它窗口修改" |

> **Npgsql 6+ 的时间坑**：默认把 `DateTime` 映射成 `timestamptz` 并按 UTC 换算，
> 直接迁移过去会让所有日程**整体平移 8 小时**。我们的做法是显式 `HasColumnType("timestamp without time zone")`，
> 并且**不要**用 `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` 这种全局旁路（它会污染审计时间）。

### 7.4 查询与索引

- 大小写不敏感搜索：`EF.Functions.ILike(x.Title, $"%{q}%")`（SQLite 的 `LIKE` 默认不区分大小写，PG 区分，坑 F6）。
- 必建索引：`Schedules(PlannedStart)`、`Schedules(Start,End)`、`Todos(DueAt)`、`Todos(IsCompleted, Priority)`、`FocusSessions(StartedAt)`、`StudyRoomMembers(StudyRoomId)`、`StudyRoomMessages(StudyRoomId, SentAt)`。
- 表名保持 PascalCase 并**始终加引号**（EF 默认行为）；禁止手写裸 SQL 拼表名（坑 F7）。

### 7.5 备份格式 v2（provider 无关）

```
helium-backup-YYYYMMDD-HHmmss.zip
├── manifest.json          { formatVersion: 2, appVersion, createdAtUtc, provider, tables: [...], sha256: {...} }
├── data/
│   ├── Schedules.jsonl        每行一个 JSON 对象（列名 → 值；时间用 ISO-8601 本地无偏移）
│   ├── Todos.jsonl
│   ├── FocusSessions.jsonl
│   ├── StudyRooms.jsonl
│   ├── StudyRoomMembers.jsonl
│   └── StudyRoomMessages.jsonl
└── assets/wallpaper.png（可选）
```

- 导出：`EF` 查询 → `System.Text.Json` 序列化；单表 > 50 000 行时改用 Npgsql `BeginBinaryImport`（`COPY`）。
- 导入：**一个事务**内按外键顺序插入；模式 `Replace`（TRUNCATE + INSERT）与 `Merge`（`ON CONFLICT (Id) DO UPDATE`）都要支持。
- 兼容旧包：`.zip` 里若是 `data/helium.db`（SQLite 单文件），走"仅升级期可用"的一次性导入通道，0.6 之后删除。
- 导入后必须修序列（见 §7.3），否则下一次插入撞主键。

### 7.6 测试策略

| 测试 | 依赖 | 门控 |
| --- | --- | --- |
| 仓储/迁移/备份往返 | 真实 PG | `HELIUM_TEST_POSTGRES` 未设置时 `SUCCEED("跳过：未提供 PostgreSQL")` |
| 领域/规则 | 无 | 永远跑 |
| 界面/渲染 | 无头 Avalonia | `--*-check` |

本地开发用 `infra/docker-compose.yml`（`postgres:17-alpine`，端口 5432，库 `helium_todo_test`）。

---

## 8. Behaviors：用 Xaml.Behaviors，但先验证

### 8.1 用户要求与实测事实

要求：**用 `Avalonia.Xaml.Behaviors`**（官方仓库已声明该分支可用于 Avalonia 12）。
实测（2026-11-09，本机）：

| 事实 | 证据 |
| --- | --- |
| NuGet 上**没有 12.x** | `Avalonia.Xaml.Behaviors` / `.Interactions` / `.Interactions.Custom` / `.Interactivity` 最新都是 **11.3.0.6**（nuget.org 与本机镜像一致） |
| 11.3.0.6 依赖 Avalonia **11.3.0** | nuspec `<dependency id="Avalonia" version="11.3.0" />` |
| 仓库 `master` 已经是 Avalonia 12 | 提交「Update Avalonia to 12.1.5 and bump package version」，`Directory.Build.props` 里 `<VersionPrefix>12.1.5</VersionPrefix>` |
| 11.3.0.6 在 Avalonia 12 上**运行期崩** | `System.TypeLoadException: Could not load type 'Avalonia.Controls.ResourcesChangedEventArgs' … due to value type mismatch`（在挂 `BehaviorCollection` 时抛） |
| 11.3.0.6 在 Avalonia 12 上**能编译** | XAML 编译通过，`OnAttached` 甚至执行了 |

**结论**：官方源码支持 12，但**包还没发**。所以"用 Xaml.Behaviors"是对的，前提是**自己把 12.1.5 的包打出来**。

### 8.2 方案（按优先级）

**方案 A（首选，P0 第一步执行）**：从 `master` 构建本地包

```powershell
git clone --depth 1 https://github.com/wieslawsoltes/Xaml.Behaviors tools/_ext/Xaml.Behaviors
dotnet pack tools/_ext/Xaml.Behaviors/src/Avalonia.Xaml.Interactivity -c Release -o packages/local
dotnet pack tools/_ext/Xaml.Behaviors/src/Avalonia.Xaml.Behaviors -c Release -o packages/local
```

- `NuGet.config` 增加 `<add key="local" value="packages/local" />`；
- 版本引用 `12.1.5-*`（以实际打包结果为准），并**把 `packages/local/*.nupkg` 与 commit SHA 一起入库**
  （二进制入库有助于复现，体积约几十 KB）；
- 若 `master` 的包构建失败（缺包/签名问题），退回方案 B。

**方案 B（保底）**：API 兼容适配层

- `HeliumTodo.Desktop/Behaviors/` 下实现 `Interaction`（`AttachedProperty<BehaviorCollection> BehaviorsProperty`
  + `GetBehaviors/SetBehaviors`）、`Behavior<T>`、`BehaviorCollection`，签名与官方 1:1；
- XAML 里写法与官方完全一致（`xmlns:i="using:HeliumTodo.Desktop.Behaviors"` + `<i:Interaction.Behaviors>`）；
- 日后官方发 12.x：删三个文件、加包、把 `xmlns` 换回 `Avalonia.Xaml.Interactivity`。
- ⚠️ **已知风险**：我在 spike 里自建的同名 attached property 曾被 XAML 编译器拒绝
  （`AVLN2000: Unable to resolve suitable regular or attached property Behaviors on type …`），
  换 namespace / 换注册重载都没解决。所以方案 B **必须先做 30 分钟验证**：
  能解析就用 XAML 写法；不能解析就改成代码挂载
  （`Interaction.Attach(control, new DragWindowBehavior())` 放 View 的构造里），**方案不塌**。

**方案 C（不推荐）**：不加 Behaviors，继续 code-behind。与用户要求冲突，仅在 A、B 都失败时启用。

### 8.3 用 Behavior 取代的 code-behind（清单）

| 现在的位置 | 行为 | 迁移后 |
| --- | --- | --- |
| `ShellView.axaml.cs:OnTitleBarPointerPressed` | 按住标题栏拖窗口 | `DragWindowBehavior` |
| `ShellView.axaml.cs:OnTitleBarDoubleTapped` | 双击最大化/还原 | `ToggleMaximizeBehavior` |
| `ShellView.axaml.cs:OnMinimizeClick` / `OnMaximizeClick` / `OnCloseClick` | 窗口按钮 | `WindowStateBehavior(Minimize/Maximize/Close)` |
| `ShellView.axaml.cs:OnToggleRailClick` | 图标轨展开 + 降质 | `ToggleRailBehavior`（内部调 ViewModel 命令 + `BeginRailAnimation`） |
| `MainWindow.axaml.cs:OnResizeGripPressed`（8 处） | 抓手拖拽改大小 | `ResizeWindowBehavior`（`Tag` 决定方向） |
| `StudyRoomView.axaml.cs:OnMessageKeyDown` | 回车发送 | `EnterKeyCommandBehavior` |
| `TodoView.axaml.cs:OnCategoryPresetClick` | 分类预设 | 直接改成 `Command` 绑定（不需要 Behavior） |
| `DashboardView.axaml.cs:OnTimelineScrollChanged` | 滚动联动标签 | `ScrollSyncBehavior` |

### 8.4 门禁

新增 `--behavior-check`：
1. 每个 Behavior 在 `--shell` 渲染里都能挂上（`Interaction.GetBehaviors(control).Count > 0`）；
2. 拖动窗口 / 双击最大化 / 回车发送 / 抓手改大小各跑一次断言；
3. 断言 `OnDetaching` 后事件已解绑（否则内存泄漏）。

---

## 9. 插件接口：暂时禁用

### 9.1 为什么

用户明确：插件技术规范与"允许程序暴露哪些接口类型"的合理性都还不成熟。
现状风险：`PluginHost` 用 `Assembly.LoadFrom` 反射实例化外部 dll，等于把**任意代码执行**
接到主进程，而我们既没有权限模型、也没有版本协商、也没有崩溃隔离。

### 9.2 怎么做（不是删掉，是"关掉 + 留档案"）

1. `HeliumTodo.Application/Plugins/IHeliumPlugin.cs` 保留，加 `[Obsolete("0.5.0-alpha 起暂时禁用，见 DEVELOPMENT_SPEC.md §9")]`，
   **不参与编译期引用**（`#if HELIUM_PLUGINS`，默认不定义）。
2. `PluginHost` 整类删除；7 处 `_pluginHost` 字段与构造注入一并删除。
3. 设置页「插件」分组保留（用户要求文案照抄），内容改成只读说明：
   > 「插件接口在本版本中暂时关闭。我们正在重新设计插件规范（可暴露的接口类型、权限与版本兼容策略），
   > 完成后再开放。已有的插件目录不会被读取，也不会执行。」
4. 目录 `plugins/` 若存在，启动时**只记录不加载**（日志一行：`[插件] 检测到 plugins 目录，本版本不加载`）。
5. 恢复条件（写进文档，避免无限期黑箱）：
   - 暴露接口白名单（只读会话数据 + 自带 UI 扩展点）确定并冻结一个版本；
   - 插件清单 `plugin.json`（id / version / 所需宿主版本 / 权限）；
   - 独立 `AssemblyLoadContext` + 未处理异常隔离 + 超时熔断；
   - `--plugin-check` 门禁（加载一个示例插件、验证崩溃隔离）。

---

## 10. 生命周期完整性（第一优先级）

### 10.1 启动序列（每一步都必须能失败得有话说）

| # | 步骤 | 失败时 |
| --- | --- | --- |
| 1 | 解析命令行（`--self-check` / `--db-ping` / `--version` / `--safe-mode`） | 打印用法后退出码 2 |
| 2 | 读配置（`HELIUM_CONFIG_DIR` → 可分离 `config.json`） | 配置损坏：备份成 `config.bad-<时间>.json`，用默认值继续，并在界面提示条说明 |
| 3 | 单实例检查（`HELIUM_APP_ID` 可覆盖，`Mutex "Local\{id}.lock"`） | 已有实例：**`Dispatcher.UIThread.Post(() => desktop.Shutdown())`** 唤回旧窗口后退出 0 |
| 4 | 建 DI 容器 | 抛异常时写 `crash-<时间>.log` 并显示"启动失败"页 |
| 5 | 连数据库 + `Migrate()` | 连不上：显示"数据库不可用"页（重试 / 打开配置 / 打开日志），**不崩溃、不静默降级** |
| 6 | 种子数据（仅空库） | 失败：记日志并继续（不阻塞界面） |
| 7 | 建主窗口 | 失败：写崩溃日志，退出码 1 |
| 8 | 启动后台服务（提醒 30 s、壁纸、托盘） | 每个服务独立 `try/catch`；失败只影响该功能，界面提示一次 |
| 9 | 首次渲染完成后再播开场动画 | 无（遮罩最坏情况是闪一下） |

### 10.2 退出序列

| 场景 | 行为 |
| --- | --- |
| 点关闭按钮（默认） | 隐藏到托盘，进程保留；写 `[窗口] 已隐藏到托盘` |
| 托盘菜单「退出」 | `Settle()` → 停提醒/壁纸/托盘 → 刷配置（`FlushPendingSave`）→ `Shutdown(0)` |
| 崩溃 | 全局兜底捕获 → 写 `crash-*.log`（含版本、后端、最近 200 行日志）→ 提示"重启后可从备份恢复" |
| 被外部结束 | 不可避免；但 `desktop.Exit` 会写 `[退出] 进程正常结束，退出码 N` 以便区分 |
| 重启（改渲染后端） | 先 `SingleInstanceGuard.Dispose()` 放锁 → 起新进程 → 旧进程退出；调试器附加时拒绝重启 |

### 10.3 全局兜底（三处，缺一不可）

```csharp
AppDomain.CurrentDomain.UnhandledException += ...;      // 同步崩溃
TaskScheduler.UnobservedTaskException += ...;           // 未观察的 Task 异常（必须 SetObserved）
Dispatcher.UIThread.UnhandledException += ...;          // UI 线程异常（能救就救，不能救就写日志）
```

### 10.4 日志

- 位置：`<配置目录>/logs/background.log`，单文件上限 5 MB，保留 3 个（`background.1.log` …）。
- 格式：`HH:mm:ss.fff [类别] 消息`；类别固定 `[启动]/[窗口]/[数据]/[备份]/[提醒]/[壁纸]/[插件]/[退出]/[异常]`。
- **禁止**记录：密码、连接串明文、完整用户路径（只记文件名）。

### 10.5 `--safe-mode`

跳过壁纸、跳过托盘、跳过提醒，直接进主界面；用于"界面起不来/托盘卡死"的现场排障。

---

## 11. 功能完整性清单（0.5.0 验收表）

> 原则：0.3.5-alpha 已交付的功能，0.5.0 **一件不许少**。每一条都要有对应的自检或探针。

### 11.1 今日工作流（每日工作流）

- [ ] 24 小时时间轴：整点线、半时线、刻度标签、左侧留白 54 px（刻度 46 + 间隙 8）。
- [ ] 日程/待办/专注/冲突四类条目区分颜色与图例。
- [ ] 重叠日程自动分道（并查集分组 + 贪心分道；`laneCount` = 组内最大并发数，不是组大小）。
- [ ] 冲突检测（时间重叠对），右侧冲突列表可跳到条目。
- [ ] 前一天 / 今天 / 后一天；非今天时"今天"按钮高亮可用。
- [ ] 当前时刻红线（仅今天显示）。
- [ ] 空闲时段列表 + 一键「安排」把待办放进时间轴。
- [ ] 今日重点（按优先级排序）、今日专注时长、待办未完成数、已安排数、冲突数。
- [ ] 滚动位置保持（切页回来不跳回顶部）。

### 11.2 日程

- [ ] 增删改查；标题、开始/结束、时间块（上午/下午/晚上/全天）、状态（计划/进行中/完成/取消）、地点、备注、提醒偏移（开始/5/10/30/60/120 分钟/自定义）。
- [ ] 重复规则（每天/每周几/每月/自定义间隔），展开当天实例。
- [ ] 冲突在保存时提示，可强制保存。
- [ ] 编辑器默认收起，存完自动收起。

### 11.3 待办

- [ ] 快速添加（一行输入）、展开「更多」填分类/优先级/截止/提醒。
- [ ] 优先级（低/中/高/紧急）、分类预设、完成勾选、删除。
- [ ] 截止时间临期提示；提醒与日程同一套 `ReminderPlanner`。
- [ ] 把待办安排到时间轴（生成 `PlannedStart/PlannedEnd`）。
- [ ] 筛选（全部/今天/未完成/已完成/按分类）。

### 11.4 专注

- [ ] 计时器（预设 15/25/45/60/90 分钟 + 自定义），开始/暂停/继续/结束。
- [ ] 时钟表盘（剩余时间环形进度 + 数字），窗口尺寸变化时按可用空间缩放。
- [ ] 完成后写 `FocusSession`（开始/结束/时长/关联待办或日程）。
- [ ] 预订专注时长（`PlannedStart`），在工作流时间轴上以"专注 / 预订"图例显示。
- [ ] 无头环境定时器不触发时，界面不得卡在"进行中"（用真实时间差计算）。

### 11.5 自习室

- [ ] 建房 / 加入（房间码）/ 离开；房间列表。
- [ ] 成员列表（昵称、在线状态、今日专注时长）。
- [ ] 聊天消息（回车发送，历史按时间倒序/正序与现状一致）。
- [ ] 自控模块（白名单/黑名单，当前版本的配置项照抄）。
- [ ] 联机（`LanPeerTransport`）保留代码但默认关闭（`EnableNetworkStudyRoom=false`）；0.5.0 不改协议。

### 11.6 设置（十组，文案见 §12）

- [ ] 外观：明暗模式、强调色（含取色器）、功能区不透明度、卡片圆角/阴影跟随主题。
- [ ] 背景壁纸：启用、选择文件、动图/视频、蒙版强度。
- [ ] 专注、自控模块、每日工作流：现有全部选项与默认值不变。
- [ ] 自习室：联机开关（默认关）、端口、昵称。
- [ ] 插件：**只读说明**（§9）。
- [ ] 数据与文件：导出 zip、导入（Replace/Merge）、打开数据目录、自动备份开关与保留份数。
- [ ] 底层设置：渲染后端（Auto/Software/OpenGL/Vulkan）+ **重启确认框**（"更改此项需要重启，是否立即重启" / "确认" / "取消更改"）、日志级别、诊断。
- [ ] 关于：版本、构建时间、许可证入口（GPL-3.0）、第三方声明。

### 11.7 数据与备份

- [ ] 导出：`.zip`（manifest + data + assets），默认落桌面，被拒（受控文件夹访问）时落 `backups/` 并提示改存位置。
- [ ] 导入：Replace / Merge；事务性（失败全回滚）；导入前自动做一次快照。
- [ ] 自动备份：启动后 + 每 24 h，保留 N 份，`backups/helium.YYYYMMDD_HHmmss.db` → 0.5.0 起改为 `.zip`（v2 格式）。
- [ ] 恢复：从备份列表选择 → 校验 SHA256 → 导入。

### 11.8 通知与托盘

- [ ] 提醒窗口 `[now-2min, now]`，`HashSet` 去重，重复日程次日仍提醒。
- [ ] 托盘图标 + 右键菜单（显示主窗口 / 退出）；`Shell_NotifyIconW` 失败重试 5 次并退化为"关闭即退出"。
- [ ] 最小化到托盘；单实例第二次启动唤回。

---

## 12. 界面文案：完全照搬

### 12.1 规则

1. **唯一真相**：`UI_TEXT_MAP.md`（由 `tools/gen_ui_text_map.py` 生成，当前 **8 个文件 / 231 处**）。
2. 重构界面时，任何**用户可见**文字（标题、按钮、占位符、提示、空状态、工具提示、说明段落）
   **必须逐字一致**：不改写、不润色、不"顺手优化"、不增减标点。
3. 新增文案必须同时更新 `UI_TEXT_MAP.md`（重跑生成脚本）并在 `PROGRESS.md` 说明来源。
4. 文案与控件解耦：文案放 `Resources/ Strings.axaml`（`x:Key`）或 `Resources.resx`，
   但**值**必须照抄现有字符串；改值 = 改需求，需要单独确认。
5. 空状态文案（如"还没有待办，去「待办事项」页加一个吧"）属于必须保留的用户可见文字。
6. **说明性文字也算**：设置项下方那句解释（"功能介绍"）、分组说明、按钮旁的辅助句，
   同样逐字照抄。不许把它"改写得更专业"，也不许因为换了控件就删掉。

### 12.2 必抄清单（抽样，全量以 `UI_TEXT_MAP.md` 为准）

| 位置 | 原文 |
| --- | --- |
| 页面标题 | `今日工作流`、`日程安排`、`待办事项`、`专注时长`、`自习室`、`设置` |
| 设置分组 | `外观`、`背景壁纸`、`专注`、`自控模块`、`每日工作流`、`自习室`、`插件`、`数据与文件`、`底层设置`、`关于` |
| 工作流 | `24 小时时间轴`、`空闲时段`、`点「安排」把待办放进时间轴`、`冲突提醒`、`没有任何冲突，今天的安排很清爽 🎉`、`今日重点`、`按优先级排序`、`还没有待办，去「待办事项」页加一个吧`、`截止 {0:MM-dd HH:mm}` |
| 工具栏 | `前一天`、`今天`、`后一天`、`重新加载` |
| 联机 | `联机`、`房间码`、`加入房间`、`离开房间` |
| 重启确认 | `更改此项需要重启，是否立即重启`、`确认`、`取消更改` |
| 提示条 | `已取消更改，渲染后端仍是「软件渲染」。`（示例，全量见 `UI_TEXT_MAP.md`） |

> 验收方式：`--copy-check`（新增）扫描 `UI_TEXT_MAP.md` 里的每一条字符串，
> 在编译后的 XAML 资源里必须能原样找到；缺一条即红。

---

## 13. GPL-3.0 合规

### 13.1 许可选择

- 许可证：**GPL-3.0-only**（不是 `or-later`）。
  理由：`or-later` 等于预先同意未来的 v4 条款；本项目希望条款可预期。若日后要改，需全体贡献者同意。
- 版权人：`Helium Todo contributors`（个人贡献者聚合体），`Copyright (c) 2025 Helium Todo contributors`。

### 13.2 必须落地的文件

| 文件 | 内容 |
| --- | --- |
| `LICENSE` | GPL-3.0 全文（英文原版，不要自行翻译后当法律文本） |
| `COPYING` | 指向 `LICENSE`（可选，GPL 项目习惯） |
| `THIRD-PARTY-NOTICES.md` | 下表全部依赖的许可证与版权行 |
| `LICENSES/` | 各依赖的许可证原文（MIT/Apache-2.0/PostgreSQL/BSD 各一份） |
| `README` 顶部 | 一段"许可"说明 + 源码地址 |

### 13.3 每个源文件的头（两行，不许省）

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

门禁：`tools/check-license-headers.py` 扫描 `*.cs` / `*.axaml`，缺头即失败（P0 接入 CI 脚本）。

### 13.4 第三方依赖许可证（必须先核对，再写进 THIRD-PARTY-NOTICES.md）

| 依赖 | 许可证 | 与 GPL-3.0 兼容 | 注意 |
| --- | --- | --- | --- |
| Avalonia / Avalonia.Desktop / Themes.Fluent / Headless / HarfBuzz | MIT | ✅ | 保留版权与许可声明 |
| Material.Avalonia / .Dialogs | MIT | ✅ | 同上 |
| Material.Icons.Avalonia | MIT（图标为 Apache-2.0） | ✅ | 图标需保留 NOTICE |
| CommunityToolkit.Mvvm | MIT | ✅ | |
| Microsoft.EntityFrameworkCore / Npgsql.EntityFrameworkCore.PostgreSQL | MIT / PostgreSQL License | ✅ | Npgsql 的 PostgreSQL License 属宽松许可 |
| SkiaSharp | MIT | ✅ | 原生库 `libSkiaSharp` 也是 MIT |
| .NET 运行时（自包含发布时） | MIT | ✅ | 分发时附带其许可声明 |
| xunit / Microsoft.NET.Test.Sdk | Apache-2.0 / MIT | ✅ | 只进测试，不随产品分发 |
| **Segoe Fluent Icons / Segoe MDL2 Assets** | **专有（微软）** | 不适用 | **绝不许**打包字体文件；只能引用系统已安装字体名（现状即如此） |
| **FluentAvaloniaUI** | MIT | ✅ | 0.5.0 移除；历史版本仍在 git 历史里，`THIRD-PARTY-NOTICES` 里保留一行说明 |

### 13.5 分发注意（GPLv3 特有）

- **源码可得**：发布二进制时必须能对应到同一版本的源码（tag + 构建脚本）。发布包内附 `SOURCE.txt` 写明仓库地址与 commit。
- **安装信息（Anti-Tivoization）**：若将来发布到"用户产品"（锁定硬件的设备），需提供安装修改版的方式；桌面 PC 分发不触发该条，但文档里保留说明。
- **不额外加限制**：不许在 EULA 里写"禁止商用/禁止反编译"等与 GPLv3 冲突的条款。
- **贡献**：`CONTRIBUTING.md` 要求签署 DCO（`Signed-off-by`），不要求 CLA（避免版权集中导致将来无法改许可）。

---

## 14. 编码规范（针对前期不规范逐条下禁令）

### 14.1 十条硬禁令（每条都对应真实事故）

| # | 禁令 | 事故来源 |
| --- | --- | --- |
| 1 | 不许 `catch { }` 空吞异常；至少 `catch (Exception ex) { _logger.LogWarning(ex, "..."); }` | 联机 `AcceptLoopAsync` 的异常被丢弃，症状与根因完全对不上 |
| 2 | 不许 `async void`（事件处理器除外，且必须 try/catch） | `OnFocusCompleted` 曾把进程带走 |
| 3 | 不许 `_ = Task.Run(...)` 丢弃任务 | 同上，异常彻底静默 |
| 4 | 不许字符串拼 SQL；要动态条件用 LINQ 或参数化 | 注入风险 + `EF1002` 警告 |
| 5 | 不许魔法数（时长、阈值、颜色、尺寸）；常量进 `Domain/Policies` 且写清来历 | `180 ms`/`0.96`/`1280` 散落各处曾导致动画与预算不一致 |
| 6 | 不许 `TODO`/`FIXME`/`HACK` 进主干；要么现在做，要么开 issue | 0.3.x 曾有 6 处悬空 |
| 7 | 不许在 ViewModel 里 `new Window()` / `MessageBox` / `Dispatcher.Invoke` | 无法无头测试 |
| 8 | 不许用 `services.GetService(...)!` 取依赖 | 现在是 5 个 ViewModel 的写法，空引用炸弹 |
| 9 | 不许 `unsafe`；不许 `dynamic` | 项目策略 |
| 10 | 不许静默改历史文档数字（`PROGRESS.md` 历史小节、旧截图说明） | 改完文档就从记录变成谎言 |

### 14.2 代码风格（`.editorconfig` 写死，CI 检查）

- 可空引用类型开启；`#nullable disable` 需注释理由。
- 文件作用域命名空间（`namespace X;`），禁止块式。
- 私有字段 `_camelCase`；常量 `PascalCase`；异步方法 `...Async` 且必须 `await`。
- 每个 `public` 类型有 XML 文档注释；每个"为什么"写注释（不是"是什么"）。
- 方法 ≤ 60 行、类 ≤ 600 行、参数 ≤ 5 个（超了先想拆分）。
- `using` 排序、`var` 仅在类型显而易见时使用。
- 一个文件一个 public 类型（嵌套私有类型允许）。

### 14.3 分析器与构建

```xml
<Nullable>enable</Nullable>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>   <!-- P4 起开启，逐项目推进 -->
```

- P1 起加 `Microsoft.CodeAnalysis.NetAnalyzers`（内置）+ `Roslynator.Analyzers`（可选）。
- 每阶段把警告清零再开 `TreatWarningsAsErrors`，不许一次性全开导致积压。

### 14.4 资源与线程

- `IDisposable` 一律 `using` / `await using`；长生命周期对象由 DI 容器管。
- 定时器用完即 `Stop()` + `Dispose()`；窗口隐藏时暂停壁纸与提醒。
- `DispatcherTimer` 只用于"没有渲染帧也能跑"的场景（如 3 秒提示条、兜底看门狗）；
  需要逐帧的动画用 `TopLevel.RequestAnimationFrame`。
- 所有 COM 对象（Media Foundation）必须 `Marshal.ReleaseComObject`。

---

## 15. 质量门禁

### 15.1 每轮必跑（沿用 + 扩展）

```powershell
dotnet build HeliumTodo.slnx -c Debug                      # 0 警告 0 错误
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll          # ≥190 全绿
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --shell
# 15 个既有自检 + 3 个新增：--db-check / --theme-check / --behavior-check
pwsh tools\check-file-integrity.ps1
git status --short                                          # 必须为空
```

### 15.2 新增自检

| 自检 | 断言 |
| --- | --- |
| `--db-check` | 连接可用；迁移到最新；6 张表列名/类型与模型一致；往返 CRUD；备份导出→导入→再导出等价 |
| `--theme-check` | MaterialTheme 已应用；明暗切换后卡片色/文字色/图标色变化；强调色生效；`CardShadow` 等资源未被主题字典吞掉 |
| `--behavior-check` | 8 个 Behavior 全部挂载；各触发一次；`OnDetaching` 后事件计数归零 |
| `--copy-check` | `UI_TEXT_MAP.md` 里每条文案在资源里原样存在 |

### 15.3 性能预算（参考 0.3.5 实测，不许明显退步）

| 指标 | 预算 |
| --- | --- |
| 冷启动到首帧 | ≤ 1.5 s（现状 ~1 s 首帧、8 s 内存稳定） |
| 工作集 / 私有字节 / 私有工作集（空配置，默认 Auto） | ≤ 260 / 300 / 90 MB（现状 208.1 / 256.9 / 68.6） |
| 图标轨展开动画 | P50 ≤ 16.7 ms（≥60 fps），P95 ≤ 33.3 ms |
| 最大化/还原 | 走 DWM，`probe-native-anim.ps1` 采到 ≥12 个单调递减可见面积 |
| 时间轴首屏渲染 | ≤ 50 ms |
| 24 h 闲置内存增长 | 0（无泄漏） |

### 15.4 真机探针（16 个 `probe-*.ps1` + `probe-common.ps1` + 2 个截图工具，全部入库）

`probe-common.ps1`（公共启动器）、`probe-defaults`、`probe-window-style`、`probe-window-frame`、
`probe-native-anim`、`probe-intro`、`probe-rail-anim`、`probe-minimize-restore`、`probe-single-instance`、
`probe-lifecycle`、`probe-memory`、`probe-render-backend`、`probe-dwm-attrs`、`probe-dwm-frame`、
`probe-maximize-anim`、`probe-maximize`、`probe-resize-post`、`capture-window`、`capture-screenshot`。

> 含中文的 `.ps1` 必须 **UTF-8 带 BOM + CRLF**；启动应用一律走 `probe-common.ps1` 的 `Start-HeliumApp`（坑 I1）。

---

## 16. 现有项目树（0.3.5-alpha 实况）

> 生成方式：排除 `.git`、`bin`、`obj`、`dist`、`.vs`、`.idea`、`output`、`.tmp*`。
> 共 **227** 个文件（含文档、截图、探针、测试数据）。标注 `→ 0.5.0` 的是迁移归属。

```
Helium todo demo/
├── HeliumTodo.slnx                        # 解决方案（0.5.0 改为七个项目）
├── Directory.Build.props                  # 版本号 / 语言 / 全局属性  ← 0.5.0 改 0.5.0-alpha
├── Directory.Packages.props               # 中央包版本（含大量"为什么钉这个版本"的注释）
├── global.json                            # SDK 10.0.401
├── NuGet.config                           # 镜像源（0.5.0 增加 packages/local 本地源）
├── .gitignore / .gitattributes            # LF 统一、二进制标记
│
├── HeliumTodo.Core/                       # → Domain + Application
│   ├── Backup/                            #   → Application（契约）
│   │   ├── BackupFormat.cs
│   │   ├── BackupInspection.cs
│   │   ├── BackupManifest.cs
│   │   ├── BackupPackageReader.cs
│   │   └── BackupPackageWriter.cs
│   ├── Configuration/                     #   → Application
│   │   ├── AccentSources.cs
│   │   ├── AppConfig.cs
│   │   ├── ColorSchemes.cs
│   │   └── RenderBackendSettings.cs
│   ├── Imaging/ApngParser.cs              #   → Infrastructure（解码细节）
│   ├── Ipc/                               #   → Application（预留，未启用）
│   │   ├── IIpcChannel.cs
│   │   └── IpcContracts.cs
│   ├── Lifecycle/                         #   → Domain/Policies
│   │   ├── AppIdentity.cs
│   │   └── RestartPolicy.cs
│   ├── Media/PlaybackLimits.cs            #   → Domain/Policies
│   ├── Models/                            #   → Domain/Entities + ValueObjects
│   │   ├── Enums.cs
│   │   ├── FocusSession.cs
│   │   ├── ReminderPresets.cs
│   │   ├── ScheduleItem.cs
│   │   ├── StudyRoom.cs
│   │   └── TodoItem.cs
│   ├── Networking/                        #   → Domain（契约）/ Infrastructure（实现）
│   │   ├── PeerEndpoint.cs
│   │   └── PeerMessage.cs
│   ├── Notifications/                     #   → Application
│   │   ├── INotificationService.cs
│   │   ├── NoticePolicy.cs
│   │   ├── ReminderItem.cs
│   │   └── ReminderPlanner.cs
│   ├── Plugins/IHeliumPlugin.cs           #   → 存档（#if HELIUM_PLUGINS，默认不编译）
│   ├── Services/                          #   → Domain/Services
│   │   ├── Conflict.cs
│   │   ├── DayTimeline.cs
│   │   ├── DayTimelineBuilder.cs
│   │   ├── FocusTimerService.cs
│   │   ├── RecurrenceExpander.cs
│   │   └── StudyRoomService.cs
│   ├── Theming/                           #   → Domain（规则）+ Desktop（应用）
│   │   ├── AccentColor.cs
│   │   ├── BuiltInThemes.cs
│   │   ├── IHeliumTheme.cs
│   │   ├── RailMetrics.cs
│   │   ├── WallpaperAccentExtractor.cs
│   │   └── WallpaperScrim.cs
│   ├── Windowing/WindowTransitionPolicy.cs #   → Domain/Policies
│   ├── VersionInfo.cs
│   └── HeliumTodo.Core.csproj
│
├── HeliumTodo.Data/                       # → Infrastructure
│   ├── DataServiceCollectionExtensions.cs  #   → DI 注册（改为 Npgsql）
│   ├── HeliumDbContext.cs                  #   → Persistence（删 EnsureColumns）
│   ├── DatabaseInitializer.cs              #   → Persistence/Migrations 引导
│   ├── SeedData.cs
│   ├── ScheduleRepository.cs
│   ├── TodoRepository.cs
│   ├── FocusRepository.cs
│   ├── StudyRoomRepository.cs
│   ├── DataBackupService.cs                #   → 重写为备份 v2（JSONL）
│   ├── BackupExportResult.cs
│   ├── BackupImportResult.cs
│   ├── LanPeerTransport.cs                 #   → 保留（默认关闭）
│   ├── LocalOnlyPeerTransport.cs
│   └── HeliumTodo.Data.csproj
│
├── HeliumTodo.App/                        # → Desktop
│   ├── App.axaml / App.axaml.cs            #   组合根 + 生命周期（0.5.0 拆出 Composition/）
│   ├── Program.cs                          #   RHI 后端选择 + 命令行
│   ├── ViewLocator.cs
│   ├── app.manifest
│   ├── Controls/                           #   仅保留"库内没有"的
│   │   ├── ClockControls.cs                #     → 先试 Material Clock
│   │   ├── ColorPickerControls.cs          #     → 换 Avalonia ColorPicker
│   │   ├── TimelineGrid.cs                 #     → 保留（登记）
│   │   ├── WidthObserverPanel.cs           #     → 保留（内部工具）
│   │   └── ReminderPicker.axaml(.cs)
│   ├── Converters/CommonConverters.cs
│   ├── Services/
│   │   ├── FolderLauncher.cs
│   │   ├── MediaFoundationVideo.cs
│   │   ├── PluginHost.cs                   #     → 删除（§9）
│   │   ├── ReminderService.cs
│   │   ├── RenderBackendChoice.cs
│   │   ├── SingleInstanceGuard.cs
│   │   ├── ThemeService.cs                 #     → 改 PaletteHelper
│   │   ├── TrayService.cs
│   │   ├── WallpaperDecoder.cs
│   │   ├── WallpaperScaler.cs
│   │   ├── WallpaperService.cs
│   │   └── WindowsToastNotifier.cs
│   ├── Styles/DesignTokens.axaml           #   令牌（卡片/圆角/阴影/图标字体）
│   ├── Styles/Controls.axaml               #   控件样式（含 4 条 FA 透明样式 → 删除）
│   ├── ViewModels/                         #   9 个
│   │   ├── AppState.cs
│   │   ├── ColorPaletteViewModel.cs
│   │   ├── DashboardViewModel.cs
│   │   ├── FocusViewModel.cs
│   │   ├── ReminderEditState.cs
│   │   ├── ScheduleViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   ├── ShellViewModel.cs
│   │   ├── StudyRoomViewModel.cs
│   │   ├── TimelineEntryViewModel.cs
│   │   ├── TimelineMetrics.cs
│   │   ├── TodoViewModel.cs
│   │   └── ViewModelBase.cs
│   ├── Views/                              #   7 页 + 外壳 + 主窗口
│   │   ├── DashboardView.axaml(.cs)        #     今日工作流
│   │   ├── ScheduleView.axaml(.cs)         #     日程安排
│   │   ├── TodoView.axaml(.cs)             #     待办事项
│   │   ├── FocusView.axaml(.cs)            #     专注时长
│   │   ├── StudyRoomView.axaml(.cs)        #     自习室
│   │   ├── SettingsView.axaml(.cs)         #     设置（十组）
│   │   ├── ShellView.axaml(.cs)            #     图标轨 + 标题栏
│   │   └── MainWindow.axaml(.cs)           #     无边框窗口 + 壁纸 + 抓手 + 遮罩
│   └── Windowing/
│       ├── InteractionQuality.cs           #   交互降质（引用计数）
│       ├── WindowAnimationMath.cs          #   180ms 缓动
│       ├── WindowAnimator.cs               #   开场遮罩动画
│       ├── WindowChrome.cs                 #   WS_CAPTION + DWM 过渡
│       └── WindowResizeGrips.cs            #   8 抓手（按帧合并）
│
├── HeliumTodo.Tests/                      # 20 个测试文件（190 用例）
│   ├── AccentColorTests.cs              ├── AppIdentityTests.cs
│   ├── ApngParserTests.cs               ├── BackupTests.cs
│   ├── ColorSchemesTests.cs             ├── DayTimelineBuilderTests.cs
│   ├── FocusTimerServiceTests.cs        ├── NetworkingTests.cs
│   ├── NoticePolicyTests.cs             ├── PlaybackLimitsTests.cs
│   ├── RailMetricsTests.cs              ├── RecurrenceExpanderTests.cs
│   ├── ReminderPlannerTests.cs          ├── RenderBackendSettingsTests.cs
│   ├── RestartPolicyTests.cs            ├── StudyRoomRepositoryTests.cs
│   ├── TestWorkspace.cs                 ├── VersionInfoTests.cs
│   ├── WallpaperAccentExtractorTests.cs ├── WallpaperScrimTests.cs
│   └── WindowTransitionPolicyTests.cs
│
├── HeliumTodo.SelfTest/Program.cs          # 反射跑 [Fact]（async 必须 GetAwaiter().GetResult()）
├── HeliumTodo.RenderCheck/Program.cs       # → Verification：15 项 --check + 8 方式截图
│
├── tools/
│   ├── check-file-integrity.ps1            #   177 文件 SHA256 基线
│   ├── file-hashes.json
│   ├── gen_ui_text_map.py                  #   界面文案清单生成器
│   ├── migrate-av12.ps1                    #   Avalonia 11→12 迁移脚本（历史）
│   ├── capture-screenshot.ps1 / capture-window.ps1
│   └── probe-*.ps1（16 个真机探针）
│
├── docs/                                   # 历史截图（7 页 + 玻璃/暗色 + 弹窗）
│   ├── screenshot-dashboard.png … screenshot-settings-notice.png
│   └── avalonia12-render/（6 页旧版对照）
│
├── testdata/wallpaper/                     # anim.gif / anim.png / photo.jpg
├── Data/                                   # 运行时数据（已在 .gitignore，仅本地存在）
└── 文档
    ├── AGENTS.md                  # 给 AI 的硬规则（0.5.0 需同步版本/门禁/路径）
    ├── ARCHITECTURE.md            # 架构与约定（历史决策记录，不许改历史数字）
    ├── DEVELOPMENT_PLAYBOOK.md    # 每轮七步流程 + 技能清单
    ├── DEVELOPMENT_SPEC.md        # ★ 本文档（0.5.0-alpha 技术栈重写规格）
    ├── PROGRESS.md                # 逐轮开发记录（历史小节禁止修改）
    ├── PROJECT_OVERVIEW.md        # 项目全景
    ├── TECH_STACK.md              # 旧技术栈（被本文档取代）
    ├── UI_TEXT_MAP.md             # 界面文字 → 代码行号（文案唯一真相）
    ├── UI_REDESIGN_BRIEF.md       # 大修 UI 的原始设计说明
    ├── WINDOWS_DEPENDENCIES.md    # Windows 平台依赖说明
    ├── WINDOW_ANIMATION_FIX_PLAN.md  # 窗口动效修复计划（已完成）
    └── AVALONIA12_FEASIBILITY.md  # Avalonia 12 可行性评估（已完成）
```

---

## 17. 前期坑点大全（按类别，88 条）

> 格式：**症状 → 根因 → 规则**。每条都是真踩过的，不是"可能"。
> 0.5.0 的代码复审清单里，这 88 条逐条对照。
> 分布：A Avalonia/XAML 16 · B 主题 7 · C 布局 5 · D 渲染 11 · E 窗口生命周期 10 ·
> F 数据备份 11 · G 构建 6 · H 测试 6 · I PowerShell 9 · J 工程文档 7。

### A. Avalonia / XAML / 编译

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| A1 | `CS1061: "Border"未包含"East"的定义` | `Name="WindowEdge"` 生成的同名字段遮住了 `WindowEdge` 枚举 | 不给控件起与类型/枚举同名的 `Name` |
| A2 | `Cannot override FINAL property` + 页面类型不可用 | 自定义属性与控件 FINAL 属性同名（`ComboBox.currentValue`） | 换名（`selectedMinutes`）；查 XML 文档确认 FINAL |
| A3 | `The property 'Content' is assigned more than once`（VS 报 23 处，`dotnet build` 不报） | 同时设 `Content` 又塞子元素 | FA 控件子元素放 `Footer`；提交前看 VS 错误列表 |
| A4 | `AVLN1001: An XML comment cannot contain '--'` | XAML 注释里写了 `--shell-check` | XAML/XML 注释里不许出现连续两个减号 |
| A5 | `NU1010`（所有包都缺 PackageVersion） | `Directory.Packages.props` 的 XML 注释里写了 `--` | 同上；改 props 前先想这一点 |
| A6 | `AVLN2201: Unable to parse selector` | 样式选择器写全名 `FluentAvalonia.UI.Controls\|Type` | 写 `fa\|Type`（`xmlns:fa`） |
| A7 | `AVLN2000: Internal compiler error … ResolveContentPropertyTransformer` | 在样式里写 `<Setter Property="Transitions">` | 过渡只放在元素上或控件模板里 |
| A8 | `CS0117: "Image"未包含"RenderOptions"的定义` | `RenderOptions` 是附加属性，不能写进对象初始化器 | 用 `RenderOptions.SetBitmapInterpolationMode(image, …)` |
| A9 | `CS0122: "IBitmap"不可访问` | Avalonia 12 把 `IBitmap` 收起来了 | 用公开的 `Bitmap.PixelSize` / `IImage` |
| A10 | `CS0117: "TextRenderingMode"未包含"Aliased"` | 枚举值是 `Alias`；且该 API 已 `[Obsolete]` | 用 `TextOptions.TextRenderingMode`（新 API） |
| A11 | 自绘控件拖动不回写、且不报错 | `AvaloniaProperty.Register` 默认 `OneWay` | 显式 `defaultBindingMode: BindingMode.TwoWay` |
| A12 | 无头测试里定时器/`Loaded` 完全不触发 | Avalonia 无头平台不驱动 `DispatcherTimer`、不触发 `Loaded` | 首屏逻辑放 `OnAttachedToVisualTree`；需要兜底就加看门狗并用探针开关跳过断言 |
| A13 | `Getter` 拿不到 `WindowState` 变化通知 | Avalonia 12 的 `WindowStateProperty` 是 `StyledProperty`，**没有** `WindowStateChanged` 事件 | 重写 `OnPropertyChanged(AvaloniaPropertyChangedEventArgs)` |
| A14 | `Window.BeginResizeDrag` 无反应 | 只对 `WS_THICKFRAME` 的系统循环生效，无边框窗口不适用 | 自己算尺寸 + 每帧只改一次 |
| A15 | `RequestAnimationFrame` 回调不来 | 窗口静止时不产帧 | 逐帧动画期间必须有"下一次请求"的驱动；超时用看门狗收尾 |
| A16 | 绑定在运行期静默失败 | 缺 `x:DataType` / 属性名拼错 | 全部 View 写 `x:DataType`，编译绑定 |

### B. 主题 / 资源 / 样式

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| B1 | 设置页每个分组自带一块白底，改 `Background` 无效 | `FASettingsExpanderItem` 与标题 `ToggleButton` 的**模板内 `Border#Root`** 写死 `#B3FFFFFF`（34 行） | "背景透明"要按模板实际颜色验证；用 `/template/ Border#Root` 选择器覆盖 |
| B2 | 代码赋值 `CardSurfaceBrush`/`CardShadow` 静默无效 | 同名资源写在 `ThemeDictionaries` 里，主题字典优先级更高 | 代码要改的资源**必须**放在主题字典外 |
| B3 | 提示条半透明，底下文字透上来 | `FAInfoBar` 背景刷带 alpha，且被当浮层用 | 提示条要么占一行，要么用不透明背景 + 高 `ZIndex` |
| B4 | FA 控件圆角改不动 | `FASettingsExpander`/`FAInfoBar` **没有** `CornerRadius` 属性 | 外层套 `Border CornerRadius=… ClipToBounds=True` |
| B5 | 强调色设了不生效 | 直接写 `Application.Resources` 被 FA 主题字典覆盖 | 走 `FluentAvaloniaTheme.CustomAccentColor`（0.5.0 起换 `PaletteHelper`） |
| B6 | 图标大小不齐 / 选中不变白 | 用 emoji 当图标 | 用单一图标字体（`Segoe Fluent Icons` 或 `Material.Icons`） |
| B7 | 十张小卡 vs 一整张卡，视觉"被落下" | 每页分区结构不一致 | 六页共用同一骨架 + `--layout-check` 断言 |

### C. 布局与交互细节

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| C1 | 卡片顶边在 36/45/46 之间飘 | 标题行没给 `MinHeight` | 标题行固定 `MinHeight=40 Margin=4,0,4,20` |
| C2 | 抓手点不中（偏 26 px） | 动画残留 `ScaleTransform(0.96)`，`TranslatePoint` 被缩放 | 动画结束必须 `Settle()` 清 `RenderTransform`；抓手断言可量化 |
| C3 | 展开时条目贴着轨道边 | 按钮 `Margin=0,3` | 展开态 `Margin=6,3`；用 `Classes.expanded` 区分 |
| C4 | 收起态图标方块不是 44×44 | 模板去掉了 `Width`，靠拉伸定宽 | 收起态用样式钉 44，展开态才 `Width=NaN` + `Stretch` |
| C5 | 文本按新宽度重新排版导致整页重排（+14 ms/帧） | 轨道宽度改变 → 内容列宽改变 → 文本重排 | 交互期间降质；必要时内容列固定宽度（需产品确认） |

### D. 渲染与性能

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| D1 | 内存比预期多 ~180 MB 私有字节 | ANGLE 把 NVIDIA 驱动栈映射进进程 | 知道取舍：GPU 流畅 / 软件省内存；默认按场景选并记录 |
| D2 | 软件渲染改一次窗口尺寸 45 ms | `SetWindowPos` + DWM + 整个表面重建（2560×1440×4 B = 14.7 MB） | 布局不花钱（≈0 ms），钱在"换尺寸本身"；能让 DWM 播就别自己播 |
| D3 | `NU1102: 找不到 Avalonia 12.1.4` | `Avalonia.Controls.DataGrid` 只发到 12.1.2（被 FluentAvalonia 拖进来） | 升 Avalonia 前逐个子包核对；去掉 FA 后此约束消失 |
| D4 | 两个 `libSkiaSharp.dll`，随机崩 | SkiaSharp 与 Avalonia 传递依赖版本不一致 | 必须同版（现 3.119.4） |
| D5 | 每帧重采样壁纸（2560 上 +27 ms） | `Image Stretch=UniformToFill` 每次按新尺寸插值源图 | 静态壁纸先缩放到窗口像素（`WallpaperScaler`）再 `Stretch=Fill` |
| D6 | `Visual.Opacity` 动画很贵（37 ms/帧） | 改 `Opacity` 会 push 离屏图层 | 淡入改"遮罩画刷 Alpha"（4.4 ms/帧） |
| D7 | 量到 264 ms/帧的假数 | 我们每帧写 `Width`，过渡也每帧写，互相拉扯 | 测过渡成本时先 `Transitions = null` 再按帧写 |
| D8 | 单帧尖峰让断言永远红 | 一轮只采 20 多帧，GC/抢 CPU 会打出孤立尖峰 | 判据用 `P95 < 2×P50` + `最慢 < 4×P50`，最慢只提示 |
| D9 | 动图壁纸内存暴涨 | 1920 长边 × 40 帧不封顶 | 长边 ≤1280、总字节 ≤96 MB、播放 ≤30 fps |
| D10 | MP4 壁纸内存冲到 3 GB | 每帧没释放 COM sample | 每帧 `Marshal.ReleaseComObject`；不要开 `MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING` |
| D11 | 换视频尺寸 `0xC00D36B4` | Media Foundation 不接受中途改类型 | 自己 `ScaleBitmap` 缩放 |

### E. 窗口与生命周期

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| E1 | 无边框窗口最小化/最大化没有系统动画 | 样式位缺 `WS_CAPTION`（`0x00C00000`） | 补 `WS_BORDER\|WS_DLGFRAME`；`WM_NCCALCSIZE` 钩子保证不长标题栏 |
| E2 | 打开窗口"闪一下"而不是淡入 | 改 `Window.Opacity` → `WS_EX_LAYERED`，DWM 重建重定向表面；进度按真实时间算，首帧迟到直接跳终点 | 用遮罩画刷 Alpha；单帧步进上限 0.2 |
| E3 | 最小化再回来：半透明 + 缩小 4% | 动画中最小化，定时器在隐藏窗口上跑不出帧 | 进 `Minimized` 立刻 `Settle()`；回来补播出现动画 |
| E4 | 第二实例退出码 `0xE0434352` 崩溃 | `OnFrameworkInitializationCompleted` 里直接 `desktop.Shutdown()`，消息循环还没起 | `Dispatcher.UIThread.Post(() => desktop.Shutdown())` |
| E5 | 第二实例退出码 `0xFFFFFFFF` | 被外部终止（VS 停止调试/任务管理器/探针 Kill） | 判别用 `desktop.Exit` 日志；不要把它当自己的返回码 |
| E6 | 改渲染后端重启后，新进程只喊一声就退出 | 旧进程还占着单实例锁 | 重启前 `SingleInstanceGuard.Dispose()` |
| E7 | VS 调试时点重启，编译报 `MSB3027` 文件被占用 | 新进程成了孤儿，占住 exe | 调试器附加时拒绝重启（`Debugger.IsAttached`）；`MSB3027` 先查占用进程 |
| E8 | 探针/脚本启动应用永久挂起 | 本机会话**没有资源管理器**，`Start-Process` 走 `ShellExecuteEx` 卡死 | 一律 `ProcessStartInfo{ UseShellExecute=false, CreateNoWindow=true }` |
| E9 | 托盘图标装不上 | 开机时资源管理器未就绪（错误码 5） | `NIM_ADD` 重试 5 次 + `ManualResetEventSlim` 握手 |
| E10 | 关窗口进程不退 | 关闭拦截未生效（托盘初始化失败就不装拦截） | 托盘失败也必须装关闭拦截 |

### F. 数据与备份

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| F1 | 备份拿到旧数据 | WAL 模式下直接拷 `.db`，WAL 未合并 | `PRAGMA wal_checkpoint(TRUNCATE)` 或 `VACUUM INTO`（0.5.0 起改 JSONL 导出） |
| F2 | 导入时换文件失败/数据丢失 | Windows 上文件被占用，关连接换文件不可靠 | 单事务 `ATTACH DATABASE` 搬表（0.5.0 起改 JSONL + 事务 INSERT） |
| F3 | 自检把用户真实数据合并了 | 复用 `%APPDATA%` 真实容器 | 自检必须 `HELIUM_CONFIG_DIR` 指沙盒 |
| F4 | 导入撞唯一约束整包失败 | `StudyRooms.Code` 唯一，合并时冲突 | 冲突行跳过并连带跳过成员/消息 |
| F5 | 枚举/时间在 PG 里对不上 | 旧库存 TEXT `"yyyy-MM-dd HH:mm:ss"` 与 INTEGER 枚举 | 迁移脚本按旧契约逐列转换；枚举 int 值保持不变 |
| F6 | 搜索在 PG 上大小写敏感 | SQLite `LIKE` 默认不敏感，PG 敏感 | 用 `EF.Functions.ILike` |
| F7 | `relation "schedules" does not exist` | PG 未加引号标识符被小写化 | 表名保持 EF 的引号写法，禁止手写裸 SQL |
| F8 | 迁移后所有日程平移 8 小时 | Npgsql 默认 `timestamptz` + UTC 换算 | 墙钟时间列显式 `timestamp without time zone` |
| F9 | 导入后新增记录撞主键 | identity 序列没跟上 max(id) | 导入后 `setval(pg_get_serial_sequence(...), max("Id"))` |
| F10 | 导出崩溃：`UnauthorizedAccessException` | 桌面被"受控文件夹访问"拦住；该异常**不继承** `IOException` | 捕获后落 `backups/` 并提示改存位置 |
| F11 | `EF1002` 警告 | 插值字符串拼 SQL | 参数化 / LINQ |

### G. 构建与工具链

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| G1 | 只有 DataGrid 是旧版，IDE 莫名标红 | `CentralPackageTransitivePinningEnabled=false` 时中央版本不管传递依赖 | 必须 `true` |
| G2 | `FluentAvaloniaUI 3` 要求 `net10.0` | 该包只有 `net10.0` 一个目标 | 已跟随；0.5.0 去掉后仍保持 net10.0 |
| G3 | FA 3 升级后 224/252 个类型改名 | 3.x 加 `FA` 前缀避冲突 | 升级前先读改名表；`FluentAvaloniaTheme` 例外 |
| G4 | `dotnet test` 起不来 | 沙箱拦 testhost | 门禁用 `SelfTest` |
| G5 | 编译慢（改一个 `.cs` 2.6–3.2 s） | `CompileAvaloniaXamlTask` 1.16 s + 冷启动惩罚 | 知道成本；`dotnet build-server shutdown` 可复位 |
| G6 | `CMake`/`dotnet` 输出被截断或卡住 | 子进程继承 stdout | 重定向输出（探针脚本必须） |

### H. 测试与自检

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| H1 | 190 个测试 500 ms 全绿（假绿） | `SelfTest` 丢弃了 `Task` | `if (result is Task t) t.GetAwaiter().GetResult();` |
| H2 | 断言期望值写错 | 手算错（`0.35+0.2=0.55` 写成 0.4） | 断言里写算式；先跑一次看实际值再定期望 |
| H3 | 无头截图与不传壁纸时逐像素相同 | `--wallpaper` 只改 VM 状态，不进 `MainWindow` 壁纸图层 | 壁纸正确性用组件级断言（`WallpaperScaler` + 像素采样） |
| H4 | 自检取到错误的提示条 | `GetVisualDescendants().OfType<FAInfoBar>().FirstOrDefault()` 先找到外壳那条 | 按 `DataContext` 过滤再取 |
| H5 | 探针给出假阴性 | DWM 原生动画只改视觉不改窗口矩形，采 `GetWindowRect` 数不到中间尺寸 | 用屏幕像素面积时间序列（`probe-native-anim.ps1`） |
| H6 | 探针断言随机变红 | 采样窗口太短 + 孤立尖峰 | 判据用 P95/尖峰双阈值 |

### I. PowerShell / 脚本 / 环境

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| I1 | `Start-Process` 卡死 | 无资源管理器会话走 `ShellExecuteEx` | 用 `probe-common.ps1` 的 `Start-HeliumApp` |
| I2 | `$hit[-1]` 取到单个字符，`.Trim()` 报错 | 单元素管道结果不是数组 | `@($hit)[-1]` |
| I3 | 含中文 `.ps1` 报编码错/乱码 | 保存成 UTF-8 无 BOM | UTF-8 **带 BOM** + CRLF |
| I4 | `powershell -File` 被拦（`UnauthorizedAccess`） | 执行策略 | 用 `pwsh -NoProfile -File` |
| I5 | `GCHandle.Alloc(byte[])` 报 `Object contains references` | PowerShell 把返回值装箱成 `object[]` | `Marshal.AllocHGlobal` + `Marshal.Copy` |
| I6 | 位图行错位 | `Format32bppArgb` 的 stride > width×4 | 所有像素操作带 stride |
| I7 | `echo "... exit=$?"` 误报 | `$?` 被命令替换污染 | 先存变量再打印 |
| I8 | 量到的窗口尺寸是假的（1147×704） | PowerShell 默认 DPI 不感知 | `SetProcessDpiAwarenessContext(-4)` |
| I9 | 外部 `\| tail` 等不到结束 | 子进程继承 stdout 句柄 | `RedirectStandardOutput/Error = true` |

### J. 工程与文档

| # | 症状 | 根因 | 规则 |
| --- | --- | --- | --- |
| J1 | `git add` 把 bin/obj 全加进去 | 先 add 后写 `.gitignore` | 先 `.gitignore` 再 `git add -A` |
| J2 | 每个文件都报 `LF will be replaced by CRLF` | `core.autocrlf` 与 `.gitattributes` 冲突 | `core.autocrlf=false` + `* text=auto eol=lf` |
| J3 | 指纹基线里混进临时沙盒 | 探针把沙盒建在仓库内 | 沙盒用 `%TEMP%`；`-Update` 后复查 diff |
| J4 | 改版本号时历史数字被一起改掉 | 全局替换 | 只改当前值，`PROGRESS.md` 历史小节不动 |
| J5 | 文档数字与实测不一致 | 改了代码没同步文档 | 每轮门禁后同步 `ARCHITECTURE.md` 头部快照 |
| J6 | `--output` / `--wallpaper` 传值被静默忽略 | 只认 `--name=value` | 命令行一律 `=` 连接 |
| J7 | 本地构建产物 454 MB 进了仓库 | `dist/` 未忽略 | `dist/`、`Data/`、`*.db` 由 `.gitignore` 挡住 |

---

## 18. 阶段计划（仅计划）

### P0 · 工程基建（1–2 人日）

1. `Directory.Build.props` 版本 → 0.5.0-alpha（§1.1 全表）。
2. 加 `LICENSE`、`THIRD-PARTY-NOTICES.md`、`LICENSES/`、`CONTRIBUTING.md`；`tools/check-license-headers.py`。
3. 代码按 §3.1 改名（`git mv` + `slnx` + 16 个探针路径 + 文档命令）。
4. 移除 `FluentAvaloniaUI`（先只替换 `App.axaml` 的主题入口，控件替换留到 P4 —— 此阶段界面会变形，**允许**，但必须单独提交以便回滚）。
5. 删除 `PluginHost` 与 7 处注入，`IHeliumPlugin` 改 `#if HELIUM_PLUGINS`。
6. 门禁跑通（build + SelfTest + 15 checks + 指纹基线重建）。
   **退出条件**：构建 0 警告、SelfTest 190/190、15 项全绿、`git status` 干净。

### P1 · Domain + Application（3–5 人日）

- 按 §3.2 拆目录、冻结接口；把 `Core` 里的规则类原样迁入（**先只搬不改**）。
- 190 个测试跟着搬家，断言不变（这一步是"结构化重构"，行为必须零变化）。
- 引入 `IClock`、`Result<T>`；删掉 `PluginHost` 残留。
  **退出条件**：SelfTest 190/190；`Domain` 的 `using` 里没有 Avalonia/EF/Npgsql（用 arch 测试断言）。

### P2 · Infrastructure：PostgreSQL（4–6 人日）

- `UseNpgsql` + `IEntityTypeConfiguration` 全量重写映射；`Migrations` 初版。
- 备份 v2（JSONL）；旧 SQLite 一次性导入通道。
- `--db-check` 新增；docker-compose 开发库。
  **退出条件**：空库迁移成功；6 表 CRUD 往返；备份导出→导入→再导出字节等价（除时间戳）。

### P3 · Material 主题骨架（1–2 人日）

- `MaterialTheme` 替换 `FluentAvaloniaTheme`；`PaletteHelper` 替换 `CustomAccentColor`。
- 令牌映射表（§6.3）；`--theme-check` 新增。
  **退出条件**：明暗切换、强调色、卡片不透明度全生效（像素断言）。

### P4 · 控件替换（3–4 人日）

- 逐页替换 147 处 FA 控件；`ColorPickerControls` → `ColorPicker`；时钟 → `Material Clock`。
- 每页做完跑 `--layout-check` + 截图归档 + 像素对比。
  **退出条件**：七页视觉验收通过；`CUSTOM_CONTROLS.md` 只剩 4 个登记项。

### P5 · Behaviors 与外壳回归（2–3 人日）

- 按 §8 落地；8 个 Behavior + `--behavior-check`。
- 外壳回归：DWM 过渡、开场遮罩、图标轨、抓手（跑 `probe-native-anim` / `probe-intro` / `probe-rail-anim` / `probe-minimize-restore`）。
  **退出条件**：探针数字不低于 0.3.5。

### P6 · 验证与性能（1–2 人日）

- `--copy-check`；性能预算表（§15.3）写进 `--perf-check` 断言。
  **退出条件**：预算全部达标或已记录偏差理由。

### P7 · 打包与收尾（1 人日）

- `dotnet publish -r win-x64 --self-contained`；zip 命名 `HeliumTodo-0.5.0-win-x64.zip`。
- 文档收尾：`ARCHITECTURE.md` 订正、`TECH_STACK.md` 指向本文档、`PROGRESS.md` 新节、`UI_TEXT_MAP.md` 重生成、指纹基线。
  **退出条件**：发布包能在一台干净 Windows 上启动并完成一次待办 CRUD + 一次备份导出。

---

## 19. 风险登记表

| # | 风险 | 可能性 | 影响 | 应对 |
| --- | --- | --- | --- | --- |
| R1 | `Avalonia.Xaml.Behaviors` 没有可用的 12.x 包（源码打包失败） | 中 | 高（交互层方案改道） | 方案 B（适配层 + 代码挂载）；P0 第一天验证，最坏退回 code-behind |
| R2 | 自建 attached property 无法被 XAML 解析 | 中 | 中 | 改用代码挂载（`Interaction.Attach`），XAML 不出现 Behavior |
| R3 | Material `Expander` 行模板无法复现"一行一条设置 + 右侧控件"观感 | 中 | 中 | 保留自建行模板（仍是"组合现有控件"，不违反控件优先） |
| R4 | Material 圆角/阴影体系与 18 px 卡片冲突 | 高 | 低 | 保留自研令牌，只让 Material 管字体/按钮/输入框 |
| R5 | PostgreSQL 需要服务端，破坏"本地优先/断网可用" | 高 | 高 | 产品上明确"需要 PostgreSQL"；设置页写清；启动页给可操作的错误页 |
| R6 | 190 个测试迁移期间出现"测试通过但行为变了" | 中 | 高 | P1 只搬不改；P2 起每改一处都保留旧断言做对照 |
| R7 | 备份格式 v2 与旧包不兼容 | 高 | 中 | 保留一次性旧包导入通道；`formatVersion` 显式校验 |
| R8 | 时间语义搞错导致数据整体平移 | 中 | 高 | 迁移前后跑 6 条时间往返断言（含跨月/跨年/DST 日） |
| R9 | 控件替换后性能回退（Material 模板层数更多） | 中 | 中 | `--perf-check` 每阶段跑；`InteractionQuality` 继续生效 |
| R10 | GPL 头 + 改名让历史 blame 断裂 | 高 | 低 | 头两行单独一次提交；改名用 `git mv` 保留历史 |

---

## 20. 文档索引与维护约定

| 文档 | 职责 | 0.5.0 期间的动作 |
| --- | --- | --- |
| `DEVELOPMENT_SPEC.md`（本文） | 技术栈与规范的最高效力文件 | 每次栈决策变更都改这里，并记日期 |
| `ARCHITECTURE.md` | 架构说明 + 历史决策记录 | 只往前走：订正错误结论，**不改历史数字** |
| `PROGRESS.md` | 逐轮开发记录 | 追加新节，禁改历史节 |
| `TECH_STACK.md` | 旧技术栈快照 | 头部加"已被 DEVELOPMENT_SPEC.md 取代" |
| `UI_TEXT_MAP.md` | 界面文案 → 代码位置 | 每次界面改动后重跑生成器 |
| `AGENTS.md` | 给 AI 的硬规则 | 同步：项目名、版本、门禁命令、`--db-check`/`--theme-check`/`--behavior-check` |
| `DEVELOPMENT_PLAYBOOK.md` | 每轮流程 | 同步门禁表与探针清单 |
| `CUSTOM_CONTROLS.md`（新建） | 自绘控件登记 | 每加一个自绘控件加一行 |
| `THIRD-PARTY-NOTICES.md`（新建） | 依赖许可 | 每次加依赖必须更新 |

**维护三条**：

1. 数字必须来自实测命令输出，不许估算；写进文档要同时写"怎么复现"。
2. 任何"我们试过但没用"的结论要留在文档里（连同反例），否则下一个人会再试一次。
3. 文档里的版本号、项目名、命令，与仓库不一致时**以仓库为准并立刻订正文档**。

---

## 附录 A：0.5.0 开工第一天的验证清单（30 分钟）

```powershell
# 1) Behaviors 到底能不能用（决定 §8 走 A 还是 B）
git clone --depth 1 https://github.com/wieslawsoltes/Xaml.Behaviors tools/_ext/Xaml.Behaviors
dotnet pack tools/_ext/Xaml.Behaviors/src/Avalonia.Xaml.Interactivity -c Release -o packages/local

# 2) Material 能否装上并渲染（不做控件替换）
dotnet add HeliumTodo.Desktop package Material.Avalonia --version 3.21.0

# 3) PG 能否连上
docker run --rm -e POSTGRES_PASSWORD=pg -p 5432:5432 postgres:17-alpine

# 4) 旧包是否还能装（备选：Avalonia 12.1.5 + Material）
```

三项任一失败，按 §19 的对应风险条目改道，**不要**硬推。

## 附录 B：本规格与 0.3.5-alpha 的差异（给评审看的一张表）

| 变化 | 影响面 | 可回滚性 |
| --- | --- | --- |
| 项目改名 7 个 | 全部脚本/文档/门禁 | 高（一次 commit 可 revert） |
| 去 FluentAvaloniaUI | 147 处控件 + 主题入口 | 中（包还在，主题入口一处） |
| 换 Material | 全站视觉 | 高（包 + `App.axaml`） |
| 换 PostgreSQL | 数据层 + 备份 + 测试 | 低（数据迁移不可逆，需先导出旧库） |
| Behaviors | 10 处事件绑定 | 高（可退回 code-behind） |
| 插件禁用 | 7 处注入 + 设置页一组 | 高（源码保留） |
| GPL-3.0 | 每个文件头 + 分发 | 不可逆（许可一旦发布无法收回） |

> **最后一条提醒**：许可证是唯一"发布后收不回"的决定。
> 在打第一个 0.5.0 发布包之前，把 `LICENSE`、`THIRD-PARTY-NOTICES.md`、
> 每个文件的两行 SPDX 头全部就位；发布之后再补，等于对已发出的副本无权追认。