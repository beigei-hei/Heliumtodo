# 窗口动效卡顿 / 最小化缺失 · 解决方案

> 状态：**待批准的实施计划**（尚未动代码）
> 触发：用户提供的录屏显示「窗口打开卡顿、放大缩小卡顿、最小化窗口缺失」
> 涉及版本：`v0.3.5-alpha`，分支 `dev`
> 本文只给方案与验收口径，不含实现。实施前请按 `DEVELOPMENT_PLAYBOOK.md` 走流程。

---

## 一、根因

三个症状对应三条独立根因。前两条有仓库自己的实测数字支撑，第三条是代码走查得到的确定性结论。

### 根因 A：软件渲染下「改一次 HWND 尺寸」是 45 ms 的固定开销，逐帧改尺寸在物理上不可能流畅

`PROGRESS.md:3745-3749` 的真机对照已经把话说完了：

| 配置 | 帧间隔（ms） | 帧数 |
| --- | --- | --- |
| 软件渲染 + 无降质（改动前） | 18.1 / **91.6** / 64.2 / 57.9 | 5 |
| 软件渲染 + 降质（现状） | 20.7 / 48.3 / 37.2 / 45.8 / 51.3 | 6 |
| GPU（`HELIUM_RENDER=angle`） | 15 / 24 / 18.9 / 11.8 / 11.9 / 12.2 / 11.8 / 12.6 / 17.9 / 19 / 11.4 / 12.3 / 23.4 | **14** |

变体 H（改尺寸但什么都不画）仍有 13~15 ms（`PROGRESS.md:3731`），说明开销不在「画内容」，而在**换尺寸本身**——软件渲染的窗口表面按面积分配，2560×1440×4 B ≈ 14.7 MB，每帧重新分配 + 整窗 blit。这也解释了为什么无头 J 档 23 ms、真机 45 ms：**差的这 22 ms 正是 Win32 表面重建那一段，无头路径量不到。**

`HeliumTodo.App/Windowing/WindowAnimator.cs:239`（`AnimateWindow`）每帧调 `:292`（`ApplyRect`）改 `Position/Width/Height`，正好踩在这个 45 ms 上。

> **结论：`InteractionQuality` 已经把能砍的都砍了（−57%），沿「降低每帧画面成本」这条路继续优化已经走到头。**

### 根因 B：DWM 不给我们播动画，是因为窗口样式位少了 `WS_CAPTION`，不是「无边框窗口的宿命」

`HeliumTodo.App/Windowing/WindowChrome.cs:47-50` 只补回了 `WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX`。按 `WindowChrome.cs:14-15` 记录的实测值解码：

| 窗口 | 样式位 | 解码 |
| --- | --- | --- |
| 我们的（`None` + 补样式） | `0x160F0000` | VISIBLE \| CLIPSIBLINGS \| CLIPCHILDREN \| MIN \| MAX \| SYSMENU \| THICKFRAME |
| Avalonia `BorderOnly` | `0x16870000` | 上面 + `WS_BORDER`（0x00800000） |
| **真正的普通 Win32 窗口** | **`0x16CF0000`** | 上面 + **`WS_DLGFRAME`（0x00400000）** |

`WS_CAPTION = WS_BORDER | WS_DLGFRAME = 0x00C00000`。**我们的窗口从来没有过 caption 位**，而 DWM 的最小化 / 最大化 / 还原过渡动画正是以「这个窗口有没有标题栏框」为门槛的。

`ARCHITECTURE.md:358-362` 记录的「补了 THICKFRAME 仍然瞬间跳变、查遍 DWM 属性没有开关被关」完全符合这个解释——查的是 DWM 侧的开关，缺的是窗口侧的样式位。

这条一旦成立，**三个症状里的两个（放大缩小卡顿、最小化缺失）会被系统免费解决**，而且在软件渲染下也是 60 fps：DWM 动画发生在合成器里，与我们的光栅化后端无关。

### 根因 C：最小化路径根本没接进动画体系，会留下「隐形窗口」

`HeliumTodo.App/Views/ShellView.axaml.cs:110-116` 直接 `WindowState = Minimized`，而 `FinishAnimationsImmediately()`（→ `Settle()`）全项目只有一个调用点：`HeliumTodo.App/App.axaml.cs:267`（藏到托盘）。后果：

