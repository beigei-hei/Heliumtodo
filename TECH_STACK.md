# Helium Todo · 技术栈与运行时画像

> 版本 **0.3.5-alpha** ｜ 本文对标的是仓库当前代码，不是计划。
> 所有数字都是在开发机上实测出来的（命令写在每节末尾，可以自己复现）。

---

## 一、技术栈总表

### 运行时与框架

| 项 | 取值 | 说明 |
| --- | --- | --- |
| 目标框架 | `net10.0`（六个项目一致） | `global.json` 把 SDK 锁在 `10.0.401` |
| UI 框架 | **Avalonia 12.1.2** | 跨平台 XAML；本机只用 Windows 后端 |
| 控件库 | **FluentAvaloniaUI 3.1.0** | Fluent 2 风格；FA 3 给 224/252 个类型加了 `FA` 前缀 |
| 绘图 | **SkiaSharp 3.119.4** | 必须与 Avalonia 的传递依赖同版，否则会加载两个 `libSkiaSharp` 直接崩 |
| MVVM | **CommunityToolkit.Mvvm 8.4.0** | `[ObservableProperty]` / `[RelayCommand]` 源生成器 |
| 数据访问 | **EF Core 8.0.11** + `Microsoft.EntityFrameworkCore.Sqlite` | `net10.0` 上跑 8.0.x 没问题，没升 9/10 是为了少折腾 |
| 依赖注入 | `Microsoft.Extensions.DependencyInjection 8.0.1` | `App.axaml.cs` 里手工注册 20 处 |
| 测试 | **xunit 2.9.2** + `Microsoft.NET.Test.Sdk 17.11.1` | 179 个测试 |
| 视频解码 | **Windows Media Foundation**（系统组件，`mfplat.dll` / `mfreadwrite.dll`） | 零额外打包体积；`MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING` 请求 RGB32，缩放自己用 Skia 做 |
| 渲染后端 | `Win32RenderingMode`：Software（默认）/ AngleEgl / Wgl / Vulkan | 设置页「底层设置」或 `HELIUM_RENDER` 切换，重启生效 |

### 包管理

- `Directory.Packages.props` 开启**中央包管理**，并设 `CentralPackageTransitivePinningEnabled=true`。
- 版本只在那一处写，`csproj` 里只写 `<PackageReference Include="…" />`。
- 好处：`SkiaSharp` / `Avalonia` 这种"必须全家同版"的包不会再出现半新半旧。

### 自己写的替代品（为什么不用现成的）

| 需求 | 现成方案 | 本项目的做法 | 原因 |
| --- | --- | --- | --- |
| APNG 解码 | SkiaSharp | `HeliumTodo.Core/Imaging/ApngParser.cs`（手工拆帧） | Skia 的 `SKCodec` **不认 APNG**，`FrameCount` 返回 0 |
| 托盘图标 | WinForms `NotifyIcon` | `HeliumTodo.App/Services/TrayService.cs`（纯 P/Invoke） | WinForms 会把 TFM 变成 `net10.0-windows`，而项目被无头渲染工具引用 |
| 系统通知 | WinRT `ToastNotificationManager` | `WindowsToastNotifier` → 托盘气泡 | WinRT 通知需要注册 AppUserModelID + 开始菜单快捷方式 + 额外互操作包 |
| 窗口圆角 / 最大化动画 | DWM | 圆角用 `Win32Properties.WindowCornerPreference`；动画自己写 180 ms | 无边框窗口（`WindowDecorations="None"`）下 DWM 不给动画，实测确认过 |
| 数据库迁移 | EF Migrations | `EnsureCreated()` + `EnsureColumns()` 手工补列 | 单机小应用，迁移文件比补列更难维护 |
| 测试运行器 | `dotnet test` | `HeliumTodo.SelfTest`（反射跑 `[Fact]`） | 本机沙箱里 testhost 起不来 |

---

## 二、项目结构与依赖方向

```
HeliumTodo.slnx
├─ HeliumTodo.Core          领域模型 + 纯算法（不依赖 UI，也不依赖 EF）
├─ HeliumTodo.Data          EF Core + SQLite + 备份 + 联机传输
├─ HeliumTodo.App           Avalonia 界面（Views / ViewModels / Controls / Services / Windowing）
├─ HeliumTodo.Tests         xunit 测试（168 个）
├─ HeliumTodo.SelfTest      反射运行器（沙箱里替代 dotnet test）
└─ HeliumTodo.RenderCheck   无头渲染 + 13 个专项自检 + 后台探针入口
```

依赖方向（箭头 = 引用）：

