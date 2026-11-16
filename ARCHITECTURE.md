# Helium Todo · 程序架构

> **一句话概括**：单进程 Windows 桌面应用，四层结构（表现 / 应用服务 / 领域 / 存储），
> 数据落在本地 SQLite，可选局域网 P2P 联机，界面与业务通过 MVVM 彻底解耦。
>
> 本文所有结构、类名、方法签名、行数均**取自当前代码实际内容**（2026-11-03 核对）。
>
> 开发流程（每轮怎么走、用哪些技能、质量门禁）见 [`DEVELOPMENT_PLAYBOOK.md`](DEVELOPMENT_PLAYBOOK.md)，
> 给 AI 的硬规则见 [`AGENTS.md`](AGENTS.md)。本文只描述「现在的代码长什么样」，讲流程的不在这里。

---

## 0. 快照

| 项目 | 值 |
| --- | --- |
| 版本 | `0.5.0-alpha` |
| 目标框架 | `net10.0`（.NET 10 / C# 14） |
| UI 框架 | Avalonia **12.1.5** + FluentAvaloniaUI **3.1.0**（设置页分组与提示条自绘） |
| MVVM | CommunityToolkit.Mvvm 8.4.0（源生成器） |
| 数据 | EF Core 10.0.14 + Npgsql 10.0.3 / PostgreSQL 17（只用 Migration） |
| 图像 | SkiaSharp 3.119.4（Avalonia 传递依赖，必须版本一致） |
| 联机 | 原生 `System.Net.Sockets`，TCP + UDP 广播，无中心服务器 |
| 渲染 | 默认 **Auto**（Avalonia 自带，走 ANGLE→D3D11）；设置页「底层设置」或 `HELIUM_RENDER` 可切软件渲染 / OpenGL / Vulkan（见 §18.2） |
| 规模 | **33 631 行** · 137 个源文件（`.cs` + `.axaml`，不含 `bin`/`obj`） |
| 测试 | **190** 个单元测试 + 7 项渲染自检 + 15 个 `--check` 专项自检 |
| 窗口动画 | 打开时遮罩淡出（自己做）；最小化 / 最大化 / 还原交给 DWM（见 §5.2） |

### 各项目规模

只数 `.cs` 与 `.axaml`（`bin`/`obj`/`.csproj` 不算）：

| 项目 | 文件 | 行数 | 职责 |
| --- | ---: | ---: | --- |
| `HeliumTodo.Domain` | 42 | 6 063 | 领域模型 + 纯算法 + 备份格式 + 提醒调度 + 窗口过渡规则（零 UI 依赖、零数据库依赖、零包引用） |
| `HeliumTodo.Infrastructure` | 13 | 3 588 | EF Core 上下文、仓储、种子数据、网络传输、备份服务 |
| `HeliumTodo.Desktop` | 59 | 16 543 | Avalonia 界面、ViewModel、自绘控件、应用服务、窗口封装 |
| `HeliumTodo.Tests` | 21 | 3 520 | **190** 个 xunit 测试 |
| `HeliumTodo.SelfTest` | 1 | 160 | 反射式测试运行器（绕过被拦的 `dotnet test`） |
| `HeliumTodo.Verification` | 1 | 3 753 | 无头渲染自检 + 15 个 `--check` 专项断言 |

---

## 1. 总体分层

```
┌─ 表现层 · HeliumTodo.Desktop ───────────────────────────────────────────────
│
│   MainWindow.axaml ── 壁纸三明治（Image / 蒙版 Border / ShellView）
│        │
│        └─ ShellView.axaml ── FANavigationView
│              │  PaneHeader：品牌区 + 版本号
│              │  Header   ：状态条 + FAInfoBar（警告条）
│              └  Content  ：ContentControl ── ViewLocator ──> 6 个页面 View
│
│   ┌────────────┬──────────┬──────────┬──────────┬──────────┬──────────┐
│   │ 今日工作流  │ 日程     │ 待办事项  │ 专注     │ 自习室   │ 设置     │
│   │ Dashboard  │ Schedule │ Todo     │ Focus    │ StudyRoom│ Settings │
│   └────────────┴──────────┴──────────┴──────────┴──────────┴──────────┘
│        每个页面 = View(.axaml) + ViewModel + 可选自绘控件
│        插件可以往 MenuItems 里再塞页面（见第 14 节）
│
├─ 应用服务层 · HeliumTodo.Desktop/Services ──────────────────────────────────
│
│   PluginHost        插件发现/加载/卸载（AssemblyLoadContext）
│   ThemeService      主题 + 强调色 → 写入 Avalonia 资源字典
│   WallpaperDecoder  SkiaSharp 解码静态图（PNG/JPG/WebP/BMP）→ Bitmap
│   WallpaperService  DispatcherTimer 逐帧播放，对外只暴露 CurrentFrame
│
├─ 领域层 · HeliumTodo.Domain ──────────────────────────────────────────────
│
│   Models/           ScheduleItem · TodoItem · FocusSession · StudyRoom · Enums
│   Services/         DayTimelineBuilder  ← 三大核心功能的交汇点
│                     FocusTimerService   ← 专注计时状态机
│                     StudyRoomService    ← 自习室业务编排
│                     RecurrenceExpander  ← 重复日程展开
│                     Conflict / DayTimeline ← 值对象
│   Theming/          IHeliumTheme · ThemeRegistry · BuiltInThemes(9 套)
│                     AccentColor · WallpaperAccentExtractor（取色算法）
│   Imaging/          ApngParser（手工拆 APNG 帧）
│   Networking/       PeerEndpoint · PeerMessage / PeerMessageCodec
│   Configuration/    AppConfig · ConfigService · AccentSources
│   Plugins/          IHeliumPlugin · IPeerTransport（接口在此定义）
│
├─ 存储层 · HeliumTodo.Infrastructure ──────────────────────────────────────────────
│
│   HeliumDbContext   6 张表（Code-First，EnsureCreated 建库）
│   Repositories      ScheduleRepository · TodoRepository
│                     FocusRepository · StudyRoomRepository
│   SeedData          首次运行铺示例数据（5 待办 / 6 日程 / 5 条专注记录）
│   DatabaseInitializer 建库 + 补种子 + 兼容性检查
│
├─ 网络传输层（Data 层里的两个实现）──────────────────────────────────────
│
│   LanPeerTransport     局域网 P2P：TCP 星形拓扑 + UDP 广播发现
│   LocalOnlyPeerTransport 纯本地占位（调试界面时用，不开端口）
│
└─ 基础设施 ──────────────────────────────────────────────────────────────
    配置：%AppData%\Helium Todo\config.json（可移植模式下退到程序目录）
    数据：%AppData%\Helium Todo\helium.db
    插件：%AppData%\Helium Todo\Plugins\
```

**分层的硬约束**（不是建议，是编译期保证）：

| 约束 | 怎么保证的 |
| --- | --- |
| `Core` 不引用任何 UI / 数据库包 | `HeliumTodo.Domain.csproj` **零** `PackageReference` |
| `Core` 不认识 Avalonia | 全项目 `grep Avalonia` 在 Core 目录下为 0 命中 |
| `Data` 只依赖 `Core` | csproj 里只有一条 `ProjectReference` |
| 界面只通过接口碰网络 | ViewModel 依赖 `StudyRoomService`，不直接碰 `IPeerTransport` 实现 |
| 换数据库/换网络实现不改业务代码 | 全部走 DI 容器注册（见第 4 节） |

---

## 2. 项目依赖图

```
                        ┌──────────────────────┐
                        │  HeliumTodo.Domain     │   22 文件 / 4125 行
                        │  领域模型 + 纯算法    │   零外部包依赖
                        └──────────┬───────────┘
                                   │ ProjectReference
                    ┌──────────────┴───────────────┐
                    │                              │
        ┌───────────▼──────────┐       ┌───────────▼────────────┐
        │  HeliumTodo.Infrastructure     │       │   HeliumTodo.Tests     │  16 文件 / 3212 行
        │  EF Core + 仓储 + 备份 │       │   168 个 xunit 测试     │
        └───────────┬──────────┘       └───────────┬────────────┘
                    │                              │
        ┌───────────▼──────────┐                   │
        │  HeliumTodo.Desktop      │                   │
        │  Avalonia 界面       │                   │
        └───────────┬──────────┘                   │
                    │                              │
        ┌───────────▼──────────┐       ┌───────────▼────────────┐
        │ HeliumTodo.Verification│      │  HeliumTodo.SelfTest   │
        │ 无头渲染 7 项截图      │       │  反射跑测试（绕过 dotnet test）
        └──────────────────────┘       └────────────────────────┘
```

`HeliumTodo.SelfTest` 引用 `HeliumTodo.Tests` 的理由：**测试宿主在本机沙箱里起不来**
（`dotnet test` 的 testhost 被拦），所以改用反射自己扫 `[Fact]` 方法跑。
详见第 16 节的「工程约束」。

---

## 3. 启动时序

`App.OnFrameworkInitializationCompleted()` 里严格七步，任一步失败都不该炸掉整个程序：

```
① 配置      new ConfigService() → TryPrepareDirectory() → Load()
             └ 目录不可写？退 <BaseDirectory>\Data（可移植模式）
             └ 文件损坏？改名 config.broken.<时间戳>.json 留档 + 用默认值重建
                              │
② 主题注册表  new ThemeRegistry() → 内置 9 套主题
                              │
③ 依赖注入   BuildServiceProvider(configService, themeRegistry, out pluginHost)
                              │      ← 第 4 节完整清单
                              │
④ 数据库     DatabaseInitializer.Initialize() → EnsureCreated + 补种子
                              │
⑤ 应用主题   themeRegistry.Apply(config.ThemeId)
             ThemeService.Apply(registry.Current)        ← 写 SystemAccentColor 等资源
             ThemeService.ApplyAccentColor(...)           ← 按 AccentSource 决定用谁的色
             App.ApplyScrimOpacity(config.WallpaperScrimOpacity)
                              │
⑥ 壁纸       若 WallpaperEnabled && 文件存在 → 后台 Task.Run 解码（不阻塞启动）
                              │
⑦ 插件       pluginHost.LoadPlugins() → 返回 (成功数, 失败数)，只写调试输出
                              │
⑧ 开窗口     if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
                new MainWindow { DataContext = ShellViewModel }
             ⚠ 无头渲染模式下【不】建窗口，否则没有桌面会崩
                              │
退出钩子     desktop.ShutdownRequested → pluginHost.ShutdownAll() + OnExit()
```

> 「⑦ 插件」和第 ⑧ 步之间那条类型判断是**踩过坑的**：
> `HeliumTodo.Verification` 用无头模式自建窗口，如果这里无条件 new MainWindow，
> 无头环境下直接抛异常。

---

## 4. 依赖注入容器全景

`App.axaml.cs:180` 的 `BuildServiceProvider` 里注册了这些（都是单例）：

| 类型 | 生命周期 | 说明 |
| --- | --- | --- |
| `ConfigService` | Singleton | 配置读写，构造时已 `Load()` 过 |
| `ThemeRegistry` | Singleton | 主题清单 + 当前主题 |
| `ThemeService` | Singleton | 主题/强调色 → Avalonia 资源 |
| `FocusTimerService` | Singleton | 专注计时器（**全局唯一**，跨页面共享） |
| `WallpaperService` | Singleton | 壁纸播放器 |
| `IPeerTransport` → `LanPeerTransport` | Singleton | ⚠ 换联机实现只改这一行 |
| `StudyRoomService` | Singleton | 自习室业务 |
| `ScheduleRepository` | Singleton | |
| `TodoRepository` | Singleton | |
| `FocusRepository` | Singleton | |
| `StudyRoomRepository` | Singleton | |
| `PluginHost` | Singleton | 工厂方法构造（要传日志工厂） |
| `AppState` | Singleton | 全局状态 + `DataChanged` 事件总线 |
| `ShellViewModel` | Singleton | 外壳 VM，页面 VM 由它按需创建 |
| `ILogger<>` | Singleton | `AddDebug()`，输出到 VS「调试 → 输出」 |

**扩展点就埋在这里**（源码注释原文）：

```csharp
// 想换数据库实现？改 AddHeliumData 那一行。
// 想换网络实现（联机自习）？改 IPeerTransport 那一行。
// 其它代码全都不用动。
```

---

## 5. MVVM 映射表

| 页面（导航项） | View | ViewModel | 主要数据源 | 核心交互 |
| --- | --- | --- | --- | --- |
| 今日工作流 | [DashboardView.axaml](HeliumTodo.Desktop/Views/DashboardView.axaml) 372 行 | `DashboardViewModel` 521 行 | `DayTimelineBuilder` 聚合的当日时间轴 | 时间轴点条目看详情、点待办直接勾完成 |
| 日程 | [ScheduleView.axaml](HeliumTodo.Desktop/Views/ScheduleView.axaml) 268 行 | `ScheduleViewModel` 475 行 | `ScheduleRepository` + `RecurrenceExpander` | 增删改 + 重复规则 + 按天分组 |
| 待办事项 | [TodoView.axaml](HeliumTodo.Desktop/Views/TodoView.axaml) 300 行 | `TodoViewModel` 424 行 | `TodoRepository` | 勾完成 / 优先级 / 到期 / 筛选 / 「开始专注」 |
| 专注 | [FocusView.axaml](HeliumTodo.Desktop/Views/FocusView.axaml) 287 行 | `FocusViewModel` 572 行 | `FocusTimerService` + `FocusRepository` | 时钟表盘、开始/暂停/放弃、干扰计数 |
| 自习室 | [StudyRoomView.axaml](HeliumTodo.Desktop/Views/StudyRoomView.axaml) 320 行 | `StudyRoomViewModel` 505 行 | `StudyRoomService` + `StudyRoomRepository` | 建房/加入、成员状态、聊天、共同专注 |
| 设置（Footer 区） | [SettingsView.axaml](HeliumTodo.Desktop/Views/SettingsView.axaml) 443 行 | `SettingsViewModel` 672 行 | `ConfigService` | 8 个折叠区：外观/壁纸/专注/自控/工作流/自习室/数据/插件/关于 |

**公共部件**

| 部件 | 文件 | 说明 |
| --- | --- | --- |
| 外壳 | `ShellView.axaml` 144 行 + `.axaml.cs` 120 行 | 自绘标题栏 + 56px 图标轨 + 内容区（见 5.1） |
| 主窗口 | `MainWindow.axaml` 45 行 | 壁纸三明治 + 窗口尺寸持久化 |
| 时间轴 | `Controls/TimelineGrid.cs` 242 行 | 全自绘（`Render` 里画网格线、块、现在时刻线） |
| 时钟 | `Controls/ClockControls.cs` 264 行 | 表盘 + 进度弧 + 指针 |
| 调色板 | `Controls/ColorPickerControls.cs` 324 行 | `SaturationValueSquare` + `GradientSlider` 自绘 |
| 宽度观察 | `Controls/WidthObserverPanel.cs` 39 行 | 解决 ScrollViewer 里卡片宽度塌陷 |
| 值转换器 | `Converters/CommonConverters.cs` 148 行 | 6 个：BoolToOpacity / BoolToVisible / BoolToCollapsed / PriorityBrush / PriorityText / TimeBlockText |
| 行 VM | `ViewModels/TimelineEntryViewModel.cs` 252 行 | 时间轴每个方块的呈现数据（含像素位置比例） |
| VM→View 解析 | `ViewLocator.cs` 57 行 | 约定：`XxxViewModel` → `XxxView` |

### 5.1 界面外壳的三列结构（界面改版后）

```
MainWindow（WindowDecorations="BorderOnly"，系统边框还在但标题栏不要了）
└─ Grid（三层叠着）
   ├─ Image   壁纸（Stretch=UniformToFill，静态图）
   ├─ Border  蒙版（WallpaperScrimBrush + WallpaperScrimOpacity）
   └─ ShellView
      └─ Grid RowDefinitions="Auto,*"
         ├─ Border x:Name="TitleBar"  Height=48   ← 自绘标题栏
         │    左：品牌方块 + Helium Todo + 版本号
         │    中：StatusText
         │    右：3 个 windowButton（字形 E921 / E922 / E8BB）
         └─ Grid ColumnDefinitions="Auto,*"
            ├─ Border Width=56  ← 图标轨（卡片样式，DockPanel：
            │    上面 MenuItems、下面 FooterItems，都用 railItem 模板）
            └─ Grid RowDefinitions="Auto,*"
               ├─ FAInfoBar（IsAlertVisible / AlertMessage）
               └─ ContentControl Content="{Binding CurrentPage}"
```

几个不那么显然的点：

- **不用 `FANavigationView` 了。** 它的 Pane 自带一堆内边距和展开动画，撑不进 56px 纯图标轨，
  选中态也不容易做成「强调色方块」。现在是一条 `ItemsControl` + `Button.railItem`，
  选中态由 `NavigationItemViewModel.IsSelected` 驱动（`Classes.active`）。
- **标题栏拖动**：`PointerPressed` 里先判断按下的不是按钮（`IsInsideButton` 沿父链上溯），
  再 `window.BeginMoveDrag(e)`；双击切最大化。不这么判的话，点最小化/关闭会被当成拖动。
- **首屏内容不能只靠 `Loaded`**：无头渲染下 `Loaded` 不触发，只挂它会让内容区空白，
  所以 `OnAttachedToVisualTree` 与 `Loaded` 都调 `EnsureFirstPage()`（幂等）。
- **卡片令牌**：`CardSurfaceBrush`（`#CCFFFFFF` / 深色 `#B31C1C1C`）、`CardBorderBrush`、
  `CardInsetBrush`、`CardShadow`（`0 8 24 0 #14000000`）、`RadiusCard=18`、
  `RightRailWidth=360`、`RailWidth=56`、`RailItemSize=44`、`TitleBarHeight=48`。
  卡片半透明是"壁纸模糊 24"这套观感成立的前提，两者要一起调。

---

### 5.1.1 窗口相关代码的封装（`Windowing/` 三件套）

窗口这件事被切成四个互不依赖的小对象，`MainWindow.axaml.cs` 只剩下"装配"：

| 文件 | 职责 | 为什么单独放 |
| --- | --- | --- |
| `Windowing/WindowChrome.cs` | 样式位 + `WM_NCCALCSIZE`（无边框窗口还能拖边、最大化不盖任务栏） | 平台相关、一次性设置，和窗口生命周期无关 |
| `Windowing/WindowAnimator.cs` | 最大化 / 还原 / 打开动效 | 有状态（定时器）、有中断处理（`Settle()`），独立才测得动 |
| `Windowing/WindowAnimationMath.cs` | 缓动、进度换算、矩形插值（纯函数） | 纯算术不碰窗口，无头环境也能断言 |
| `Windowing/WindowResizeGrips.cs` | 8 个抓手 + 指针捕获 + 拖拽换算 | 只在 `WindowState.Normal` 时有意义，逻辑自成一体 |

