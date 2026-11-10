# AGENTS.md · 给 AI 的硬规则

> 这是一个 **C# 14 / .NET 10 + Avalonia 12 + Material.Avalonia 3.21 + PostgreSQL 17** 的本地优先桌面应用（0.5.0-alpha P6；FluentAvalonia 已移除，外壳交互走 Behaviors，文案与性能有机器门禁）。
> 改代码之前先读 [`DEVELOPMENT_PLAYBOOK.md`](DEVELOPMENT_PLAYBOOK.md)（流程）与 [`ARCHITECTURE.md`](ARCHITECTURE.md)（约定）。
> 本文件只列**不许破**的规则，不解释为什么——解释在 playbook 里。

## 必须先做的四件事

1. **先给验收标准**：把用户的话拆成 3–6 条可验证的行为，再说怎么改。
2. **先想怎么证明**：能写单测就写单测；界面改动必须准备无头渲染 + 像素统计的验证方式（Agent 不能看图，"看着挺好"不算证据）。
3. **先跑门禁再报告**：`dotnet build`（0 警告 0 错误）、`SelfTest`（196/196，含真库用例）、`--shell`、十九项 `--check`、指纹基线，全绿才交付。
4. **收尾同步文档**：`PROGRESS.md`（这一轮）、`ARCHITECTURE.md`（新约定）、`UI_TEXT_MAP.md`（改了界面文案时）、指纹基线（`-Update` 后复查）。

## 分层铁律

- `HeliumTodo.Domain` **不许** `using Avalonia`、**不许** 引用 EF Core / Sqlite。它必须能被纯控制台测试。
- 依赖方向单向：`Desktop → Application → Domain`；`Infrastructure → Application → Domain`。`Infrastructure` 不许引用 `Desktop`。
- 可测的算术与规则放 `Domain`（例：`TimelineMetrics` / `PlaybackLimits` / `RestartPolicy` / `ColorSchemes` / `RenderBackendSettings`）；界面层只做装配。

## 界面铁律（都是踩过坑换来的）

- 一个页面**只有一张卡**（`Border Classes="card"`）；模块名那一行在卡片**外面**，与卡片左边距 4px。
- 六页分区必须完全一致：`Margin="12,4,12,12"`、标题行 `MinHeight="40" Margin="4,0,4,20"` → 卡片顶边 **64**、左边缘 **12**。改一页就要改六页，并跑 `--layout-check`。
- 卡片底色 `CardSurfaceBrush` **不许**写进 `ThemeDictionaries`（主题字典优先级更高，代码赋值会静默失效）；圆角只有一个来源 `RadiusCard=18`。
- 提示条**不用** `FAInfoBar`：自绘 `Border Name="NoticeBar"` + `ZIndex="10"`，
  和卡片同格、排在卡片**后面**（叠在功能区上方），底板跟随明暗，3 秒自动收起（`NoticePolicy`）。
  注意 Avalonia 12 里写 `ZIndex="10"`，写 `Panel.ZIndex` 会报 `AVLN3000`。
- 设置页折叠分组是自绘 `SettingsExpander` / `SettingsExpanderItem`（`Desktop/Controls/SettingsExpander.cs`，模板在 `Styles/SettingsControls.axaml`）：模板里**任何一层都不许给背景**（0.4 的 FA 模板写死 `#B3FFFFFF`，就是"控件下面垫一层"的根因）；`--rail-check` 会数着 10 组 27 行验证。
- 外壳交互（标题栏拖拽 / 双击最大化 / 窗口按钮 / 8 个抓手 / 回车发送 / 滚动同步）只许用
  `Desktop/Behaviors/` 下的 `Behavior<T>`，别再往 code-behind 里加 `PointerPressed` / `KeyDown`；
  新增行为必须同时加 `--behavior-check` 断言（挂载数量 + 解绑后不触发）。
- 卡片内容必须向内缩：`Border.card` 用 `Padding="8"`，里面再套一层同半径（`RadiusCard`）且 `ClipToBounds="True"` 的容器才放滚动区，否则悬停色块会压在圆角上。
- 图标轨宽度只能取自 `RailMetrics`（收起 56 / 展开 188 / 条目 44 / 176），别在 XAML 里写数字。
- 改 XAML 时小心 `FINAL` 属性（`ComboBox.currentValue` 之类）与名字冲突（`Name="WindowEdge"` 会遮住 `WindowEdge` 枚举）。
- **内部控件动画只许一条属性在动**：图标轨展开时整窗只有轨道的 `Width` 在动，条目按钮用
  `Classes.expanded` 切成"跟着拉伸"，自己**不许**再挂一条 `Width` 过渡（否则每帧 1+N 次整页重排）。
  文字淡入用 `Opacity`（不改布局），不许用 `IsVisible` 切。
- **壁纸必须先缩放到窗口像素**（`HeliumTodo.Desktop/Services/WallpaperScaler.cs`）：把 4K 原图直接丢给
  `Image Stretch="UniformToFill"` 会让软件渲染每帧重采样一次（2560×1440 上 +27 ms/帧）。
- 过渡时长不许在 XAML 里写死数字：读 `RailMetrics.AnimationDurationMs` /
  `WindowTransitionPolicy.DurationMs`，界面、降质时长、自检断言三处共用一个数。

## 动效铁律

- **能让系统播的就别自己播**。最小化 / 最大化 / 还原交给 DWM：窗口样式位必须有
  `WS_CAPTION`（`WindowChrome.cs`），这是我们自己动画播不出来的唯一原因。
  别再把 `Window.State` 的过渡写回逐帧改尺寸。验证：`tools/probe-window-style.ps1`、
  `tools/probe-native-anim.ps1`（真机，DWM 会给 ≥12 个单调变化的可见面积）。