> 开场动画播到一半（180 ms 内）点最小化 → 定时器在隐藏窗口上继续跑但拿不到帧 → 窗口永久停在 `Opacity < 1` + `ScaleTransform(0.96)` → 从**任务栏**点回来时 `RestoreMainWindow`（`App.axaml.cs:353`）不会被调用（那只服务托盘），于是回来的是**一个半透明、缩小了 4% 的窗口**；且按 `ARCHITECTURE.md:386-388` 已记录的坐标换算问题，边缘抓手命中点会偏 26 px（x=1277 被算成 1251.5），**拖拽改大小同时失效**。

用户说的「最小化窗口的缺失」很可能就是这个——不是没有动画，是**最小化再回来窗口就不对了**。

---

## 二、方案总览

按杠杆从高到低。**L1 先做，成功则 L2 大部分可删。**

| | 动作 | 解决 | 成本 | 风险 |
| --- | --- | --- | --- | --- |
| **L1** | 补 `WS_CAPTION`，让 DWM 自己播最小化 / 最大化 / 还原 | 症状 2、3 根治 | 1 行 + 1 个探针 | 低；不成功则无副作用 |
| **L1+** | 迁到 Avalonia 12 原生 `WindowDrawnDecorations` | 症状 2、3 + 删掉自绘抓手 | 中（结构性） | 中 |
| **L2** | 保底：把动画从「改 HWND」挪到「改内容矩阵」 | 症状 2 缓解（6 帧 → 9~12 帧） | 中 | 低 |
| **L3** | 重写开场动画：去掉 `Window.Opacity` 与整窗缩放 | 症状 1 | 小 | 低 |
| **L4** | 修最小化路径的 5 个正确性 bug | 症状 3 | 小 | 无 |
| **L5** | 修 `--perf-check` 的失真（前面几条都靠它验收） | 度量 | 小 | 无 |

**L3、L4、L5 无论 L1 结果如何都必须做**，可以先并行推进。

---

## 三、L1 · 补 `WS_CAPTION`，把动画交回 DWM（首选）

### 改动

`HeliumTodo.App/Windowing/WindowChrome.cs:31-36` 加常量，`:47-50` 的回调里或上它：

```csharp
private const uint WS_BORDER   = 0x00800000u;
private const uint WS_DLGFRAME = 0x00400000u;
private const uint WS_CAPTION  = WS_BORDER | WS_DLGFRAME;   // 0x00C00000

(style | WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX, exStyle)
```

不会长出标题栏：`WindowChrome.cs:65-90` 的 `WM_NCCALCSIZE` 钩子已经把 `rgrc[0]` 原样返回（非客户区 = 0），caption 位只影响 DWM 的**过渡动画判定**，不影响布局。这正是 Chromium / WPF WindowChrome 那套自绘外壳的标准配方。

同时显式**写**一次（不要只读）：`DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, FALSE)`。

### 必须一并改掉的冲突点

**开场动画不许再动 `Window.Opacity`。** 已在 `Avalonia.Win32.dll`（12.1.4）的导入表里确认存在 `SetLayeredWindowAttributes` + `WS_EX_LAYERED`：Avalonia 的 `Window.Opacity` 走的是**分层窗口**路径。

`WindowAnimator.PlayIntro` 在第 0 帧把 `Opacity` 从 1 设成 0（加上 `WS_EX_LAYERED`，`:147`）、在最后一帧设回 1（摘掉 `WS_EX_LAYERED`，`:184`）——两次改扩展样式位都会让 DWM 重建窗口的重定向表面，这既是开场那一下的额外卡顿来源，也可能让 DWM 拒绝为分层窗口播过渡动画。

> **这一条不做，L1 大概率验不出来。** 具体改法见第六节 L3。

### 成功之后可以删掉的东西

- `WindowAnimator.cs` 的 `Maximize`（:88）/ `Restore`（:104）/ `AnimateWindow`（:239）/ `ApplyRect`（:292）/ `CurrentPixelRect`（:281）/ `GetWorkArea`（:303），约 120 行——最大化 / 还原交给系统。
- `ShellView.axaml.cs:128-143` 的 `ToggleMaximize` 退化成一行 `WindowState = ...`。
- `WindowAnimator.cs:316-341` 的 `HELIUM_MAXIMIZE_TEST` 探针钩子。
- `_restoreRect` 字段及其失同步问题（见 L4 第 2 条）整体消失。