`MainWindow.axaml.cs` 里只剩：

```csharp
InitializeComponent();
WindowChrome.Attach(this);
_animator = new WindowAnimator(this) { Trace = App.LogBackground };
_grips = new WindowResizeGrips(this);
_grips.Attach();
Opened += OnOpened;
Closing += OnClosing;
```

对外只暴露两个动作：`ToggleMaximizeAnimated()`（标题栏双击 / 最大化按钮）、
`PlayShowAnimation()`（首次显示、从托盘唤醒）。

相关的另一件基础设施是 `HeliumTodo.Desktop/Services/SingleInstanceGuard.cs`：
`Mutex "Local\<appId>.lock"` + `EventWaitHandle "Local\<appId>.show"`，
非首实例只负责 `RequestShow()` 然后自己退出。它有两个作用：

* 正常情况：防止开两份程序同时写同一个数据库；
* **托盘失效时的退路**：即使托盘图标加不上（例如开机自启时资源管理器还没起好），
  用户再双击一次程序也能把窗口叫回来 —— 这条退路不依赖任何平台特性。

`<appId>` 默认是 `HeliumTodo`，可以用 `HELIUM_APP_ID` 换掉（规则在
`HeliumTodo.Domain/Lifecycle/AppIdentity.cs`）。留这个口子是因为**自检与探针需要在同一台
机器上拉起两份互不相干的实例**：共用一个名字时，第二份会以为"已经有人在跑"、
不建窗口，测试就卡在"等窗口"那一步 —— 现象完全不像"名字撞了"，本项目为此白花过一次时间。
它顺带也是绿色版并存的实现基础。

#### 第二个实例退出（这里曾经崩过一次）

非首实例的收尾**不能**在 `OnFrameworkInitializationCompleted` 里直接 `desktop.Shutdown()`：

```
OnFrameworkInitializationCompleted  ← 在这里 Shutdown，Dispatcher 当场就关了
        ↓ 返回
StartCore → Dispatcher.MainLoop     ← 进循环时抛
InvalidOperationException: Cannot perform requested operation because the Dispatcher shut down
```

进程以 `0xE0434352`（未处理异常）退出，用 Visual Studio 跑就是"程序是异常结束的"。
正确写法是把关闭**排进消息循环**：`Dispatcher.UIThread.Post(() => desktop.Shutdown());`，
让 `MainLoop` 先起来、再正常退出，退出码 `0`。验证脚本：`tools/probe-single-instance.ps1`。

另外，`desktop.Exit` 里会记一行 `[退出] 进程正常结束，退出码 0`。
排查"程序是被谁结束的"时这行很关键：**`0xFFFFFFFF` 不是我们返回的**
（全部退出路径都是 0），它是被外部终止（VS 停止调试、任务管理器结束任务、
探测脚本 `Kill()`）时系统给的值。

### 5.2 窗口装饰与「壁纸为什么不铺满」

主窗口用 `WindowDecorations="None"`，主窗口自己画标题栏，代价是自己补改大小。

```
MainWindow (WindowDecorations=None, 客户区 = 窗口矩形)
├── Image         壁纸，UniformToFill
├── Border        蒙版（WallpaperScrimBrush / WallpaperScrimOpacity）
├── ShellView     界面本体（透明背景）
└── Grid ResizeGrips
    ├── 4 条边 6px  ── Tag=North/South/West/East
    └── 4 个角 10px ── Tag=NorthWest/…/SouthEast
```

- 为什么不用 `BorderOnly`：它只去掉标题栏，`WS_THICKFRAME` 那一圈边框还在，
  由 DWM 用浅灰画，实测上 7px / 左右 7–8px / 下 8px，壁纸铺不进去 —— 就是用户说的「上方空白」。
- 为什么不用 `ExtendClientAreaToDecorationsHint`：只能把顶上那条从 7px 压到 1px，左右竖条还在。
- 改大小的实现：`MainWindow.OnResizeGripPressed` 记下 `Position` / `Bounds`，
  `PointerMoved` 里按位移改 `Width` / `Height`（`Window.BeginResizeDrag` 对无边框窗口无效，
  因为它依赖 `WS_THICKFRAME`）。纯托管，零 P/Invoke。
- 量尺寸的工具：`tools/probe-window-frame.ps1`（窗口 vs 客户区）、
  `tools/probe-resize-post.ps1`（PostMessage 模拟拖边）、
  `tools/probe-dwm-frame.ps1`（DWM 圆角偏好 + 可见边框厚度）。

#### 圆角与描边：交给 DWM，不要自己画

`WindowDecorations="None"` 清掉了 `WS_THICKFRAME`，顺带也让 Windows 11 不再给窗口裁圆角。
补回来的方式是**一个附加属性**，不需要 P/Invoke：

```xml
<Window WindowDecorations="None"
        Win32Properties.WindowCornerPreference="Round">
```

`Avalonia.Controls.Win32Properties.WindowCornerPreference` 的取值是
`Default` / `DoNotRound` / `Round` / `RoundSmall`，内部转成 `DWMWA_WINDOW_CORNER_PREFERENCE`。

⚠️ **设了这个属性之后不要再自己画 1px 描边。** 实测（`tools/probe-dwm-frame.ps1`）：

| 属性 | 读回 | 说明 |
| --- | --- | --- |
| `DWMWA_VISIBLE_FRAME_BORDER_THICKNESS`（37） | 2 px @150% ≈ 1 DIP | DWM 已经画了边框 |
| `DWMWA_WINDOW_CORNER_PREFERENCE`（33） | 2（Round） | DWM 会裁圆角 |

自己再叠一层 `Border` 会变成内外两条线。第一版就是这么写的（还踩了
`Name="WindowEdge"` 遮住同名枚举、报 5 个 `CS1061` 的坑），后来整段删掉了。

#### 最大化 / 最小化动画：DWM 自己会，前提是样式位里有 `WS_CAPTION`

**这里曾经写错过一次，改正记录如下。** 早先的结论是"补了 `WS_THICKFRAME` 之后 DWM 仍然不给动画，
所以无边框窗口就是没动画"，于是自己写了一套逐帧改窗口矩形的动画。后来把样式位打印出来对比才发现：

| 窗口 | `GWL_STYLE` | 差额 |
| --- | --- | --- |
| 本项目（自绘，改前） | `0x160F0000` | 只有 `WS_THICKFRAME` / `WS_SYSMENU` / `WS_MIN` / `WS_MAX` |
| Avalonia `BorderOnly` | `0x16870000` | 多了 `WS_BORDER`（`0x00800000`） |
| 普通可最大化窗口 | `0x16CF0000` | 多了 `WS_CAPTION = WS_BORDER \| WS_DLGFRAME`（`0x00C00000`） |

DWM 判定"这是不是一个正常窗口"看的是 **`WS_CAPTION`**，不是 `WS_THICKFRAME`。补上它之后：

- 最小化 / 最大化 / 还原 / Win+↑ / Win+↓ / 拖到顶边 / 双击标题栏 / Aero Snap **全部**白捡；
- 动画在**合成器**里跑，软件渲染下也是满帧（真机实测最小化 30 个、最大化 32 个不同可见面积，
  单调下降 95% / 86%）；
- 不会长出标题栏 —— `WM_NCCALCSIZE` 依旧把非客户区抹成 0，壁纸照样铺满。

代码在 `HeliumTodo.Desktop/Windowing/WindowChrome.cs`：样式回调里加 `WS_CAPTION`，
`Opened` 之后再显式 `DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED /*3*/, 0, 4)`
（万一被系统/组策略关过）。

配套删掉的东西（约 210 行）：`WindowAnimator` 的 `ToggleMaximize` / `Maximize` / `Restore` /
`AnimateWindow` / `ApplyRect` / `CurrentPixelRect` / `GetWorkArea` / `ScheduleSelfTestIfRequested`，
`WindowAnimationMath` 的 `Interpolate` / `Frames` / `FrameInterval` / `IntroStartScale`。
`ShellView.ToggleMaximize()` 现在只有一行：切 `WindowState`。

验证工具（都是正式脚本，不是临时物）：

| 脚本 | 验什么 | 实测 |
| --- | --- | --- |
| `tools/probe-window-style.ps1` | `GWL_STYLE` 是否含 `WS_CAPTION`；窗口矩形是否仍等于客户区 | `0x16CF0000`；1295×857 vs 1294×857 ✔ |
| `tools/probe-native-anim.ps1` | 触发最小化/最大化，按帧统计"可见像素面积" | 30 / 32 个不同面积，单调下降 95% / 86% ✔ |
| `tools/probe-intro.ps1` | 读日志里的遮罩 Alpha 序列 | 8 帧 / 6 个 Alpha / 255→0 ✔ |
| `tools/probe-minimize-restore.ps1` | 最小化→恢复的收尾与补播（读 `background.log`） | 两条状态行都命中 ✔ |

**为什么原来的探针给的是假阴性**：`tools/probe-maximize-anim.ps1` 靠高频 `GetWindowRect` 数中间尺寸，
而 DWM 原生动画只改视觉、不改矩形 —— 数出来永远是 1 个值。改动画之后必须换一种量法（数像素面积）。

#### 打开动画：一层遮罩淡出，不碰 `Window.Opacity`

给窗口设小于 1 的不透明度会让 Win32 走 `WS_EX_LAYERED`（分层窗口）：每帧整窗重新合成
（`--perf-check` F 档 37 ms/帧），而且改扩展样式会让 DWM 重建重定向表面 —— 正好把上面刚弄好的
原生过渡又弄没。所以现在的做法是：

- `MainWindow.axaml` 里一层 `Border Name="IntroVeil"`（`IsHitTestVisible="False"`），
  每帧只改它画刷的 Alpha（`--perf-check` M 档 4.4 ms/帧，与纯重绘同量级）；
- 帧驱动用 `TopLevel.RequestAnimationFrame`，不是 `DispatcherTimer`（定时器按 11.25 ms 排，
  真机一帧 45 ms，排进来的帧只会越积越多，量出来是 18/91/64/58 这种极不均匀的间隔）；
- **单帧进度上限** `WindowTransitionPolicy.ClampIntroProgress`：冷启动第一帧到第二帧可能隔 176 ms
  （布局 + 字体光栅化 + 首次出图把 UI 线程占住），按真实时间算会一步跳到终点、变成"闪一下"。
  夹住单帧步进 ≤ 0.2（至少 5 帧）之后，迟到的帧只会把动画拉长，不会跳；
- 不再缩放内容：那 4% 在 180 ms 里几乎看不出来，却要让整棵内容树每帧重算，
  还会把抓手坐标算偏（`TranslatePoint` 曾把 x=1277 算成 1251.5）。

两个必须记住的点（都是踩出来的）：

1. **计时从第一帧起算**：`Stopwatch? clock = null;` + 每帧 `clock ??= Stopwatch.StartNew();`。
   若在 `PlayIntro()` 入口就开表，第一帧到来时 180 ms 早已过去。
2. **`Settle()` 不是可选项**：窗口要藏起来、要关、要最小化之前一律调 `FinishAnimationsImmediately()`。
   否则遮罩会留在屏幕上、`RenderTransform` 会留着（抓手算偏 26 px）。
   `MainWindow` 在 `WindowStateProperty` 变化时盯住这件事：进 `Minimized` 立刻 `Settle()`，
   从 `Minimized` 回来补一次 `PlayIntro()`（这是 L4 修的 bug，以前从任务栏点回来是个半透明窗口）。

可量测性：动画不再改窗口矩形的，所以 `GetWindowRect` 采不到中间值 —— 这也正是它变快的原因。

#### （历史）最大化动画：样式位还得借回来

`WindowDecorations="None"` 会丢掉 `WS_THICKFRAME`，而 Windows 判断"这是不是一个
能最大化的普通窗口"看的正是它 —— 少了它，最大化直接跳变、没有 DWM 缩放动画。

`HeliumTodo.Desktop/Windowing/WindowChrome.cs`（早期叫 `Views/MainWindow.Win32.cs`）用 Avalonia 12 给的两个钩子解决：

```csharp
// ① 样式位加回来（注意委托返回的是 (style, exStyle) 元组）
Win32Properties.AddWindowStylesCallback(this,
    static (style, exStyle) =>
        (style | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX, exStyle));

// ② WM_NCCALCSIZE 里把 rgrc[0] 原样留着 → 非客户区为 0，7px 灰边不会回来
Win32Properties.AddWndProcHookCallback(this, OnNativeMessage);
```

`WM_NCCALCSIZE` 的 `wParam` 为 `TRUE` 时才带 `NCCALCSIZE_PARAMS*`；
最大化时窗口矩形是**整个屏幕**，要换成 `MonitorFromWindow` + `GetMonitorInfo` 的工作区，
否则客户区会盖住任务栏。

实测（150% 缩放）：客户区 = 窗口矩形（壁纸铺满）；最大化后客户区 1706×1018 ≈ 工作区 1707×1019；
DWM `Round` 与 2px 描边都保留。量测脚本 `tools/probe-maximize.ps1`。

### 5.3 打开文件夹：一条抽象，五条退路

用户点「打开文件夹」时，代码不是直接 `Process.Start("explorer.exe")`，而是按顺序试：

```
IFolderLauncher (Avalonia Launcher)   ← 首选：交给系统外壳，有返回值
  ↓ 返回 false / 抛异常
explorer.exe + UseShellExecute=true   ← 交给外壳，避开 0xc0000142
  ↓
目录路径 + Verb="open"
  ↓
explorer.exe + UseShellExecute=false  ← 下策：没有外壳拉起，可能起不来
  ↓
cmd.exe /c start "" "<path>"          ← 最后一条
  ↓ 全失败
NoticeText 里一次性列出每条的原因 + 完整路径
```

- `HeliumTodo.Desktop/Services/FolderLauncher.cs` 的 `AvaloniaFolderLauncher` 从
  `IClassicDesktopStyleApplicationLifetime.MainWindow` 取 `TopLevel`（窗口藏到托盘时依然可用），
  调 `Launcher.LaunchDirectoryInfoAsync`。
- 为什么保留后面四条：Launcher 在无头/受限环境会返回 false，
  而"点了没反应"比"用了次优手段但开了"更糟。

## 6. 三大核心功能怎么互相联系（重点）

需求第 4 条要求「日程 / 专注时长 / 待办事项**互相联系**」。联系点不是散落在界面里的几行代码，
而是**汇集到一个函数**上：

```csharp
// HeliumTodo.Domain/Services/DayTimelineBuilder.cs:27
public static DayTimeline Build(
    DateOnly date,
    IEnumerable<ScheduleItem> schedules,
    IEnumerable<TodoItem> todos,
    IEnumerable<FocusSession> focusSessions)
```

三个数据源进来，一条统一时间轴出去。所以「三者联动」在架构上是**同一个数据管线的三个输入**。

### 联动全景

```
       ┌──────────────┐        ┌──────────────┐        ┌──────────────┐
       │  日程日程表   │        │  待办事项     │        │  专注记录     │
       │ ScheduleItem │        │  TodoItem    │        │ FocusSession │
       └──────┬───────┘        └──────┬───────┘        └──────┬───────┘
              │ 重复规则展开           │ 有计划时间/当天截止     │ 已完成的专注
              │ RecurrenceExpander    │ 才上时间轴             │ 自动落库
              │                       │                       │
              └───────────┬───────────┴───────────┬───────────┘
                          │                       │
                          ▼                       ▼
              ┌──────────────────────────────────────────┐
              │   DayTimelineBuilder.Build(date, …)      │
              │   ┌────────────────────────────────┐     │
              │   │ ① CollectSchedules            │     │
              │   │ ② CollectTodos                │     │
              │   │ ③ CollectFocusSessions        │     │
              │   │ ④ RemoveDuplicateTodoEntries  │     │ ← 同一条待办被日程和待办各表示一次时去重
              │   │ ⑤ AssignLanes   并查集组 + 贪心 │     │ ← 算出 lane / laneCount
              │   │ ⑥ DetectConflicts             │     │ ← 时间重叠 / 超载 / 逾期
              │   │ ⑦ CalculateBusyDuration       │     │
              │   └────────────────────────────────┘     │
              └──────────────────┬───────────────────────┘
                                 ▼
                    DayTimeline { Entries, Conflicts, BusyDuration }
                                 │
        ┌────────────────────────┼────────────────────────┐
        ▼                        ▼                        ▼
 ┌──────────────┐        ┌──────────────┐        ┌──────────────┐
 │ 每日工作流    │        │ 冲突提示      │        │ 总览指标卡    │
 │ 图形化时间轴  │        │ 红色边框+列表 │        │ 忙碌时长/完成率│
 └──────────────┘        └──────────────┘        └──────────────┘
```

### 四条具体的联动路径

| # | 从 | 到 | 机制 |
| --- | --- | --- | --- |
| 1 | 待办 → 工作流 | 待办出现在时间轴上对应时段 | `TodoItem.PlannedStart` 有值，或 `DueAt` 是当天 |
| 2 | 待办 → 专注 | 待办页「开始专注」把 `TodoId` 带进计时器 | `FocusTimerService.Start(duration, title, todoId)` |
| 3 | 专注 → 待办 | 专注结束把时长累加到对应待办 | `FocusViewModel.SaveFocusRecordAsync` → `TodoRepository.AddFocusedTimeAsync(todoId, actual)` → 写 `TodoItem.FocusedDuration`（**不自动勾完成**，勾不勾由人决定） |
| 4 | 专注 → 工作流 | 已完成的专注作为 `FocusEntry` 回到时间轴 | `CollectFocusSessions` 读 `FocusSessions` 表 |

> **去重那条（④）值得单独说**：一条待办如果既有日程块又有待办块，
> 时间轴上会出现两个重叠方块、还会误报「冲突」。
> `RemoveDuplicateTodoEntries(entries, schedules)` 把「日程已经表达过的那条待办」丢掉，
> 这是需求 4「互相联系」在实现上最容易出错的地方。

### 跨页面刷新：AppState 事件总线

```
   任意页面改了数据（勾待办 / 建日程 / 完成专注）
                    │
                    ▼
        AppState.NotifyDataChanged(AppDataType)
                    │  event EventHandler<AppDataType>? DataChanged
        ┌───────────┼───────────┬───────────┐
        ▼           ▼           ▼           ▼
   总览页刷新   日程页刷新   待办页刷新   工作流重算
```

`AppState` 同时管两件事：**「今天是哪天」**（`Today` / `SelectedDate` / `RefreshTodayIfNeeded()`
用于处理程序跨零点还开着的情况）和 **「数据变了通知谁」**。