- **动画期间不许改 `Window.Opacity`**。Avalonia 走 `WS_EX_LAYERED` 分层窗口，改它会让 DWM
  重建重定向表面，还会让 DWM 拒绝播过渡。淡入用 `IntroVeil`（一层遮罩改画刷 Alpha）。
- 窗口被最小化时**必须**立刻 `Settle()`（`MainWindow.OnPropertyChanged` 里已接），
  否则动画停在半路：窗口半透明、内容留着缩放、边缘抓手偏 26 px。
- 窗口正在动（开场 / 拖拽改大小）时**必须**在 `InteractionQuality.Begin()` 作用域里：
  壁纸降 `LowQuality`、关卡片阴影；预计超过 250 ms 才暂停动图（`WindowTransitionPolicy`）。
  别删这个作用域，也别把降质改回去。
- 拖拽改大小只许**按帧**改窗口（`RequestAnimationFrame` 合并），不许在 `PointerMoved` 里直接改。
- 定性能结论前先跑 `--perf-check` 看数字；无头数字只用来**比较**，不代表真机（同一档无头 23 ms、真机 45 ms）。
- 凡是要用代码改的资源，**不许**写进 `ThemeDictionaries`（`CardSurfaceBrush` / `WallpaperScrimBrush` / `CardShadow` 都踩过）。

## 测试铁律

- 测试方法名用中文；方法上方一行中文注释写"为什么值得测"。
- `[[nodiscard]]` 风格的返回值（如 `Assert` 之外的自研检查）不要静默丢弃。
- `SelfTest` 运行器必须用 `if (result is Task t) t.GetAwaiter().GetResult();`，否则 async 测试会假绿。
- 自检必须用 `HELIUM_CONFIG_DIR` 指到沙盒目录，**绝不**碰 `%APPDATA%\Helium Todo` 的真实数据。

## 禁止

- 不提交/遗留临时物：`output/`、`_probe-*`、`tools/_*.py`、`.tmp-*`。
- 不改 `PROGRESS.md` 里的历史小节（旧版本号与旧数字是当时的事实）。
- 不擅自升级 Avalonia / SkiaSharp 版本（SkiaSharp 必须与 Avalonia 传递依赖同版，否则运行期崩）。
- 不把默认渲染后端改回 `Software`：默认是 `Auto`（显卡），理由是动效帧率（见 `ARCHITECTURE.md` §18.2 与 §19.9）。想省内存是**用户**在设置页里选，不是我们替他选。
- 不在写法里引入 `unsafe`（项目未开 `AllowUnsafeBlocks`）。
- 新增 `.cs` / `.axaml` 必须带两行 SPDX 头（`python tools\check-license-headers.py` 会拦），见 `DEVELOPMENT_SPEC.md` §13.3。
- 不为了省事关掉自检项（`--layout-check` 这类是为了防回归，红了要修根因）。
- 不在 `OnFrameworkInitializationCompleted` 里直接 `desktop.Shutdown()`：此时消息循环还没起来，
  关了 Dispatcher 会让 `StartCore` 抛 `InvalidOperationException`，进程以 `0xE0434352` 退出。
  要关就 `Dispatcher.UIThread.Post(() => desktop.Shutdown())`。

## 版本管理（2026-11-02 起）

- `main` = 发布线，`dev` = 开发线，日常在 `dev` 上改；标签打版本（当前 `v0.3.5-alpha`）。
- **每轮一个提交**，消息写"做了什么 + 为什么"，不写"update"这种空话。
- 交付前 `git status --short` 必须是**空**的：临时物（`output/`、`_probe-*`、`tools/_*.py`）要么删掉，要么就别生成。
- `bin/`、`obj/`、`dist/`、`Data/`、`*.db` 由 `.gitignore` 挡着，**不要** `git add -f` 硬塞进去。
- 换行统一 LF（`.gitattributes` 里 `* text=auto eol=lf`）；含中文的 `.ps1` 例外，必须 UTF-8 **带 BOM** + CRLF。

## 常用命令

```powershell
# 主存储是 PostgreSQL 17（便携版也认）；连不上时先看 HELIUM_DB
$env:HELIUM_DB = "Host=127.0.0.1;Port=55432;Database=helium_todo;Username=postgres"
dotnet build HeliumTodo.slnx -c Debug
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --shell
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --db-check
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --theme-check
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --behavior-check
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --copy-check
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --layout-check
dotnet HeliumTodo.Verification\bin\Debug\net10.0\HeliumTodo.Verification.dll --perf-check
git status --short          # 交付前这里必须是空的
git add -A && git commit -m "…"
pwsh tools\check-file-integrity.ps1
python tools\gen_ui_text_map.py
```

数据库相关测试要另设 `HELIUM_TEST_POSTGRES`（指向管理库 `postgres`，测试会在里面建临时库）；不设则这些用例跳过。

调试用的环境变量（都是"只在自检/截图时开"）：`HELIUM_CONFIG_DIR`（沙盒目录）、`HELIUM_APP_ID`（换单实例名字，让两份探针互不干扰）、`HELIUM_DB` / `HELIUM_DB_PASSWORD`（数据库连接）、`HELIUM_RENDER`（渲染后端）、`HELIUM_DARK`、`HELIUM_CARD_OPACITY`、`HELIUM_SHELL_PAGE`、`HELIUM_NO_RESTART`、`HELIUM_FAKE_DEBUGGER`、`HELIUM_*_DIALOG=1`（拍弹窗截图）、`HELIUM_ANIM_TRACE`（窗口动画逐帧帧间隔）、`HELIUM_NO_INTERACTION_QUALITY`（关掉动效降质做 A/B）。