### 白捡的收益（现在的实现根本做不到）

`Win+↑` / `Win+↓` / 拖到屏幕顶边 / 双击任务栏 / Aero Snap 分屏，全部走系统动画；且窗口状态由系统单一记账，`_restoreRect` 再也不会和系统失同步。

### 验证方法

> ⚠️ **现有探针在这里会给出假阴性。** `tools/probe-maximize-anim.ps1` 靠高频采样 `GetWindowRect` 数「中间尺寸」来证明动画在跑。但 DWM 的原生动画**改的是窗口视觉，不是矩形**——矩形只有 1~2 个值，探针会判「瞬间跳变」。必须换验证手段：

1. **新增 `tools/probe-window-style.ps1`**：打印 `GetWindowLong(hwnd, GWL_STYLE)`，断言 `& 0x00C00000 == 0x00C00000`。
2. **新增 `tools/probe-native-anim.ps1`**：触发最小化，用 `PrintWindow` / `BitBlt` 对整个屏幕区域以 8 ms 间隔连拍 30 张，统计「窗口可见像素面积」的时间序列。原生 DWM 动画给出单调递减的 15~20 个不同面积；跳变只有 1~2 个。这是唯一能客观区分两者的办法（面积序列是数字，不依赖人看图）。
3. 保留 `--shell` / `--layout-check` 全绿，确认补 caption 位没有把 7 px 灰边带回来——重点看 `tools/probe-window-frame.ps1` 的「窗口矩形 vs 客户区」是否仍然相等。

---

## 四、L1+ · 迁到 Avalonia 12 原生 `WindowDrawnDecorations`（L1 成功后的结构性收尾）

Avalonia 12 已经内置了这套东西，而项目现在是手写的。以下 API 已在 `~/.nuget/packages/avalonia/12.1.4/lib/net10.0/Avalonia.Controls.xml` 核过：

- `Avalonia.Controls.Chrome.WindowDrawnDecorations.Attach(Window)` / `Detach`
- `EnabledParts`：`DrawnWindowDecorationParts.TitleBar | Border | Shadow | ResizeGrips | All | None`
- `Avalonia.Input.WindowDecorationsElementRole`：`TitleBar` / `MinimizeButton` / `MaximizeButton` / `CloseButton` / `FullScreenButton` / `ResizeN…ResizeSW` / `User` / `DecorationsElement`
- `Avalonia.Controls.Win32Properties.NonClientHitTestResult` 附加属性
- `IWindowImpl.RequestedDrawnDecorations` 的官方说明原文：**「X11 needs shadow, border, and resize grips; Win32 only needs titlebar/buttons」**

也就是说：Win32 上采用这套之后，**边框、阴影、改大小全部保持原生**，Avalonia 只接管标题栏和三个按钮。落地映射：

| 现在 | 换成 |
| --- | --- |
| `ShellView.axaml:78-82` 的 `TitleBar` + `OnTitleBarPointerPressed` / `OnTitleBarDoubleTapped` | `ElementRole = TitleBar` |
| `ShellView.axaml:111 / 114 / 117` 三个 `Click` 处理器 | `MinimizeButton` / `MaximizeButton` / `CloseButton`（Win32 上映射到 `HTMINBUTTON` / `HTMAXBUTTON` / `HTCLOSE`，**行为由系统执行**） |
| `MainWindow.axaml:82-112` 八个透明 `Border` + `WindowResizeGrips.cs`（189 行） | `ResizeN…ResizeSW` 角色，**整个类可删** |
| `WindowChrome.cs` 手写样式位 + `WM_NCCALCSIZE` | 由框架负责 |

额外白捡：`WindowResizeGrips.cs:23-24` 自己承认的「拖动由界面帧驱动，不像系统那样硬件光标实时跟手」——回到系统改大小循环之后这个问题自动消失，而且**拖拽改大小也不再需要 `InteractionQuality` 降质**，因为不再逐帧走 Avalonia 的光栅化路径。