> ⚠️ **订阅方一律用 `AppDataTypeExtensions.Includes`，不要用 `HasFlag`。**
>
> `AppDataType` 是 `[Flags]`，而 `Timeline = Schedules | Todos | Focus` 是一个**组合值**。
> `type.HasFlag(AppDataType.Timeline)` 问的是「三类都变了吗」，单改一条待办时 `type == Todos`，
> 结果 `2.HasFlag(7) == false` —— 今日工作流就把自己判成"和我无关"，统计和时间轴全停在旧数据。
> 正确写法 `type.Includes(AppDataType.Timeline)`，语义是「涉及其中任意一类就刷新」。
> 这个 bug 在 2026-10-29 修掉，四个页面统一改了名字（`DashboardViewModel.cs:45` 等）。

### 设置变更事件：SettingsChanged

`DataChanged` 只管业务数据；设置项（比如「局域网联机」开关）走另一条轻量通道：

```
  设置页拨动开关
        |  Config.EnableNetworkStudyRoom = value; Save();
        v
   AppState.NotifySettingsChanged()   ->   event SettingsChanged
        |
        v
   StudyRoomViewModel.RefreshNetworkState()  ->  IsLanEnabled / CanGoOnline / OnlineStatusText
```

订阅方收到后如果发现联机被关掉，会顺手 `LeaveRoomAsync()`，避免留一个还在监听端口的房间。

---

## 7. 时间轴构建管线细节

| 步骤 | 方法 | 做什么 | 复杂度 |
| --- | --- | --- | --- |
| ① | `CollectSchedules` | 按 `RecurrenceExpander.OccursOn(date)` 筛出当天该出现的日程 | O(n) |
| ② | `CollectTodos` | 只取「有安排时间」或「当天到期」的待办 | O(n) |
| ③ | `CollectFocusSessions` | 已完成且落在当天的专注记录 | O(n) |
| ④ | `RemoveDuplicateTodoEntries` | 去掉被日程表示过的待办 | O(n·m) |
| ⑤ | `AssignLanes` | **并查集分组 + 贪心分列**，`laneCount` = 组内最大并发数（不是组大小） | O(n²) 最坏 |
| ⑥ | `DetectConflicts` | 输出 `Conflict { Kind, Severity, Message, Start, End, InvolvedSourceIds }` | O(n²) |
| ⑦ | `CalculateBusyDuration` | 忙碌时长（重叠部分只算一次） | O(n log n) |

`ConflictKind` 共 5 种：

| 值 | 含义 |
| --- | --- |
| `ScheduleOverlap = 0` | 两条日程时间重叠（时间轴上错位显示） |
| `FocusOverlapsSchedule = 1` | 预订的专注时段撞上已有日程 |
| `TodoDueInBusyTime = 2` | 待办截止时间落在忙碌时段里 |
| `PlannedTodoOverlapsSchedule = 3` | 安排做待办的时间撞上日程 |
| `OverloadedDay = 4` | 一天排太满（超过 `OverloadThreshold = 12 小时`） |

`ConflictSeverity` 三档：Info / Warning / Error —— 界面上分别对应蓝 / 橙 / 红三种边框。

---

## 8. 专注计时状态机

```
                    Start(duration,title,todoId)
        ┌────────┐ ─────────────────────────────► ┌───────────┐
        │  Idle  │                                │  Focusing │◄─┐
        └────────┘ ◄───────────────────────────── └─────┬─────┘  │ Resume()
             ▲            Abandon()                     │        │
             │                                           │ Pause()│
             │                                           ▼        │
             │                                     ┌───────────┐  │
             │                                     │  Paused   │──┘
             │                                     └───────────┘
             │                                           │
             │        Tick(delta) 累计到 PlannedDuration │
             │                                           ▼
             │                                    ┌──────────────┐
             └────────────────────────────────────│  Break       │
                SkipBreak() / 休息结束              └──────────────┘
                                                  StartBreak(d)
                                                  → FocusCompleted 事件
```

| 成员 | 作用 |
| --- | --- |
| `Phase` | `enum FocusPhase { Idle = 0, Focusing = 1, Break = 2, Paused = 3 }` |
| `Elapsed` / `Remaining` / `Progress` | 界面直接绑的显示量 |
| `InterruptionCount` + `RegisterInterruption()` | **自控模块**：切走窗口时界面调一次，计数上屏 |
| `TodayTotal` + `SeedTodayTotal()` | 今日累计专注（启动时从数据库灌一次） |
| `Tick(TimeSpan delta)` | **纯函数式推进**，不自己起 Timer —— 便于单元测试（不睡眠也能测 25 分钟） |
| `FocusCompleted` 事件 | 携带 `FocusCompletedEventArgs`（计划/实际时长、标题、TodoId） |

> `Tick` 由界面层 `DispatcherTimer` 驱动，服务本身不持有时钟。
> 这是「Core 层零依赖」带来的好处：11 个专注测试全部是**瞬间跑完**的。

---

## 9. 背景壁纸子系统

```
  文件（PNG / JPG / GIF / APNG）
        │
        ▼
  ┌──────────────────────────────────────────────────────────┐
  │ Core/Imaging/ApngParser.cs       450 行                    │
  │   IsApng() / TryParse()                                    │
  │   ⚠ SkiaSharp 的 SKCodec 不认 APNG（FrameCount == 0）       │
  │   解法：把 fdAT 的 payload 重新拼成一张普通 PNG 再交给 Skia  │
  └────────────────────────┬─────────────────────────────────┘
                           ▼
  ┌──────────────────────────────────────────────────────────┐
  │ App/Services/WallpaperDecoder.cs  486 行                   │
  │   • 静态图长边限制 1920（只占一帧，不心疼）                  │
  │   • 动图长边限制 1280（几十帧同时驻留，见 §18）              │
  │   • 总帧预算 96 MB，超了按 step 抽帧、延迟累加               │
  │   • 逐帧 SKCodec（GIF 必须按顺序 + 指定 PriorFrame）         │
  │   • 可选模糊 SKImageFilter.CreateBlur                        │
  │   • SKBitmap → WriteableBitmap（逐行 Marshal.Copy，无 unsafe）│
  └────────────────────────┬─────────────────────────────────┘
                           ▼
  ┌──────────────────────────────────────────────────────────┐
  │ App/Services/WallpaperService.cs  240 行                   │
  │   DispatcherTimer 逐帧排期（每帧单独设 Interval）           │
  │   对外：CurrentFrame / Accent / IsAnimated / AdvanceFrame() │
  └────────────────────────┬─────────────────────────────────┘
                           ▼
  ┌──────────────────────────────────────────────────────────┐
  │ ShellViewModel（转接层）                                    │
  │   WallpaperService.Changed += RefreshWallpaper             │
  │   → WallpaperImage / IsWallpaperVisible                    │
  │   ⚠ 服务本身不发属性通知，所以在这里做一层转接              │
  └────────────────────────┬─────────────────────────────────┘
                           ▼
  ┌──────────────────────────────────────────────────────────┐
  │ MainWindow.axaml 三层（x:DataType = ShellViewModel）        │
  │   Image(Source=WallpaperImage, UniformToFill)              │
  │   Border(Background=WallpaperScrimBrush,                   │
  │          Opacity=WallpaperScrimOpacity —— 都是资源)         │
  │   ShellView(FANavigationView Background=Transparent)       │
  └──────────────────────────────────────────────────────────┘

  取色支线：
  Core/Theming/WallpaperAccentExtractor.cs  246 行
     色相 24 桶投票 → 取最高票桶的加权平均
     权重 = 饱和度² × (0.30 + 0.70 × 亮度)
     夹饱和度 [0.42,0.86]、明度 [0.30,0.46] → 保证白字对比度
     纯黑白图 → IsFallback = true，退回默认蓝
        │
        ▼
  ThemeService.ApplyAccentColor(hex) → MaterialPrimary*/MaterialSecondary* 资源键 + SystemAccentColor
```

**强调色的三种来源**（`AccentSources`）：

```
  accentSource = "theme"       → 用 IHeliumTheme.AccentColorHex（9 套内置主题各一个）
               = "wallpaper"   → 用壁纸取色的结果
               = "custom"      → 用 customAccentHex（调色板手调）
```

---

## 10. 联机自习子系统

### 拓扑：星形 + 房主中继（无中心服务器）

```
                    ┌───────────────────────────────┐
                    │   房主 Host（建房间的人）        │
                    │   TCP Listener :47470          │
                    │   ├ 保存所有 client session    │
                    │   ├ 转发聊天/状态给其他人        │
                    │   └ 每 4 秒广播地址表 Peers     │
                    └───┬───────────┬───────────┬───┘
             TCP 连接     │           │           │
        ┌────────────────┘           │           └──────────────┐
        ▼                            ▼                          ▼
  ┌───────────┐              ┌───────────┐              ┌───────────┐
  │  组员 A    │              │  组员 B    │              │  组员 C    │
  └───────────┘              └───────────┘              └───────────┘
        ▲                            ▲                          ▲
        └──────────── UDP 广播发现（同一网段自动找到房主）────────┘
                      UDP :47470  "谁在开自习室？"
```

**为什么是星形而不是真 P2P**：需求是「无中心服务器」，星形拓扑里房主就是那个"中心"，
但它**是用户之一、不是独立服务器进程** —— 房主关窗口房间就散，符合"无中心服务器"的字面要求。
真 P2P 网格（每两人之间都连）在 NAT 环境下需要穿透，明确没做。

### 协议：一行一条 JSON

```
  PeerMessageKinds:
    hello    ──► 加入时自报身份 + ListenPort + AdvertisedEndpoint
    join     ──► 请求进房间（带房间码）
    welcome  ◄── 房主确认 + 房间信息
    status   双向  专注状态（是否在专注/专注什么/还剩多久）
    presence 双向  上下线
    chat     双向  聊天（房主中继）
    leave    双向  离开
    ping     每 4 秒发一次，带 SentAtUnixMs
    pong     原样回传 SentAtUnixMs → 算 RTT
    peers    房主 → 全体，地址表 [{PeerId, DisplayName, Endpoint, RoundTripMs}]
```

| 设计点 | 数值 / 做法 | 为什么 |
| --- | --- | --- |
| 心跳间隔 | 4 秒 | 拔网线时 TCP 不会立刻报错，靠心跳才能发现 |
| 判掉线 | 15 秒无消息 | 「幽灵成员」会让成员列表骗人 |
| 自报端口 | `ListenPort` | TCP 出去的**源端口是随机的**，不报对方无法反连 |
| 对外地址 | `AdvertisedEndpoint` | 对应 EasyTier 的 `--mapped-listeners` |
| IPv6 | `TcpListener(IPAddress.IPv6Any)` + `DualMode` | 一端口双栈，IPv4/IPv6 都能进 |
| 地址规范化 | `IsIPv4MappedToIPv6` 时 `.MapToIPv4()` | 否则显示成 `[::ffff:192.168.1.5]`，用户没法照着输 |

> **踩过的坑**：对**已接受**的连接设 `DualMode = true`，会让随后 `GetStream()` 抛
> `InvalidOperationException`。这个异常发生在 `Task.Run` 内部被完全吞掉，
> 症状表现为"客户端连上了但房主收不到任何消息" —— 见 `PROGRESS.md`。

---

## 11. 数据备份子系统（统一 .zip）

### 为什么要「统一」

备份在三个场景里发生，原来各写各的：

| 场景 | 旧做法 | 现在 |
| --- | --- | --- |
| 启动时自动备份 | `File.Copy(db, backups/xxx.db)` | `BackupAutomatically()` → `.zip` |
| 用户手动导出 | **不存在** | `Export()` → `.zip`（用户选路径） |
| 导入前安全备份 | **不存在** | `BackupAutomatically(isPreImport: true)` → `pre-import-*.zip` |

三个场景现在产出同一种文件、走同一套读写代码 —— 这就是「统一」。

### 包结构

```
HeliumTodo-backup-20261027-183012.zip
├── manifest.json           清单（格式 ID / 版本 / 每项 SHA256 / 各项条数）
├── data/helium.db          数据库快照（VACUUM INTO 产出，不含 WAL 尾巴）
├── data/config.json        配置
└── assets/wallpaper.<ext>  壁纸（可选）
```

### 分层：格式在 Core，搬运在 Data，界面在 App

```
  App   SettingsViewModel ──┬── ExportToAsync(path)          ┐
      SettingsView.axaml    ├── BeginImportAsync(zip)        │ 只管状态与弹窗
      (导出/导入/确认浮层)   └── ImportReplace/MergeCommand  ┘
                                    │
  Data  DataBackupService ──────────┘   ★ 唯一知道"怎么搬数据"的地方
        ├─ Export()          VACUUM INTO → 写包
        ├─ Inspect()         只读清单，判断"能不能导"
        ├─ Import()          ATTACH + 单事务搬运
        └─ BackupAutomatically()  启动/导入前自动留档，按时间保留 7 份
                                    │
  Core  Backup/ ────────────────────┘   ★ 纯格式，不碰数据库连接
        BackupFormat        常量：格式 ID、条目名、文件名规则
        BackupManifest      清单模型 + ToJsonBytes / TryParse
        BackupPackageWriter 写 zip（先写 .tmp 再改名）
        BackupPackageReader 读 zip + 校验 SHA256（IDisposable）
        BackupInspection    给界面看的"体检报告"
```

**硬边界**：`Core` 里的备份代码不认识 EF Core，也不知道数据表长什么样 ——
它只知道「有一个 zip，里面有清单和几个字节流」。表结构的知识全在 `Data`。

### 导入流程（六步，每步失败都不改坏库）

```
① 读包校验      BackupPackageReader.Open + TryVerifyChecksums
                格式 ID 不对 / 版本太新 / 清单缺失 → 直接拒绝，一点没动
② 安全备份      只在「替换」模式做：BackupAutomatically(isPreImport: true)
③ 解出数据库    data/helium.db → %TEMP%\helium-import-{guid}.db
④ 搬运数据      ATTACH ... AS src; BEGIN; ...; COMMIT; DETACH
⑤ 应用配置      ApplyImportedConfig（本机路径不动）
⑥ 恢复壁纸      写进 <ConfigDirectory>/wallpapers/，更新 WallpaperPath
```

第 ④ 步是整件事的核心，也是唯一可能"改坏库"的地方，所以包在一个事务里：

```sql
ATTACH DATABASE 'C:\...\helium-import-xxx.db' AS src;
BEGIN;
-- 替换：先倒序清空，再原样搬
DELETE FROM main."StudyRoomMessages"; ...
INSERT INTO main."Todos" ("Id","Title",...) SELECT "Id","Title",... FROM src."Todos";
COMMIT;
DETACH DATABASE src;
```

**为什么是 ATTACH 而不是「关程序换文件」**：Windows 上删/覆盖一个被连接持有的
数据库文件必然失败，而程序里随时可能有别的线程开着连接。ATTACH + SQL 搬运
全程不动文件句柄，也就不存在"占用"问题。

### 替换 vs 合并（语义差异是设计出来的，不是妥协）

| | 替换 `Replace` | 合并 `Merge` |
| --- | --- | --- |
| 现有数据 | 清空 | 保留 |
| 主键 | 原样保留 | 重新编号（`Dictionary<long,long> idMaps`） |
| 子表外键 | 原样 | 按映射换算；**父记录没搬过来就置 `NULL`** |
| 唯一键冲突 | 不会发生 | 跳过该行（连带它的子记录） |
| 安全备份 | 导入前自动留档 | 不需要 |

两条容易被忽略的规则：

1. **父记录没搬过来，外键必须置 `NULL`，不能随便指一个。**
   专注记录指向的待办若没被搬过来，重新指向别的待办等于**伪造用户行为**。
2. **合并不是无脑追加。** 全库唯一索引只有 `StudyRooms.Code`
   （`HeliumDbContext.cs:102`），同一房间码就是同一个房间 →
   跳过整行，而不是改个新码塞进去。

### 列集合取交集（旧备份也能导入）

搬数据时列名不是写死的，而是 `PRAGMA main.table_info("T")` 与
`PRAGMA src.table_info("T")` 的**交集**：

```csharp
var columns = IntersectColumns(mainColumns, sourceColumns);
```

备份里少一列（旧版本没这个字段）就只搬有的那些，多出来的列用默认值。
这让「以后加字段」不会变成「以后加字段 = 旧备份全废」。

### 界面

设置 → 数据与文件：

| 行 | 控件 |
| --- | --- |
| 导出 / 导入 | 两个按钮（`OnExportBackup` / `OnImportBackup`，走系统文件选择器） |
| 备份文件夹 | 显示真实路径 + 打开文件夹 |
| 最近一次备份操作 | `BackupStatusText`，如「已导出 x.zip，82.8 KB · 8 条日程 · …」 |

导入不是点了就执行，先弹确认浮层（`IsImportConfirmOpen`），
把「包里有什么」摆出来再让用户选替换 / 合并 / 取消。

---

### 11.1 目标位置写不进去时怎么办

导出是"用户挑一个文件夹，我们把 zip 放进去"。这个文件夹可能是只读盘、网络位置、
或者被 Windows「受控文件夹访问」保护着的桌面 —— 后者的表现是 `UnauthorizedAccessException`。

```
BackupPackageWriter.Write
   ├─ 建目录失败 / 开临时文件失败 / 改名失败
   └─> BackupWriteDeniedException（带 TargetPath + 一句人话）
          └─> DataBackupService.Export 捕获
                 ├─ 改存到 <数据库目录>/backups/
                 ├─ 重名就加 (2)(3)…，不覆盖旧备份
                 └─> BackupExportResult { RequestedPath = 原路径, FellBack = true }
                        └─> 界面：状态栏说"改存到备份文件夹"，提示条说"那个文件夹系统不让写"
```

> ⚠️ **`UnauthorizedAccessException` 不是 `IOException` 的子类**（它直接继承 `SystemException`）。
> 这就是为什么原来"导出失败"会崩出一个未处理异常对话框：上层的 `catch (IOException)` 根本没接住。

## 12. 通知、后台常驻与进程间接口

这三块都是"程序关掉窗口以后还在干活"相关的，所以放在一起讲。

### 12.1 生命周期：关窗口 = 藏起来

```
用户点窗口右上角 ×
  └─> MainWindow.Closing
        ├─ 托盘图标已就绪 ──> e.Cancel = true; window.Hide(); 进程继续跑
        └─ 托盘没起来   ──> 放行，窗口真的关掉（系统默认行为）
```

**为什么托盘失败必须放行**：如果拦下 `Closing` 却没能把窗口叫回来，用户会得到一个
既关不掉也找不到的进程，只能去任务管理器里杀。