```
App ──► Data ──► Core
 │                 ▲
 └─────────────────┘
Tests ──► Data ──► Core
SelfTest ──► Tests
RenderCheck ──► App
```

**硬约束**：`Core` 一个 `PackageReference` 都没有（只有 `System.*` 与 BCL），所以算法层可以被任何宿主复用 —— 这也是将来做 CLI / IPC 宿主的前提。

### 各层职责与规模（实测行数）

| 项目 | 文件 | 行数 | 主要职责 |
| --- | --- | --- | --- |
| `Core` | 44 | 5 677 | 模型、重复规则、时间轴构建（并查集分列 + 冲突检测）、专注计时状态机、备份格式、提醒调度、IPC 契约、壁纸取色 |
| `Data` | 22 | 3 693 | `HeliumDbContext`、5 个仓储、种子数据、`.zip` 备份服务、局域网传输 |
| `App` | 65 | 13 862 | 6 个页面 + 外壳、9 个 ViewModel、自绘控件、壁纸服务、托盘、提醒、窗口封装 |
| `Tests` | 22 | 3 386 | 179 个测试 |
| `SelfTest` | 7 | 230 | 反射跑 `[Fact]`，支持 `async Task` |
| `RenderCheck` | 1 | 3 182 | 7 页渲染截图 + 14 个 `--check` |

---

## 三、运行时画像（这一节是本轮的重点）

### 3.1 三个数字先分清

同一时刻任务管理器 / `GetProcessMemoryInfo` 会给出三个不同的"内存"，容易吵架：

| 名字 | 含义 | 本应用（空配置、软件渲染） |
| --- | --- | --- |
| 工作集 Working Set | 进程当前映射的物理页总量 | 178–182 MB |
| 私有字节 Private Bytes | 已提交的私有内存（含换出的） | 78–83 MB |
| **私有工作集 Private Working Set** | 任务管理器「内存」列显示的那个 | **45–48 MB** |

> 多次采样会在上面这个区间里浮动（最近一次：181.5 / 83.1 / 45.3 MB，21 线程 / 496 句柄），
> 下面各表为了让差值能直接相减，统一取 178.2 / 78.2 / 47.7 那一组。

> 用户说"占 170 MB"时，指的极可能是**工作集**（或旧版本 ANGLE 渲染下的工作集 208 MB）。

### 3.2 启动曲线（稳定后不再增长）

```
1 秒   105 MB   ← 运行时 + Skia + FluentAvalonia 加载
2-3 秒 130 MB
5 秒   146 MB
8 秒   178 MB   ← 后台提醒定时器首扫 + 托盘线程 + 主题/壁纸初始化
20 秒  178 MB   ← 持平
```

没有内存泄漏：20 秒采样曲线持平，反复开合页面也不回升。

### 3.3 渲染后端：这轮最大的一笔

Avalonia 在 Windows 上默认走 ANGLE（把 OpenGL 调用翻译成 D3D11）。ANGLE 会把**整套显卡驱动栈**映射进进程：

| 模块 | 大小 | 是什么 |
| --- | --- | --- |
| `nvgpucomp64.dll` | 94.1 MB | NVIDIA 的 GPU 着色器编译器 |
| `nvwgf2umx.dll` | 85.6 MB | NVIDIA 的 D3D 用户态驱动 |
| `nvppex.dll` | 7.2 MB | NVIDIA 后处理扩展 |
| `av_libGLESv2.dll` | 5.2 MB | ANGLE 本体 |
| `d3dcompiler_47.dll` | 4.5 MB | 微软的 HLSL 编译器 |

这些都是**按需映射**的映像，但它们在进程的工作集里、也计进私有字节，同时线程数从 19 涨到 84、句柄从 481 涨到 846。

**结论**：这个界面是纯 2D 卡片 + 少量 180 ms 动画，Skia 的 CPU 光栅化完全够用，没必要为它拖一整套显卡驱动。于是把默认后端改成 `Software`。

实测对照：

| 后端 | 工作集 | 私有字节 | 私有工作集 | 线程 | 句柄 |
| --- | --- | --- | --- | --- | --- |
| ANGLE（原默认） | 208.1 MB | 256.9 MB | 68.6 MB | 84 | 846 |
| **Software（现默认）** | **178.2 MB** | **78.2 MB** | **47.7 MB** | **19** | **481** |
| 差值 | −29.9 MB | **−178.7 MB** | −20.9 MB | **−65** | **−365** |

视觉上等价（同一界面两种后端各抓一张图，逐像素比对）：

- 均值 239.2 vs 239.8，不同颜色 2 759 种 vs 3 071 种；
- 像素差 > 8 的占 **0.45%**，> 32 的占 0.38% —— 差异全部落在文字抗锯齿上。