> ⚠️ 这一条是结构改动，需要单独一轮，**不要和 L1 混在一起做**。

---

## 五、L2 · 保底方案：把动画从「改 HWND」挪到「改内容矩阵」

如果补了 `WS_CAPTION` DWM 仍然不播（可能性存在，例如 Win11 26300 对非客户区面积为 0 的窗口另有判定），则改动画的**承载对象**：一帧都不改窗口尺寸，只改内容的变换矩阵。

### 做法

重写 `WindowAnimator.AnimateWindow`（`:239`）：

1. 算出 `from`（当前矩形）、`to`（目标矩形）——沿用现有 `CurrentPixelRect()` / `GetWorkArea()`。
2. **一次性**把 HWND 落到 `to`（最大化就 `WindowState = Maximized`；还原就直接设 `Position/Width/Height`）。整轮动画只改一次尺寸 → 45 ms 只付一次。
3. 立刻在内容根元素上装 `MatrixTransform`，`RenderTransformOrigin = TopLeft`，初值 = 把 `to` 映射回 `from` 的矩阵：
   `sx = from.W / to.W`，`sy = from.H / to.H`，`tx = from.X - to.X`，`ty = from.Y - to.Y`（均换算为 DIP）。
4. 逐帧 `t: 0 → 1`，把矩阵按 `EaseOut(t)` 向单位矩阵插值；同时给内容根设 `RectangleClip`，尺寸 = 当前插值矩形，防止内容溢出到窗口外。
5. `t == 1` 时清空 `RenderTransform` 与 `Clip`，恢复完全体。

### 与已被否掉的「快照拉伸」实验的区别（重要，别当成重复劳动）

`PROGRESS.md:3739-3741` 记录：动画期间拍位图只拉伸那张图，真机一点没快。**那次实验仍然逐帧改 HWND 尺寸**，所以它证明的恰恰是「45 ms 花在换尺寸上，不在画内容上」——而这正是本方案要移除的东西。本方案把改尺寸从循环里彻底拿掉，两者不是同一个假设。

### 预期与代价

- 每帧成本从「改尺寸 + 整窗重画」降到「改内容矩阵 + 整窗重画」，即 `PROGRESS.md:3730` 的 G 档量级（无头 13.9~15.6 ms），且**不再付 14.7 MB 表面重建的那 22 ms**。真机预计 45 ms → 18~22 ms，帧数 6 → 9~12。
- 代价：DWM 的窗口阴影 / 圆角在第 0 帧就跳到终态，只有内容在动。**这是必须让用户拍板的取舍**——接受不了就只能走第九节的「换 GPU 后端」。
- 还原方向：`to` 比 `from` 小，矩阵初值是放大（`sx > 1`）配 `Clip` 裁掉溢出部分，视觉上是「小窗口出现、内容从放大状态收回」。HWND 外面那块旧区域会瞬间空出来，这点和 Windows 原生还原动画有差，验收时要明确接受。

---

## 六、L3 · 重写开场动画（症状 1）

现在 `WindowAnimator.PlayIntro`（`:135`）每帧同时做两件在软件渲染下都贵的操作：`Window.Opacity`（F 档 14~16 ms）+ 内容 `ScaleTransform`（G 档 13.9~15.6 ms）。改法：

1. **删掉 `Window.Opacity`**，理由见第三节（`WS_EX_LAYERED` 抖动 + 与 DWM 动画冲突）。
2. **淡入改成「一块盖在最上层的纯色遮罩，改画刷 Alpha」**：在 `MainWindow.axaml:56` 的根 `Grid` 末尾加 `Border Name="IntroVeil"`，`Background` 绑一个 `SolidColorBrush`，`IsHitTestVisible="False"`，Z 序高于壁纸三层但低于 `NoticeBar`。每帧只改 `brush.Color` 的 A 通道（1 → 0），收尾 `IsVisible = false`。

   为什么这样便宜：**改画刷 Alpha 不需要离屏图层**（改 `Visual.Opacity` 会强制 push layer，即整窗离屏缓冲 + 回混，就是 F/G 档那 14 ms）；改画刷只是让 Skia 用新颜色重画那个矩形，成本落在 `PROGRESS.md:3727` 的 D 档（1.4~2.0 ms）。