真退出只有一条路：托盘菜单「退出 Helium Todo」→ 置 `_exitRequested` → `window.Close()`
→ `desktop.Shutdown()`。`desktop.ShutdownRequested` 里再补一次
`_reminders?.Stop()` + `_tray?.Dispose()` 兜底。

| 成员 | 位置 | 作用 |
| --- | --- | --- |
| `_exitRequested` | `HeliumTodo.Desktop/App.axaml.cs` | 区分"用户点 ×"和"用户要退出" |
| `StartBackgroundServices(desktop, pluginHost)` | 同上 | 起提醒定时器 + 托盘，挂 `Closing` |
| `RestoreMainWindow()` | 同上 | 托盘「显示主界面」：`Show` + 还原 `WindowState` + `Activate` |
| `BackupOnceAsync()` | 同上 | 托盘「立即备份一次」，`Task.Run` 里跑，不挡 UI |
| `LogBackground(string)` | 同上 | 写 `<ConfigDirectory>/background.log` |

> WinExe 没有控制台，后台事件又都发生在窗口关掉之后 —— 除了这行日志没有第二个观测口。
> 这一轮就是靠它一次定位了"托盘加不上图标"和"提醒不到点"两个问题。

### 12.2 提醒：三层，每层单独可测

```
ReminderService（App，30 秒一次）
   └─> ReminderPlanner.CollectDue(schedules, todos, now, tolerance, alreadyFired)   ← Core，纯函数
          └─> IReadOnlyList<ReminderItem>
                 └─> INotificationService.Notify(NotificationMessage)
                        └─> WindowsToastNotifier ──> TrayService.ShowBalloon(...)
```

- **判定窗口是 `[now - tolerance, now]`**，不是 `now`。定时器有抖动，只认"正好这一毫秒"
  等于永远扫不到。`tolerance` 取 2 分钟。
- **重复日程要挪到今天**：先过 `RecurrenceExpander.OccursOn(date)`，命中后把 `Start`
  换成"今天 + 原来的时刻再算提醒时刻"，否则"每周二的课"第三周起就不再提醒。
- **去重键带日期**：`ReminderItem.Key = $"{Kind}:{SourceId}:{RemindAt:yyyyMMddHHmm}"`。
  同一个待办今天弹过、明天还得能弹；同一天里反复扫描不能重复弹。
- **提醒时刻**：日程 = `Start - ReminderMinutes`；待办 = `(DueAt ?? PlannedStart) - ReminderMinutes`。
  两者都为 null 就不提醒。

| 类型 | 文件 | 说明 |
| --- | --- | --- |
| 预设 | `HeliumTodo.Domain/Models/ReminderPresets.cs` | 不提醒 / 开始时 / 5 / 10 / 30 分钟 / 1 / 2 小时，另有 `Compose(h, m)` 给自定义 |
| 数据 | `HeliumTodo.Domain/Notifications/ReminderItem.cs` | `ReminderSourceKind` + 一条待发提醒 |
| 算法 | `HeliumTodo.Domain/Notifications/ReminderPlanner.cs` | 纯函数，不碰数据库、不碰时间 |
| 契约 | `HeliumTodo.Domain/Notifications/INotificationService.cs` | `IsSupported` / `Notify` / `Activated` + `NullNotificationService` |
| 调度 | `HeliumTodo.Desktop/Services/ReminderService.cs` | `Timer` + `HashSet` 去重 + `ScanAsync()` 给自检 |
| 平台 | `HeliumTodo.Desktop/Services/WindowsToastNotifier.cs` | 转托盘气泡（Win10 起统一进通知中心） |

### 12.3 托盘：为什么不用 WinForms 的 `NotifyIcon`

用 `NotifyIcon` 得把 TFM 改成 `net10.0-windows`；而 `HeliumTodo.Verification` 是
`net10.0` 并且引用 `HeliumTodo.Desktop`，一改就是连锁反应。所以 `TrayService` 直接
P/Invoke：`Shell_NotifyIconW` + 自建隐藏窗口 + 独立 STA 线程消息循环 + 手画图标
（`CreateDIBSection` → `CreateIconIndirect`，32×32 圆角方块 + 白色 H）。

还处理了 `TaskbarCreated` 广播 —— 资源管理器重启之后图标会自动补回来，
否则用户重启一次 explorer 就再也点不到托盘了。

### 12.4 IPC：只有契约，没有实现

```
IIpcChannel（HeliumTodo.Domain/Ipc/IIpcChannel.cs）
├─ bool IsRunning
├─ string Endpoint
├─ Task StartAsync(CancellationToken) / Task StopAsync()
├─ Task<IpcResponse> SendAsync(IpcRequest, CancellationToken)
└─ event EventHandler<IpcRequest>? RequestReceived
```

| 类型 | 内容 |
| --- | --- |
| `IpcRequest` | `required string Action` + `JsonElement? Payload` |
| `IpcResponse` | `required bool Ok` + `string? Error` + `JsonElement? Data`（`Success` / `Failure` 工厂） |
| `IpcActions` | `system.ping` / `system.describe` / `schedule.query` / `todo.query` / `todo.create` / `focus.query` |
| `IpcJson` | camelCase + 大小写不敏感 + 不写 null |
| `NullIpcChannel` | 永远 `IsRunning = false`，`SendAsync` 返回「没有可用的进程间通道。」 |

DI 里注册 `services.AddSingleton<IIpcChannel, NullIpcChannel>();`，
将来换成命名管道实现即可，界面一行都不用改。**界面里没有任何 IPC 字样**（用户明确要求）。

### 12.5 时间轴首尾留白

`TimelineGrid.VerticalPadding`（默认 14）把"线的位置"和"文字的位置"分开：
线画在 `VerticalPadding - StartHour * HourHeight`，内容高度 = `24 × HourHeight + 2 × VerticalPadding`。
方块高度也按去掉上下留白之后的可画区算，否则会出现"线在 14、方块还在 0"的错位。

## 13. 存储层

```
  SQLite 单文件：%AppData%\Helium Todo\helium.db（WAL 模式）

  ┌────────────────────┐       ┌────────────────────┐
  │ Schedules          │       │ Todos              │
  │ Id (PK)            │       │ Id (PK)            │
  │ Title / Notes      │       │ Title / Notes      │
  │ Start / End        │       │ Priority           │
  │ RecurrenceRule     │       │ PlannedStart/End   │
  │ Status             │       │ DueAt              │
  │ idx: Start, Status │       │ IsCompleted        │
  └────────────────────┘       │ idx: IsCompleted,  │
                               │      Priority, DueAt│
                               └────────────────────┘

  ┌────────────────────┐       ┌────────────────────┐
  │ FocusSessions      │       │ StudyRooms         │
  │ Id (PK)            │       │ Id (PK) / Code(UQ) │
  │ PlannedStart       │       │ Name / HostName    │
  │ PlannedDuration    │       │ IsLocalOnly        │
  │ ActualDuration     │       └─────────┬──────────┘
  │ Status             │                 │
  │ TodoId (FK 逻辑)    │       ┌─────────▼──────────┐
  │ idx: PlannedStart, │       │ StudyRoomMembers   │
  │      Status        │       │ PeerId / DisplayName│
  └────────────────────┘       │ IsOnline / IsFocusing│
                               └────────────────────┘
                               ┌────────────────────┐
                               │ StudyRoomMessages  │
                               │ SenderName / Content│
                               │ SentAt / IsSystem  │
                               └────────────────────┘
```

**仓储层**

```
  ScheduleRepository   获取当天/区间日程、增删改
  TodoRepository       获取全部/今天、软删除、批量更新
  FocusRepository      今日统计、最近记录、区间汇总
  StudyRoomRepository  最近房间、成员、消息（取最近 50 条）
```

数据访问全部走 EF Core，**没有** 手写 SQL（唯一例外是建库时的 `PRAGMA journal_mode = WAL`）。

> **后加的列**：`StudyRooms.ClosedAt`（可空）是在「离开房间要记住」这个需求之后才加的。
> `null` 表示「还在房间里」，只有它为 null 的行才会被 `StudyRoomRepository.GetOpenAsync()` 载回界面；
> 点「离开」时写时间戳，重新进同一个房间时再清回 `null`。

### 老库怎么跟上新结构

项目故意不用 EF Migrations（结构还在动，迁移文件会一直冲突），所以
`EnsureDatabaseReady()` = `EnsureCreated()` + `EnsureColumns()`：

```
EnsureCreated()  只在「库 / 表还不存在」时建表，已有库它一个字段都不改
EnsureColumns()  自己查 PRAGMA table_info，缺哪列就 ALTER TABLE 补哪列
```

补 `StudyRooms.ClosedAt` 时还顺带把**存量房间**一次性标成已离开 ——
因为升级前根本没有「离开房间」这个概念，那些行都是「点了离开却没记住」的残留。
不这么处理，用户升级后第一次打开自习室页仍旧会看到自己在房间里。
时间存储约定：`DateTime` → TEXT `"yyyy-MM-dd HH:mm:ss"`，`TimeSpan` → TEXT `"hh:mm:ss"`，
枚举 → INTEGER，布尔 → 0/1。

---

## 14. 扩展点（需求第 8 条）

| 扩展点 | 接口 / 入口 | 怎么加 |
| --- | --- | --- |
| **主题** | `IHeliumTheme` + `ThemeRegistry.Register()` | 实现 4 个属性（Id / DisplayName / Kind / AccentColorHex）后注册 |
| **插件** | `IHeliumPlugin` | 编译成 dll 丢进 `%AppData%\Helium Todo\Plugins\`，`PluginHost` 自动扫 |
| **插件页面** | `IHeliumHostContext.RegisterPage(icon, title, factory)` | 注册的页面会自动出现在左侧导航栏（`ShellViewModel` 读 `pluginHost.RegisteredPages`） |
| **网络实现** | `IPeerTransport` | 换 DI 里那一行；界面和业务都不用动 |
| **数据库** | `AddHeliumData(path)` | 换实现就换这一行 |
| **配色** | `AccentSources` + `AppConfig.AccentSource` | 已内置三源（主题/壁纸/自定义） |
| **配置项** | `AppConfig` 加属性 + `ConfigService` 自动序列化 | 加字段即可，JSON 里自动出现 |

**插件生命周期**（`PluginHost` 392 行）：

```
  LoadPlugins()
     ├ 扫 Plugins 目录下的 *.dll
     ├ 每个 dll 一个 AssemblyLoadContext（可卸载）
     ├ 找实现 IHeliumPlugin 的类型 → 无参构造
     └ Initialize(IHeliumHostContext host)  ← 上下文里给的是：配置读写、日志、数据访问、事件
       返回 (成功数, 失败数)  —— 单个插件坏了不影响其它插件，也不影响主程序

  ShutdownAll()   ← 退出时调用（desktop.ShutdownRequested）
```

**主题链路**（`ThemeService` 264 行）：

```
  IHeliumTheme.AccentColorHex
        │
        ▼
  MaterialPrimary{Light,Mid,Dark}(Brush|ForegroundBrush)   ← 0.5.0 起按 Material 键名直写应用资源
        │
        ▼
  Application.Resources["SystemAccentColor"] 等 7 个键
        │
        ▼
  DesignTokens.axaml 里 DynamicResource 引用 → 所有强调色一起变
```

> 0.5.0 首版曾把 MaterialTheme 当唯一主题；2026-11-10 按用户反馈回退到 FluentAvalonia
> （小尺寸按钮/下拉框观感），当前链路是 `FluentAvaloniaTheme.CustomAccentColor` +
> `SystemAccentColor` 兜底，详见 §20.14。下面这段 Material 键名方案只作历史记录。

---

## 15. 目录树

```
Helium todo demo/
├── HeliumTodo.slnx                    解决方案（.slnx 新格式）
├── global.json                        SDK 锁定 10.0.401
├── Directory.Build.props              版本号 0.3.5-alpha 等全局属性
├── Directory.Packages.props           集中包版本管理
│
├── HeliumTodo.Domain/                   领域层（零外部依赖）
│   ├── Models/                        Enums ScheduleItem TodoItem FocusSession StudyRoom
│   ├── Services/                      DayTimelineBuilder(518) FocusTimerService(225)
│   │                                  StudyRoomService(373) RecurrenceExpander(106)
│   │                                  DayTimeline(147) Conflict(77)
│   ├── Theming/                       IHeliumTheme BuiltInThemes(9套) AccentColor
│   │                                  WallpaperAccentExtractor
│   ├── Imaging/                       ApngParser(450)
│   ├── Networking/                    PeerEndpoint PeerMessage
│   ├── Configuration/                 AppConfig(345) AccentSources
│   ├── Backup/                        备份格式（纯格式，不碰数据库）
│   │                                  BackupFormat BackupManifest
│   │                                  BackupPackageWriter BackupPackageReader
│   │                                  BackupInspection
│   ├── Plugins/                       IHeliumPlugin + IPeerTransport
│   └── VersionInfo.cs
│
├── HeliumTodo.Infrastructure/                   存储 + 网络实现
│   ├── HeliumDbContext.cs             6 张表
│   ├── *Repository.cs                 4 个仓储
│   ├── SeedData.cs                   首次运行示例数据
│   ├── DatabaseInitializer.cs        建库/补种子
│   ├── DataBackupService.cs          导出 / 导入 / 自动备份（唯一搬数据的地方）
│   ├── LanPeerTransport.cs(1369)     局域网 P2P
│   └── LocalOnlyPeerTransport.cs     纯本地占位
│
├── HeliumTodo.Desktop/                    表现层
│   ├── App.axaml(.cs)                 启动编排（七步）+ 资源字典
│   ├── Program.cs / ViewLocator.cs
│   ├── Views/                         6 页 + ShellView + MainWindow
│   ├── ViewModels/                    Shell Dashboard Schedule Todo Focus
│   │                                  StudyRoom Settings ColorPalette
│   │                                  TimelineEntry AppState ViewModelBase
│   ├── Controls/                      TimelineGrid ClockControls
│   │                                  ColorPickerControls WidthObserverPanel
│   ├── Services/                      PluginHost ThemeService
│   │                                  WallpaperDecoder WallpaperService
│   ├── Styles/                        DesignTokens.axaml(160) Controls.axaml(199)
│   └── Converters/                    CommonConverters.cs(6 个)
│
├── HeliumTodo.Tests/                  168 个 xunit 测试（16 文件）
├── HeliumTodo.SelfTest/               反射式测试运行器
├── HeliumTodo.Verification/            无头渲染自检（7 页 + 12 项 --check）
│
├── tools/
│   ├── check-file-integrity.ps1       SHA256 指纹校验
│   ├── file-hashes.json               166 个文件基线
│   └── migrate-av12.ps1               Avalonia 11→12 批量改名
│
├── output/render/                     7 张界面截图（自检产物）
└── docs/                              文档与历史截图
```

---

## 16. 工程约束与踩坑沉淀

### 验证三件套（每次改完必跑）

```powershell
dotnet build HeliumTodo.slnx -c Debug
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll     # 116 测试
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --shell          # 7 项渲染
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --refresh-check  # 6 项联动
powershell tools\check-file-integrity.ps1                                # 116 文件指纹
```

### 为什么有 `SelfTest` 和 `RenderCheck` 两个怪项目

| 问题 | 现象 | 解法 |
| --- | --- | --- |
| `dotnet test` 跑不起来 | testhost 被沙箱拦，报通信超时 | 自研 `SelfTest`：反射扫 `[Fact]` 自己调 |
| 界面没法在无头环境验证 | 无法截图确认"到底长什么样" | `RenderCheck`：Avalonia 无头平台 + `RenderTargetBitmap` 存 PNG |

> `SelfTest` 曾有个**假绿** bug：`method.Invoke()` 返回的 `Task` 被丢弃，
> 于是所有 `async Task` 测试"发射后不管"，await 之后的断言从没执行过。
> 修法是 `if (result is Task task) task.GetAwaiter().GetResult();`。
> **教训：看到"测试全绿"要先确认测试真的跑了**（104 个测试用 500 多毫秒，是可疑信号）。
>
> **另一条教训（2026-10-27）：会写文件的自检必须自带沙盒目录。**
> `--backup-check` 曾经复用真实的应用容器，直接把用户的待办从 5 条合并成了 20 条。
> 现在它跑在 `%TEMP%\helium-backup-sandbox\{guid}\` 里（`HELIUM_CONFIG_DIR`）。

### 其它长期有效的约束

| 约束 | 原因 |
| --- | --- |
| 自绘控件的可写属性必须显式 `BindingMode.TwoWay` | `AvaloniaProperty.Register` 默认 OneWay，拖了不回写、且**不报错** |
| `FASettingsExpanderItem` 的控件要放 `Footer` | 同时设 `Content` 又塞子元素 = `Content` 赋值两次（VS 报 23 个 MC30xx） |
| XAML 里 `Content` 重复赋值只有 VS 分析器报，`dotnet build` 不报 | 所以不能只看构建通过 |
| 改大段 XAML 前先 `Copy-Item` 备份 | 文本切片式批量改写的失败率高 |
| 强调色按 Material 键名写 `Application.Resources` | `MaterialTheme.PrimaryColor` 只收枚举色板；`PaletteHelper` 写不进挂进 Styles 的实例 |
| SkiaSharp 版本必须与 Avalonia 传递依赖完全一致 | 否则两个 `libSkiaSharp.dll`，随机崩溃 |

---

## 17. 数据流总图（一张图看完全部）

```
                     ┌──────────────────────────────────────────┐
   用户操作 ─────────►│  6 个页面 View（Avalonia XAML + 自绘控件）  │
                     └──────────────┬───────────────────────────┘
                                    │ 命令绑定 / 双向绑定
                                    ▼
                     ┌──────────────────────────────────────────┐
                     │  9 个 ViewModel（CommunityToolkit 源生成） │
                     └──┬──────────┬──────────┬──────────┬───────┘
                        │          │          │          │
              ┌─────────▼──┐  ┌────▼─────┐ ┌──▼───────┐ ┌▼──────────┐
              │ AppState   │  │ 领域服务  │ │ 仓储      │ │ 应用服务   │
              │ 事件总线    │  │ Core     │ │ Data     │ │ Services  │
              └─────┬──────┘  └────┬─────┘ └──┬───────┘ └┬──────────┘
                    │              │           │          │
                    │        ┌─────▼─────┐     │     ┌────▼─────────┐
                    │        │DayTimeline│     │     │ThemeService  │
                    │        │Builder    │     │     │Wallpaper*    │
                    │        │FocusTimer │     │     │PluginHost    │
                    │        └───────────┘     │     └──────┬───────┘
                    │                          │            │
                    │                    ┌─────▼─────┐  ┌───▼──────────┐
                    └───────────────────►│ EF Core   │  │ Avalonia 资源 │
                         数据变更通知      │ SQLite    │  │ / Skia 位图   │
                                          └───────────┘  └──────────────┘
                                                │
                                    ┌───────────▼────────────┐
                                    │ helium.db（本地单文件）  │
                                    └────────────────────────┘

                     ┌──────────────────────────────────────────┐
    网络 ───────────►│ LanPeerTransport ⇄ 局域网其他设备         │
                     │ StudyRoomService 订阅它的 3 个事件          │
                     └──────────────────────────────────────────┘