想换回去只需一个环境变量（不用改代码、不用重编译）：

```powershell
$env:HELIUM_RENDER = "angle"    # 或 software / wgl / vulkan / auto
```

### 3.4 动态壁纸：限制帧数与长边

动图是"几十帧同时驻留"，代价随帧数线性增长。改成按需限额之后：

| 场景 | 工作集 | 私有字节 | 备注 |
| --- | --- | --- | --- |
| 无壁纸 | 178.2 MB | 78.2 MB | 基线 |
| 1920×1080 / 40 帧 GIF（改前） | 332.3 MB | 236.6 MB | 长边 1920、帧预算 192 MB |
| **1280×720 / 最多 26 帧（改后）** | **237.3 MB** | **144.6 MB** | 长边 1280、帧预算 96 MB |

即动态壁纸的额外开销从 **+154 MB** 压到 **+63 MB**，而壁纸上面还盖着一层蒙版、缩到 1280 肉眼几乎看不出来。

规则写在 `HeliumTodo.App/Services/WallpaperDecoder.cs`：

- `MaxLongSide = 1920` —— 静态图（只占一帧）保持原样；
- `MaxAnimatedLongSide = 1280` —— 动图一律缩到这个长边；
- `FrameBudgetBytes = 96 MB` —— 超预算就抽帧，宁可变慢也不吃满内存。

### 3.5 试过但**没有收益**的（避免后人重复劳动）

| 手段 | 结果 |
| --- | --- |
| `DOTNET_GCConserveMemory=7/9` | 8 秒稳定值一模一样（178.6–179 MB），只是多花了 CPU |
| `InvariantGlobalization` | 这台机器**根本没加载 ICU**（模块表里没有 `icudt*.dll`），省不到那 30 MB |
| 关掉托盘/提醒线程 | 只省几百 KB 和 2 个线程，而它们是"后台常驻"的全部意义 |

### 3.6 复现命令

```powershell
# 内存采样（含私有工作集与模块明细）
powershell -ExecutionPolicy Bypass -File tools\probe-memory.ps1 `
  -ExePath HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe -Seconds 20

# 抓真实窗口（用 PW_RENDERFULLCONTENT，否则 DirectComposition 内容全黑）
powershell -ExecutionPolicy Bypass -File tools\capture-window.ps1 `
  -ExePath HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe `
  -OutPath output\capture.png