3. **删掉 `ScaleTransform 0.96 → 1`**（`:148-149` 设初值、`:169-171` 逐帧改）。4% 的缩放在 180 ms 内几乎不可感知，却要每帧付 14 ms 的整窗变换重画。顺带解决 `ARCHITECTURE.md:386-388` 记录的「缩放导致抓手命中点偏 26 px」——**没有缩放，就没有那个坐标陷阱，`Settle()` 的必要性也大幅下降**。

   若产品坚持保留缩放，必须以 `--perf-check` 新增变体（见第八节）实测：**单帧成本超 8 ms 就砍掉**。
4. **驱动源换成 `RequestAnimationFrame`**，替掉 `DispatcherTimer`（`:158` 与 `:247`）。定时器按 11.25 ms 排（`WindowAnimationMath.cs:30`），但真机一帧 45 ms，于是 tick 与实际出帧脱节，产生实测的 `18.1 / 91.6 / 64.2 / 57.9` 这种**极不均匀**的帧间隔——不均匀比慢更让人觉得「卡」。渲染回调保证「改一帧 = 出一帧」，忙的时候自动丢帧而不是排队。项目自己已经在 `WindowResizeGrips.ScheduleApply`（`:158-175`）用了这个手法，动画器应当一致。
5. `WindowAnimationMath.Frames = 16`（`:21`）在换掉定时器后只剩「推导 `FrameInterval`」一个用途，直接删；`DurationMs` / `EaseOut` / `ProgressOf` / `Interpolate` 保留。

---

## 七、L4 · 修最小化路径的正确性 bug（无论 L1 成不成立都要做）

1. **接上 `Settle()`。** 在 `MainWindow` 构造函数（`MainWindow.axaml.cs:27`）里订阅 `this.GetObservable(WindowStateProperty)`（Avalonia 12 的 `WindowStateProperty` 是 `StyledProperty`，已在 `Avalonia.Controls.xml` 确认；**没有** `WindowStateChanged` 事件，别去找）：
   - 进入 `Minimized` → 立刻 `_animator.Settle()`（根因 C 的直接修复）；
   - 离开 `Minimized` 且此前不是从托盘回来的 → 播一次 `PlayIntro()`（补上「从任务栏点回来是硬生生蹦出来」的缺口；`App.axaml.cs:353-371` 目前只覆盖托盘）。

2. **`_restoreRect` 失同步。** 它只在 `WindowAnimator.Maximize()`（`:99`）里赋值。用 `Win+↑` / 拖到顶边 / 双击任务栏最大化时它保持 null，`Restore()`（`:106`）就静默退化成瞬间跳变。改由上面那个 `WindowStateProperty` 订阅统一记账：**任何**进入 `Maximized` 的转换，都把转换前的 Normal 矩形存下来。（L1 成功则整块连同 `_restoreRect` 一起删。）

3. **`Restore()` 一帧内改了两次尺寸。** `:117-118` 先 `WindowState = Normal`（系统自己会摆一次尺寸）再 `ApplyRect(start)`（我们拽回去）= 两次 45 ms 的表面重建。注释说「同一个 UI 帧里完成，屏幕上不会闪」，但成本是双份的，而且这正是还原方向比最大化方向更卡的来源。合并成一次落地（L2 方案下天然消失）。

4. **`OnClosing` 会存下动画中途的尺寸。** `MainWindow.axaml.cs:95-99` 的判据是 `WindowState == Normal`，但还原动画全程窗口都是 `Normal` 且尺寸是中间值——此刻关窗，下次启动就恢复出一个畸形尺寸。改为存 `_restoreRect`（或「已稳定」标记下的尺寸），并在写盘前先 `Settle()`。

5. **`InteractionQuality` 里暂停动图壁纸可能是负收益。** `InteractionQuality.cs:117-146` 对 180 ms 的动画做 `Stop()` → `Start()`，视频壁纸重启要重新 seek + 解码首帧，这个代价可能超过它省下的每帧 33 ms。改为**只在预计动画时长 > 250 ms 时**（即改尺寸类动画，不含开场遮罩淡入）才暂停。