```

---

## 18. 渲染后端与内存画像

### 18.1 三个"内存"先分清

| 名字 | 含义 | 本应用（空配置、软件渲染） |
| --- | --- | ---: |
| 工作集 Working Set | 进程当前映射的物理页 | 178.2 MB |
| 私有字节 Private Bytes | 已提交的私有内存（含换出的） | 78.2 MB |
| 私有工作集 Private Working Set | 任务管理器「内存」列显示的那个 | **47.7 MB** |

说"占 170 MB"时指的通常是**工作集**。启动曲线：1 s 105 MB → 5 s 146 MB →
8 s 178 MB（提醒首扫 + 托盘线程 + 主题/壁纸初始化）→ 20 s 持平，**无泄漏**。

### 18.2 内存这笔账，以及默认后端为什么改回来

> **结论先说**：当前默认是 **`auto`（显卡）**。这一节保留的软件渲染数字，
> 是"想省内存就把设置页里的后端换成软件渲染"时你要付/能省多少的依据。

0.3.3 曾经把默认值定成软件渲染，理由是省内存（下面的表就是那次实测）。
0.3.5 把它改回 `auto`：窗口动效补齐之后，软件渲染"每改一次窗口尺寸 45 ms"的固定成本
藏不住了 —— 2560×1440 上图标轨展开只有 25–43 fps，同一台机器换 ANGLE 是 74 fps。
省下来的二十多兆私有内存，是用"动效掉一半帧"换的，不划算。

Avalonia 在 Windows 上默认用 **ANGLE**（把 OpenGL 翻译成 D3D11）。它会把手一伸，
把**整套显卡驱动栈**映射进进程：

| 模块 | 大小 | 是什么 |
| --- | ---: | --- |
| `nvgpucomp64.dll` | 94.1 MB | NVIDIA GPU 着色器编译器 |
| `nvwgf2umx.dll` | 85.6 MB | NVIDIA D3D 用户态驱动 |
| `nvppex.dll` | 7.2 MB | NVIDIA 后处理扩展 |
| `av_libGLESv2.dll` | 5.2 MB | ANGLE 本体 |
| `d3dcompiler_47.dll` | 4.5 MB | 微软 HLSL 编译器 |

这个界面是纯 2D 卡片 + 少量 180 ms 动画，Skia 的 CPU 光栅化画得出来；
但"画得出来"和"动得起来"是两件事 —— 软件渲染做窗口尺寸变化的动画时，代价见 §19.9 与 §19.10。实测对照：

| 后端 | 工作集 | 私有字节 | 私有工作集 | 线程 | 句柄 |
| --- | ---: | ---: | ---: | ---: | ---: |
| **ANGLE（现默认）** | 208.1 MB | 256.9 MB | 68.6 MB | 84 | 846 |
| Software（0.3.3–0.3.4 的默认，现在可在设置页选） | **178.2 MB** | **78.2 MB** | **47.7 MB** | **19** | **481** |

视觉等价性用像素统计验证过（同一界面两种后端各抓一张图）：像素差 > 8 的仅 **0.45%**，
且全部落在文字抗锯齿边缘。

**三种切换方式**（优先级从高到低）：

| 方式 | 写法 | 用途 |
| --- | --- | --- |
| 环境变量 | `$env:HELIUM_RENDER = "angle"`（`software` / `angle` / `wgl` / `vulkan` / `auto`） | 排障、临时对比 |
| 设置页 | 设置 →「底层设置」→ 渲染后端（`wgl` / `auto` / `software` / `vulkan`），重启生效 | 普通用户 |
| 配置文件 | `config.json` 的 `"RenderBackend": "vulkan"` | 脚本化部署 |

界面上的四档与底层枚举的对应，以及"为什么不能热切换"：

| 界面文字 | 配置值 | `Win32RenderingMode` |
| --- | --- | --- |
| OpenGL | `wgl` | `Wgl` |
| Avalonia 自带渲染器（默认） | `auto` | `AngleEgl` |
| 软件渲染 | `software` | `Software` |
| Vulkan | `vulkan` | `Vulkan` |

> 渲染后端必须在 Avalonia 初始化**之前**定下来，那时 `ConfigService` 还没读配置。
> 所以启动路径上只能"偷看"一眼配置文件：`RenderBackendChoice.PeekConfigured()` 用
> `JsonDocument.Parse` 只取 `RenderBackend` 一个字段，不调 `ConfigService.Load()`
> （后者首次运行会写文件，属于启动路径上不该有的副作用）。读取失败一律静默回落默认值。
> `App/Services/RenderBackendChoice.cs` 与 `Core/Configuration/RenderBackendSettings.cs`
> 分工：前者管"Avalonia 枚举怎么选"，后者管"字符串怎么认、界面怎么显示"，后者不引用
> Avalonia 所以能单测。

### 18.3 动态壁纸的限额

动图是"几十帧同时驻留"，代价随帧数线性涨。规则在 `App/Services/WallpaperDecoder.cs`：

| 常量 | 值 | 管谁 |
| --- | --- | --- |
| `MaxLongSide` | 1920 | 静态图（只占一帧） |
| `MaxAnimatedLongSide` | 1280 | 动图一律缩到这个长边 |
| `FrameBudgetBytes` | 96 MB | 超预算就抽帧，宁可变慢也不吃满内存 |

1920×1080 / 40 帧 GIF 实测：改前 332.3 MB 工作集 / 236.6 MB 私有字节 →
改后 **237.3 MB / 144.6 MB**，即壁纸的额外开销从 +154 MB 压到 +63 MB，
而壁纸上面还盖着一层蒙版，缩到 1280 肉眼几乎看不出来。

### 18.3.1 视频壁纸（MP4）与 30 帧/秒上限

动图除了 GIF/APNG，还支持 MP4 / MOV / WMV 等视频，走 **Windows Media Foundation**
（系统组件，零额外打包体积）。链路：

`WallpaperService.LoadAsync` → `WallpaperDecoder.DecodeFile` →
（视频）`MediaFoundationVideo.TryDecode` → `IMFSourceReader` 读帧 → `SKBitmap` 列表
→ （其它）原来的 `SKCodec` 路径。

三个来自实测的硬约束（细节见 `PROGRESS.md` 同日小节）：

| 约束 | 现象 | 应对 |
| --- | --- | --- |
| 视频处理器不接受"换分辨率"的输出类型 | `SetCurrentMediaType` 恒 `0xC00D36B4` | 只改子类型、保留原尺寸，拿到帧后自己用 Skia 缩 |
| `IMFSample` 必须显式释放 | 否则解码期峰值 3007 MB | 每帧 `Marshal.ReleaseComObject`，峰值降到 1510 MB |
| 非 Windows 无 MF | `DllNotFoundException` | `OperatingSystem.IsWindows()` 前置判断 + 中文错误提示 |

帧率上限落在 `Core/Media/PlaybackLimits.cs`（纯常量 + 纯函数，可单测）：

| 常量 | 值 | 说明 |
| --- | ---: | --- |
| `MaxFramesPerSecond` | 30 | 需求上限 |
| `MinFrameIntervalMs` | **34** | 1000/30 = 33.33；取 33 会变成 30.3 帧/秒 |
| `MaxFrameIntervalMs` | 10 000 | 防"停一小时"的异常帧挂死定时器 |
| `FallbackFrameIntervalMs` | 100 | 间隔缺失时的兜底 |

两处生效：解码侧（不足 34 ms 的帧直接丢，不占内存）+ 播放侧
（`WallpaperService.NextFrameDelayMs` 把原始间隔夹一遍再排进定时器，这是真正的兜底，
GIF 里写 20 ms 也拦得住）。

### 18.4 明确排除过的优化（省得重新试）

| 手段 | 结论 |
| --- | --- |
| `DOTNET_GCConserveMemory=7/9` | 稳定值与不设完全相同，白花 CPU |
| `InvariantGlobalization` | 本机根本没加载 ICU，省不到 |
| 关掉提醒/托盘线程 | 只省几百 KB 和 2 个线程，而它们是"后台常驻"的全部意义 |

### 18.5 顺带修掉的两个隐患

1. `App/ViewModels/FocusViewModel.cs` 的 `OnFocusCompleted` 是 `async void`，
   原来没有 try/catch —— 专注结束时若写库抛异常会直接带走进程；现在包了 try/catch + 日志。
2. 窗口隐藏到托盘后，`WallpaperService` 的逐帧定时器仍在跑（后台空耗 CPU / 白费电）；
   现在 `App.axaml.cs` 在隐藏时 `Stop()`、`RestoreMainWindow()` 里 `Start()`。

### 18.6 复现

```powershell
powershell -File tools\probe-memory.ps1  -ExePath <App.exe> -Seconds 20   # 三个数字 + 线程/句柄 + 模块明细
powershell -File tools\capture-window.ps1 -ExePath <App.exe> -OutPath out.png  # 抓真实窗口
```

---

*本文档由代码实际内容核对生成；行号与类名如与代码不符，以代码为准。*

## 19. 十个界面细节的约定

### 19.1 时间轴横向：刻度多宽，方块就从哪开始

24 小时时间轴上，「00:00…24:00」这些刻度文字画在网格自己的 `Canvas` 里，
而日程方块是**另一个** `Canvas` 上的 `Border`，两者的横坐标分别由
「XAML 里的一个数」和「ViewModel 里算出来的一个数」决定。这两个数一旦写岔，
方块就会压住刻度文字（用户截图里 14:00/16:00/18:00 被整行盖住，就是这么来的）。

约定：**只有一个来源**，`HeliumTodo.Desktop/ViewModels/TimelineMetrics.cs`：

| 常量 | 值 | 用在哪 |
| --- | ---: | --- |
| `LabelWidth` | 46 | `DashboardView.axaml` 的 `LabelMargin`、网格画刻度的右边界 |
| `AxisGap` | 8 | 刻度文字与竖轴线之间的呼吸缝 |
| `LeftInset` | **54** = 46 + 8 | `TimelineEntryViewModel.Resize(...)` 的 `leftInset` 参数 |

`Resize` 拿到 `leftInset` 后先夹到画布宽度以内，再按 `canvasWidth - inset` 平分车道：

```csharp
var inset = Math.Clamp(leftInset, 0, Math.Max(0, _canvasWidth - 1));
var laneSpan = Math.Max(0, _canvasWidth - inset);
var laneWidth = laneSpan / laneCount;
X = inset + _entry.Lane * laneWidth;
```

`--timeline-check` 里对应的三条断言（7–9）就是拿这个数当基准：
刻度宽度 = `LabelWidth`、第一列 X = 54、列宽 = `(画布宽 - 54) / 车道数`。

### 19.2 改渲染后端：先问，再重启

改 Avalonia 的渲染后端**必须在 Avalonia 初始化之前**定下来（那时配置还没读），
所以运行时切换是不可能的，只能改配置 + 重启进程。于是有了这个流程：

```
用户动下拉框
  └─ NeedsRestart(Config.RenderBackend, 选中值) 为真？
       ├─ 否 → 只刷新一行状态文字，什么都不写
       └─ 是 → 记进 _pendingRenderBackendKey，弹「更改此项需要重启，是否立即重启」
              ├─ 确认 → 写配置 + 立即 Save()（不走 500 ms 防抖，进程马上要退）→ TryRestart()
              └─ 取消更改 → 下拉框静默退回原值，配置一个字都不写
```

四个必须守住的点：

| 点 | 为什么 |
| --- | --- |
| 选中的那一刻**不能**先写配置 | 否则「取消更改」已经晚了 —— 配置里已经是新值，下次启动就变了 |
| 退回原值要 `_suppressRenderBackendChange` 包一层 | 否则赋值又触发 `OnSelectedRenderBackendChanged`，再弹一次，死循环 |
| `ConfirmBackendRestart` 直接 `_configService.Save()` | `ScheduleSave()` 有 500 ms 防抖，重启会把进程送走，配置就丢了 |
| `TryRestart()` 里**先放单实例锁再起新进程** | 锁还在的话新进程认为"已经有一个实例"，只喊一声就退出，表现为"界面闪一下、后端还是旧的" |

`TryRestart()` 的顺序：`_exitRequested = true`（否则关闭会按"藏到托盘"处理）
→ `_singleInstance?.Dispose()` → `Process.Start(ProcessPath)` → `desktop.Shutdown()`。

界面用的是**页内遮罩浮层**，不是 `ContentDialog` / `ShowDialog`：
自绘无边框窗口下模态窗口容易被抢焦点或被别的窗口盖住，无头自检也点不到它。

### 19.3 调试器里不许自己重启（MSB3027 的来历）

`TryRestart()` 第一件事是判断 `Debugger.IsAttached`，在场就直接返回
`RestartOutcome.BlockedByDebugger`，一个字节都不动。原因是一条真实的故障链：

```
VS 调试 → 点「确认」→ 我们 Process.Start 一个新进程（不受调试器管）
        → 旧进程（调试器管的那个）退出 → VS 显示"调试已结束"
        → 新进程还活着，抓着 bin\Debug\net10.0\HeliumTodo.Desktop.exe
        → 下一次编译：MSB3026 重试 10 次 → MSB3027「文件被 HeliumTodo.Desktop (PID) 锁定」
```

而且新进程往往已经藏进托盘（窗口关了 → 我们的关闭拦截生效），
用户在任务管理器之外根本找不到它，只能猜哪个进程该杀。

三种结果的语义（`HeliumTodo.Domain/Lifecycle/RestartPolicy.cs`）：

| 结果 | 谁产生 | 界面怎么说 |
| --- | --- | --- |
| `Started` | `RestartPolicy.Decide(false)` | 不提示，进程马上退出 |
| `BlockedByDebugger` | `RestartPolicy.Decide(true)` | 「配置已保存…请用调试器的「重新启动」(Ctrl+Shift+F5)」 |
| `Failed` | 拿不到 exe 路径 / 起进程抛异常 | 「自动重启没成功，请手动关闭后重新打开」 |

调试器判断也做成了纯函数 `RestartPolicy.Decide(bool)`，好让单元测试钉住规则；
`--restart-prompt-check` 则用 `HELIUM_FAKE_DEBUGGER=1` 模拟"调试器在场"，
断言**配置照样存盘**、**没有偷偷开新进程**（对比 `HeliumTodo.Desktop` 进程数）、
提示语里带着「调试」二字。

要是已经踩坑了（进程在跑、编译报 MSB3027）：

```powershell
Get-CimInstance Win32_Process -Filter "Name='HeliumTodo.Desktop.exe'" |
    Select-Object ProcessId, ParentProcessId, CommandLine
Get-Process -Name HeliumTodo.Desktop | Stop-Process -Force
```

### 19.4 提示条叠在功能区上方，卡片圆角只有一个来源

两条都是"看起来像层级错乱"的观感问题，根因却一个在布局、一个在控件模板。

**提示条用自绘 `Border` 叠在功能区上方，`FAInfoBar` 已经不再使用。**
历史走过两步：最早它叠在内容顶上，因为 `FAInfoBar` 的底板带透明通道，
底下的卡片标题透上来，看着像"两行字压在一起"（0.3.3 之前那次修复）；
于是改成占一行把内容推开 —— 但用户要的是**叠**（"直接叠在功能区上方
而不是与设置界面错开"），占一行反而把内容整体挤下去了。

现在的做法是：不用 `FAInfoBar`，自己画一条 `Border`，放进功能区的同一格里。

```xml
<Grid RowDefinitions="Auto,*" Margin="12,4,12,12">   <!-- 标题一行，内容一行 -->
  <Grid Grid.Row="0" .../>                            <!-- 模块名那一行 -->
  <Grid Grid.Row="1">                                 <!-- 卡片 + 提示条同一格 -->
    <Border Classes="card" ...> ... </Border>          <!-- 先画：内容在下 -->
    <Border Name="NoticeBar" ZIndex="10" ... />        <!-- 后画：提示条在上 -->
  </Grid>
</Grid>
```

三条约定：

- **必须是兄弟节点、排在卡片后面**，靠 `ZIndex="10"` + 文档顺序压在上面。
  注意 Avalonia 12 里要写 `ZIndex="10"`，写 `Panel.ZIndex` 会报
  `AVLN3000: Unable to find suitable setter or adder for property ZIndex`。
- **底板不透明**，颜色跟着明暗走（`NoticeSurfaceBrush` 亮 `#FFF7F7F7` / 暗 `#FF2B2B2B`），
  所以即使叠着也看得清，不会再"透"出底下的字。
- **3 秒自动消失**，规则在 `HeliumTodo.Domain/Notifications/NoticePolicy.cs`
  （`DisplayMilliseconds = 3000`、`ShouldExpire(elapsed)`）。
  ViewModel 用一个 200 ms 的 `DispatcherTimer` 轮询，`ExpireNotice(DateTime now)`
  把"现在"当参数传进去 —— 自检因此不用真的等 3 秒。

**圆角只有一个来源：`RadiusCard`（18）。**
`Border.card` 用 `CornerRadius="{DynamicResource RadiusCard}"`；
而 `FAInfoBar` 和 `FASettingsExpander` 这两个模板控件**都没有 `CornerRadius` 属性**
（后者公开属性只有 `Description` / `IconSource` / `Footer` / `IsExpanded` /
`ActionIconSource` / `IsClickEnabled` / `Command` / `CommandParameter`），
所以统一靠外层 `Border` + `ClipToBounds="True"` 裁出来：

```xml
<Border CornerRadius="{DynamicResource RadiusCard}" ClipToBounds="True">
  <fa:FASettingsExpander Header="外观" ... />
</Border>
```

`ClipToBounds` 不能省 —— 不裁剪的话控件自己的直角背景会从圆角外壳里冒出来。
调圆角时只改 `HeliumTodo.Desktop/Styles/DesignTokens.axaml` 里的 `RadiusCard`，
不要在各个视图里写死数字。

### 19.5 分区统一：模块名在外，内容在内

六个页面的结构现在是同一套（以「今日工作流」为模板）：

```xml
<Grid RowDefinitions="Auto,*" Margin="12,4,12,12">
  <Grid Grid.Row="0" ColumnDefinitions="*,Auto" MinHeight="40" Margin="4,0,4,44">  <!-- 模块名那一行 -->
    <StackPanel ...>标题 + 概览</StackPanel>
    <StackPanel Grid.Column="1" ...>动作（按钮 / 统计）</StackPanel>
  </Grid>
  <Border Grid.Row="1" Classes="card">…功能区…</Border>
</Grid>
```