```

---

## 四、功能模块与关键技术点

| 模块 | 入口 | 关键技术点 |
| --- | --- | --- |
| 每日工作流 | `Core/Services/DayTimelineBuilder.cs` | 并查集分组 + 贪心分列算重叠偏移；`laneCount` = 组内最大并发；5 种冲突检测；12 小时过载阈值 |
| 重复日程 | `Core/Services/RecurrenceExpander.cs` | `daily` / `weekly:N` / `monthly:N` / `weekday`，按天展开而不是预生成 |
| 专注计时 | `Core/Services/FocusTimerService.cs` | `Idle/Focusing/Break/Paused` 四态；打扰计数；结束时把时长累加回待办的 `FocusedDuration` |
| 自习室（局域网） | `Data/LanPeerTransport.cs` | 星形拓扑、一行一条 JSON、4 秒心跳 / 15 秒判掉线、UDP 广播发现、默认关闭 |
| 备份 | `Core/Backup/*` + `Data/DataBackupService.cs` | 一个 `.zip` 装库 + 配置 + 壁纸；清单带 SHA256；替换/合并两种导入 |
| 提醒 | `Core/Notifications/*` + `App/Services/ReminderService.cs` | 30 秒扫一次、2 分钟容差、`HashSet` 去重、落到 Windows 通知中心 |
| 壁纸 | `Core/Imaging/ApngParser.cs` + `App/Services/WallpaperService.cs` | 手工拆 APNG、GIF 逐帧、模糊 + 蒙版、取主色 |
| 窗口外壳 | `App/Windowing/*` | 自绘标题栏 + 三列卡片 + 8 个改大小抓手 + 180 ms 最大化动画 + 单实例唤回 |

---

## 五、质量保障体系

### 测试（168 个）

| 分组 | 数量 | 覆盖 |
| --- | --- | --- |
| 时间轴 / 冲突 | 24 | 分列、重叠、过载、去重 |
| 壁纸取色 | 20 | 色相投票、饱和度/明度夹取 |
| 提醒 | 21 | 触发窗口、重复日程、待办基准 |
| 联机 | 19 | 心跳、地址表、房主中继 |
| 备份 | 14 | 清单校验、替换/合并、路径不可写 |
| 专注计时 | 11 | 状态机、打扰、累加 |
| APNG | 11 | 拆帧、序号、CRC |
| 其它（重复规则 / 版本 / 仓储 / 配置） | 21 | — |

跑法（沙箱里 `dotnet test` 起不来，所以有反射运行器）：

```powershell
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll
```

### 专项自检（14 个 `--check`，全部 `exit=0`）

| 开关 | 项数 | 检查什么 |
| --- | --- | --- |
| `--shell-check` | 11 | 图标轨 6 项、标题栏 48、窗口按钮 3 个、点击换页 |
| `--refresh-check` | 6 | 待办增删后统计/时间轴自动刷新 |
| `--room-check` | 5 | 建房、离开、重进不回到房间 |
| `--backup-check` | 15 | 导出/导入/校验和/坏文件/落盘兜底 |
| `--folder-check` | 6 | 打开文件夹五条退路 |
| `--timeline-check` | 7 | 0 点不顶格、24 点不触底 |
| `--reminder-check` | 11 | 提醒下拉 → 数据库 → 调度 → 通知 |
| `--window-check` | 11 | 开场动画、拖边改大小、单实例唤回 |
| `--restart-prompt-check` | 17 | 改渲染后端 → 重启提示 → 确认/取消/调试器三档 |
| `--notice-check` | 13 | 提示条叠在功能区上方、跟随明暗、3 秒自动收起、点 × 立刻收起 |
| `--appearance-check` | 14 | 明暗切换、玻璃浓度 alpha、蒙版合并后的颜色与强度（0.45 / 0.72 / 上限 0.95） |
| `--perf-check` | 5 | 交互降质真的更快（低品质壁纸 + 关阴影，实测 53.2 → 23.0 ms / 快 57%）；改尺寸比纯重绘贵；最慢一帧 < 120 ms |
| `--rail-check` | 20 | 设置排在自习室下面、图标轨 56↔188 展开与宽度过渡、设置页 10 个分组标题 + 34 行分组行的背景全透明 |
| `--layout-check` | 6 页 × 6 项 | 六页分区是否同一套（左/右/顶边缘、缝、标题字号） |
| `--shell` | 7 页 | 每页无头渲染出 PNG |
| `--tree N` | — | 打印可视树（查布局用） |

### 后台探针（真实进程，不在无头里）

| 脚本 | 验证 |
| --- | --- |
| `tools/probe-lifecycle.ps1` | 关窗口后进程仍在、再双击能唤回 |
| `tools/probe-intro.ps1` | 开场动画逐帧播出（15 帧 / 11 个不透明度） |
| `tools/probe-maximize-anim.ps1` | 最大化/还原逐帧（20 个中间尺寸） |
| `tools/probe-memory.ps1` | 内存采样 + 模块明细 |
| `tools/probe-window-frame.ps1` | 窗口样式位、圆角偏好、边框厚度 |
| `tools/probe-dwm-attrs.ps1` | DWM 五个属性 |
| `tools/capture-window.ps1` | 抓真实窗口成 PNG |

---

## 六、已知限制（写清楚，免得当成 bug）

1. **托盘图标在本机沙箱里加不上**：`Shell_NotifyIconW` 固定返回错误码 5（`ERROR_ACCESS_DENIED`），因为该会话没有可交互的通知区域。真实桌面可用；代码里失败也照样拦截关闭（由单实例守卫兜底唤回）。
2. **无头渲染里 `DispatcherTimer` 不触发**，所以所有"逐帧"行为都靠真实进程探针验证，`--check` 里只断言纯函数与首帧状态。
3. **`dotnet test` 在本机沙箱起不来**（testhost 被拦），替代品是 `HeliumTodo.SelfTest`。
4. **.NET 进程写 `%TEMP%` 会被拒**（`UnauthorizedAccessException`），所以测试与自检的临时目录都放在程序目录或项目目录内。
5. **联机不做 NAT 穿透 / 加密 / TUN**，只在同一局域网内用，且默认关闭。
6. **数据库升级靠手工补列**（`EnsureColumns()`），加字段时要同步改那里。

---

## 七、一句话总结

> 它是一套 **.NET 10 + Avalonia 12 的本地优先桌面应用**：Core 层零依赖、可被任何宿主复用；
> Data 层把 SQLite、`.zip` 备份、局域网传输收在一起；App 层用自绘外壳做出三列卡片工作台。
> 渲染走 Skia 软件光栅化（换来 −178 MB 私有内存、−65 个线程），动态壁纸按 1280 / 96 MB 限额，
> 所以空配置下稳定在 **178 MB 工作集 / 47.7 MB 私有工作集**。