---

## 八、L5 · 修 `--perf-check` 的失真（前面几条的验收都依赖它）

`HeliumTodo.RenderCheck/Program.cs:754`（`RunPerfCheck`）现在的量法和真机差了两档，导致「无头 23 ms / 真机 45 ms」这种没法用来做决策的数字：

| 失真点 | 现状 | 改成 |
| --- | --- | --- |
| 窗口尺寸 | 1400×900（`:766-771`） | 加 `--perf-size=2560x1440`，默认取主屏工作区。表面成本按面积走，1400×900 只有 2560×1440 的 25% |
| 壁纸尺寸 | `CreatePerfWallpaper(1600, 1000)`（`:781`） | 用 `WallpaperDecoder.MaxLongSide = 1920`（`HeliumTodo.App/Services/WallpaperDecoder.cs:77`，线上真实值）。在大屏上它是**上采样**，成本和 1600×1000 完全不同 |
| 改尺寸幅度 | `ResizeSlightly` | 按真实动画幅度改（1400×900 ↔ 整个工作区），不是「稍微改一点」 |
| 只看均值 | `Mean` | 同时报 **P50 / P95 / 最大值 / 变异系数**。卡顿的本质是帧间隔不均匀（18/91/64/58 的均值 58 看着还行，体感是灾难） |

新增三个变体：

- **K：只改内容 `RenderTransform`，不改窗口尺寸** ← L2 方案的直接依据，现在完全没有这个数据点
- **L：改窗口尺寸 + 纯色壁纸 + 无阴影** ← 隔离出 Win32 表面重建的净成本，用来解释「无头 vs 真机」的差额
- **M：只改遮罩画刷 Alpha** ← L3 方案的直接依据，预期落在 D 档（1.4~2.0 ms）

新增 **`--anim-check`**：跑真实的开场 / 最大化动画循环，断言 `帧数 ≥ 10` 且 `最大帧间隔 < 2 × P50`（不均匀度门禁）。这条比均值断言更能防回归。

---

## 九、关于「换 GPU 后端」这个选项（需要用户拍板，不替它做决定）

`HeliumTodo.App/Program.cs:59-65` 记录了默认软件渲染的实测代价：+187 MB 驱动模块映射（nvgpucomp64 94 MB + nvwgf2umx 86 MB + nvppex 7 MB）、线程 19 → 84、私有工作集 48 → 69 MB。而软件渲染的代价是窗口动效永久卡在 6 fps。

**如果 L1 成立，这个取舍就不用重做了**——DWM 动画在合成器里跑，软件渲染后端照样 60 fps，两个成本都躲掉。这是把 L1 排在最前面的核心理由。

如果 L1 失败、且 L2 的 9~12 帧仍不达标，那就只剩改默认后端这一条路：`HeliumTodo.Core/Configuration/RenderBackendSettings.cs:33` 的 `Default` 从 `Software` 改成 `Auto`，走 `HeliumTodo.App/Services/RenderBackendChoice.cs:57-63`（`ToModes`）的 `AngleEgl`。

> 这是产品决策，不是技术决策，需要明确批准后再动。

---

## 十、验收标准

按 `AGENTS.md`「先给验收标准」的要求，把用户的话拆成 6 条可验证的行为：

1. `GetWindowLong(GWL_STYLE)` 含 `WS_CAPTION`，且 `tools/probe-window-frame.ps1` 仍报「窗口矩形 = 客户区」（壁纸照样铺满，没长出灰边）。
2. 点最小化按钮，`tools/probe-native-anim.ps1` 采到 **≥ 12 个单调递减的不同可见面积**（跳变只有 1~2 个）→ 证明最小化动画存在且在跑。
3. 最大化 / 还原同上，采到 ≥ 12 个不同面积；`--anim-check` 报 `最大帧间隔 < 2 × P50`。
4. 开场动画播到一半（< 100 ms 内）点最小化，再从**任务栏**点回来：窗口 `Opacity == 1`、内容 `RenderTransform == null`、右边缘抓手命中点与 `bounds` 偏差 **< 2 px**（现在偏 26 px）→ 根因 C 已修。
5. 用 `Win+↑` 最大化后再点还原按钮，走的是**有动画**的还原路径而不是瞬间跳变（现在必然跳变）。
6. `--perf-check` 新增 K / L / M 三档在 2560×1440 + 1920 长边壁纸下出数，且 **M 档 < 4 ms**。