三条理由，缺一条这个约定就不成立：

1. **标题属于页面，不属于卡片。** 标题写进卡片头时，页面顶部就是卡片的圆角，
   而不是这块区域的起点；换页时标题会跟着卡片的 Padding 上下跳。
2. **标题行与功能区之间只隔一个 `Margin`。** 别的页面要加提示条时，
   提示条自己占一行（`RowDefinitions="Auto,Auto,*"`），标题行位置不变。
3. **概览数字横排，不要数字在上、单位在下。** 竖排的两行会把标题行撑高，
   六个页面切来切去时卡片顶边忽高忽低。三页统一成
   `数字（metric 16）+ 单位（caption）` 的水平 `StackPanel Spacing="5"`。

提示条收起时不留空白，靠的是**外层 `Panel` 带 `IsVisible`**：

```xml
<Panel Grid.Row="1" Margin="0,0,0,8" IsVisible="{Binding IsNoticeVisible}">
  <fa:FAInfoBar IsOpen="{Binding IsNoticeVisible}" ... />
</Panel>
```

`IsVisible=false` 的面板不参与布局，它自己的 `Margin` 也一起消失 ——
只绑 `FAInfoBar.IsOpen` 的话，条子高度是 0 了，但外面那 8px 还在。

**标题行的字比卡片头大两档**（`Styles/Controls.axaml`）：

| 类名 | 字号 | 用在哪 |
| --- | --- | --- |
| `pageHeader` | **24** SemiBold | 模块名（六个页面统一） |
| `pageCaption` | **15**（透明度 0.62） | 标题右边的概览/字幕 |
| `pageMetric` | **24** SemiBold | 标题行右侧的统计数字 |
| `cardHeader` | 16 SemiBold | 卡片自己的头（一张卡） |

比 `cardHeader` 大两档是有原因的：模块名代表"整个页面"，卡片头只代表"一张卡"，
两者同样大小时看不出层级；只大一档（20）在 1400 宽的窗口里仍然"缩"在左上角，
看不出这是个页面标题。

标题行统一 `MinHeight="40"`、下边距 `Margin="4,0,4,20"` —— 前者让六个页面的行高
（而不是行"内容高"）一致，后者把功能区整体压下去，让标题与内容之间透气。
（0.3.3 一度用过 44，后来按用户要求"只留一行小三宋体的空隙"收紧到 20：15pt ≈ 20px，
卡片顶边随之从 88 回到 64。）
字号从 20 调到 24 时 `MinHeight` 也必须跟着从 32 抬到 40，否则 24px 的字会顶满
32px 的行、标题和卡片之间又变得贴在一起。

**六个页面的功能区宽度也必须是同一个来源。** 设置页原先自己加了 `MaxWidth="1100"`，
在 1400 宽窗口里它的卡片比其他页窄一圈、右边缘落在 x=1122，切换页面时能明显看到
卡片"缩回去"。现在 `MaxWidth` 全部删掉，宽度只由页面外边距 `Margin="12,4,12,12"` 决定。

实测（`--shell` 渲染 1400×900 后按像素量，2026-11-02）：

| 页面 | 标题文字 y | 卡片顶边 y | 标题左边 x | 卡片左右边缘 |
| --- | --- | --- | --- | --- |
| 今日工作流（模板） | 14–36 | **64** | 16 | 12 – 1387 |
| 待办事项 | 14–36 | **64** | 16 | 12 – 1387 |
| 日程 | 14–36 | **64** | 20 | 12 – 1387 |
| 专注 | 14–36 | **64** | 16 | 12 – 1387 |
| 自习室 | 14–36 | **64** | 19 | 12 – 1387 |
| 设置 | 15–36 | **64** | 16 | 13 – 1386 |

（标题左边 x 差 1–4 px 是字形本身左边距不同：「日」「待」这些字贴左，「自」「专」略松，
不是对齐问题。行高 64 = `4`(页面外边距) + `40`(标题行 MinHeight) + `20`(标题行下边距)，
其中 20 是照「一行小三宋体」定的 —— 15pt ≈ 20px。）

设置页原来内容块限宽 `MaxWidth="1100"` 并居中，导致它的卡片比其他页窄一圈
（右边缘落在 x=1122），切页时能明显看到"缩回去"。0.3.3 把三处 `MaxWidth` 全删掉，
宽度只由页面外边距决定，六页左右边缘才对得上。

### 19.6 外观只有一个来源：明暗决定底色，强调色决定颜色

0.3.3 把原来的「主题下拉 + 强调色来源下拉」合并成两件事，各管一半：

| 谁来管 | 配置项 | 落到哪儿 |
| --- | --- | --- |
| 明暗 | `AppConfig.ColorScheme`（light / dark） | `fluent-light-blue` / `fluent-dark-blue` 主题 + 卡片底色 |
| 颜色 | `AppConfig.AccentSource` + `CustomAccentHex` | `ThemeService` → `MaterialPrimary*/MaterialSecondary*` 资源键 |
| 玻璃浓度 | `AppConfig.CardOpacity`（0–100，有壁纸自动 82） | `CardSurfaceBrush` 的 alpha |
| 暗色灰蒙版 | `AppConfig.DarkWallpaperScrim`（10–20） | `DarkWallpaperScrimOpacity` 资源 |

`ThemeId` 还留在配置里，但已经**降级成派生物**：界面改明暗时由 `ColorSchemes.ToThemeId`
算出来写进去，只为兼容老配置和备份清单。老配置没有 `ColorScheme` 字段时，
`ConfigService.Load()` 会用 `ColorSchemes.FromThemeId(ThemeId)` 反推一次 ——
少了这一步，用着暗色的老用户升级后会被闪一下白屏。

#### 坑：主题字典会把同名应用级资源挡住

`CardSurfaceBrush` 原本写在 `DesignTokens.axaml` 的 `ThemeDictionaries` 里（Light/Default/Dark 三份）。
结果 `App.ApplyCardOpacity(...)` 里

```csharp
Application.Current.Resources["CardSurfaceBrush"] = new SolidColorBrush(...);
```

写了等于没写：**主题字典的查找优先级高于应用级同名资源**，界面纹丝不动，也不报任何错。

修法是把三份定义全删掉，只在字典外面留一个默认值，让代码成为唯一来源。
验证方式也记一下：光断言 `Resources["CardSurfaceBrush"]` 是不够的（那是我们刚写进去的值），
必须去视觉树里找一个 `Classes` 含 `card` 的 `Border`，看它 `Background` 的 alpha ——
`--appearance-check` 的第 9 条断言就是干这个的。

#### 动态保存：150ms 防抖 + 切页补写

设置项改动立刻写盘会连累拖滑块（一次拖动几十次 `Save`），
所以 `SettingsViewModel.ScheduleSave()` 走 150ms 防抖；
但"拖完立刻切页"时定时器还没到点，页面就被换走了 ——
`SettingsView.OnDetachedFromVisualTree` 里补一次 `FlushPendingSave()`，
保证这一脚不会踩空。

### 19.7 一个页面只有一张卡：设置页的"十张小卡"事件

`SettingsView.axaml` 原本是十个分组 = **十张独立小卡**，每张各自
`<Border CornerRadius="{DynamicResource RadiusCard}" ClipToBounds="True">` 套一个
`FASettingsExpander`，外面用 `StackPanel Spacing="12"` 竖排。

问题不在于难看，而在于**跟别页不是一套**：其他五页都是一整块功能区卡片，
设置页切过去能清楚看到卡片之间那 12px 页底色缝，看起来像"没做完的页面"。
用户连着提了两次（"你老是把设置界面落下"），都是这个观感。

修法是把十张卡合并成一张：

```xml
<Border Grid.Row="2" Classes="card" Padding="0" ClipToBounds="True">
  <ScrollViewer>
    <StackPanel Margin="0" Spacing="0">
      <!-- 十个 FASettingsExpander，外壳的 CornerRadius 全部改成 0 -->
```

十处外层 `CornerRadius` 必须同时改成 `0`：不改的话每段还是自己长成小卡，
合起来就是"一张大卡里嵌十张小卡"。`Padding="0"` + `ClipToBounds="True"` 是为了让
内部滚动条贴到卡片边上、且内容不会溢出圆角。

自习室同类问题：它把"局域网联机"状态做成了常驻 `FAInfoBar` 单独占一行，
于是这一页的卡片顶端比别人低 60px（y=124 vs 64）。改成把状态收进卡片第一行
（一行 `cardHeader` + 一行 `caption`）后六页齐平。
另外"没进房间"时那张创建/加入卡原来 `VerticalAlignment="Top"`，
卡片只到 y=262 就断了、下面一大片页底色 —— 去掉 `VerticalAlignment` 让它跟着行高拉伸。

#### `--layout-check`：让"被落下"自动报红

这类问题肉眼能看出来、但每次都靠人盯不现实，所以固化成 6 页 × 6 项断言：

| # | 断言 | 判据 |
| --- | --- | --- |
| 1 | 左边缘是 12 | 顶行卡片并集 `Min(X) == 12` |
| 2 | 右边缘是 宽-12 | 顶行卡片并集 `Max(X+W) == 1388`（两栏页必须取并集，只看单卡会误报） |
| 3 | 顶端是 64 | `Min(Y) == 64` |
| 4 | 顶行只有列间距那一条缝 | `Σ间隙 == 12 × (卡数-1)` |
| 5 | 有页面标题且字号 24 | 找到 `Classes` 含 `pageHeader` 的 `TextBlock`，`FontSize == 24` |
| 6 | 标题与卡片左边缘差 4 | `|标题X - 卡片X| == 4`（页面外边距 12 + 标题行 4） |

外加一条"卡片之间没有漏出底色"：沿宽度采 ~24 个点，**整行 95% 以上都是页底色
且连续 ≥3 行**才算一条缝；只统计"这条缝下面还有卡"的情况（页面底部露底色是正常的）。

两个坑记在这里：

1. **不能用单点竖线扫描判缝**。输入框、内嵌面板的底色可能正好等于页底色，
   单点扫过去就误报。必须按整行判定 —— 真正的卡片间隙是整行贯穿的。
2. **`CopyPixels` 需要非托管指针**，而 `HeliumTodo.Verification` 没开 `AllowUnsafeBlocks`。
   用 `Marshal.AllocHGlobal` + `CopyPixels(..., pointer, length, stride)` + `Marshal.Copy` 中转，
   比为一个自检工具改动全局编译选项划算。

### 19.8 一层蒙版、一条叠层的提示、一条能展开的图标轨

三件事都是 0.3.3-alpha 那一轮按用户原话改的，共同点是"以前用两个东西凑出的效果，
现在只留一个，而且必须有自检盯着"。

**① 壁纸蒙版只有一层，颜色和强度由明暗决定。**
以前是两层：`WallpaperScrimBrush`（亮白 / 暗黑）+ 一层独立的"暗色灰蒙版"，
暗色下两层灰叠在一起，像两张半透明纸。现在合并成一层，规则收进
`HeliumTodo.Domain/Theming/WallpaperScrim.cs`：

| 项 | 亮色 | 暗色 |
| --- | --- | --- |
| 底色 | `#FFFFFF`（提亮） | `#000000`（压暗） |
| 强度 | `percent / 100` | `min(0.95, percent / 100 × 1.6)` |

`DarkStrengthBoost = 1.6`、`MaxStrength = 0.95` 都是常量：暗色下用户那个 45%
会变成 72%，但永远不到 1 —— 到 1 就是纯黑，壁纸等于被关掉了。
切换明暗时必须**重算一遍**（`App.ApplyWallpaperScrim(percent, isDark)`），
否则暗色下还停着亮色那份白色底板。

`WallpaperScrimBrush` 也从 `ThemeDictionaries` 里搬了出来（和 `CardSurfaceBrush`
同一个坑：主题字典优先于应用级同名资源，代码赋值会静默失效）。

**② 图标轨可以展开成"图标 + 名称"，宽度带非线性过渡。**
尺寸集中在 `HeliumTodo.Domain/Theming/RailMetrics.cs`：收起 56、展开 188、
水平外边距 12、条目宽 44 / 176。两侧一起变才不会出现"容器拉开了、按钮没跟上、
名称被裁成半截"这种事。动效是 `DoubleTransition` 260 ms + `CubicEaseOut`，
挂在轨道 `Border` 的 `Width` 上：

```xml
<Border Name="NavRail" Width="{Binding RailWidth}">
  <Border.Transitions>
    <Transitions>
      <DoubleTransition Property="Width" Duration="0:0:0.26">
        <CubicEaseOut />
      </DoubleTransition>
    </Transitions>
  </Border.Transitions>
</Border>
```

顺手把「设置」从 `FooterItems` 挪进了 `MenuItems`（紧跟「自习室」）——
用户要求它出现在联机下面，而 `FooterItems` 是贴底渲染的，位置对不上。

**③ 设置页的分组控件和分组里的每一行都必须透明。**
0.4 时有**两层**底色：`FASettingsExpander` 与 `FASettingsExpanderItem` 的控件主题
各自把 `Background` 写死成 `#B3FFFFFF`，所以外层设了 `Transparent` 之后，
十个分组里的每一行仍在各刷一层白 —— 叠在卡片上就是"控件下面垫了一层"。

P4 换成自绘 `SettingsExpander` / `SettingsExpanderItem` 后，模板
（`HeliumTodo.Desktop/Styles/SettingsControls.axaml`）里**没有任何一层给背景**，
这个问题从源头消失，`fa|` 覆盖样式和 `xmlns:fa` 一并删除。

`--rail-check` 会数 10 个分组、27 行，并逐行断言 `Background` 透明或 null；
以后改模板时别给根 `Border`/`StackPanel` 加底色，否则这条检查会红。

**④ 内外圆角同半径，内容向内缩 8px。**

设置卡是 `RadiusCard=18`；如果里面 `Padding=0`，滚动内容会直接顶到卡的边上，
分组的悬停/按下底色就是一块直角矩形压在圆角上，四个角看起来"缺一块"。
约定：

```xml
<Border Classes="card" Padding="8" ClipToBounds="True">
  <Border CornerRadius="{DynamicResource RadiusCard}" ClipToBounds="True" Background="Transparent">
    <ScrollViewer …> … </ScrollViewer>
  </Border>
</Border>
```

外边距 8（`SpaceS`）+ 内层同半径 18 + 内层裁边界。实测内层实际落在 `x=9, y=9`、
`1291x755`（卡的边框占 1px），`--rail-check` 的三条断言盯着这三件事。

三条对应的自检：

| 自检 | 项数 | 钉住什么 |
| --- | --- | --- |
| `--appearance-check` | 14 | 卡片 alpha、暗色底色、蒙版颜色与强度（0.45 / 0.72 / 上限 0.95）、画出来的刷子就是资源那一份、壁纸缩放的尺寸与裁切位置 |
| `--notice-check` | 13 | 提示条默认收起、与滚动容器**重叠**（证明是叠层）、`ZIndex>=10`、亮暗底板不同、2 秒不消失 / 3.5 秒消失、点 × 立刻收起 |
| `--rail-check` | 23 | 设置排在自习室下面、收起 56/44（按钮真实 Bounds 44×44）、展开时按钮跟着轨道一起变宽、**导航按钮自己没有宽度过渡**（只有轨道一条在动）、6 个名称靠不透明度淡入淡出（始终留在布局里）、10 个分组标题 + **34 行分组行**的底色全透明、设置卡 8px 内边距、内层圆角与卡片同为 18、内层裁边界 |
| `--perf-check` | 14 | 改尺寸的固定成本（A–M 十三档）、降质确实更快、内容矩阵比改尺寸快一半、遮罩 Alpha 与纯重绘同量级、**P95 < 2×P50** 且**最慢一帧 < 4×P50**（分两条，避免孤立尖峰让自检随机变红）、末尾对 §15.3 预算断言（纯重绘 P50 ≤ 33.3 ms、图标轨 P50 ≤ 33.3 ms、降质不倒退、私有字节 ≤ 300 MB） |
| `--anim-check` | 16 | 政策数值（180ms/10 帧/5 帧下限/250ms 阈值）、动画中途被最小化后不透明度和变换都干净、遮罩被收掉、回来会重播、`Settle()` 干净、抓手不偏 |

截图开关也跟着补齐（都在 `HeliumTodo.Verification`）：

- `HELIUM_DARK=1` / `HELIUM_CARD_OPACITY=NN`：现在是**全局**开关，`--shell` 也吃；
  以前只写在 `RenderPage` 里，导致 `--shell` 拍出来的"暗色图"和亮色一模一样。
- `HELIUM_RAIL_EXPANDED=1`：先展开轨道、等 500 ms 把 260 ms 过渡走完再截图。

### 19.9 窗口动起来的时候：能让系统播的就别自己播

窗口动画现在只剩两件事：**打开时的遮罩淡出**（自己播）和**最小化/最大化**
（交给 DWM，见 5.2）。拖拽改大小仍然逐帧改窗口尺寸，那是用户手动在拖，没有别的办法。

软件渲染下"改一次窗口尺寸"到底多贵（`--perf-check`，1400×900 ↔ 1920×1280，无头真绘图）：

| 场景 | 平均 ms/帧 | 折合帧率 |
| --- | --- | --- |
| A 改尺寸 + 高品质壁纸（改造前） | 117 | 9 |
| E 改尺寸，只布局不画 | 0.1 | — |
| H 改尺寸，什么内容都不画 | 24 | 41 |
| J 低品质壁纸 + 无阴影（拖拽时用的） | 42 | 24 |
| M 只改遮罩画刷 Alpha（开场用的） | 4.4 | 228 |
| D 不改尺寸，纯重绘 | 5.0 | 201 |

从这张表能读出三件事：布局几乎不花钱；钱全在"整窗重新光栅化 + 帧缓冲重建"；
**不改窗口尺寸，成本直接掉到 1/20**。所以约定四条：

1. **能让系统播的就别自己播**。最小化/最大化/还原交给 DWM（`WS_CAPTION` 那个开关）。
   自己写的逐帧改矩形版本已经删了 —— 软件渲染下 5~6 帧跑完 180 ms，又慢又不均匀。
2. **动画期间自动降质**：`HeliumTodo.Desktop/Windowing/InteractionQuality.cs` 用引用计数包住一段交互，
   进入时把壁纸降成 `LowQuality`、关掉卡片阴影；退出时还原。拖拽
   （`WindowResizeGrips`）包着它；`HELIUM_NO_INTERACTION_QUALITY=1` 可整体关掉做对照。
   动图壁纸只在预计 > 250 ms 时才停（视频壁纸重启要重新 seek + 解码首帧，负收益），
   规则在 `HeliumTodo.Domain/Windowing/WindowTransitionPolicy.ShouldPauseWallpaper`。
