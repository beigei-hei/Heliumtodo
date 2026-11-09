# Helium Todo · 项目全景

> ⚠️ **历史快照（v0.3.5-alpha）**。0.5.0 的分层与技术栈以
> [`DEVELOPMENT_SPEC.md`](DEVELOPMENT_SPEC.md) 和 [`ARCHITECTURE.md`](ARCHITECTURE.md) 为准：
> 项目已改名 `Domain` / `Application` / `Infrastructure` / `Desktop`，主存储是 PostgreSQL 17，
> 界面是 Material.Avalonia 3.21（FluentAvaloniaUI 已移除）。下文保留当时的记录，不再逐行更新。

> 这份文档回答三个问题：**项目里有哪些文件**、**依赖了哪些库**、**程序是怎么搭起来的**。
>
> 生成日期：2026-10-15（2026-10-24 更新到 Avalonia 12）　·　版本：**v0.3.5-alpha**　·　目标框架：**net10.0**
>
> 项目自己的进度与踩坑记录见 [`PROGRESS.md`](PROGRESS.md)，
> 本文只做「结构说明」，不重复那边的历史。
>
> **改代码之前请先读** [`DEVELOPMENT_PLAYBOOK.md`](DEVELOPMENT_PLAYBOOK.md)（开发流程、技能集采用方案、质量门禁、交接清单）
> 与 [`AGENTS.md`](AGENTS.md)（给 AI 的硬规则）——那两份讲「怎么干活」，本文讲「东西长什么样」。

---

## 目录