---

## 十一、门禁与文档同步

**门禁**（`AGENTS.md`「先跑门禁再报告」）：

```powershell
dotnet build HeliumTodo.slnx -c Debug          # 0 警告 0 错误
dotnet HeliumTodo.SelfTest\bin\Debug\net10.0\HeliumTodo.SelfTest.dll   # ≥ 179/179（本轮会新增用例）
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --shell
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --layout-check
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --rail-check
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --perf-check
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --anim-check   # 本轮新增
git status --short                             # 交付前必须是空的
pwsh tools\check-file-integrity.ps1
```

**新增 Core 侧可测规则**（`AGENTS.md` 分层铁律：「可测的算术与规则放 Core」）：
新增 `HeliumTodo.Core/Windowing/WindowTransitionPolicy.cs`，放时长 / 帧预算 / 「是否需要暂停壁纸」的阈值判定，纯托管无 Avalonia 依赖，配中文方法名单测；`WindowAnimationMath` 里依赖 `PixelRect` 的插值留在 App 层。

**文档同步**：

- `PROGRESS.md`：新增本轮小节。**不改 `PROGRESS.md:3706-3782` 的旧数字**（那是当时的事实）。
- `ARCHITECTURE.md`：改写 5.2 的「最大化动画：DWM 不给，就自己写」一节（`:358-362`）。如果 L1 成立，那一节的结论（「就是 DWM 不认这个无边框窗口」）是**错的**，必须订正为「缺 `WS_CAPTION`」，否则下一个人还会照着错结论走一遍。
- `AGENTS.md`：动效铁律加一条——**不许在动画期间改 `Window.Opacity`（会抖 `WS_EX_LAYERED`）**。
- `tools/file-hashes.json`：`-Update` 后复查。

---

## 十二、建议的执行顺序

```
L5 先修度量（没有可信数字，后面每一步都验不了）
  ↓
L3 重写开场动画（独立、低风险，先拿掉 WS_EX_LAYERED 这个 L1 的干扰源）
  ↓
L1 补 WS_CAPTION + 探针验证            ← 决策点
  ├─ 成立   → L4 修 bug → 删 WindowAnimator 的最大化/还原部分 → L1+ 迁 WindowDrawnDecorations
  └─ 不成立 → L2 内容矩阵动画 → L4 修 bug
```

**版本管理**（按 `AGENTS.md`「版本管理（2026-11-02 起）」）：日常在 `dev` 上改，**每层一个提交**，消息写「做了什么 + 为什么」；交付前 `git status --short` 必须是空的——本轮新增的 `tools/probe-window-style.ps1` / `tools/probe-native-anim.ps1` 是正式工具（不是 `_probe-*` 临时物），要一并入库并更新 `tools/file-hashes.json`。含中文的 `.ps1` 必须 UTF-8 **带 BOM** + CRLF。

---

## 附：症状 → 方案对照表

| 用户看到的现象 | 根因 | 方案 | 预期结果 |
| --- | --- | --- | --- |
| 窗口打开卡顿 | `Window.Opacity` 抖 `WS_EX_LAYERED` + 整窗 `ScaleTransform`（各 14~16 ms/帧）+ 定时器与实际出帧脱节 | **L3** | 单帧成本 14~16 ms → ~2 ms，16 帧全部到位，帧间隔均匀 |
| 放大缩小卡顿 | 逐帧改 HWND 尺寸，软件渲染下 45 ms/帧固定开销 | **L1**（首选）/ **L2**（保底） | L1：DWM 原生 60 fps；L2：6 帧 → 9~12 帧 |
| 最小化窗口缺失 | ① 没有最小化动画；② 最小化未接 `Settle()`，动画中途最小化后窗口停在半透明 + 0.96 缩放，从任务栏回来就是坏的 | **L1** 解决 ①；**L4** 解决 ②（无论 L1 成不成立都要做） | 最小化有系统动画；任意时刻最小化再回来，窗口都是完全体且拖拽可用 |