3. **拖拽只按帧改一次窗口**：`OnPointerMoved` 只记目标值，用 `TopLevel.RequestAnimationFrame`
   合并到下一帧（鼠标回报率比显示器刷新率高得多）。
4. **凡是要用代码改的资源，不许写进 `ThemeDictionaries`**：`CardSurfaceBrush`、`WallpaperScrimBrush`、
   `CardShadow` 都踩过 —— 主题字典优先级高于应用级同名资源，代码赋值静默失效。
   `App.ApplyCardShadow(bool isDark)` / `App.SetCardShadowSuppressed(bool)` 就是靠"资源只有一份"才生效的。

时间轴与帧预算的实话：**软件渲染在 2560×1440 下拖拽仍然只有 ~20 fps**（真机 45 ms/帧），
这是"内存 ↔ 流畅"的取舍。换成 DWM 播放的最小化/最大化在软件渲染下也是满帧，因为它们不经过我们。
`--perf-check` 的十条断言（含"遮罩 Alpha 与纯重绘同量级""内容矩阵动画快一半"）就是防止以后有人把这些改回去。

### 19.10 内部控件的展开：只有一条属性在动，壁纸先缩到自己那么大

图标轨展开（点左上角那个按钮，56 → 188）是应用里唯一一条"会改变整页可用宽度"的动画，
也是踩坑最多的一条。真机实测（2560×1440，260 ms 过渡）：

| 变化 | ms/帧 | 折合 |
| --- | --- | --- |
| 每个条目按钮各挂一条 `Width` 过渡 + 4K 壁纸直接交给 `Image` | 50.2 | 20 fps |
| 同上，临时关掉壁纸 | 23.3 | 43 fps |
| 只留轨道一条 `Width`，壁纸先缩放到窗口像素 | **31.7 / P50 23.1** | **≈43 fps** |
| 同上，换 GPU 后端（`HELIUM_RENDER=angle`） | **13.5 / P50 12.2** | **≈74 fps** |

三条约定：

1. **只有一条属性在动**。轨道 `Border` 的 `Width` 是唯一动的东西；条目按钮用
   `Classes.expanded` 切到"填满轨道"，自己不挂过渡；按钮里的名称用 `Opacity` 淡入淡出
   （`IsVisible` 会换一遍布局树，展开第一帧要多花 25 ms）。
2. **壁纸必须先缩放到窗口像素**。`HeliumTodo.Desktop/Services/WallpaperScaler.cs` 用
   `RenderTargetBitmap` 按 `UniformToFill` 裁一张与窗口 1:1 的图；把 4K 原图直接丢给
   `Image Stretch="UniformToFill"` 会让软件渲染**每帧重采样一次**（+27 ms/帧）。
   尺寸变化用 160 ms 防抖重做，源图和尺寸都没变就复用缓存；动图壁纸例外（直接把原图交出去）。
3. **动画期间同样降质**，但**不许**停动图壁纸：`BeginRailAnimation()` 传
   `pauseWallpaper: false` —— 260 ms 的过渡停一次视频再启要重新 seek，比不停更糟
   （阈值规则见 `WindowTransitionPolicy.ShouldPauseWallpaper`）。收尾用 `DispatcherTimer`，
   因为动画结束后没有帧，`RequestAnimationFrame` 永远等不到下一次回调。

判据的教训：`--perf-check` 原来用"最慢一帧 < 2 × P50"判断卡不卡，实测出现过
`P50 63.2 ms / 最慢 134.6 ms`（2.13 倍）而肉眼完全没事 —— 一轮只采 20 多帧，孤立尖峰是噪声。
现在拆成 `P95 < 2 × P50`（尾部抬高才叫卡）与 `最慢 < 4 × P50`（拦住 5 倍那种真跳变），
规则在 `WindowTransitionPolicy.IsFramePacingAcceptable` / `IsWorstFrameAcceptable`。
真机侧由 `tools/probe-rail-anim.ps1` 守着：帧数 ≥ 10、P50 ≤ 16.7 ms、P95 ≤ 33.3 ms。

壁纸缩放本身也要验，但不能靠截图：`--shell --wallpaper=<png>` 拍出来的图与不传壁纸时
**逐像素完全相同**（`RenderCheck` 的 `--wallpaper` 只改 VM 状态，不经过 `MainWindow` 的壁纸图层），
所以无头截图**看不出**壁纸画得对不对。改成直接测算术：`--appearance-check` 造一张
2560×1440 的"左红、中绿、右蓝"三竖带源图，缩到 1400×900，断言尺寸相等、左缘红、右缘蓝、
中间绿 —— UniformToFill 的系数是 0.625，左右各裁 160 px，所以左缘只能看到红、右缘只能看到蓝，
**颜色对得上就说明裁切居中且没左右翻转**。读像素的办法：`RenderTargetBitmap` 不能直接读，
先 `CopyPixels` 到 `WriteableBitmap` 的 `ILockedFramebuffer`，再 `Marshal.Copy` 取中间一行。

## 20. 0.5.0-alpha P0 的约定（2026-11-09）

0.5.0 的目标架构写在 [`DEVELOPMENT_SPEC.md`](DEVELOPMENT_SPEC.md)；本文其余章节描述的是
**当前实际运行的代码**。P0 只改了工程结构与许可证，运行行为与 0.3.5 相同，但有三条新约定：

### 20.1 五个项目与依赖方向

```
Desktop ──► Application ──► Domain
   │            ▲
   └──► Infrastructure ──┘
```

- `Domain`：纯 BCL，没有 `PackageReference`，不许出现 `using Avalonia` / EF / Npgsql。
- `Application`：用例与接口层：配置（`Configuration`）、进程间通信（`Ipc`）、通知接口
  （`Abstractions`）、`Result` 契约（`Contracts`）、跨仓储用例（`Services`）。
- `Infrastructure`：EF Core + PostgreSQL + 备份 v2 + 联机传输。
- `Desktop`：Avalonia 界面与组合根（`App.axaml.cs` 是唯一允许同时看到三层的文件）。
- `Verification`：无头渲染、十五项 `--check`、探针入口。

命名空间与项目名一一对应。**注意**：`HeliumTodo.Application` 会在名字查找里遮住
`Avalonia.Application`，所以 `App : Avalonia.Application` 与所有 `Application.*` 调用点
必须写全名；不要再尝试用文件级 `using Application = Avalonia.Application;` 绕过。

### 20.2 插件接口关闭

`IHeliumPlugin` / `IHeliumHostContext` 在 `HeliumTodo.Application/Plugins/`，
整文件位于 `#if HELIUM_PLUGINS` 内且标了 `[Obsolete]`；默认不定义该符号。
`PluginHost` 已删除，设置页「插件」分组只剩只读说明。恢复插件前先读规格 §9，
重新设计可暴露类型、权限与版本兼容策略，不要直接把 `PluginHost.cs` 从历史里捡回来。

### 20.3 许可证头是门禁的一部分

每个 `.cs` / `.axaml` 头两行必须是：

```csharp
// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only
```

（`.axaml` 用 XML 注释包住同样的两行。）`python tools/check-license-headers.py`
扫描全部源文件，缺一个就退出 1。新增文件时先写头再写代码。

### 20.4 尚未切换的技术栈（P0 时的偏差，P2 起解除）

P0 提交时 Material.Avalonia、PostgreSQL/Npgsql、EF Core 10、Avalonia 12.1.5 装不上
（当时 NuGet HTTPS 在系统层失败，本机也没有 PostgreSQL），所以快照里保留
FluentAvaloniaUI 3.1.0 与 EF Core 8.0.11 + SQLite。该环境问题后来消失：
镜像还原可用，便携 PostgreSQL 17.11 跑在 `127.0.0.1:55432`。P2 起按规格切换数据库，
P3/P4 切换主题与控件，不要再引用这段历史偏差当借口。

### 20.5 P1 的契约归属

- `Domain` 只留纯规则与领域类型：`Models`（实体/枚举）、`Services`（可测的纯逻辑）、
  `Policies`（阈值与文案策略）、`Abstractions`（`IClock` 这类无依赖接口）、
  `Backup` / `Imaging` / `Lifecycle` / `Media` / `Networking` / `Theming` / `Windowing`。
- `Application` 持有「配置、IPC、通知、用例」。判断标准：**需要读配置、需要跨仓储、
  需要触达平台能力**的东西属于应用层；只要输入输出都是纯数据的属于领域层。
- 分层不是靠自觉：`HeliumTodo.Tests/ArchitectureTests.cs` 用程序集引用断言锁住
  `Domain` 不引用 Avalonia / EF / Npgsql，也不反向引用 Application / Infrastructure。


### 20.6 数据层（P2 起）

- **唯一主存储是 PostgreSQL 17**，连接信息在 `AppConfig.Storage`；环境变量
  `HELIUM_DB`（整条串）和 `HELIUM_DB_PASSWORD`（只覆盖密码）优先于 `config.json`。
  连接串在日志里必须经过 `PostgresConnection.Mask()`。
- **结构变更只走 EF Migration**：`DatabaseInitializer` 只做 `Migrate()`、拒绝"库比程序新"、
  空库灌种子。禁止 `EnsureCreated()`、禁止运行期 `ALTER TABLE`、禁止按列名探测后补列。
- **时间列语义**：业务时间（开始/截止/计划）是墙上时钟，映射 `timestamp without time zone`；
  审计时间（`CreatedAt`）是绝对时刻，映射 `timestamptz`，写入转 UTC、读出转本地。
  新增实体时先问一句"这个时间换时区该不该变"。
- **备份 v2** 是 `manifest.json` + 每表一份 JSONL。读取端逐表校验 SHA256；
  Replace 在一个重试策略包裹的事务里清空再插回并修序列；Merge 按 Id 覆盖/追加，
  房间按唯一 `Code` 归一。v1（SQLite）只识别、不自动转换。
- 真库测试靠 `HELIUM_TEST_POSTGRES`（指向管理库）；没设置就跳过，不要改成"假绿"。
  `--db-check` 直接查 `information_schema` 与 `pg_indexes`，核对列类型与索引。

### 20.7 主题层（P3 起）

> **本节记录 P3–P4 的 Material 时期，已由 §20.14 取代。** 当前实现是 FluentAvaloniaUI 3.1.0；
> 保留本节是为了解释"为什么当初写 `Application.Resources` 的 Material 键"以及那些坑。

- **P3 时期两套主题并存，P4 起只剩 Material**：P3 时 `App.axaml` 先 `FluentAvaloniaTheme`、
  后 `MaterialTheme`，后加者优先；P4 把设置页折叠分组和两条提示条换成自绘控件后，
  FA 主题、`FluentAvaloniaUI` 包、`DataGrid` 钉版一并删除。现在 `MaterialTheme` 是唯一设计系统。
- **明暗切换必须同时改两处**：`Application.RequestedThemeVariant`（应用级变体）和
  `MaterialTheme.BaseTheme`（Material 自己的 `MaterialLight` / `MaterialDark` 资源集）。
  只改前者会出现"窗口深色、输入框还是浅色"。
- **强调色写 `Application.Resources`，键名跟 Material 一致**：
  `MaterialPrimaryLight/Mid/Dark`、`MaterialPrimaryLight/Mid/DarkBrush`、
  `MaterialPrimary*ForegroundBrush`，Secondary 同理。`MaterialTheme.PrimaryColor`
  只收枚举色板；`PaletteHelper` 在 3.21.0 已 Obsolete 且接不到挂进 Styles 的主题实例
  （实测写不进去）。应用级资源优先于样式内默认值，这是保住任意 `#RRGGBB` 的唯一通道。
- **不要等 MaterialTheme 的 100ms 定时器来断言颜色**：它走 `DispatcherTimer`，
  无头自检里不触发；`--theme-check` 断言"明暗属性已变 + 资源颜色正确 + 像素正确"，
  而不是等 `CurrentTheme` 自己刷新。
- 卡片底色、阴影、圆角仍只有一个来源：`DesignTokens.axaml`（且不放进 `ThemeDictionaries`）。
  `--theme-check` 会渲染像素核对亮色 `#FFFFFF` / 暗色 `#1C1C1C`，并确认 `CardShadow` 非空。

### 20.8 自绘设置控件与提示条（P4 起）

- **设置页折叠分组不再引第三方控件**：`HeliumTodo.Desktop/Controls/SettingsExpander.cs`
  提供 `SettingsExpander`（`Header` / `Description` / `IsExpanded` / `Items`）和
  `SettingsExpanderItem`（`Content` / `Description` / `Footer`），模板在
  `Styles/SettingsControls.axaml`。模板里**任何一层都不给背景**——0.4 的
  `FASettingsExpanderItem` 在控件主题里写死 `#B3FFFFFF`，是"设置行下面垫了一层"的根因，
  换成自己的模板后不需要再用 `fa|` 样式去压。
- **`Items` 是普通 `AvaloniaList`，不是虚拟化集合**：设置页 10 组 27 行全部实例化，
  `--rail-check` 直接数数据行（27）并逐行断言背景透明；折叠只切 `ItemsControl.IsVisible`，
  不重建控件。
- **提示条统一自绘 `Border`**：壳层的严重冲突条与专注页提示条都用 `NoticeSurfaceBrush` /
  `NoticeBorderBrush` / `NoticeForegroundBrush`（图标另用 `NoticeIconBrush`），不再有
  `FAInfoBar`。这样提示条高度、圆角、明暗跟随全由我们的令牌决定。
- 新增界面检查仍遵守"无头渲染 + 像素/结构断言"：`--rail-check` 读控件树，`--shell`
  逐页出图，`--theme-check` 读渲染像素。

### 20.9 行为层（P5 起）

- 外壳交互从 code-behind 事件搬到 Xaml.Behaviors.Avalonia 12.1.5：标题栏拖拽 / 双击最大化、
  三个窗口按钮、8 个改大小抓手、自习室回车发送、仪表盘时间轴滚动同步，全部用
  Behavior<T> 挂在 XAML 的 Interaction.Behaviors 上。老包 Avalonia.Xaml.Behaviors
  11.3.0.6 在 Avalonia 12 上会 TypeLoadException，必须用新包 id。
- **能记录就不要真的做**：无头的 Window.BeginMoveDrag 既不移动窗口又会留下指针捕获，
  DragWindowBehavior.StartDrag 抽成 protected virtual，--behavior-check 用子类记录
  调用次数，而不是真的拖窗口。

### 20.10 文案与性能门禁（P6 起）

- **`--copy-check` 是"文案不许漂"的机器闸门**：`UI_TEXT_MAP.md` 是唯一来源（生成器
  `tools/gen_ui_text_map.py`），检查把清单里每条记录拆回"位置 + 片段"，从片段里抽用户可见
  字面量（`.axaml` 只认 `Text/Content/Header/ToolTip.Tip/Watermark/PlaceholderText/
  Description/Title/Message/Footer` 与文本节点；`.cs` 认所有双引号字符串），再按 UTF-16LE
  在 `HeliumTodo.Desktop/Application/Domain/Infrastructure.dll` 里逐条搜字节。
  找不到就是文案被改了却没重新生成清单，或者清单里的字面量根本没进产物。
- 位置是 `path:line`，判断 XML 要用 `location.Contains(".axaml:")`，不能用
  `EndsWith(".axaml")`——后者恒为 false，会把属性值当文案抓进 C# 分支。
- **`--perf-check` 末尾直接对 §15.3 预算断言**：纯重绘 P50 ≤ 33.3 ms（时间轴首屏 ≤ 50 ms
  的无头代理）、图标轨展开 P50 ≤ 33.3 ms、降质不慢于原样、进程私有字节 ≤ 300 MB。
  冷启动 ≤ 1.5 s 与 DWM 最大化帧数无头测不了，由真机 `tools/probe-*.ps1` 采，检查里打印
  偏差行而不是假装测过。

### 20.11 并发令牌（P6 起）

- 六张表都用 PostgreSQL 的 `xmin` 系统列做乐观并发：`HeliumDbContext.OnModelCreating` 里
  `Property<uint>("xmin").IsRowVersion()`。EF Core 10 的 Npgsql 提供程序已经删掉
  `UseXminAsConcurrencyToken()`，别照着旧文档写。
- **xmin 不建真实列**：`xmin` 是系统列，EF 的迁移会生成 `AddColumn`，但 Npgsql 的 SQL 生成器
  按 `SystemColumnNames` 整条跳过。迁移里显式留空并注释原因，免得后来人以为要加列。
- **游离对象不能直接 `Update`**：列表查询全部 `AsNoTracking`，对象上没有 xmin，`Update`
  会拼 `WHERE xmin = 0`，UPDATE 影响 0 行后抛 `DbUpdateConcurrencyException`。所有更新必须先按 Id
  取带 xmin 的跟踪实体，再 `CurrentValues.SetValues(incoming)`；备份合并/导入里的
  房间与成员同理（按 Id 或房间码映射）。
- 冲突翻译成 `HeliumTodo.Application.Exceptions.ConcurrencyConflictException`（固定文案
  「数据已被其它窗口修改，请刷新后重试。」），`App.axaml.cs` 在
  `Dispatcher.UIThread.UnhandledException` 里接到后写壳层警告条并 `Handled = true`；
  真库用例 `ConcurrencyTests.两个上下文改同一行时后提交的一方拿到冲突` 守着。

### 20.12 发布包（P7 起）

- 发布只用 `pwsh tools\publish.ps1`（`dotnet publish -c Release -r win-x64 --self-contained`），
  产物在 `dist\HeliumTodo-0.5.0-win-x64.zip`。脚本同时拷 `LICENSE`、`THIRD-PARTY-NOTICES.md`、
  `LICENSES/`，生成 `SOURCE.txt`（版本 + commit + 构建命令），最后打印 SHA256。
- `dist/` 在 `.gitignore` 里，**不进仓库**；发布二进制必须能对应到同一 commit 的源码，
  所以 `SOURCE.txt` 里的 commit 是发布前最后一次提交，打包后不再改工作区。
- 已发布副本的 SPDX 头不可追认：新 `.cs` / `.axaml` 必须在打包前过
  `python tools\check-license-headers.py`；EF 重新生成 `HeliumDbContextModelSnapshot.cs` 会丢掉头，
  生成后要补回两行再打包。
- 干净机器验收：解压后 `HeliumTodo.Desktop.exe` 能启动（首次会建配置目录、迁移数据库、
  写自动备份），待办 CRUD 与备份导出走 `SelfTest` 里的真库用例 + 手动点一遍。
  

### 20.13 窗口交互热修（0.5.0-alpha 交付后）