1. [一句话概括](#一句话概括)
2. [完整文件树](#完整文件树)
3. [依赖库](#依赖库)
4. [程序主架构](#程序主架构)
5. [三大核心功能怎么互相联系](#三大核心功能怎么互相联系)
6. [两个扩展点](#两个扩展点)
7. [构建与验证流水线](#构建与验证流水线)
8. [配置文件与数据存放位置](#配置文件与数据存放位置)
9. [改代码前该看哪个文件](#改代码前该看哪个文件)

---

## 一句话概括

一个**本地优先**的「日程 + 专注 + 待办」桌面工具。
C# 12 / .NET 10，界面用 Avalonia 12.1 + FluentAvaloniaUI 3.1（Fluent 2 设计），
数据存 SQLite，**不联网、不上传**。

分层很干净，而且是**单向依赖**：

```
HeliumTodo.Desktop  ──▶  HeliumTodo.Infrastructure  ──▶  HeliumTodo.Domain
 （界面）              （存储）              （纯逻辑，无任何外部依赖）
```

`Domain` 不引用 Avalonia、不引用 EF Core —— 所以它的单元测试跑得飞快（190 个测试约 2 秒，纯算法那部分只占几十毫秒），
以后想换界面框架或换数据库，动的都是外层，`Domain` 一行不用改。

---

## 完整文件树

只列**源文件**。`bin/`、`obj/`、`output/`、`.idea/`、`.vs/` 是编译产物和编辑器缓存，已省略。

```
Helium todo demo/
├── .idea/                              JetBrains(Rider) 项目配置
├── .vs/                                Visual Studio 项目配置
├── Data/                               ← 便携模式的数据目录（见文末说明）
│   ├── backups/
│   │   └── HeliumTodo-backup-*.zip     自动备份，保留最近 7 份（数据库+配置+壁纸打包）
│   ├── config.json                     配置副本（便携模式下用这份）
│   └── helium.db                       SQLite 数据库（76 KB）
│
├── HeliumTodo.Desktop/                     【界面层】Avalonia 桌面程序
│   ├── Controls/                       自绘控件
│   │   ├── ClockControls.cs            ClockFace(时钟表盘) + ProgressRing(圆环进度)
│   │   ├── TimelineGrid.cs             24 小时时间轴的网格与刻度
│   │   ├── WidthObserverPanel.cs       能知道自身宽度的 Panel（错位布局要用）
│   │   └── ColorPickerControls.cs      方形选色 + 通用渐变滑块（调色板用）
│   ├── Converters/
│   │   └── CommonConverters.cs         6 个值转换器（优先级→颜色、布尔→可见性…）
│   ├── Services/
│   │   ├── PluginHost.cs               插件宿主：扫描、加载、注册主题与页面
│   │   ├── ThemeService.cs             主题应用（换肤 + 强调色）
│   │   ├── WallpaperDecoder.cs         ★ 壁纸解码：SkiaSharp 拆帧 / 缩放 / 模糊 / 取色
│   │   └── WallpaperService.cs         壁纸播放：逐帧定时切换，暴露当前帧
│   ├── Styles/
│   │   ├── DesignTokens.axaml          ★ 设计令牌：颜色/间距/圆角/字号全在这
│   │   └── Controls.axaml              控件样式（按钮、卡片、标题…）
│   ├── ViewModels/                     ← MVVM 的 VM 层
│   │   ├── AppState.cs                 ★ 全局公告板：三大功能靠它联动
│   │   ├── ShellViewModel.cs           导航栏 + 页面切换
│   │   ├── DashboardViewModel.cs       「今日工作流」（门面页，含时间轴）
│   │   ├── ScheduleViewModel.cs        「日程」
│   │   ├── TodoViewModel.cs            「待办事项」
│   │   ├── FocusViewModel.cs           「专注」
│   │   ├── StudyRoomViewModel.cs       「自习室」
│   │   ├── SettingsViewModel.cs        「设置」
│   │   ├── ColorPaletteViewModel.cs    RGB ↔ HSV ↔ 十六进制 三方同步
│   │   ├── TimelineEntryViewModel.cs   时间轴上一个小方块的数据
│   │   └── ViewModelBase.cs            VM 基类
│   ├── Windowing/                      ← 窗口相关的四小块（和页面无关，只服务 MainWindow）
│   │   ├── WindowChrome.cs             样式位 + WM_NCCALCSIZE（无边框仍可拖边、最大化不盖任务栏）
│   │   ├── WindowAnimator.cs           最大化 / 还原 / 打开动效（含 Settle() 收尾）
│   │   ├── WindowAnimationMath.cs      缓动与矩形插值（纯函数，可单测）
│   │   └── WindowResizeGrips.cs        8 个抓手 + 指针捕获 + DPI 换算
│   ├── Views/                          ← MVVM 的 V 层（每个页面 = .axaml + 代码后置 .cs）
│   │   ├── MainWindow.axaml            窗口外壳（壁纸三层 + 8 个改大小抓手）
│   │   ├── MainWindow.axaml.cs         只做装配：把 Windowing/ 里的三件套接起来
│   │   ├── ShellView.axaml             自绘外壳：标题栏 + 左侧图标轨 + 内容区
│   │   ├── ShellView.axaml.cs
│   │   ├── DashboardView.axaml         今日工作流页（316 行 XAML，最复杂）
│   │   ├── DashboardView.axaml.cs
│   │   ├── ScheduleView.axaml          日程页
│   │   ├── ScheduleView.axaml.cs
│   │   ├── TodoView.axaml              待办页
│   │   ├── TodoView.axaml.cs
│   │   ├── FocusView.axaml             专注页
│   │   ├── FocusView.axaml.cs
│   │   ├── StudyRoomView.axaml         自习室页
│   │   ├── StudyRoomView.axaml.cs
│   │   ├── SettingsView.axaml          设置页
│   │   └── SettingsView.axaml.cs
│   ├── App.axaml                       应用级 XAML：主题、资源字典、转换器实例
│   ├── App.axaml.cs                    ★ 启动装配：读配置→DI→建库→主题→插件→开窗
│   ├── Program.cs                      入口点（含命令行参数处理）
│   ├── ViewLocator.cs                  VM→View 的自动映射（约定优于配置）
│   ├── app.manifest                    Windows 清单（DPI 感知等）
│   └── HeliumTodo.Desktop.csproj
│
├── HeliumTodo.Domain/                    【领域内核】纯逻辑，零外部依赖
│   ├── Models/
│   │   ├── Enums.cs                    优先级 / 时段 / 状态 等枚举 + 中文显示名
│   │   ├── ScheduleItem.cs             日程模型
│   │   ├── TodoItem.cs                 待办模型
│   │   ├── FocusSession.cs             专注记录模型
│   │   └── StudyRoom.cs                自习室 / 成员 / 消息 模型
│   ├── Services/                       ★ 项目的算法都在这
│   │   ├── DayTimelineBuilder.cs       ★★ 最核心：建时间轴 + 冲突检测 + 错位分列
│   │   ├── DayTimeline.cs              时间轴数据结构
│   │   ├── Conflict.cs                 冲突模型（三级严重程度）
│   │   ├── RecurrenceExpander.cs       重复规则解析（"weekly:1,3,5"）
│   │   ├── FocusTimerService.cs        专注计时（纯逻辑，由外部驱动 tick）
│   │   └── StudyRoomService.cs         自习室业务逻辑（含联机房间、成员、消息）
│   ├── Configuration/
│   │   ├── AppConfig.cs                ★ 配置模型 + 读写 JSON + 便携模式回退
│   │   └── AccentSources.cs            强调色来源：主题 / 壁纸 / 自定义
│   ├── Plugins/
│   │   └── IHeliumPlugin.cs            插件接口 + 宿主上下文接口
│   ├── Networking/                     ★ 联机（局域网 P2P，无中心服务器）
│   │   ├── PeerEndpoint.cs             地址解析：IPv4 / IPv6 / 主机名 / 带端口
│   │   └── PeerMessage.cs              线协议：一行一条 JSON + 编解码
│   ├── Imaging/
│   │   └── ApngParser.cs               ★ 把 APNG 拆成独立帧（Skia 不认 APNG）
│   ├── Theming/
│   │   ├── IHeliumTheme.cs             主题接口 + ThemeRegistry
│   │   ├── BuiltInThemes.cs            内置主题列表
│   │   ├── AccentColor.cs              HSL ↔ RGB、十六进制
│   │   └── WallpaperAccentExtractor.cs ★ 从壁纸里挑主色调（按色相分桶投票）
│   ├── Backup/                        ★ 备份格式（纯格式，不认识 EF Core）
│   │   ├── BackupFormat.cs             格式 ID / 条目名 / 文件名规则 / 版本号
│   │   ├── BackupManifest.cs           清单模型 + 序列化
│   │   ├── BackupPackageWriter.cs      写 .zip（先写 .tmp 再改名）
│   │   ├── BackupPackageReader.cs      读 .zip + 逐项校验 SHA256
│   │   └── BackupInspection.cs         给界面看的「这份包能不能导」
│   ├── VersionInfo.cs                  版本号（读程序集特性）
│   └── HeliumTodo.Domain.csproj
│
├── HeliumTodo.Infrastructure/                    【存储层】EF Core + SQLite + 联机传输
│   ├── HeliumDbContext.cs              6 张表的映射
│   ├── DataServiceCollectionExtensions.cs   AddHeliumData() 一行注册
│   ├── DatabaseInitializer.cs          建表 / 首次灌示例数据
│   ├── DataBackupService.cs            ★ 导出 / 导入 / 自动备份（唯一搬数据的地方）
│   ├── SeedData.cs                     示例数据（故意造出重叠，好演示错位展示）
│   ├── ScheduleRepository.cs           日程仓储
│   ├── TodoRepository.cs               待办仓储
│   ├── FocusRepository.cs              专注仓储
│   ├── StudyRoomRepository.cs          自习室仓储
│   ├── LanPeerTransport.cs             ★ 局域网传输：双栈 TCP 监听 + UDP 房间广播发现
│   ├── LocalOnlyPeerTransport.cs       本地模式（未联网时的占位实现）
│   └── HeliumTodo.Infrastructure.csproj
│
├── HeliumTodo.Tests/                   【单元测试】xunit，139 个用例
│   ├── DayTimelineBuilderTests.cs      ★ 20 KB，覆盖错位算法与冲突检测
│   ├── NetworkingTests.cs              ★ 地址解析 + 编解码 + 真实 TCP 回环
│   ├── ApngParserTests.cs              APNG 拆帧（手写最小 APNG 当素材）
│   ├── AccentColorTests.cs             HSL 换算
│   ├── WallpaperAccentExtractorTests.cs 取色 + 强调色来源规则
│   ├── FocusTimerServiceTests.cs       计时器状态机
│   ├── RecurrenceExpanderTests.cs      重复规则解析
│   ├── BackupTests.cs                  ★ 备份往返：导出 / 校验 / 替换 / 合并 / 安全网
│   ├── VersionInfoTests.cs             版本号格式
│   └── HeliumTodo.Tests.csproj
│
├── HeliumTodo.SelfTest/                【测试运行器】不依赖测试宿主进程
│   ├── Program.cs                      用反射直接调测试方法（受限环境也能跑）
│   └── HeliumTodo.SelfTest.csproj
│
├── HeliumTodo.Verification/             【界面自检】无头渲染成 PNG
│   ├── Program.cs                      6 个页面各渲染一张图（--shell 再加外壳），输出到 output/render/
│   └── HeliumTodo.Verification.csproj
│
├── output/                             渲染自检产出的 PNG
│
├── tools/
│   ├── check-file-integrity.ps1        158 个源文件的 SHA256 指纹校验
│   ├── file-hashes.json                指纹基线
│   ├── migrate-av12.ps1                Avalonia 11 → 12 + FA 3 的一次性迁移脚本（已执行完，留作记录）
│   └── capture-screenshot.ps1          启动真窗口并截图
│
├── Directory.Build.props               ★ 全局编译设置（版本号、语言特性）
├── Directory.Packages.props            ★ 中央包版本管理（所有 NuGet 版本在这）
├── global.json                         锁定 .NET SDK 10.0.401
├── HeliumTodo.slnx                     ★ 解决方案（XML 新格式，无 GUID）
├── NuGet.config                        包源配置（含离线缓存兜底）
├── AGENTS.md                           ★ 给 AI 的硬规则（改代码前必读）
├── DEVELOPMENT_PLAYBOOK.md             ★ 开发流程手册（技能集怎么用、质量门禁、交接清单）
├── PROGRESS.md                         开发进度与踩坑记录
└── PROJECT_OVERVIEW.md                 ← 本文件：文件树 + 依赖 + 架构
```

### 几个值得注意的点

| 文件 | 为什么重要 |
| --- | --- |
| `DayTimelineBuilder.cs` | 整个项目**最值钱的一段代码**。用户特别要求的「错位展示」和冲突检测都在这，单文件 20 KB，测试也 20 KB |
| `AppState.cs` | 三大功能联动的**唯一机制**。看懂它就理解了需求第 4 条 |
| `DesignTokens.axaml` | 所有颜色/间距/字号集中在这。想改配色只动这一个文件 |
| `Directory.Packages.props` | 所有 NuGet 版本集中管理。升级库只改这里 |
| `App.axaml.cs` | 启动装配顺序一目了然，是**理解项目的最佳入口** |

---

## 依赖库

### 版本管理方式

用的是 **中央包版本管理**（Central Package Management）：

- `Directory.Packages.props` 声明**所有**包的版本号
- 各 `.csproj` 里只写 `<PackageReference Include="Avalonia" />`，**不写版本**

好处：升级 Avalonia 只需改一处，不用翻 6 个 csproj。

### 直接依赖

| 项目 | NuGet 包 | 版本 | 用途 |
| --- | --- | --- | --- |
| **Domain** | *（无）* | — | **故意零依赖**：这样单元测试不用拉起任何框架 |
| **Infrastructure** | Microsoft.EntityFrameworkCore | 8.0.11 | ORM |
| | Microsoft.EntityFrameworkCore.Sqlite | 8.0.11 | SQLite 驱动 |
| | Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | DI 抽象 |
| | Microsoft.Extensions.Logging | 8.0.1 | 日志抽象 |
| **App** | Avalonia | 12.1.2 | 跨平台 UI 框架 |
| | Avalonia.Desktop | 12.1.2 | 桌面生命周期 |
| | Avalonia.Themes.Fluent | 12.1.2 | Fluent 基础主题 |
| | Avalonia.Fonts.Inter | 12.1.2 | Inter 字体 |
| | **Avalonia.HarfBuzz** | 12.1.2 | **Avalonia 12 新增的必需显式引用**：文字排版被拆成独立包 |
| | **FluentAvaloniaUI** | **3.1.0** | **Fluent 2 设计系统**（需求第 3 条） |
| | CommunityToolkit.Mvvm | 8.4.0 | MVVM 源生成器 |
| | Microsoft.Extensions.DependencyInjection | 8.0.1 | DI 容器 |
| | Microsoft.Extensions.Logging(.Debug) | 8.0.1 | 日志 |
| **Tests** | Microsoft.NET.Test.Sdk | 17.11.1 | 测试宿主 |
| | xunit | 2.9.2 | 测试框架 |
| | xunit.runner.visualstudio | 2.8.2 | VS/Rider 集成 |
| **SelfTest** | xunit | 2.9.2 | 复用测试特性（`[Fact]`） |
| **Verification** | Avalonia + Avalonia.Headless | 12.1.2 | 无头渲染 |
| | 其余同 App | | |

> **`Avalonia.Diagnostics` 已经不在了。** Avalonia 12 移除了这个包，
> 原来只在 Debug 配置引用它的那段也一起删了（代码里从没调用过 `AttachDevTools`）。

### 关键传递依赖（自动带进来的）

| 包 | 版本 | 怎么来的 |
| --- | --- | --- |
| SkiaSharp | 3.119.4 | Avalonia 的渲染后端；壁纸解码直接用它（**必须与 Avalonia 依赖的版本完全一致**） |
| HarfBuzzSharp | 8.3.1.3 | 文字排版 |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.6 | EF Core Sqlite 的底层 |
| **Avalonia.Controls.DataGrid** | **12.1.2** | FluentAvaloniaUI 要的 |
| **Avalonia.Controls.ColorPicker** | **12.1.2** | FluentAvaloniaUI 要的 |
| MicroCom.Runtime | 0.11.6 | FluentAvaloniaUI 要的 |

> ⚠️ **DataGrid / ColorPicker 这两个是我们没直接用、但必须显式钉版本的包。**
>
> 而且它们**卡住了整套的版本上限**：这两个包最高只发布到 **12.1.2**，
> 而主包 `Avalonia` 已经到 12.1.4。所以整套只能取交集，统一 12.1.2。
>
> 另外，如果不显式钉住，它们会按 FluentAvaloniaUI 声明的依赖落下来，
> 导致输出目录里**只有它俩是旧版本、其余都是新的** —— 这种「程序集混版」会让
> IDE 的语法分析跨版本解析失败，表现成莫名其妙的标红（但编译能过）。
>
> 所以 `Directory.Packages.props` 里必须同时有这几行：
> ```xml
> <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
> <PackageVersion Include="Avalonia.Controls.DataGrid" Version="12.1.2" />
> <PackageVersion Include="Avalonia.Controls.ColorPicker" Version="12.1.2" />
> ```
> 详细排查过程见 [`PROGRESS.md`](PROGRESS.md) 的「Avalonia 程序集混版」一节。

### 依赖关系图

```
                    ┌─────────────────────┐
                    │  HeliumTodo.Tests   │  xunit
                    └──────────┬──────────┘
                               │ 引用
                    ┌──────────▼──────────┐
   HeliumTodo.Domain  │    （被所有项目引用） │  ★ 零外部依赖
                    └──────────┬──────────┘
                     ┌─────────┴─────────┐
                     │                   │
          ┌──────────▼──────────┐  ┌─────▼───────────────┐
          │  HeliumTodo.Infrastructure    │  │ HeliumTodo.SelfTest │
          │  EF Core + SQLite   │  │ (复用 Tests 的代码)  │
          └──────────┬──────────┘  └─────────────────────┘
                     │
          ┌──────────▼──────────┐
          │  HeliumTodo.Desktop     │  Avalonia + FluentAvaloniaUI
          │  （界面 + 启动装配） │
          └──────────┬──────────┘
                     │ 引用
          ┌──────────▼──────────┐
          │ HeliumTodo.Verification │  无头渲染界面成 PNG
          └─────────────────────┘
```

---

## 程序主架构

### 启动顺序（`App.axaml.cs`）

理解这个项目最好的入口就是看启动装配，七步：

```
① 读配置          ConfigService.Load()          → 知道用户上次设置成什么样
② 建主题注册表    new ThemeRegistry()           → 内置主题先登记
③ 建依赖注入容器  BuildServiceProvider()        → 谁需要什么服务，自动给
④ 初始化数据库    DatabaseInitializer.Initialize() → 建表 + 首次灌示例数据 + 自动备份
⑤ 应用主题        themeRegistry.Apply(...)      → 套用上次选的主题
⑥ 加载插件        pluginHost.LoadPlugins()      → 扫描 plugins 文件夹
⑦ 开窗口          new MainWindow()              → 界面出来
```

> **一个容易忽略的细节**：第 ⑦ 步有类型判断 ——
> 只有「桌面生命周期」才开主窗口。
> 界面渲染自检走的是**无头模式**，它会自己建窗口，
> 这里再开一个就会在没有桌面的环境里崩掉。

### 分层职责

| 层 | 项目 | 该放什么 | **不该**放什么 |
| --- | --- | --- | --- |
| 领域内核 | `HeliumTodo.Domain` | 模型、算法、配置、扩展点接口 | 界面代码、EF Core、任何 NuGet 包 |
| 存储 | `HeliumTodo.Infrastructure` | 数据库映射、仓储、种子数据 | 界面代码、业务规则 |
| 界面 | `HeliumTodo.Desktop` | 视图、视图模型、自绘控件、样式 | 业务算法（应该在 Domain） |

这条边界是靠**单向依赖**强制保证的：`Domain` 想引用 Avalonia 都引用不到（没这个包）。

### MVVM 怎么落地的

```
        ┌──────────────┐  数据绑定   ┌──────────────┐
        │     View     │ ◀─────────  │  ViewModel   │
        │   (.axaml)   │  ─────────▶ │    (.cs)     │
        └──────────────┘  命令/事件   └──────┬───────┘
                                             │ 调用
                                      ┌──────▼───────┐
                                      │  Repository  │
                                      │   (仓储)      │
                                      └──────┬───────┘
                                             │
                                      ┌──────▼───────┐
                                      │   SQLite     │
                                      └──────────────┘
```

**几个刻意的选择**：

- **编译期绑定**：`AvaloniaUseCompiledBindingsByDefault=true`
  绑定写错在编译时就能查出来，而不是运行时才崩。
  配套地，`App.axaml.cs` 里移除了 Avalonia 默认的反射式绑定校验插件，
  否则同一个属性会被通知两次。

- **源生成器写 MVVM**：139 处 `[ObservableProperty]` / `[RelayCommand]`，
  编译时生成属性与命令，省掉大量样板代码。
  代价是 IDE 必须能跑源生成器才能正确解析（Rider 没问题）。

- **`ViewLocator` 约定映射**：`XxxViewModel` 自动找 `XxxView`，
  不用手写一堆 `DataTemplate`。

### 六层界面结构

```
MainWindow                 窗口（标题栏、尺寸）
  └─ ShellView             FANavigationView 导航外壳
       ├─ 今日工作流  →  DashboardViewModel   ← 门面页：概览 + 24 小时时间轴
       ├─ 日程        →  ScheduleViewModel
       ├─ 待办事项    →  TodoViewModel
       ├─ 专注        →  FocusViewModel       ← 含自绘时钟表盘
       ├─ 自习室      →  StudyRoomViewModel
       └─ 设置        →  SettingsViewModel    ← 配置文件的可视化入口
```

导航项是在 `ShellViewModel.BuildNavigation()` 里注册的，
**插件注册的页面也会自动追加到这里**。

---

## 三大核心功能怎么互相联系

这是需求第 4 条「三大核心功能互相联系」，靠 `AppState` 这块**公告板**实现：

```
                    ┌─────────────────────────┐
                    │       AppState          │
                    │  （全局共享 + 通知广播）  │
                    │                         │
                    │  event DataChanged      │
                    │  NotifyChanged(类型)     │
                    └────────┬────────────────┘
                             │ 谁改了数据就喊一声
        ┌────────────────────┼────────────────────┐
        │                    │                    │
   ┌────▼─────┐        ┌─────▼─────┐       ┌─────▼─────┐
   │  日程     │        │   待办     │       │   专注     │
   └────┬─────┘        └─────┬─────┘       └─────┬─────┘
        │                    │                    │
        │ 决定哪些时段是忙的   │ 安排到时间轴         │ 结束时长累加
        │                    │                    │
        └────────────────────┼────────────────────┘
                             ▼
                   ┌───────────────────┐
                   │   今日工作流       │
                   │  （时间轴 + 冲突） │
                   └───────────────────┘
```

具体三条链路：

| 链路 | 效果 |
| --- | --- |
| **待办 → 时间轴** | 给待办安排时间，它立刻以方块形式出现在 24 小时网格上 |
| **专注 → 待办** | 专注结束后，时长累加到关联的那条待办（显示「已专注 52 分钟」） |
| **日程 / 待办 / 专注 → 冲突检测** | 任意一项变动都会重算时间轴，重叠的时段被标红并列出冲突 |

`AppDataType` 是个 `[Flags]` 枚举，页面可以只订阅自己关心的那类变化：

```csharp
Timeline = Schedules | Todos | Focus    // 任意一项变了，时间轴都要重算
```

---

## 两个扩展点

需求第 8 条要求「编译期预留可扩展入口」，落成两个接口：

### ① 主题扩展 —— `IHeliumTheme`

```csharp
public interface IHeliumTheme
{
    string Id { get; }
    string DisplayName { get; }
    ThemeKind Kind { get; }
    string AccentColorHex { get; }
}
```

- `ThemeRegistry` 管注册与切换
- `BuiltInThemes.cs` 里是内置的几套
- **插件也能往里注册主题**，注册完自动出现在设置页的下拉框里

### ② 插件扩展 —— `IHeliumPlugin`

```csharp
public interface IHeliumPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    void Initialize(IHeliumHostContext host);   // 拿到宿主上下文
    void Shutdown() { }                          // 默认空实现
}
```

宿主给插件的上下文 `IHeliumHostContext` 提供：

| 能力 | 方法 |
| --- | --- |
| 读版本号 | `AppVersion` |
| 注册主题 | `Themes` |
| 写日志 | `Log(message)` |
| **注册新页面** | `RegisterPage(icon, title, factory)` ← 注册完自动进导航栏 |

### 还有第三个，为「联机自习」留的 —— `IPeerTransport`

```csharp
Task JoinRoomAsync(string roomCode, ...);
Task BroadcastStatusAsync(PeerStatus status, ...);
```

现在是 `LocalOnlyPeerTransport`（**不联网**的占位实现）。
以后实现无中心服务器联机时，**只要在 `App.axaml.cs` 里换掉那一行注册**：

```csharp
services.AddSingleton<IPeerTransport, LocalOnlyPeerTransport>();
//                                     ^^^^^^^^^^^^^^^^^^^^^ 换成真正的 P2P 实现
```

业务代码一行都不用改。

---

## 构建与验证流水线

四道关卡，每道都能独立跑（详细流程与验收标准见 [`DEVELOPMENT_PLAYBOOK.md`](DEVELOPMENT_PLAYBOOK.md)）：

```powershell
# ① 编译（0 警告 0 错误是当前的基线）
dotnet build HeliumTodo.slnx -c Debug

# ② 单元测试：168 个用例，约 2.8 秒
#    注意这里用的是自研的 SelfTest 运行器而不是 dotnet test ——
#    受限环境（沙箱/精简 CI）里 dotnet test 要启动测试宿主进程会被拒绝
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll

# ③ 界面渲染自检：6 个页面各渲染成 PNG，输出到 output/render/
#    不需要桌面环境，编译通过 ≠ 界面没问题，这一步才验证「画得对不对」
#    加 --shell 会额外渲染整个主窗口（导航栏 + 内容区）成 外壳.png
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --shell

# ④ 源文件完整性：166 个文件的 SHA256 指纹
#    防止批量脚本改坏注释或字符串（编译器查不出来的那种）
pwsh tools\check-file-integrity.ps1

# ⑤ 十二项行为自检（每项都要求 exit=0 且 fails=0）
#    shell / refresh / room / backup / folder / timeline / reminder /
#    window / restart-prompt / notice / appearance / layout
$rc = "HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll"
foreach ($c in "--shell-check","--refresh-check","--room-check","--backup-check",
               "--folder-check","--timeline-check","--reminder-check","--window-check",
               "--restart-prompt-check","--notice-check","--appearance-check","--layout-check") {
  dotnet $rc $c
}
```

十二项自检各管一块：`--refresh-check` 三大功能互相联动、`--timeline-check` 时间轴布局与冲突红框、
`--layout-check` 六个页面的分区是否还一致（左 12 / 顶 64 / 卡片间不漏底色），
`--notice-check` / `--appearance-check` 管提示条与明暗玻璃，`--restart-prompt-check` 管改渲染后端时的确认弹窗。
界面改动的视觉验证一律走「无头渲染 PNG + 像素统计」，不靠肉眼（见 playbook 第 5 节）。

### 为什么需要自研的 SelfTest 运行器

正常的 `dotnet test` 要启动一个**测试宿主进程**，再由它附加到父进程。
这在某些受限环境里会被系统拒绝（报「拒绝访问」）。

`HeliumTodo.SelfTest` 改成用**反射直接调用测试方法**，不启动任何额外进程，
因此在任何环境下都能跑。

⚠️ 它和 `HeliumTodo.Tests` 用的是**同一批测试代码**，不是另写一份 ——
所以两者结果一致。

---

## 配置文件与数据存放位置

### 两个文件，职责分开

| 文件 | 内容 | 格式 |
| --- | --- | --- |
| `config.json` | 所有设置（主题、时长、阈值…） | **纯文本 JSON，可以用记事本直接改** |
| `helium.db` | 日程 / 待办 / 专注记录 / 自习室 | SQLite |

**分开存是刻意的**：设置可以随手改、随手备份、拷给别人；
数据是正经的用户资产，不该混在一起。

### 想换电脑：一个 .zip 全带走

「设置 → 数据与文件 → 导出」会把**数据库 + 配置 + 壁纸**打成一个压缩包：

```
HeliumTodo-backup-20261027-183012.zip
├── manifest.json           清单：版本、导出时间、每项 SHA256、几条数据
├── data/helium.db          数据库快照（VACUUM INTO 产出，WAL 里的改动不会丢）
├── data/config.json        配置
└── assets/wallpaper.<扩展> 壁纸（没设就没有这一项）
```

到了新机器上「导入」这一个文件就行，会问一次**替换**还是**合并**：

| 模式 | 会发生什么 |
| --- | --- |
| 替换 | 清空现有数据，换成备份里的（**删之前先自动备份当前数据**） |
| 合并 | 保留现有数据，备份里的追加进来（主键重新编号，房间码重复就跳过） |

启动时的自动备份（`backups/` 里保留最近 7 份）产出的也是同一种 `.zip`，
所以「自动备份」的文件也能直接拿去导入 —— 这就是「统一格式」的好处。

### 放在哪个目录 —— 两级回退

```
① 首选  %AppData%\Helium Todo\
             │
             │ 如果这个位置不可写（装在只读目录、U 盘、权限受限…）
             ▼
② 回退  程序旁边的 Data\        ← 叫「便携模式」
```

程序启动时会实际**试写一次**来判断能不能用，而不是猜。
用便携模式时界面上会明确提示，并说明回退原因。

本项目仓库里的 `Data/` 目录就是便携模式的落点。

### 命令行参数

```
--version              看版本号
--help                 看所有参数
--self-check           自检：加载界面并检查有没有错误，然后退出
--screenshot out.png   把界面渲染成 PNG
--tree                 顺便打印可视化树（排查「控件怎么没显示」时很有用）
```

---

## 改代码前该看哪个文件

按「我想改什么」索引：

| 我想改… | 去看这个文件 |
| --- | --- |
| **错位展示 / 冲突检测算法** | `HeliumTodo.Domain/Services/DayTimelineBuilder.cs` ⭐ 最核心 |
| 时间轴的显示效果 | `HeliumTodo.Desktop/Controls/TimelineGrid.cs` + `Views/DashboardView.axaml` |
| 时钟表盘 | `HeliumTodo.Desktop/Controls/ClockControls.cs` |
| **配色 / 字号 / 圆角 / 间距** | `HeliumTodo.Desktop/Styles/DesignTokens.axaml` ⭐ 全在这 |
| 控件外观（按钮、卡片…） | `HeliumTodo.Desktop/Styles/Controls.axaml` |
| **三大功能怎么联动** | `HeliumTodo.Desktop/ViewModels/AppState.cs` ⭐ |
| **备份 / 导出 / 导入** | `HeliumTodo.Infrastructure/DataBackupService.cs` ⭐ 唯一搬数据的地方；格式在 `Domain/Backup/` |
| 启动装配流程 | `HeliumTodo.Desktop/App.axaml.cs` |
| 导航栏有哪些页面 | `HeliumTodo.Desktop/ViewModels/ShellViewModel.cs` 的 `BuildNavigation()` |
| 数据库表结构 | `HeliumTodo.Infrastructure/HeliumDbContext.cs` |
| 示例数据 | `HeliumTodo.Infrastructure/SeedData.cs` |
| 配置项有哪些 | `HeliumTodo.Domain/Configuration/AppConfig.cs` |
| **升级 NuGet 包版本** | `Directory.Packages.props` ⚠️ 注意盯着 DataGrid/ColorPicker 别混版 |
| 版本号 | `Directory.Build.props` 的 `<Version>` |
| 加新页面 | 1. 写 ViewModel（继承 `ViewModelBase`）<br>2. 写 View（`XxxView.axaml`，名字要和 VM 对应）<br>3. 在 `ShellViewModel.BuildNavigation()` 里注册 |
| 加新算法 | 放 `HeliumTodo.Domain/Services/`，然后去 `HeliumTodo.Tests/` 加测试 |

---

## 附：本项目**没有**用到的东西

避免后来的人疑惑「为什么没看到 X」：

| 没有 | 原因 |
| --- | --- |
| `.editorconfig` | 代码风格靠 `DesignTokens` 和评审保持一致；如果多人协作建议补一个 |
| Git 仓库 | 2026-11-02 已初始化：`main` 是发布线、`dev` 是开发线，首个提交打了标签 `v0.3.5-alpha`；`.gitignore` 挡掉 `bin/`/`obj/`/`dist/`/`Data/`/`output/`，`.gitattributes` 统一 LF |
| `dotnet test` | 受限环境跑不了测试宿主进程，改用自研的 `SelfTest` 运行器 |
| 网络请求 | 需求就是「本地优先」。联机自习接口已留（`IPeerTransport`）但【暂缓】 |
| 遥测 / 崩溃上报 | 同上，不联网 |