- **设置分组标题不许让 `ToggleButton` 自己画底**：默认主题会在 checked/焦点状态铺一层
  `#EEEEEE` 方底。标题又是卡片 18px 圆角下的第一层，方底被裁出来就是“上圆下方”。
  `Styles/SettingsControls.axaml` 里用 `SettingsExpanderHeaderTheme` 把模板换成一个
  按 `RadiusXL` 裁圆的 `Border`，背景透明，只在悬停/按下给一点半透明色。
- **设置项 Footer 不占整行**：`SettingsExpanderItem` 模板是 `Grid ColumnDefinitions="*,Auto"`，
  左列文字、右列 Footer 右对齐；竖向 `StackPanel` 时代 Footer 的 `ContentPresenter`
  默认 Stretch，按钮会拉满 1358px。
- **打开文件夹永远不直接 CreateProcess `explorer.exe`**：explorer 属于外壳，绕过外壳启动
  会得到 `0xc0000142`（`STATUS_DLL_INIT_FAILED`）。现在的链是
  Avalonia `Launcher.LaunchDirectoryInfoAsync` → 外壳启 `explorer.exe` → 目录交给系统
  → `cmd /c start`；每级失败原因汇总到提示条。`--folder-check` 端到端等资源管理器窗口出现。
- **启动居中要自己写 `Position`**：`WindowStartupLocation="CenterScreen"` 只在创建时按初始
  `Width/Height` 算一次；`OnOpened` 里先按工作区（DIP）判断放不放得下，放不下才收到 94%，
  然后按最终像素尺寸显式 `Position = 工作区原点 + 居中偏移`。
- **静态壁纸用 `UniformToFill`，不是 `Fill`**：预缩放位图对应的是“上一次窗口尺寸”，
  拖拽中位图还没重做，`Fill` 会把它硬拉成新宽高（看起来就是“壁纸突然变形”）。
  `UniformToFill` 在尺寸一致时逐像素等价，尺寸变化时只等比缩放并裁掉边缘；
  160ms 防抖在拖拽期间只续期不重建 `RenderTargetBitmap`，松手后再按新尺寸重做。
- **改窗口尺寸只写真正变化的属性**：`WindowResizeGrips` 每帧只对变化了的
  `Position` / `Width` / `Height` 赋值，避免一次拖动里重复触发 `WM_WINDOWPOSCHANGED`。
- **`auto` 渲染后端的回退**：`ToModes(auto)` 现在是 `[AngleEgl, Software]`——优先显卡，
  但 AngleEgl 在缺驱动/远程桌面/虚拟机里初始化失败时仍能启动。

### 20.14 设计系统回退与单文件发布（2026-11-10 热修 2）

- **回退原因**：用户实机看 0.5.0 首版后反馈 Material.Avalonia 在小尺寸按钮、下拉框上的观感不如
  FluentAvalonia，要求换回。按“用户指令优先于规格”处理，`Directory.Packages.props` 恢复
  `FluentAvaloniaUI 3.1.0` + `Avalonia.Themes.Fluent 12.1.5`，重新钉 `Avalonia.Controls.DataGrid 12.1.2`
  （FA 3.1.0 的传递依赖，NuGet 上没有 12.1.5）；Material.* 三个包与 `MaterialTheme` 一并删除。
  这是对 `DEVELOPMENT_SPEC.md` §6 的显式偏差，记录在此与 `PROGRESS.md`，规格其余部分仍严格执行。
- **主题代码**：`App.axaml` 只挂 `FluentAvaloniaTheme`；`ThemeService.Apply` 只切
  `RequestedThemeVariant`，强调色走 `FindFluentAvaloniaTheme()?.CustomAccentColor`，再写
  `SystemAccentColor` / `Light1..3` / `Dark1..3` 兜底。Material 的 `ApplyMaterial*`、
  `ReadableForeground` 全部删除——不要再把 Material 资源键写回来，它们现在没人消费。
- **`--theme-check` 断言随之改成 Fluent 口径**：样式里有 `FluentAvaloniaTheme`、明暗变体、
  `CustomAccentColor == #FF00FF`、`SystemAccentColor == #FF00FF`、强调色像素、亮/暗卡片像素、
  `CardShadow` 非空。仍然是 9 条，仍然用真实无头渲染，不信任“属性设过了”。
- **单文件发布**：`tools/publish.ps1` 默认 `PublishSingleFile=true` +
  `IncludeNativeLibrariesForSelfExtract=true` + `EnableCompressionInSingleFile=true`；打包前删除
  所有 `*.pdb`（原生 Skia/HarfBuzz 的 pdb 合计 105 MB）。exe 53.8 MB，zip 46.4 MB。
- **VC++ 运行库必须随包**：self-contained .NET 不带 MSVC 运行库，SkiaSharp/HarfBuzzSharp 依赖它；
  缺 `vcruntime140.dll` / `vcruntime140_1.dll` / `msvcp140.dll` 时原生初始化失败，表现就是
  `0xc0000142`。脚本从 `System32` 复制这三个 DLL 到包根目录。
- **单文件自解压目录**：默认解压到 `%TEMP%\.net`，受策略限制时（错误码 5）会启动失败；
  文档要求把 `DOTNET_BUNDLE_EXTRACT_BASE_DIR` 指到可写目录。这是单文件模式的系统行为，不是应用 bug。

### 20.15 窗口状态字形与数据库连接自愈（2026-11-10 热修 3）

- **标题栏字形必须跟随 `WindowState`**：最大化按钮的字形不许在 XAML 里写死。用
  `WindowStateGlyphBehavior` 订阅 `Window.WindowStateProperty`，最大化/全屏 → `\uE923`，
  其它 → `\uE922`。双击标题栏、窗口按钮、DWM 系统菜单/键盘都会改同一个属性，所以三处天然一致；
  `--behavior-check` 会分别断言最大化后与还原后的字形。
- **数据库不可用不许把英文异常甩给用户**：启动先 `PostgresConnection.Probe`（TCP 1.5 s），
  失败则跳过 Migrate 并把 `DatabaseErrorText.DescribeProbeFailure` 的结果交给壳层提示条。
  运行期异常统一走 `DatabaseErrorText.Describe`：`SocketException` → 检查服务/端口；
  `28P01` → 用户名密码；`3D000` → 库不存在；`42P01` → 迁移未应用；`57P03` → 启动中；
  超时 → 防火墙/主机。原始异常只进日志，连接串打码。
- **连接参数的编辑面**：设置页「数据与文件 → 数据库连接」提供主机/端口/库名/用户名/密码与
  「测试连接」。连接串在 DI 容器构建时读取一次，所以保存后必须重启；界面用「保存并重启」
  明确这一点，不假装热生效。
- **为什么 `DatabaseErrorText` 在 Infrastructure**：它翻译的是 Npgsql/EF 的异常类型，
  且 `HeliumTodo.Tests` 不引用 Desktop（架构测试要求分层单向），放 Desktop 会让这组单测无处安放。
- **`--rail-check` 的行数基线是 28**：新增数据库连接行后，设置页十组共 28 行；改设置页结构
  必须同步这个数字，否则门禁红。

### 20.16 本机数据库用户名的自愈边界

`App.TryRepairLocalStorage` 是 0.5.0-alpha 交付后加的一条窄规则，只解决「配置里的用户名在数据库里根本不存在」：

- 只在没有 `HELIUM_DB`、主机是回环地址、原用户名登录被 `28000`/`28P01` 拒绝、`postgres` 免密能登录时生效。
- 修改发生在 `BuildServiceProvider` 之前并立即落盘，因为连接串在容器构建时读取一次；事后改配置不会重建连接。
- 改写的是 `config.json` 里的 `Username`/`Password`，绝不建角色、不执行 DDL；密码为空也只在 `postgres` 免密可登录时采用。
- 日志只写一行「已改用 postgres」，不打印密码；完整异常仍按打码连接串记录。

状态栏用 `DatabaseErrorText.Brief`（一句话），提示条和设置页用 `Describe`（含角色名、主机端口与下一步）；两处文案都由 `UI_TEXT_MAP` 管着。

### 20.17 便携数据库自启动与静态壁纸（2026-11-12 热修 5）

**便携 PostgreSQL 自启动。** 交付后便携版不会开机自启，用户隔天打开程序只会看到连接超时。

- `LocalPostgresRuntime.TryStart` 在 EF 初始化之前执行：只有主机是回环地址、配置或环境变量给了
  `PgCtlPath` / `PgDataDirectory` 时才尝试；系统服务与远程库一律不碰。
- 命令是 `pg_ctl start -D <data> -l <配置目录>\server-start.log -o "-p <port>" -w -t 20`。
  日志写应用配置目录（数据目录可能在只读盘/受控目录），启动前先试写数据目录，不可写直接报
  「数据目录不可写」而不是把 postgres 的 `Permission denied` 原样抛给用户。
  pg_ctl 与它拉起的 postgres 会继承父进程的 stdout/stderr：调用方若把输出接到管道（`dotnet … | tee`、
  CI 采集），只要 postgres 不退出，管道就永远不 EOF（实测 `--shell | Select-Object` 卡死 154 秒）。
  因此**必须把两个流重定向到子进程自己的管道**（`RedirectStandardOutput/Error = true`），随 `Process`
  释放关闭；绝不能让它继承调用方的句柄。
- `pg_ctl` 25 秒内退出且端口在 10×300 ms 内可连才算成功；非 0 退出时依次等 1.5/3 秒重试两次（刚停库时
  数据目录还在收尾/文件锁未释放），并把最后一次 pg_ctl 的 stderr 带进启动提示条；不打印密码。
- 路径可用 `HELIUM_PG_CTL` / `HELIUM_PG_DATA` 覆盖；`HELIUM_DB` 仍然优先于 `config.json`。

**动态壁纸移除。** 用户明确要求去掉 `.gif` / `.mp4` 动态壁纸。

- `WallpaperDecoder.DecodeFile` 只接受静态图（PNG / JPG / WebP / BMP）；GIF 与视频扩展名直接抛
  「0.5.0 起壁纸只支持静态图片…」。
- 删除 `MediaFoundationVideo`（750 行 COM Interop）与 `ApngParser`（及对应测试）；
  `Decode` 只取第一帧，`DecodedWallpaper` 恒为单帧。
- 选择器不再列 `*.gif` / `*.mp4`；设置页状态只显示「静态图」。
- §18.3 / §18.3.1 的帧预算与 30 fps 上限是历史设计，已不再生效。
### 20.18 启动窗口尺寸与居中（2026-11-12 热修 6）

- 启动尺寸的**唯一来源**是 `Domain/Windowing/StartupWindowPlacement.cs`：
  目标是 1920×1080 **物理像素**，按 `Screens.ScreenFromWindow(...).Scaling` 换算成 DIP；
  屏幕工作区放不下时只收缩溢出的那一维（工作区 94%），并守住窗口的 `MinWidth/MinHeight`。
- `MainWindow.OnOpened` 只负责把纯函数结果贴到 `Width` / `Height` / `Position`；
  `WindowStartupLocation="CenterScreen"` 保留，但它按创建时尺寸算，改过尺寸后**必须**重设 `Position`。
- 0.5.0 起**不再恢复** `config.json` 里的 `WindowWidth/WindowHeight`（字段保留只为兼容旧文件）；
  关窗时也不再写这两个字段，只记 `StartMaximized`。
- 验收：`StartupWindowPlacementTests` 覆盖 150%、仅高度溢出、非法缩放、极小工作区、工作区不可用五种情况；
  `--window-check` 在无头窗口上断言宽/高换算与居中坐标。

### 20.19 壁纸预缩放的构图不变量（2026-11-12 热修 7）

- 预缩放位图**永远保留原图长宽比**，尺寸取"恰好覆盖目标窗口"（`Domain/Imaging/WallpaperGeometry.cs`，
  `scale = max(targetW/srcW, targetH/srcH)`，结果向上取整、两边都不小于目标）。**不许**再按窗口长宽比裁切。
- 原因是显示层用 `Stretch="UniformToFill"`：同一长宽比的位图放进任意窗口，裁切比例只由窗口比例决定，
  与位图分辨率无关；因此拖拽中即使还挂着旧位图、松手后换成新位图，**构图也不变**，只变清晰度。
- 旧实现（按窗口比例裁切）会让拖拽中 cover 比例跟着窗口走、松手重做位图又跳回 1:1，
  用户看到的就是"壁纸突然放大缩小"。
- 验收：`WallpaperGeometryTests` 钉住 16:9→3:2、同比例、竖图进横窗、零尺寸四种边界；
  `--appearance-check` 三·五断言 2560×1440 → 1400×900 的位图是 1600×900，且左/中/右三色仍归位。

### 20.20 窗口外框补偿（2026-11-13 热修 8）

- 本窗口同时满足两个事实：`WindowChrome` 补了 `WS_CAPTION/WS_THICKFRAME`（DWM 过渡需要），
  `WM_NCCALCSIZE` 又把非客户区抹成 0（视觉无边框）。于是"客户区 = 窗口矩形"。
- Avalonia 设置 `Window.Width/Height` 时仍按"窗口 = 客户区 + 外框"计算：先
  `AdjustWindowRectExForDpi` 加一圈，再 `SetWindowPos`。这圈外框落在客户区里变成额外尺寸。
  实测 150% 缩放为 **22×56 物理像素 / 14.67×37.33 DIP**，正是"按下抓手整窗放大"和
  "启动停在 1920×1136"的来源。
- 规则：真机上所有尺寸赋值前减掉 `FramelessWindowFrame.Measure(window)` 量到的外框；
  `MinWidth/MinHeight` 先放宽到"逻辑最小 − 外框"再赋值，否则高度方向的补偿会被最小值顶回去；
  每帧 apply 时**两个方向都写**补偿值（只写一个方向会让 Avalonia 用另一方向的旧值重新布局）。
- 不要改回原生 `SetWindowPos` 方案：Avalonia 会用缓存的 `Width/Height` 在
  `WM_WINDOWPOSCHANGING` 里覆盖它。原生 `BeginResizeDrag` 也不行 —— 无头自检里也会报告平台句柄，
  实测拖拽断言直接变成 `1920 → 1920`。
- 验收：`--window-check` 新增"只按下抓手、不移动时窗口尺寸不变"（外框补偿失效会多 14.7×37.3 DIP），
  以及原有"拖 120 → 1920→1800"；真机用 `.tmp-input` 合成输入探针确认按下 300ms 尺寸不变、拖动只改一维。

### 20.21 本机数据库自动识别（2026-11-14 热修 9）

- 用户不该为了连上数据库去改 `config.json`。启动顺序固定为
  `TryRepairLocalStorage`（用户名自愈）→ `EnsureLocalDatabase`（按配置拉起便携库）→
  `TryDiscoverStorage`（自动识别）。前者失败不影响后者，后者只在“当前配置连不上”时执行。
- `StorageDiscovery` 的边界写死：只碰回环地址（`localhost / 127.0.0.1 / ::1`）；
  端口候选「配置 → 5432 → 55432」，用户候选「配置 → postgres → 当前 Windows 用户」；
  先 400 ms TCP 预检再 2 s 登录；**不猜密码**（只用空密码或配置里已有的密码）。
- 目标库不存在时允许 `CREATE DATABASE`，但库名必须先过 `^[A-Za-z_][A-Za-z0-9_]{0,62}$`
  （`CREATE DATABASE` 的标识符不能参数化）；没有权限就换下一个候选，不重试建库。
- 识别成功才写回 `config.json`（含 `PgCtlPath` / `PgDataDirectory`，从 `SHOW data_directory` 反推）；
  写回发生在容器构建之前，因为连接串只在 DI 时读一次。`HELIUM_DB` 存在时整段跳过。
- 设置页的「自动检测」只回填表单，不直接保存；真正的落盘仍由「保存并重启」完成，
  避免用户在不知情的情况下被换掉数据库连接。
- 失败不挡启动：记录 `"[启动] 未能自动识别本机数据库：…"`，原有的启动提示继续显示具体原因。


### 20.22 创建入口必须放卡片内（2026-11-14）

- 页面标题行（`MinHeight="40"` 那一行）只放模块名、概览文字/数字与状态；创建、筛选、打开文件夹这类操作属于功能区，必须放卡片内部。
- 待办页是卡片顶部的快速添加行（输入框 + 更多 + 保存）；日程页是卡片信息栏「接下来 N 天」右侧的「＋ 新建」开关。两页保持同一种“创建入口在卡片里”的结构。
- 别把按钮塞回标题行：标题行高度与卡片顶边 64 是 `--layout-check` 的布局门禁；多一个 34px 控件会把同一行基线顶歪，也会让标题行看起来像工具栏。


### 20.23 设置行：标题走 Content，整块表单走 Body（2026-11-14）

- `SettingsExpanderItem` 的三个槽位分工固定：`Content` = 行标题（字符串），`Description` = 灰色副标题，`Body` = 标题下面整块内容（表单/长文），`Footer` = 右侧控件。
- **不要**把整块内容直接写成 `SettingsExpanderItem` 的子元素：XAML 会把它赋给 `Content`，同时把 `Content="标题"` 静默覆盖掉——编译能过，运行时标题消失。0.5.0 的数据库行踩过这个坑。
- 结构化表单（例如数据库连接）必须开 `x:CompileBindings="True"`：`{Binding}` 默认走反射，属性名写错只会静默失效；开了编译绑定才会在 `dotnet build` 里报错。
- 数据库这一行的活性由 `--rail-check` 守着：行标题渲染、五个输入框在场、VM→控件与控件→VM 双向、副标题、三个命令，少一个都红。


### 20.24 壳层提醒浮层：右下角、窄卡片、不占布局行（2026-11-16）

- 壳层提醒只有一个来源：`ShellView.axaml` 根 Grid 里的 `AlertToast`。`Grid.RowSpan="2"`、`Right/Bottom` 对齐、`Margin="0,0,20,20"`、`MaxWidth="440"`、`ZIndex="20"`，底板继续用 `Notice*` 令牌，圆角 10 + 阴影。
- **不要再把它放回内容区的第 0 行**：那会把整页往下推、在数据库报错时遮住标题栏右侧的窗口按钮，也会让 `--layout-check` 的卡片顶边 64 失效。0.5.0 之前那条整行提示条就是这么来的。
- 文案上限三行（`MaxLines="3"` + `TextTrimming="CharacterEllipsis"`），完整原因放 `AlertDetail` 悬停提示；`AlertMessage` 只放一句话，不要把连接串整段塞进卡片。
- 必须带关闭按钮（`DismissAlertCommand`）；数据库错误不自动消失，用户处理完自行收起。`--notice-check` 守着位置（右/下各 20）、宽度（≤ 460）、层级（≥ 10）与关闭行为。

