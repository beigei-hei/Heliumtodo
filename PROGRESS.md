# Helium Todo · 开发进度交接单

> 这份文件是给「下次打开这个项目的人（或 AI）」看的：
> 做到哪了、什么状态、下一步干什么、哪些坑已经踩过。

当前版本：**v0.3.5-alpha**
状态：**已迁移到 Avalonia 12 + FluentAvaloniaUI 3（.NET 10），三大核心功能完整跑通；
新增「背景壁纸 + 自定义调色板 + 局域网联机自习 + 统一 .zip 备份 + 待办/日程提醒 + 托盘常驻」六块，179 个测试全绿，7 项界面渲染全部通过（含整个外壳）；
设置页说明文字已精简，「打开文件夹」改为「Avalonia Launcher → 四级进程降级」；
界面已按 `UI_REDESIGN_BRIEF.md` 改成「三列卡片工作台」（自绘标题栏 + 图标轨 + 卡片 + 右栏）**

---

> ## 📦 附带的 C++ 移植项目已分离出去
>
> 这个项目下面原本有一个 `cpp/` —— 是把 Helium Todo 移植到 C++20 + Qt 6.12
> 的一份完整实现（七个页面、257 个测试、能打包）。
>
> 它现在已经**移到独立位置**：
>
> ```
> D:\代码\dsh project\Helium todo demo     ← 本项目（C#，唯一的开发重心）
> D:\代码\dsh project\Helium todo cpp      ← C++ 移植版（独立项目）
> ```
>
> **本仓库不再包含任何 C++ 代码。** 上面那条命令、测试、渲染自检
> 全都只针对 C# 版，和 C++ 那边无关。
>
> ### 为什么分开
>
> - C# 版是**开发重心**，仓库里混着另一套技术栈的完整实现会干扰阅读和维护
> - 完整性校验（84 个文件的指纹基线）本来就不该把 C++ 那边算进来
> - 两个项目各有自己的构建体系，分开放各自清清爽爽
>
> ### 分离带来的两个小修
>
> | 问题 | 处理 |
> | --- | --- |
> | `tools/check-file-integrity.ps1` 把 `cpp\build` 里的第三方源码也扫进来（84 → 988 个文件），还撞上带 `[]` 的文件名报错 | 排除 `cpp`/`dist`/`node_modules`；改用 `-LiteralPath` 避免通配符解析 |
> | 两个 `.ps1` 脚本没有 BOM，Windows PowerShell 按 GBK 读导致中文乱码、语法报错 | 存成带 BOM 的 UTF-8 |
>
> 迁移方案文档 `PORTING_TO_CPP.md`（1352 行）也一并搬走了 ——
> 它记录的是「怎么从 C# 移植到 C++」的完整思路，属于 C++ 项目。
> 现在它在 `D:\代码\dsh project\Helium todo cpp\PORTING_TO_CPP.md`。
>
> 那份文档的附录 A 有一份 C# 代码文件清单，
> 想了解移植视角下怎么看这份代码时可以翻翻。
> 但读本项目只看这份 PROGRESS.md 就够了。
>
> ---

## 一、当前状态总览

| 部分 | 状态 | 说明 |
| --- | --- | --- |
| 解决方案骨架 | ✅ 完成 | 6 个项目，解决方案用 `.slnx` 格式，编译 **0 警告 0 错误** |
| HeliumTodo.Core（内核） | ✅ 完成 | 模型 + 冲突算法 + 计时器 + 主题/插件接口 |
| HeliumTodo.Data（存储） | ✅ 完成 | EF Core + SQLite，含示例数据 |
| HeliumTodo.App（界面） | ✅ 完成 | 6 个页面，**实机启动验证通过** |
| HeliumTodo.Tests（测试） | ✅ 完成 | **119 个单元测试全部通过**（由 SelfTest 跑） |
| HeliumTodo.SelfTest | ✅ 完成 | 不依赖测试框架的测试运行器 |
| HeliumTodo.RenderCheck | ✅ 完成 | **无头渲染自检，6 个页面 + 外壳共 7 项全部渲染成功** |
| 技术栈升级到 Avalonia 12 | ✅ 完成 | FluentAvaloniaUI 3.1.0，目标框架 net10.0 |
| 背景壁纸 + 随壁纸取色 | ✅ 完成 | PNG / JPG / GIF / APNG，主色调按色相 24 桶投票 |
| 自定义调色板 | ✅ 完成 | 方形选色 + 色相条 + RGB + 十六进制，四者双向同步 |
| 自习室联机（局域网 P2P） | ✅ 完成 | 无中心服务器，房主中转；**跨网络 / NAT 穿透未做** |
| 数据备份（统一 .zip） | ✅ 完成 | 数据库 + 配置 + 壁纸打成一个包；导入可选择替换或合并 |
| 界面改版（三列卡片工作台） | ✅ 完成 | 自绘标题栏 + 56px 图标轨 + 卡片系统 + 右栏 360；壁纸默认模糊 24 |
| 壁纸铺满整窗 | ✅ 完成 | `WindowDecorations="None"` + 自己补的 8 个改大小抓手；非客户区从 7–8px 降到 0 |
| 后台常驻（关窗口不退出） | ✅ 完成 | 托盘菜单 + 单实例守卫两条独立退路；托盘失效时再双击一次即可唤回窗口 |
| 窗口动效（打开 / 最大化） | ✅ 完成 | 自绘 180ms：淡入 + 96%→100%、最大化逐帧插值；`Settle()` 负责收尾 |

### 已经验证过的事实（不是「应该能跑」，是真的跑过了）

- `dotnet build HeliumTodo.slnx` → **0 警告 0 错误**
- 119 个单元测试 → **全部通过**（约 1.9 秒）
- 6 个页面 + 整个外壳共 7 项无头渲染 → **全部成功**，图片在 `output/render/`
- 六个导航图标真的点一遍（`--shell-check` 11 项）→ **页面都换了、选中态只有一个**
- 导出一份备份再导入回来 → **数据条数一致、合并模式不产生重复**（`--backup-check` 15 项断言）
- 真实程序启动 → **窗口正常显示、无异常退出**（198 MB 内存）

---

## 二、怎么跑起来

根目录 `global.json` 已锁定 .NET **10.0.401** SDK。
项目的目标框架现在是 **net10.0** —— 这是迁移到 FluentAvaloniaUI 3 时被逼的：
FA 3 只发布了 net10.0 一个目标框架，没有 net8.0 版本（详见第三节）。

```powershell
# 1) 编译
dotnet build HeliumTodo.slnx -c Debug

# 2) 跑单元测试（139 个）
dotnet run --project HeliumTodo.SelfTest

# 3) 联动自检：加 / 完成待办后，今日工作流会不会自己刷新；拨联机开关自习室页会不会跟着变
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --refresh-check

# 4) 时间轴首尾自检（7 项）与提醒自检（11 项）
#    --timeline-check 看 00:00 / 24:00 有没有被切掉
#    --reminder-check 走一遍"选提醒 → 存库 → 调度器挑得到 → 通知正文对不对"
# 5) 导入导出自检（15 项，自己建沙盒目录，不动你的真实数据）
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --backup-check

# 6) 界面渲染自检（不需要桌面环境，输出 PNG 到 output/render/）
dotnet run --project HeliumTodo.RenderCheck
#    加 --tree 可以顺便打印可视化树，排查「控件怎么没显示」时很有用
dotnet run --project HeliumTodo.RenderCheck -- --tree
#    加 --shell 会额外渲染整个主窗口（左侧导航栏 + 内容区）成 外壳.png
dotnet run --project HeliumTodo.RenderCheck -- --shell

# 7) 启动程序
dotnet run --project HeliumTodo.App
```

### 不想装 .NET SDK 也想跑？（给别人试用 / 换台电脑测）

```powershell
dotnet publish HeliumTodo.App/HeliumTodo.App.csproj -c Release -r win-x64 ^
  --self-contained true -o dist/HeliumTodo-0.1.0-win-x64
```

产物是**自包含**的（连 .NET 运行时一起打包，219 MB，删掉 `*.pdb` 后 119 MB），
双击 `dist/HeliumTodo-0.1.0-win-x64/HeliumTodo.App.exe` 就能跑，目标机器**不需要装 .NET**。

- `-r win-x64` 就是「amd64 架构」；32 位机器用 `win-x86`，ARM 机器用 `win-arm64`
- 数据仍然存在 `%APPDATA%\Helium Todo\`，和从 IDE 里跑的是**同一份**数据
- 想便携（数据跟着 exe 走）：在 exe 旁边放一个 `config.json`，程序会进便携模式

> **编译莫名失败时**（报 0 错误但构建不过），加这两个参数：
> `-m:1 -nodeReuse:false`（单进程、不复用编译节点）。
> 这是本机环境的已知问题，见第六节。

---

## 三、这一轮做了什么

### F. 联机优化（参考 EasyTier）+ 房主才显示地址

用户提的两点：①「联机部分仅在作为房主创建房间的时候显示主机 IP 地址」②「参考 easytier 的联机实现方式优化联机」。

**① 地址只在房主这边显示**

第一版把地址块放在「还没进房间」那张卡片里 —— 结果是**建房成功的那一刻它就消失了**，
因为进房间后整张卡片会被「房间内」那块换掉。
现在挪到房间头部卡片里，单独占一行（塞在表头那个 Grid 里会被右边的按钮挤窄、文字被裁掉），
`IsVisible="{Binding IsHost}"` 绑到 `StudyRoomViewModel.IsHost`，
而它来自 `StudyRoomService.IsHost` → `IPeerTransport.IsHost` → 传输层真实的 `_isHost`。

**② 照 EasyTier 的做法补了四件事**

| EasyTier 的做法 | 本项目对应的实现 | 为什么值得抄 |
| --- | --- | --- |
| 节点互相报监听地址 | `PeerMessage.ListenPort` + 房主下发的 `Peers` 地址表 | TCP 出去的源端口是随机的，不报监听端口，别人没法反过来连你 |
| `--mapped-listeners`（对外地址） | `PeerMessage.AdvertisedEndpoint` + `IPeerTransport.AdvertisedEndpoint` | 端口映射 / 跨网段时，本机监听的和别人该连的不是同一个地址 |
| 心跳 + 延迟显示 | `HeartbeatLoopAsync`：每 4 秒 Ping，Pong 原样回传时间戳算 RTT；15 秒没消息判定掉线 | 拔网线 / 休眠时 TCP 不会告诉你对端没了，成员面板会留下「幽灵」 |
| 节点地址表下发 | `PeerMessageKinds.Peers` + `PeerMessageCodec.FromPeerList` | 客户端不只认识房主，还知道其余人在哪（将来做直连不用再改协议） |

**明确没做的**（都在文档里写了理由）：STUN / NAT 穿透、加密、TUN 虚拟网卡。
这三样是 EasyTier 做"跨公网组虚拟局域网"的核心，对一个「同宿舍/同教室一起自习」的
小工具来说收益很低，而工作量和安全责任都不小。

界面上的验证开关（只用于自检，不影响正常使用）：
`HELIUM_EMPTY_ROOM=1` 渲染"还没进房间"分支，`HELIUM_HOST_ROOM=1` 真的建一个房间渲染房主分支。

### A. 背景壁纸（PNG / JPG / GIF / APNG）

摆在哪：

| 层 | 文件 | 干什么 |
| --- | --- | --- |
| 算法层（Core） | `HeliumTodo.Core/Imaging/ApngParser.cs` | 把 APNG 拆成一帧帧独立的 PNG 字节 |
| 算法层（Core） | `HeliumTodo.Core/Theming/AccentColor.cs` | HSL ↔ RGB 换算、十六进制输出 |
| 算法层（Core） | `HeliumTodo.Core/Theming/WallpaperAccentExtractor.cs` | 从像素里挑一个「主色调」 |
| 解码层（App） | `HeliumTodo.App/Services/WallpaperDecoder.cs` | SkiaSharp 解码 + 缩放 + 模糊 + 合成动图帧 |
| 播放层（App） | `HeliumTodo.App/Services/WallpaperService.cs` | 逐帧定时切换、暴露当前帧给界面 |
| 显示层 | `MainWindow.axaml` | 三层：壁纸图 → 半透明蒙版 → `ShellView` |

几个值得说的决定：

1. **APNG 要自己拆帧，GIF 不用。** SkiaSharp 的 `SKCodec` 能解 GIF 动图，
   但 **完全不认 APNG** —— 它会把真 APNG 当成普通静态 PNG，
   `FrameCount` 直接返回 0。所以 `ApngParser` 干的事是：
   从 `fdAT` 块里把数据掏出来，配上帧自己的 `IHDR`（宽高要用 `fcTL` 里的），
   重新拼成一张**合法的普通 PNG**，交给 Skia 当静态图解。
   **不写 PNG 解码器，只做重新拼装** —— 这是这个方案最省事的地方。
2. **取色不是"算平均色"。** 平均色永远是灰扑扑的。做法是按色相分 24 个桶，
   每个像素按「鲜艳度² × 亮度」加权投票，取票数最高的那个桶的加权平均。
   然后夹进安全区（明度 0.30~0.46 浅色主题 / 0.58~0.74 深色主题），
   保证白字压在上面还能看清。
3. **动图内存有预算。** 解码时长边限制在 1920，所有帧总共最多占 192 MB。
   超了就抽帧（每 N 帧取一帧）并把延迟累加，而不是硬塞。
4. **`SKBitmap` → Avalonia `Bitmap` 不走"编码成 PNG 再解回来"**
   （每帧多 50~80 ms）。改成逐行 `Marshal.Copy` 到 `WriteableBitmap`，
   两边都是 Bgra8888 预乘，没有 unsafe。

### B. 自定义调色板

`HeliumTodo.App/Controls/ColorPickerControls.cs` 里两个自己画的控件
（没用 Avalonia 自带的 `ColorView`，为了和项目其他部分的风格一致）：

- `SaturationValueSquare` —— 方形选色区。底是当前色相的纯色，
  上面叠「横向白→透明」和「纵向透明→黑」两层渐变。
- `GradientSlider` —— 通用渐变滑块。色相条和 R/G/B 三条都用它，
  区别只在 `TrackBrush`。

配套 `ColorPaletteViewModel` 做 RGB ↔ HSV ↔ 十六进制三方同步，
用 `_syncing` 闸门防递归。

⚠️ 一个具体的坑：**灰阶时不要写 Hue。**
RGB 三个相等时算出的饱和度是 0，色相没有意义；
如果这时候还把 Hue 写回去，色相条会突然跳到红色端。代码里是
`s <= 0.0001` 就直接不碰 Hue。

强调色有三种来源（`AccentSources`）：跟随主题 / 跟随壁纸 / 自定义。

### C. 局域网联机自习

无中心服务器，星形拓扑：**谁建房谁当转发节点**。

| 文件 | 作用 |
| --- | --- |
| `HeliumTodo.Core/Networking/PeerEndpoint.cs` | 地址解析（IPv4 / IPv6 / 主机名 / 带端口） |
| `HeliumTodo.Core/Networking/PeerMessage.cs` | 线协议：一行一条 JSON + 编解码 |
| `HeliumTodo.Data/LanPeerTransport.cs` | TCP 双栈监听、UDP 房间广播发现 |
| `HeliumTodo.Core/Services/StudyRoomService.cs` | 业务层：房间、成员、消息 |

- **IPv4 + IPv6 双栈**：监听用 `TcpListener(IPAddress.IPv6Any, port)` 并开
  `Server.DualMode = true`，一个 socket 同时收两种；IPv6 不可用时退回 `IPAddress.Any`。
- **手动填 IP**：UDP 广播只在同一网段有效（路由器不转发广播），
  所以除了"发现的房间"列表，必须能手填地址。
- **端口**：默认 47470，可在界面上看到。
- **线协议故意用一行一条 JSON**：出问题直接看日志就能读懂，
  不像二进制协议要抓包分析。


### D. 迁移到 Avalonia 12 + FluentAvaloniaUI 3（上一轮）

从 **Avalonia 11.3.2 + FluentAvaloniaUI 2.4.0** 升到
**Avalonia 12.1.2 + FluentAvaloniaUI 3.1.0**。

#### 为什么目标框架也跟着变成了 net10.0

这是**被逼的**，不是我们想升：FluentAvaloniaUI 3 只发布了 **net10.0** 一个目标框架
（3.0.0 / 3.0.1 / 3.0.2 / 3.1.0 全都如此）。net8.0 的项目根本引用不了它。

Avalonia 12 本身是支持 net8.0 的，卡点完全在 FA 3 这边。

#### 整套版本必须对齐到 12.1.2

`Avalonia.Controls.DataGrid` 和 `Avalonia.Controls.ColorPicker`
**最高只到 12.1.2**，而主包 `Avalonia` 已经到 12.1.4。
所以整套取交集，统一 12.1.2：

```xml
<PackageVersion Include="Avalonia" Version="12.1.2" />
<PackageVersion Include="Avalonia.HarfBuzz" Version="12.1.2" />
<PackageVersion Include="Avalonia.Controls.DataGrid" Version="12.1.2" />
<PackageVersion Include="Avalonia.Controls.ColorPicker" Version="12.1.2" />
<PackageVersion Include="Avalonia.Headless" Version="12.1.2" />
<PackageVersion Include="FluentAvaloniaUI" Version="3.1.0" />
```

> **新东西：`Avalonia.HarfBuzz`**
> Avalonia 12 把文字排版（HarfBuzz）拆成了独立包，不显式引用就编不过。

#### 改了什么（19 个文件）

| 类别 | 改动 |
| --- | --- |
| 版本声明 | `Directory.Packages.props` 整体重写；删掉 `Avalonia.Diagnostics` 的版本项 |
| 项目文件 | 6 个 `.csproj` 的 `net8.0` → `net10.0`；删 Debug 配置下的 `Avalonia.Diagnostics` 引用（Avalonia 12 已移除该包）；App 与 RenderCheck 加 `Avalonia.HarfBuzz` |
| FA 3 类型改名 | 4 个 XAML 里 `fa:` 前缀的控件全部改名（见下），共 90 处 |
| FA 3 事件类型 | `ShellView.axaml.cs` 里的 `NavigationViewSelectionChangedEventArgs` → `FANavigationViewSelectionChangedEventArgs` |
| Avalonia 12 移除 API | `App.axaml.cs` 删掉 `RemoveDuplicateValidationPlugins()` 整个方法（`BindingPlugins` 静态类在 12 里没了，且校验插件默认就不启用，不用替代写法） |
| Avalonia 12 过时 API | 11 处 `TextBox.Watermark` → `PlaceholderText`（`TodoView.axaml` 与 `ScheduleView.axaml` 两个文件的全部改动就是这一项）；`SystemDecorations` → `WindowDecorations`；`frame.Save(path)` → `frame.Save(path, PngBitmapEncoderOptions.Default)` |

**FA 3 的改名规律：252 个导出类型里有 224 个（88%）加了 `FA` 前缀。**

| Avalonia 11（FA 2） | FA 3 |
| --- | --- |
| `NavigationView` | `FANavigationView` |
| `NavigationViewItem` | `FANavigationViewItem` |
| `NavigationViewSelectionChangedEventArgs` | `FANavigationViewSelectionChangedEventArgs` |
| `InfoBar` | `FAInfoBar` |
| `SettingsExpander` | `FASettingsExpander` |
| `SettingsExpanderItem` | `FASettingsExpanderItem` |
| `FontIconSource` | `FAFontIconSource` |
| `FluentAvaloniaTheme` | **没改名**（还用原名、原命名空间） |

> ⚠️ **XAML 改名要覆盖三种形态**，漏了第三种会报一个看不懂的错：
> 1. 开标签 `<fa:NavigationView`
> 2. 闭标签 `</fa:NavigationView>`
> 3. **附加属性形式** `fa:NavigationView.PaneHeader` ← 最容易漏
>
> 漏了第 3 种，报错是 `start tag does not match end tag`，完全指不到真正的原因。

#### 验证结果

- `dotnet build HeliumTodo.slnx` → **0 警告 0 错误**
- 46 个单元测试 → **全通过**（用时 74 ms）
- 渲染自检 → **7 项全成功**（6 个页面 + 外壳）
- 迁移在副本上又跑了一遍：`设置.png` 和 `专注.png` 的 **sha256 与前一次完全一致**，
  说明这个过程是确定性的、可复现的

#### 顺手发现并修掉了一个真 bug

加 `--shell` 渲染整个主窗口之后，第一次看到了**外壳**长什么样 ——
结果发现内容区一片空白。

原因：`ShellViewModel` 的构造函数把 `SelectedItem` 设成了「今日工作流」，
但那只改了 ViewModel 自己的字段，**导航栏控件并不知情**，
它一个都没选中 → `SelectionChanged` 从不触发 → `CurrentPage` 一直是 `null`
→ 右侧内容区空白。

这个 bug **和迁移无关，Avalonia 11 时代就有**（对比备份确认过，
外壳 XAML 除控件改名外完全一致）。它之所以一直没暴露，是因为
六个页面都是单独渲染的，绕过了外壳。

修法（`ShellView.axaml.cs` 的构造函数）：

```csharp
Loaded += (_, _) =>
{
    if (DataContext is not ShellViewModel shell) return;
    if (Nav.SelectedItem is not null) return;
    if (shell.SelectedItem is null) return;

    // 赋值会触发 SelectionChanged，导航逻辑仍然只有一条路
    Nav.SelectedItem = shell.SelectedItem;
};
```

> **教训**：自检覆盖范围决定了能发现什么。
> 「6 个页面都渲染正常」曾经让我以为界面没问题了，
> 但外壳根本不在检查范围内。补齐 `--shell` 之后一次就抓到了。

#### 视觉上的变化（FA 3 本身带来的，不是 bug）

- `FAInfoBar` 的图标从 **`!`** 变成了**蓝色圆圈里一个 `i`**
- `ComboBox`、`ToggleSwitch` 的样式比 FA 2 略朴素一点
- 整体排布稍微紧凑一些

### E. 界面渲染自检工具

`HeliumTodo.RenderCheck` 用 Avalonia 的**无头模式**把每个页面渲染成 PNG。
它的价值：**编译通过 ≠ 界面正确**。这一轮就是靠它一次性发现了 8 个问题。

关键配置（少一个都渲染不出来，踩过）：
```csharp
AppBuilder.Configure<HeliumTodo.App.App>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()          // 必须：要真的画出像素
    .SetupWithoutStarting();
```
另外 `ViewLocator` 已从 `MainWindow` 提升到 **`Application.DataTemplates`**，
这样没有主窗口也能解析页面（顺带让架构更正确）。

### 修复清单

| # | 问题 | 原因 | 修法 |
| --- | --- | --- | --- |
| 1 | 时间轴打开时停在 00:00，看不到今天的安排 | 没做初始滚动 | 打开时滚到当前时刻（往上留 1.5 小时上下文） |
| 2 | 待办列表有 4 条却显示「全清空了！」 | `EmptyHint` 无条件赋值 | 只在 `Items.Count == 0` 时显示 |
| 3 | **24 小时刻度完全不显示** | Canvas 在 ItemsControl 里拿不到尺寸而被裁掉 | 改为在自绘的 `TimelineGrid` 里直接画刻度文字 |
| 4 | 时钟中间大字压住表盘数字 | 表盘只有 272px，塞不下 | 只保留 12 和 6，倒计时放大到 40px |
| 5 | 「重复」下拉框空白 | XAML 里 `ComboBoxItem Tag=` 不参与 `SelectedValue` 绑定 | 改用 `ItemsSource` + `SelectedValueBinding` |
| 6 | 时段显示英文「Morning」 | 枚举没做本地化 | 新增 `TimeBlockTextConverter` |
| 7 | **待办在时间轴上重复出现两次** | 种子数据同时写了 `PlannedStart` 和一条 `LinkedTodoId` 日程 | 种子数据不再多插日程；另在算法里加去重保险（含 2 个新测试） |
| 8 | 「现在」红线的圆点压住刻度文字 | 红线从网格起点开始画 | 红线起点右移，避开刻度区 |
| 9 | **`AppConfig.cs` 开头被插进一个字母，变成 `Susing`** | 脚本批量改文件时的编码误伤 | 修正，并新增源文件完整性校验工具（见下） |

### 新增：源文件完整性校验

`tools/check-file-integrity.ps1` 给所有源文件算 SHA256 指纹。

为什么需要它？第 9 条那个错误是**编译报错**才发现的 —— 如果被改坏的是注释或字符串，
编译器看不出来，那才是真的危险。有了指纹基线，任何意外改动都会立刻暴露。

```powershell
pwsh tools/check-file-integrity.ps1            # 校验
pwsh tools/check-file-integrity.ps1 -Update    # 确认当前状态正确后，更新基线
```

#### 基线变更记录（都是经过确认的有意改动）

> 校验报出「内容变了」时，**别急着 -Update** —— 先确认改动是不是有意的。
> 确认过的改动记在这里，以后翻记录时能分清「有意改的」和「被误伤的」。

| 日期 | 文件 | 改动性质 | 确认人 |
| --- | --- | --- | --- |
| 2026-10-11 | `tools/file-hashes.json` | 工具自身产出，已加入排除名单 | — |
| 2026-10-12 | `HeliumTodo.App/Views/SettingsView.axaml` | 显示文字的小完善（非结构性） | 用户确认保留 |
| 2026-10-12 | `HeliumTodo.App/Views/StudyRoomView.axaml` | 显示文字的小完善（非结构性） | 用户确认保留 |
| 2026-10-12 | `HeliumTodo.App/ViewModels/StudyRoomViewModel.cs` | 显示文字的小完善（非结构性） | 用户确认保留 |
| 2026-10-20 | `Directory.Packages.props` | Avalonia 混版修复：打开 `CentralPackageTransitivePinningEnabled`，锁 DataGrid/ColorPicker 11.3.2 | 有意 |
| 2026-10-24 | 18 个文件 + 新增 `tools/migrate-av12.ps1` | **Avalonia 12 + FA 3 迁移**（详见第三节 A），全部由 `tools/migrate-av12.ps1` 一次性完成 | 有意，用户指令 |

> 2026-10-24 那批改动清单（共 18 个文件 + 1 个新增）：
> `Directory.Packages.props`、`global.json`、
> 6 个 `.csproj`（Core / Data / App / Tests / SelfTest / RenderCheck 各一个）、
> `App.axaml.cs`、`ShellView.axaml{,.cs}`、`RenderCheck/Program.cs`、
> 6 个 XAML 视图（`ShellView` / `SettingsView` / `FocusView` / `StudyRoomView`
> 做 FA 控件改名，`TodoView` / `ScheduleView` 只改 `Watermark`）、`PROGRESS.md`，
> 以及新增的 `tools/migrate-av12.ps1`。

**怎么判断一个改动该不该接受**（这三条是那次实践总结出来的）：

1. **能编译** —— `dotnet build` 0 警告 0 错误
2. **测试全绿** —— 46 个测试通过
3. **功能块齐全** —— 比如设置页 8 个分组、「恢复默认」按钮、自习室各功能按钮都还在

三条都满足，基本可以判定是良性的（多半是文案或格式微调）；
有任何一条不满足，就必须逐行查清楚再决定。

### 另外：解决方案文件换成 .slnx，版本号升到 0.0.2-dev

老 `.sln` 换成了新的 **`.slnx`**（XML 格式），顺手用 `<Folder>` 把 6 个项目分了组
（内核 / 存储 / 界面 / 测试 / 工具），在 Visual Studio 里一眼就能看清层次。

版本号从 `0.0.1-dev` 升到 **`0.0.2-dev`**，现在只需要改**一处**：
根目录 `Directory.Build.props` 里的 `<Version>` 标签，界面会自动跟着变 ——
为此新增了 `HeliumTodo.Core/VersionInfo.cs` 统一处理版本号显示。

> 为什么版本号写 `0.0.2-dev` 而不是 `0.0.2.dev`？
> 因为语义化版本（SemVer）规定预发布标记要用**横线**连接。
> 写成点连接会让 NuGet 和打包工具认不出来，编译就报错。
> 界面上想显示成什么样都行，但程序集版本必须是合法格式。

三个连带影响，都已处理：

- **编译要改用 .NET 10 SDK**（`global.json` 已改）。目标框架仍然是 `net8.0`，产物不变。
  原因：`.slnx` 需要 MSBuild 17.13+，用 .NET 8 SDK 编译会报「无法识别元素 `<Solution>`」。
- `HeliumTodo.RenderCheck` 里「向上找解决方案根目录」的判断同步改了
  （同时兼容 `.slnx` 和 `.sln` 两种后缀）。
- 顺手修掉一个旧问题：设置页之前用 `Assembly.GetName().Version` 取版本号，
  会把 `-dev` 标记悄悄丢掉，所以永远显示成 `0.1.0.0`。现在改用 `VersionInfo`。

---

## 2026-10-25 · 联机修复：两个"一直存在但看不见"的真 bug

这一轮本来只是给联机加 EasyTier 那套优化（心跳 / 延迟 / 地址表），并把主机地址限制成只有房主可见。功能写完后联机的集成测试开始报错，顺藤摸瓜挖出两个老 bug。

### bug 1：自检框架根本没等 async 测试

`HeliumTodo.SelfTest/Program.cs` 的 `InvokeTest` 原本只有 `method.Invoke(instance, null);` —— 返回的 `Task` 被丢掉，`async Task` 测试等于"发射后不管"，await 之后的断言从来没跑过、异步异常也被吞掉。所以之前"全部通过"是假的，联机那几个要真连端口的测试一次都没真正验证过。

修法是拿到返回值后按类型等它：`if (result is Task task) task.GetAwaiter().GetResult(); else if (result is ValueTask valueTask) valueTask.GetAwaiter().GetResult();`

### bug 2：给"已经连上的套接字"再设 DualMode，把连接弄废了

症状极具迷惑性：客户端 `ConnectAsync` 返回 true、`IsConnected` 是 true，但房主那边 `OnlinePeerCount` 一直是 1、收不到任何 presence 或聊天。

`AcceptLoopAsync` 里原本有 `try { client.Client.DualMode = true; } catch (SocketException) { }`。对一个**已经接受（established）**的连接改 DualMode，会让后续 `client.GetStream()` 抛 `InvalidOperationException: The operation is not allowed on non-connected sockets`；而这个异常发生在 `new PeerSession(...)` 里，外层又是 `_ = Task.Run(AcceptLoopAsync)` 这种发射后不管的写法，异常被静默吞掉 → 表现成"连上了但没人理"。

排查过程：① 先怀疑 IPv6 + DualMode 监听方式，写了完全不涉及项目代码的裸 `TcpListener` 实验，IPv6Any / DualMode / SO_REUSEADDR / 带 token 的 `AcceptTcpClientAsync` 四种组合全部正常；② 于是加临时 `[trace]` 打印，发现停在「进 accept 循环」之后、「收到连接」一次没打印；③ 把 accept 换到独立线程跑，异常终于没被吞掉、以 `Unhandled exception` 打出来，看到是 `GetStream()` 抛的。

修法：删掉那段对已接受连接设 DualMode 的代码（连接都建立好了，改它没有意义），并留注释说明。

### 结果

| 项目 | 结果 |
| --- | --- |
| 构建 | 0 警告 0 错误 |
| 单元 / 集成测试 | **104 个 · 通过 104 · 失败 0**（含真实 TCP 回环；当时的总数，现为 116） |
| 界面渲染自检 | 7 项全通过（6 个页面 + 外壳） |
| 指纹基线 | 102 个文件全部一致 |

测试总数从 105 变 104 是删掉了临时诊断测试，正式测试一个没少。

### 教训

- **"测试全绿"要先确认测试真的跑了**：`method.Invoke` 不看返回值、`_ = Task.Run(...)` 不 await，这两种写法都会把异常和断言一起吞掉。
- **异常一旦被吞，症状会和根因毫无关系**：这次症状是"网络连不上"，根因是"给已连接 socket 改了一个属性"。排查时要尽量让异常现形（换独立线程、加 catch-all 打印），而不是盯着症状猜。
## 2026-10-25 · 修掉设置页 23 处 `Content` 重复赋值

VS 的错误列表报了 23 个 `MC30..: The property 'Content' is assigned more than once.`，
全部在 `HeliumTodo.App/Views/SettingsView.axaml`。`dotnet build` 却是 0 错误 ——
只有 VS 的 XAML 分析器会报，所以一直没被命令行构建发现。

**根因**：`Content` 是 Avalonia 的内容属性，写了 `Content="标题"` 又塞子元素（ComboBox、
ToggleSwitch、Slider…），等于给同一个属性赋了两次值。

**修法**：标题留在 `Content`，控件放进 `<fa:FASettingsExpanderItem.Footer>`。
`FASettingsExpanderItem` 有 `Footer`/`FooterTemplate` 属性，Footer 渲染成卡片右侧的操作区 ——
正好是 Fluent SettingsCard 该有的样子。

顺带修好了之前一直没解决的「`FASettingsExpanderItem` 放了子元素后标题被顶掉」：
标题不再被子元素覆盖，22 个设置项现在都是「左边标题+说明、右边控件」。

**验证**：0 警告 0 错误、104 测试全绿、7 项渲染通过、指纹 102 文件一致。
截图 `output/render/设置.png`。

另外把「技术栈」那一行的描述从 `.NET 8 · Avalonia 11 · FluentAvaloniaUI（Fluent 2）`
更新为 `.NET 10 · Avalonia 12 · FluentAvaloniaUI 3（Fluent 2 风格）`。

## 2026-10-25 · 修掉调色板的两个问题（绑定方向 + 全局生效）

**症状**（用户实测）：拖调色盘时，方形选色和 RGB 滑块互不同步；调色盘显示的颜色也不会应用到全局。

**根因只有一个**：`GradientSlider` 和 `SaturationValueSquare` 是自定义控件，
`AvaloniaProperty.Register` **默认是一向绑定（OneWay）** ——
只从 ViewModel 流到控件，控件自己的值变了不会回写。
所以拖动只改了控件内部状态，`ColorPaletteViewModel` 一无所知，
后面「写配置 → 重新应用强调色」整条链自然也不会触发。

（`NumericUpDown.Value` 和 `TextBox.Text` 在 Avalonia 里本来默认就是 TwoWay，
所以只有这两种输入能生效 —— 这也是为什么问题看起来像"部分能用"。）

**修法**：给这三类属性显式声明 `defaultBindingMode: BindingMode.TwoWay`：

```csharp
AvaloniaProperty.Register<GradientSlider, double>(
    nameof(Value), defaultBindingMode: BindingMode.TwoWay);

AvaloniaProperty.Register<SaturationValueSquare, double>(
    nameof(Hue), defaultBindingMode: BindingMode.TwoWay);
```

同时需要 `using Avalonia.Data;`（`BindingMode` 在这个命名空间里）。

**验证**：临时在 `HeliumTodo.RenderCheck` 里加了个探针，模拟"用户在设置页选自定义色并写入 `#C42B1C`"，
输出证明运行时链路是通的：

```
[验证] 写入 #C42B1C 后，EffectiveAccentHex = #C42B1C
[验证] Application 资源 SystemAccentColor = #ffc42b1c
[验证] 资源 AccentSoftFill = #fff7e1df
```

外壳截图里品牌方块、导航选中条、`普通` 优先级标签、时间轴「日程」图例全部变红 —— 全局生效。
探针已删除（`grep HELIUM_ACCENT_TEST` 为 0）。

**教训**：自定义控件的可写属性，只要打算双向绑定，就必须在注册时声明 `BindingMode.TwoWay`；
忘了不会报错，只会"拖了没反应"。

**结果**：0 警告 0 错误、104 测试全绿、7 项渲染通过。

## 2026-10-27 · 统一数据导出：一个 .zip 装下所有东西

### 为什么要做这件事

需求里写的是「数据本地存储」，但**换一台电脑**或者**想留个存档**的时候，
原来只有一个办法：自己去 `%APPDATA%\Helium Todo\` 里把 `helium.db` 拷出来。

这条路有三个问题：

1. **只拷 .db 会丢配置。** 主题、壁纸、强调色都在 `config.json` 里。
2. **只拷 .db 可能拷到半截。** WAL 模式下最近的改动还在 `-wal` 文件里，
   只拷主文件就是几天前的数据。
3. **壁纸是另一个文件。** 拷完数据库，壁纸还得单独找。

所以这次把「导出」统一成一个 `.zip`：**数据库 + 配置 + 壁纸，一个文件全带走**，
导入的时候也只需要挑这一个文件。

### 包里到底有什么

```
HeliumTodo-backup-20261027-183012.zip
├── manifest.json          清单：版本、导出时间、每个文件的 SHA256、各项条数
├── data/helium.db         数据库（导出瞬间的快照，不含 WAL 尾巴）
├── data/config.json       配置
└── assets/wallpaper.<ext> 壁纸（没设壁纸时这个条目不存在）
```

`manifest.json` 是关键 —— 它让导入方**先判断能不能导**，再决定要不要动手：

```json
{
  "Format": "helium-todo-backup",
  "FormatVersion": 1,
  "AppVersion": "0.1.0-dev",
  "ExportedAt": "2026-10-27T18:30:12.4821330+08:00",
  "Database": { "Entry": "data/helium.db", "SizeBytes": 84736, "Sha256": "3f2a..." },
  "Counts": { "Schedules": 8, "Todos": 5, "FocusSessions": 3, "StudyRooms": 1 }
}
```

有了 `Format` 和 `FormatVersion`，将来的版本可以安全地拒绝「比我新」的备份，
而不是读一半发现字段对不上。有了 `Sha256`，文件被截断 / 被别的程序改过都能当场发现。

### 三个「明明能跑但不对」的坑

**坑 1：WAL 模式下直接拷 .db 拿到的是旧数据。**

SQLite 开了 WAL，写入是先落 `-wal`，主库文件要等 checkpoint 才更新。
原来的备份逻辑就是 `File.Copy(db, backup)` —— 拷到的是上一次 checkpoint 的内容。

修法用 `VACUUM INTO`：

```sql
VACUUM INTO 'C:\Users\...\Temp\snapshot.db';
```

它会让 SQLite 自己走一遍「读当前完整状态 → 写一个新库」，
写出来的就是一个干净的、不含 WAL 的完整快照。
万一 `VACUUM INTO` 失败（比如磁盘满），退回到 `PRAGMA wal_checkpoint(TRUNCATE)` + 拷主库文件。

**坑 2：导入时「先关程序再换文件」在 Windows 上不可靠。**

换数据库文件最直觉的做法是：关连接 → 覆盖文件 → 重开。
但 Avalonia 程序里随时可能有别的线程开着连接，删文件会报「文件正被占用」。

改用的办法是 **ATTACH + SQL 搬运**，全程不动文件句柄：

```sql
ATTACH DATABASE 'C:\...\Temp\helium-import-xxx.db' AS src;
BEGIN;
INSERT INTO main."Todos" ("Id", "Title", ...) SELECT "Id", "Title", ... FROM src."Todos";
COMMIT;
DETACH DATABASE src;
```

顺带解决了另一个问题：**旧版本的备份也能导入**。
搬数据的列名不是写死的，而是取两边 `PRAGMA table_info` 的**交集** ——
备份里少一列（旧版本没这个字段）就只搬有的那些。

**坑 3：把自己的备份合并回自己，撞唯一约束。**

跑 `--backup-check` 时报：

```
导入失败：SQLite Error 19: 'UNIQUE constraint failed: StudyRooms.Code'
```

原因是全库只有一个唯一索引 —— `StudyRooms.Code`（`HeliumDbContext.cs:102`）。
把自己的备份「合并」回自己时，同一个房间码必然撞。

这暴露了一个设计问题：**合并不等于「无脑追加」**，得先想清楚业务语义：
一个房间码就代表同一个房间，所以第二次遇到同一个码时，
**跳过整行（连同它的成员和消息一起跳过）**，而不是改个新码塞进去。

修法是在表规格里多一个可选的唯一列：

```csharp
new TableSpec("StudyRooms", UniqueColumn: "Code"),
```

搬每一行之前先查一下：`SELECT 1 FROM main."StudyRooms" WHERE "Code" = $v LIMIT 1`，
命中就记进 `skippedIds`，后续子表看到父记录被跳过就整行不搬。

### 替换 vs 合并

导入时问一次，两种语义完全不同：

| | 替换 | 合并 |
| --- | --- | --- |
| 现有数据 | 清空，只剩备份里的 | 保留，备份里的追加进来 |
| 主键 | 原样保留 | 重新编号（避免撞号） |
| 子表外键 | 原样 | 按新旧映射表换算；父记录没搬过来就置 `NULL` |
| 唯一约束 | 不会撞（表已清空） | 撞了就跳过该行 |
| 安全网 | **导入前自动备份当前数据**（`pre-import-*.zip`） | 不需要 |

「合并」里那句「父记录没搬过来就置 `NULL`」值得多说一句：
一条专注记录指向的待办如果没被搬过来，**不能随便指一个新的**，
那等于把「专注在这个任务上」变成了「专注在别的任务上」。置 `NULL` 才是诚实的。

### 启动时的自动备份也换了

原来启动时 `DatabaseInitializer.Backup()` 拷一份 `.db` 到 `backups/`，保留 7 份。
现在走同一个 `DataBackupService.BackupAutomatically()`，产出 `.zip`，同样保留 7 份。

**这就是「统一」的含义**：手动导出、启动自动备份、导入前安全备份，
三个场景产出的都是同一种文件，同一套代码读。

### 界面

「设置 → 数据与文件」多了三行：

| 行 | 内容 |
| --- | --- |
| 导出 / 导入 | 两个按钮，左边导出、右边导入 |
| 备份文件夹 | 显示 `backups/` 的真实路径，一键打开 |
| 最近一次备份操作 | 「还没有导出或导入过」/「已导出 xxx.zip，82.8 KB · 8 条日程…」 |

导入不是点了就执行 —— 先弹一个确认框，把**这份包里有什么**摆出来：

```
导入这份备份？
dialog-preview.zip
2026-10-27 18:18 导出 · 8 条日程 · 5 条待办 · 3 条专注记录 · 1 个自习室
「合并」保留现在的数据，把备份里的追加进来；「替换」会先自动备份当前数据，再清空。
                                            [取消]  [合并导入]  [替换导入]
```

「替换导入」故意做成红底红字 —— 它是这个界面上唯一一个会**删数据**的按钮。

### 验证

| 项目 | 结果 |
| --- | --- |
| 构建 | **0 警告 0 错误** |
| 单元测试 | **116 个全部通过**（新增 12 个：备份 11 + 房间码去重 1） |
| 无头渲染 | 6 页 + 外壳 = **7 项全部成功** |
| 导入导出全链路 | `--backup-check` **15 项断言全通过** |
| 弹窗渲染 | `HELIUM_BACKUP_DIALOG=1` 拍下确认框，位置 / 文案 / 三个按钮都正确 |

新增的 12 个测试里，几个值得点名的：

- **数据库被篡改后校验不通过** —— 改一个字节，`TryVerifyChecksums` 必须报错（证明 SHA256 真的在算）
- **格式版本更高的备份会被拒绝** —— 证明将来加字段不会把旧版本搞崩
- **替换导入前会自动备份当前数据** —— 证明那个「安全网」不是写着好看的
- **导入不会覆盖本机的数据库路径** —— 别人备份里的 `Z:\别人的电脑\helium.db` 不能变成你的路径
- **合并导入会追加数据并重新编号** —— 且所有非空外键都指向真实存在的记录

### 一个必须坦白的插曲：自检工具动了真实数据

`--backup-check` 这个自检是**走界面那条路**跑的（`new SettingsViewModel(services)`），
而 `services` 是 `App.Services` —— 指向**用户真实的 `%AppData%`**。

于是每跑一次自检，就真的执行了一次「合并导入」。等我发现时：

| | 本该是 | 实际变成 |
| --- | ---: | ---: |
| 日程 | 8 | **32** |
| 待办 | 5 | **20** |
| 专注记录 | 3 | **12** |

（每次合并都会翻倍，跑了几轮就变成了 4 倍。）

**已经修好，也把数据还回去了**：

1. 给 `App.Initialize()` 加了一个口子：读环境变量 `HELIUM_CONFIG_DIR`，
   有值就用它当配置目录（平时不存在，走正常逻辑）。
2. `RenderCheck` 在 `Main` 的**最前面**检查 `--backup-check`，先把配置目录
   切到 `%TEMP%\helium-backup-sandbox\{guid}\`。现在自检里的导出、合并、
   替换全在沙盒里发生，跟真实数据彻底隔离。
3. 用 `backups\helium.20261026_191743.db`（最后一次旧格式备份，
   内容正好是 8/5/3）把用户的库恢复回来，`PRAGMA integrity_check` 返回 `ok`。
   膨胀后的库没有丢，留在 `backups\manual-before-restore-*.db`；
   7 个被污染的 `.zip` 备份也挪到了 `%TEMP%\helium-junk-backups\`。

> **教训（比这个 bug 本身更重要）**：
> 「自检」和「测试」必须默认跑在**隔离的**数据上。
> 只要它复用了真实的应用容器，它就不是自检，而是「拿用户的数据做实验」。
> 判断标准很简单：**这个工具会不会写文件？会 → 就该有自己的沙盒目录。**

### 顺手修掉的另一个竞态

恢复完数据重跑 116 个测试，`房主能看到成员的自报地址客户端能收到地址表` 挂了一次：

```
SingleException: Assert.Single() Failure: The collection was empty
   at NetworkingTests.房主能看到成员的自报地址客户端能收到地址表() : line 310
```

不是代码回归，是**测试本身有竞态**：`ConnectAsync` 返回只代表 TCP 握手完成，
`hello` / `join` 是随后异步发出去的，房主的读循环那一刻可能还没跑到。
以前一直绿，只是因为机器快。

修法不是加 `Task.Delay`（猜时长，要么偶发失败要么拖慢），而是加了个轮询等待：

```csharp
var hostPeers = await WaitForAsync(() => host.Peers, peers => peers.Count > 0);
```

之后连跑 3 遍都是 116/116。

### 教训

1. **「能跑」不等于「对」。** 第一版导出在 WAL 模式下就是错的 —— 它不报错，
   只是悄悄少几天数据。这种错只能靠「导出→导入→比对条数」这种端到端测试抓。
2. **合并不是无脑追加。** 写合并逻辑前必须先问「主键 / 唯一键 / 外键分别该怎么办」，
   否则第一次拿自己的备份试就会撞约束。
3. **安全网要真的存在。** 「替换会删掉所有数据」这句话旁边的按钮，
   必须真的在删之前备份 —— 而且这个行为要有测试盯着。
4. **会写文件的自检，必须自带沙盒。** 见上面那个插曲。
5. **异步的网络断言要用「等条件成立」，不要用「等固定时长」。**

---

## 2026-10-27 · 「打开文件夹」按钮点了没反应：一次靠证据定位的排查

**现象**：设置页里那几处展示路径的地方（配置文件、数据库、备份文件夹、插件文件夹）
提供的「打开文件夹」按钮，点了之后既不开资源管理器窗口、也不报错 —— 四个全一样。

### 排查过程（四步，每步都在缩小范围）

| 步骤 | 做法 | 结果 |
| --- | --- | --- |
| 1 | 独立控制台里跑同样的 `Process.Start` | **成功**开了窗口；但返回的 `Process` 是 `null` |
| 2 | 在无头环境里直接 `command.Execute(null)` | 不抛异常、`NoticeText` 也没变 → 命令本身没问题 |
| 3 | 在**真实外壳**里注入鼠标事件，把按钮的 `Command` 临时换成计数器 | 4 个按钮都能命中，点一下命令执行 1 次 → 点击链路没问题 |
| 4 | 剩下两种可能 | ① `ShellExecute` 的 `open` 动作被静默吞掉 ② 提示条在滚动内容里，用户看不到 |

第 3 步踩了个坑：一开始用 `GetVisualsAt(按钮中心)` 判断"有没有被盖住"，
因为按钮在长页面靠下，`BringIntoView()` 只把它停在视口**下边缘**（四个按钮的 y 全是 884.5），
量到的坐标落在视口外，命中测试返回的是滚动容器的边框 —— 结论完全相反。
必须先滚到视口中间（`CenterInViewport`）再量。

### 修法

**① `OpenInExplorer` 改成直接起 `explorer.exe`**

原来写的是：

```csharp
Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
```

这行的意思是"让 Windows 用『文件夹』的默认处理程序打开它"。而默认处理程序是**可以被接管**的
（装过第三方文件管理器就会变），接管项一旦失效，`ShellExecute` 会**静默什么都不做**：
不抛异常、返回 `null`。于是"点了没反应"——和用户看到的一模一样。

现在显式调用：

```csharp
var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
Process.Start(new ProcessStartInfo
{
    FileName = explorer,
    Arguments = $"\"{path}\"",   // 路径有中文/空格必须带引号
    UseShellExecute = false,
    CreateNoWindow = true,
});
```

> ⚠️ **后来发现（2026-10-28）**：上面这个"直接 CreateProcess 起 explorer.exe"的写法，
> 在部分机器上会让 explorer.exe 直接起不来，弹 **0xc0000142「应用程序无法正常启动」**。
> 已改成"先交给外壳启动、再退回直接启动"的三级降级，详见下一节。

**② 提示条从 `ScrollViewer` 里挪到根 `Grid`**

它以前是滚动内容的第一行。用户在页面最下面（「数据与文件」）点按钮，报错却出现在页面最上面，
不滚回去就完全看不到 —— 于是"报错了但用户看不见"和"根本没报错"是同一个现象。
现在它浮在窗口顶部（`Margin="24,12,24,0"`、`VerticalAlignment="Top"`），滚到哪儿都能看见。

### 新增自检 `--folder-check`

```
dotnet HeliumTodo.RenderCheck\bin\Debug\net10.0\HeliumTodo.RenderCheck.dll --folder-check
```

5 项检查：

1. 4 个按钮都能被鼠标命中
2. 点一下确实执行了 `Command`（临时换成计数器，不真开窗口）
3. 端到端：执行一次「打开配置文件夹」，4 秒内等到标题含配置目录名的资源管理器窗口
   （2026-10-29 起：窗口标题读不到时只提示、不算失败，原因见下一节的「环境限制」）
4. 滚到页面最底部后，提示条仍在视口里（y = 45）
5. 上述全部通过

> ⚠️ 这个自检**会真的打开一个资源管理器窗口**（第 3 项要验证的就是它）。
>
> 2026-10-29 补充：自动化进程里用 `Process.MainWindowTitle` 读不到资源管理器的窗口标题
> （窗口确实开出来了，手工 `Start-Process explorer.exe <目录>` 能看到
> `Data - 文件资源管理器`，但同一个程序里读出来是空字符串）。所以第 3 项改成
> 「命令没报错就算过，窗口看不到只提示」，避免用一个读不到的信号去判失败。

### 教训

1. **`Process.Start` + `UseShellExecute` 是不可观测的**：成功时返回 `null`，失败时也可能不抛异常。
   凡是"交给系统默认处理程序"的操作，都改成就近的直接调用（这里是 `explorer.exe`）。
2. **提示条必须放在滚动区域外面**，否则"错误看不着"和"没有错误"在用户眼里没有区别。
3. **无头环境里用 `GetVisualsAt` 判断遮挡之前，必须先把控件滚到视口中间**，
   否则量到的坐标在视口外，得到的结论会与事实相反。
4. 排查顺序应该是「底层调用 → 命令 → 点击链路 → 呈现」，
   每一步都用能观测的证据，而不是猜。这次四步下来，真正的问题在第一步和第四步，都不是"点击"。

### 验证

- `dotnet build HeliumTodo.slnx -c Debug` → 0 警告 0 错误
- `HeliumTodo.SelfTest` → **116 个测试全绿**
- `--shell` → 7 项渲染通过；`--backup-check` → 15 项通过；`--folder-check` → 5 项通过

---

## 2026-10-28 · 三件事：XAML 救回来、explorer 启动失败、删掉大部分说明文字

### 一、`SettingsView.axaml` 被改坏之后怎么救

现象：编译报

```
Avalonia error AVLN1001: File doesn't contain valid XAML:
System.Xml.XmlException: Name cannot begin with the '<' character, hexadecimal value 0x3C.
Line 273, position 44.
```

原因不是"少了个括号"这种小错：文件里被粘进了一段 **XAML 语法示例**
（`<!-- 错误 -->` / `<!-- 正确 -->` / `属性1="值"` / `<TextBlock Text="A &amp; B" />="True">` 之类），
正好把「自控模块」那个 `FASettingsExpander` 的开标签顶掉了 —— 于是标签只剩一半，
后面所有内容都成了非法 XML。

### 这次是怎么不靠备份救回来的

| 步骤 | 做法 | 得到什么 |
| --- | --- | --- |
| 1 | 读报错行号（273）看上下文 | 知道是「自控模块」的开标签没了 |
| 2 | `grep -n "自控模块" UI_TEXT_MAP.md` | 找回原来的 `Description` 原文（`专注期间帮你盯着别乱切窗口`） |
| 3 | 把第 273–288 行换回 `<fa:FASettingsExpander Header="自控模块" ... IsExpanded="True">` | 结构接回去 |
| 4 | `xml.etree.ElementTree.fromstring` 做一次纯 XML 解析 | 比编译更快地确认"结构合法" |
| 5 | 顺手把粘进来的 5 处 CRLF 改回 LF | 这个文件全项目统一是 LF |

> **教训**：就算没有备份，也还有三条线索 —— **报错行号**、**文档里的原文索引**（`UI_TEXT_MAP.md`）、
> **XML 解析器**。前两个能还原内容，第三个能立刻判断修好没修好，不用等整轮编译。

### 二、`0xc0000142`：explorer.exe 为什么起不来

上一轮把「打开文件夹」改成了自己 `CreateProcess` 起 `C:\Windows\explorer.exe`
（`UseShellExecute = false`、`CreateNoWindow = true`）。点击后系统弹出：

```
explorer.exe - 应用程序无法正常启动 (0xc0000142)
```

`0xc0000142` = **STATUS_DLL_INIT_FAILED**（进程初始化阶段就失败）。
explorer.exe 是外壳的一部分，它期望**由外壳把它拉起来**；绕过外壳直接 CreateProcess，
在某些机器上会撞上这个错误。

四种启动方式在本机实测（临时控制台，目标目录 `%APPDATA%\Helium Todo`）：

| 方式 | 写法 | 结果 |
| --- | --- | --- |
| A | `C:\Windows\explorer.exe` + `UseShellExecute=false` + `CreateNoWindow=true` | 本机能开（但用户机器上报 0xc0000142） |
| B | `explorer.exe`（相对名）+ `UseShellExecute=true` | 能开 |
| C | 目录路径 + `UseShellExecute=true` + `Verb="open"` | 能开，`Process.Start` 返回 `null` |
| D | 目录路径 + `UseShellExecute=true` | 能开，返回 `null` |

> `explorer.exe` 的退出码**正常就是 1**，不能拿"退出码非 0"当失败判据。

所以现在的实现是**三级降级**（`SettingsViewModel.OpenInExplorer`）：

1. **交给外壳启动 `explorer.exe`** —— 不走"文件的默认处理程序"（它可能被第三方文件管理器接管而静默失效），
   但也不绕过外壳初始化（避开 0xc0000142）
2. **把文件夹路径交给系统**（`Verb="open"`）—— 老写法，兜底
3. **自己 CreateProcess 起 `C:\Windows\explorer.exe`** —— 最下策

三级都失败时，把**每一级的原因**一起显示在提示条里，而不是只说"失败了"：

```
打开文件夹失败（外壳启动资源管理器：xxx；系统默认方式：xxx；直接启动资源管理器：xxx）路径：...
```

顺带确认过：`Avalonia 12.1.2` 的 `ILauncher` 仍然只有 `LaunchUriAsync` 和 `LaunchFileAsync`
两个方法，**没有打开目录的 API**，所以这件事只能自己起进程。

### 三、删掉大部分说明文字

设置页里原本每个设置项下面都挂一句解释（`Description`），很多是"废话级"的自我说明
（比如强调色来源写着「你手动调出来的颜色」）。按用户要求删掉大部分：

- `SettingsView.axaml` 共 28 处 `Description=` → **删掉 15 处**，剩 13 处
- 剩下的 13 处 = **8 处分组标题副标题**（`外观` / `专注` / `自控模块` / `每日工作流` / `自习室` / `数据与文件` / `插件` / `关于`）
  + **5 处动态绑定**（壁纸状态、配置路径、数据库路径、备份文件夹、备份状态、插件目录 —— 这些是数据不是说明）
- 强调色旁边那行 `AccentExplanation`（就是用户点名的「你手动调出来的颜色」）整块删掉：
  XAML 里的 `TextBlock` 和 ViewModel 里的 `[ObservableProperty] private string _accentExplanation` 一起删

> 分组标题的副标题保留了 —— 它们是**章节导航**（「数据与文件」下面有什么），不是某一项的功能解释。
> 如果还要更干净，把 `FASettingsExpander` 的 `Description` 也删掉即可（8 处）。

### 验证

- `dotnet build HeliumTodo.slnx -c Debug` → 0 警告 0 错误
- `HeliumTodo.SelfTest` → **116 个测试全绿**
- `--shell` → 7 项渲染通过
- `--backup-check` → 15 项通过
- `--folder-check` → 5 项通过（含"资源管理器窗口真的出现了"，走的就是新的三级降级第 1 级）

### 教训

1. **不要绕过外壳去启动属于外壳的程序**（explorer.exe）。Windows 的 `0xc0000142` 就是这类错误的通用面罩。
2. **"成功了但返回 null" 和 "失败了但没异常" 是同一类问题**：凡是不可观测的调用，
   都要么换成可观测的写法，要么加一条端到端验证（`--folder-check` 里那句"等窗口出现"）。
3. **界面上的解释文字不是越多越好**：能自解释的控件（开关、下拉）不需要再写一句"这是开关"。
4. 改 XAML 前 `git`/备份仍是最省事的；没有备份时，**报错行号 + 文档原文索引 + XML 解析器**能救回来。

---

## 2026-10-28 · 版本号升到 0.1.0-dev，并打一份能脱离 IDE 跑的 amd64 构建

### 一、版本号 0.0.2-dev → 0.1.0-dev

语义化版本里，「加功能」走次版本号：这一轮加了壁纸、调色板、联机、统一 .zip 备份四块，
0.0.x 已经不够描述了，所以升到 **0.1.0-dev**。

改的地方（`VersionInfo` 会从程序集读，所以只需改一处源头 + 几处说明文字）：

| 文件 | 改了什么 |
| --- | --- |
| `Directory.Build.props` | `<Version>0.1.0-dev</Version>`、`AssemblyVersion`/`FileVersion` → `0.1.0.0` |
| `HeliumTodo.Core/VersionInfo.cs` | 注释里的示例版本号 |
| `SettingsViewModel.cs` / `ShellViewModel.cs` / `BackupManifest.cs` | 注释里的示例版本号 |
| `VersionInfoTests.cs` | 断言提示文字里的示例 |
| `ARCHITECTURE.md` / `PROGRESS.md` / `PROJECT_OVERVIEW.md` / `WINDOWS_DEPENDENCIES.md` | 文档中的当前版本 |

验证：exe 的 `FileVersion = 0.1.0.0`、`ProductVersion = 0.1.0-dev`；
界面「设置 → 关于 → 版本」显示 `0.1.0-dev`，左栏底部显示 `v0.1.0-dev`。

> 为什么改 `Directory.Build.props` 而不是每个 csproj：全项目共用一份属性，
> 版本号只存在一个地方，`VersionInfo` 再去读程序集的 `AssemblyInformationalVersion`。

### 二、自包含的 amd64 构建

```powershell
dotnet publish HeliumTodo.App/HeliumTodo.App.csproj -c Release -r win-x64 ^
  --self-contained true -o dist/HeliumTodo-0.1.0-win-x64
```

| 项目 | 结果 |
| --- | --- |
| 产物目录 | `dist/HeliumTodo-0.1.0-win-x64/` |
| 打包成 zip | `dist/HeliumTodo-0.1.0-win-x64.zip`（**51 MB**，250 个条目） |
| 文件数 | 247（去掉 `*.pdb` 后） |
| 体积 | 219 MB → **119 MB**（`libSkiaSharp.pdb` 81 MB、`libHarfBuzzSharp.pdb` 20 MB 是纯调试符号） |
| 依赖 | **自包含**，目标机不需要装 .NET 运行时 |
| 冒烟测试 | 双击/命令行启动成功（进程存活，工作集 206 MB），已确认能开窗 |

**为什么要删 `*.pdb`**：自包含发布默认把原生库的调试符号也拷进来，
SkiaSharp 和 HarfBuzzSharp 两个 pdb 就占 101 MB —— 发布包里没人用它们。

> `createdump.exe` 是 .NET 自带的崩溃转储工具，跟着自包含发布一起来，留着即可。

### 三、几点提醒

1. **数据位置不变**：自包含版和 IDE 里的调试版共用 `%APPDATA%\Helium Todo\helium.db`。
   要"干净的第一次启动"，把那个目录整个改名即可（程序会自动重新建库并灌示例数据）。
2. **启动时会自动备份**：每启动一次就在 `backups/` 里多一份 `.zip`，保留最近 7 份。
3. **`dist/` 不进指纹基线**：它是产物目录，`tools/check-file-integrity.ps1` 只扫源码。

### 验证

- `dotnet build HeliumTodo.slnx -c Debug` → 0 警告 0 错误
- `HeliumTodo.SelfTest` → **116 个测试全绿**
- `--shell` → 7 项渲染通过；`--backup-check` → 15 项；`--folder-check` → 5 项
- 指纹基线 → 113 个文件更新并复查一致

## 2026-10-29 · 联机默认关闭、自习室铺满、待办增删后自动刷新

### 一、联机改成"默认关闭"

`AppConfig.EnableNetworkStudyRoom` 这个字段其实一直存在（`bool`，默认 `false`），
但**全项目没有一处读它** —— 注释还写着「暂缓，现在打开也不会有联机效果」。
于是"默认关闭"只是名义上的：`LanPeerTransport` 一旦 `BecomeHost` 就真的监听端口、广播发现包。

这次把它接上线，判定收拢成三个属性（`Core/Services/StudyRoomService.cs`）：

```csharp
public bool SupportsNetworking => _transport.SupportsNetworking;   // 传输层本身行不行
public bool LanEnabled        => _config?.Current.EnableNetworkStudyRoom ?? true;
public bool IsOnlineModeAvailable => SupportsNetworking && LanEnabled;
```

`CreateRoom` 里的 `BecomeHost`、`JoinRoom`、`JoinRemoteRoomAsync` 三个入口本来就都看
`IsOnlineModeAvailable`，所以改这一处就把全部入口管住了。

- 构造函数改成 `StudyRoomService(IPeerTransport transport, ConfigService? config = null)`：
  App 里靠 DI 自动注入 `ConfigService`；单元测试里 `new StudyRoomService(transport)` 仍然成立
  （没注入配置就按"打开"算，测试不会被用户配置牵着走）。
- 设置页「自习室」组原来那行「多人联机自习 · 暂缓」换成真开关
  （`ToggleSwitch`，`OnContent="已开启"` / `OffContent="已关闭"`）。
- 自习室页分三种状态给文案：本地模式（传输层不行）/ 联机默认关闭（提示去设置里打开）/ 就绪。
  「输入联机地址」那块只在 `CanGoOnline` 时出现；关着的时候显示一条灰底说明。
- 关掉开关会顺手 `LeaveRoomAsync()`，不会留一个还在监听的房间。

### 二、自习室页铺满整个宽度

原来的布局是"主体 `Grid` 两列，左列塞着三种卡片（创建/加入、房间头、成员），右列只有聊天卡"。
没进房间时右列整块是空的，于是卡片只占左半边 —— 就是用户截图里的样子。

改法：把"不在房间"和"在房间"拆成两条互斥的行（`IsVisible="{Binding !IsInRoom}"` / `{Binding IsInRoom}`）：

- 不在房间 → 一张卡铺满整行，卡内左右分栏：「我想当房主」/「我想当房客」，下面接说明块和提示。
- 在房间 → 两列（左：房间头 / 房间内专注 / 成员列表；右：房间消息）。

### 三、待办增删后不刷新 —— `HasFlag` 用错（真 bug）

用户报"添加/完成任务时状态不自动刷新"。根因只有一行：

```csharp
// 改之前
if (type.HasFlag(AppDataType.Timeline))
```

`HasFlag` 问的是「**所有**这些位都置上了吗」，而 `AppDataType.Timeline` 是
`Schedules | Todos | Focus` 的**组合值**。单改一条待办时 `type == Todos`，
`2.HasFlag(7)` 是 `false` → 今日工作流判成"和我无关"，统计、时间轴、今日重点全停在旧数据。

另外三个页面（`TodoViewModel` / `ScheduleViewModel` / `FocusViewModel`）恰好传的是单个位或两位组合，
`HasFlag` 蒙对了，所以只有今日工作流坏。

修法：加一个把语义写清楚的扩展方法，四处统一换个名字：

```csharp
public static class AppDataTypeExtensions
{
    /// <summary>changed 里有没有涉及 watch 中的任意一类。</summary>
    public static bool Includes(this AppDataType changed, AppDataType watch)
        => (changed & watch) != 0;
}
```

### 四、顺手补的两个自检

`HeliumTodo.RenderCheck` 新增 `--refresh-check`（6 项，全部通过）：

| 断言 | 实测 |
| --- | --- |
| 新增待办后今日工作流的统计跟着变 | 未完成 4 → 5 |
| 刚加的待办能在今日工作流里看到 | ✓ |
| 勾掉待办后未完成数立刻减少 | 5 → 4 |
| 勾掉后「今日完成」加一 | 1 → 2 |
| 打开「局域网联机」后自习室页立刻可以联机 | ✓ |
| 关掉后又回到本机模式 | ✓ |

要点：页面 ViewModel 没注册进容器（外壳里是懒加载 `new` 出来的），自检里同样直接 `new`；
等刷新时不能 `Thread.Sleep`，必须一边 `Dispatcher.UIThread.RunJobs()` 一边轮询 ——
页面刷新是事件回调里 `await` 出来的，不转消息循环就一直排在队里。

### 五、这台机器上 %TEMP% 写不了（环境限制）

自检沙盒原先建在 `%TEMP%\helium-backup-sandbox\{guid}`，这次连 `Directory.CreateDirectory`
都报 `UnauthorizedAccessException`（有意思的是同一个 shell 里 python `mkdir` 却成功）。
改成建在程序自己目录下（`AppContext.BaseDirectory/_check-sandbox/{guid}`）就好了。
跑测试时如果也撞上，用 `TMP`/`TEMP` 指到项目内的目录即可，不用改代码。

### 验证

- `dotnet build HeliumTodo.slnx -c Debug` → 0 警告 0 错误
- `HeliumTodo.SelfTest` → **116 个测试全绿**
- `--shell` → 7 项渲染通过
- `--backup-check` → 15 项通过
- `--refresh-check` → 6 项通过
- `--folder-check` → 按钮与提示条全通过；"资源管理器窗口是否出现"改为提示（自动化进程里读不到窗口标题）
- 指纹基线 → 113 个文件更新并复查一致

---

## 2026-10-29 · 自习室「退出后重进还在房间里」

用户报的问题很具体：**点了离开，退出页面再进来，人还在房间里。**

### 一、根因：离开只清了内存，没清数据库

```csharp
// 改之前：离开只是把内存里的字段清空
await _roomService.LeaveRoomAsync();
CurrentRoom = null;

// 而重新进页面时，从数据库里把"最近的房间"又捞回来了
var rooms = await _roomRepo.GetAllAsync();
var latest = rooms.FirstOrDefault();     // ← 于是人又回到了房间里
```

`StudyRoom` 模型里**根本没有**"已离开"这个字段，仓储也没有任何过滤条件 ——
数据层分不清"这房间还开着"和"早就离开了"。

### 二、修法：给房间加一个 `ClosedAt`

| 文件 | 改动 |
| --- | --- |
| `HeliumTodo.Core/Models/StudyRoom.cs` | 新增 `DateTime? ClosedAt`；`null` = 还在房间里 |
| `HeliumTodo.Data/HeliumDbContext.cs` | 映射新列；`EnsureDatabaseReady()` 里补上 `EnsureColumns()` |
| `HeliumTodo.Data/StudyRoomRepository.cs` | 新增 `GetOpenAsync()`：`Where(r => r.ClosedAt == null)` |
| `HeliumTodo.App/ViewModels/StudyRoomViewModel.cs` | 进页面读 `GetOpenAsync()`；离开时写时间戳；重新进同一个房间时清回 `null` |

三个容易漏掉的点：

1. **老库不会自动多一列。** 项目不用 EF Migrations（见 `HeliumDbContext` 的注释），
   `EnsureCreated()` 只在"库/表还不存在"时建表，已有库一个字段都不改。
   所以自己写了 `EnsureColumns()`：查 `PRAGMA table_info`，缺哪列就 `ALTER TABLE` 补哪列。
2. **补列之后还要处理存量数据。** 升级前没有"离开"的概念，库里那些房间都是
   "点了离开却没记住"的残留。补完列顺手 `UPDATE ... SET ClosedAt = datetime('now','localtime')`，
   否则用户升级后第一次打开自习室页，照样会看到自己在房间里。
3. **示例数据也不能替用户做决定。** `SeedData` 里那个"期末冲刺自习室"原来没有 `ClosedAt`，
   于是全新安装第一次打开自习室页也会"已经在房间里"。现在它被标成已离开过。

### 三、顺手修掉：备份失败不该挡住建表

排查过程中发现一个更隐蔽的问题：`InitializeDatabase()` 里备份排在建表**之前**，
而且共用一个 `try`。一旦备份抛异常（磁盘满、临时目录不让写……），
后面的 `EnsureDatabaseReady()` 直接被跳过 —— 程序带着一个"结构没升级"的老库跑起来，
症状就是"功能怎么突然不对了"。现在备份单独 `try` 起来，失败只记一行日志。

> 这个坑是拿**真机数据库的副本**跑出来的：副本放进临时目录，程序启动后
> 报 `SQLite Error 14: 'unable to open database file'`。换成项目内目录就好了 ——
> 这台机器上 .NET 进程写 `%TEMP%` 会被拒（前面 `--backup-check` 也踩过同一条）。

### 四、新增自检 `--room-check`

复现路径和用户点的一模一样：

| # | 断言 | 结果 |
| --- | --- | --- |
| 1 | 刚进页面时不在任何房间 | ✓ |
| 2 | 建完房间就在房间里了（打印房间码） | ✓ |
| 3 | 点离开之后立刻回到「不在房间」 | ✓ |
| 4 | **重新进入页面不会自动回到房间里** | ✓ |
| 5 | 主动输入房间码还能回到那个房间 | ✓ |

第 4 项就是用户报的那个 bug；第 5 项保证"修完还能回去"，不是把功能改死。

### 五、验证

| 项目 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **119 个测试全绿**（原 116 + 3 个自习室仓储测试） |
| `HeliumTodo.RenderCheck --shell` | 7 项全通过 |
| `--refresh-check` | 6 项全通过 |
| `--backup-check` | 15 项全通过 |
| `--room-check` | 5 项全通过 |
| 真机数据库副本迁移 | 日程 8 / 待办 1 / 专注 3 / 自习室 9，条数不变；`ClosedAt` 列补上；9 个旧房间全部标记已离开；`PRAGMA integrity_check` = ok |

### 六、教训

1. **「界面上的离开」和「数据库里的离开」是两件事。** 写完 `CurrentRoom = null`
   以为就完事了，其实只是把内存清了；下次进页面从库里一捞，人又回去了。
2. **没有 Migrations 就得自己补列，而且补完要考虑存量数据。** 只加字段不刷数据，
   用户升级后看到的还是老样子 —— 这种"改了一半"的修法比不改更让人困惑。
3. **示例数据不要替用户做决定。** 一个没标 `ClosedAt` 的示例房间，
   能让"全新安装"的用户一打开就发现自己"在房间里"。
4. **共用 `try` 的初始化步骤会互相拖累。** 备份失败不该让建表跳过；
   不关键的步骤要单独包起来。

## 2026-11-01 · 后台进程留不住、打开没有动效、窗口代码散成四份

三条反馈，三条根因都不在"感觉"上：

| 反馈 | 实际原因 | 一句话修法 |
| --- | --- | --- |
| 后台进程留不住 | `TrayService.Start()` 抢跑，几乎总返回 `false`；`App` 拿到 `false` 就 `return`，连"拦关闭"都没装 | 等托盘线程真就绪再返回 + 托盘加不上也要拦关闭 |
| 启动时窗口直接出现 | 自绘窗口没有系统动画；自己也没写 | 自己写 180 毫秒「淡入 + 96%→100%」 |
| `MainWindow.axaml` 和几个 `MainWindow.*.cs` 割裂 | 四份 partial 各管一摊（配置 / Win32 / 动画 / 拖拽） | 抽成 `Windowing/` 四件套，`MainWindow.axaml.cs` 只做装配 |

### 一、后台进程留不住的真正原因：`Start()` 抢跑

```csharp
// 改之前（HeliumTodo.App/Services/TrayService.cs）
public bool Start()
{
    _thread.Start();
    return IsRunning;   // ← IsRunning 要等托盘线程里 NIM_ADD 成功之后才置 true
}
```

`Thread.Start()` 只是"请求线程跑起来"，主线程紧接着读 `IsRunning`，
这时托盘线程连窗口都还没建好 —— 于是几乎必然返回 `false`。
而调用方（`App.axaml.cs`）是这么写的：

```csharp
if (!_tray.Start()) { _tray = null; return; }   // 连 window.Closing 的拦截都没装
```

结果就是：**点 × 真的退出程序**。明明写好了"关窗口只是隐藏"，却被一个竞态整段跳过。

修法两条：

1. `Start()` 里加 `ManualResetEventSlim _ready`，托盘线程无论是"图标加上了"还是
   "窗口建不出来"都 `_ready.Set()`，`Start()` 改成 `_ready.Wait(5 秒); return IsRunning;`；
2. `NIM_ADD` 加 5 次重试（每次失败等 `300 × 次数` 毫秒）。开机自启时资源管理器还没就绪，
   这条路径返回的正是错误码 5（`ERROR_ACCESS_DENIED`）—— 不是没权限，是"通知区域还不存在"。

`App.axaml.cs` 同时改成：**托盘起不来也照样拦关闭**，只是文案改成
「没有托盘图标，但关闭仍会留在后台（再双击一次程序即可唤回窗口）」。

### 二、托盘之外的第二条退路：单实例守卫

只靠托盘的话，托盘一旦失效用户就没法把窗口叫回来了。所以新增
`HeliumTodo.App/Services/SingleInstanceGuard.cs`：

* `Mutex "Local\\HeliumTodo.lock"` —— 保证只有一份在跑（也是防止两个进程同时写同一个库）；
* `EventWaitHandle "Local\\HeliumTodo.show"` —— 后来者 `RequestShow()` 之后自己退出，
  先来者在后台线程收到信号就 `RestoreMainWindow()`。

于是"后台常驻"有了两条互相独立的路径：托盘菜单，或者再双击一次程序。
`tools/probe-lifecycle.ps1` 把整条链路测了一遍：

```
  ✓ 进程在跑：PID 30868
  ✓ 主窗口标题：Helium Todo
  ✓ 关窗口之后进程仍在：PID 30868
  ✓ 窗口已隐藏：True
  ✓ 第二个实例已自动退出
  ✓ 窗口被叫回来了：Helium Todo
  ✓ 生命周期检查通过
```

> 探针里设了 `HELIUM_FAKE_TRAY=1`：这台开发机的无头会话没有可交互通知区域，
> `Shell_NotifyIconW` 固定返回错误码 5。**真机上请自己确认托盘图标与右键菜单**。

### 三、打开动效：180 毫秒，自己切帧

窗口是自绘的（`WindowDecorations="None"`），系统那套"淡入 + 由小到大"自然不会替我们播。
`Windowing/WindowAnimator.PlayIntro()` 做两件事：

* `Opacity: 0 → 1` —— 淡入；
* 内容根元素的 `ScaleTransform: 0.96 → 1` —— 轻微放大（Avalonia 的 `Window` 不能直接设缩放，
  挂到内容变换上视觉等价）。

180ms / 16 帧 / `EaseOut(t) = 1-(1-t)³`。

**这里踩了一个"动画被整段吞掉"的坑**：第一版在 `PlayIntro()` 入口就
`Stopwatch.StartNew()`，而窗口 `Show` 之后第一次布局+渲染可能就要一百多毫秒 ——
等第一帧定时器真的到来，`progress` 已经等于 1，动画一步跳到终点。
探针实测只采到 **4 帧、1 个不透明度值**（等于没播）。

改成"计时从第一帧到来那一刻起算"：

```csharp
Stopwatch? clock = null;
timer.Tick += (_, _) =>
{
    clock ??= Stopwatch.StartNew();   // 第一帧才起表
    var progress = WindowAnimationMath.ProgressOf(clock.Elapsed);
    ...
};
```

改后 `tools/probe-intro.ps1`（真实窗口，`HELIUM_INTRO_TRACE=1` 逐帧打日志）实测：

```
帧数：15，不同不透明度：11，最后一帧不透明度：1
✔ 开场动画逐帧播出，并收尾到完全体
```

顺带把最大化/还原动画也复验了一遍：469 次采样、**20 个不同尺寸**
（`1316x800 → 1408x857 → … → 1720x1032 → 1576x951 → … → 1329x813`）。

### 四、`Settle()`：一个"必须存在"的收尾口

动画靠定时器推帧，最怕最后一帧不来。真发生了会怎样？无头自检正好给了现场证据：
`DispatcherTimer` 在无头平台一次都不触发 → 窗口永远停在 `opacity=0.00`、`scale=0.960`。
后果不只是"看着半透明"：

```
[诊断] 抓手 bounds=1274, 0, 6, 820 center=1251.5, 410
```

右边缘抓手在 x∈[1274,1280]，可 `TranslatePoint` 算出来是 **1251.5** —— 因为整个内容被
`ScaleTransform(0.96, 0.96)`（原点在中心 x=640）缩了一下：`640+(1277-640)*0.96=1251.5`。
于是"按在右边缘上"实际按在了 1251 处，拖拽自然没反应。

生产环境里同样会中招：动画播到一半，用户把窗口藏到托盘 / 关掉。

修法：`WindowAnimator.Settle()` —— 停表、`opacity=1`、缩放归 1、补做被打断的状态切换
（`_pendingFinish`，比如"该切成 Maximized 了"）；`MainWindow.FinishAnimationsImmediately()`
对外暴露；`App.axaml.cs` 在 `window.Hide()` **之前**调一次。
自检里也把它变成一条真断言：「收尾能强制回到完全体 — 不透明度 1.00，缩放 1.000」，
之后再做拖拽测试（`1280 → 1160`，稳定通过）。

### 五、窗口代码封装：四份 partial → `Windowing/` 四件套

| 文件 | 职责 | 行数 |
| --- | --- | --- |
| `Windowing/WindowChrome.cs` | 样式位 + `WM_NCCALCSIZE`（无边框仍可拖边、最大化不盖任务栏） | ~135 |
| `Windowing/WindowAnimator.cs` | 最大化 / 还原 / 打开动效 + `Settle()` | ~300 |
| `Windowing/WindowAnimationMath.cs` | 缓动、进度、矩形插值（纯函数） | ~70 |
| `Windowing/WindowResizeGrips.cs` | 8 个抓手 + 指针捕获 + DPI 换算 | ~120 |

`Views/MainWindow.axaml.cs` 从 190 行降到约 110 行，只剩装配：

```csharp
InitializeComponent();
WindowChrome.Attach(this);
_animator = new WindowAnimator(this) { Trace = App.LogBackground };
_grips = new WindowResizeGrips(this);
_grips.Attach();
Opened += OnOpened;
Closing += OnClosing;
```

删除 `Views/MainWindow.Win32.cs`、`Views/MainWindow.Animation.cs`。
**为什么这么分**：`WindowChrome` 是"一次性设置"，`WindowAnimator` 是"有状态的播放器"，
`WindowAnimationMath` 是"能被断言的算术"，`WindowResizeGrips` 是"只在 Normal 状态才有意义的手势"
—— 四个东西的失效方式完全不同，混在同一份 partial 里改一个必然碰到另一个。
把算术单独拆出来的直接好处是：无头环境里 `EaseOut` / `ProgressOf` / `Interpolate` 全都能断言，
不用为了测缓动去开一个真窗口。

### 六、验证

| 项目 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **141 / 141**（1936 ms） |
| `RenderCheck --shell` | 7 项全通过 |
| `--shell-check` / `--refresh-check` / `--room-check` / `--backup-check` | 全部 exit=0 |
| `--folder-check` / `--timeline-check` / `--reminder-check` | 全部 exit=0 |
| **`--window-check`（本轮新增，11 项）** | 全通过（其中 2 项在无头里 `[跳过]`，由真实窗口探针兜住） |
| `probe-intro.ps1` | 15 帧 / 11 个不透明度 / 收尾 1.00 |
| `probe-maximize-anim.ps1` | 20 个中间尺寸（去程 + 回程） |
| `probe-lifecycle.ps1` | 6 项全 ✓（含"再双击一次唤回窗口"） |

### 七、教训

1. **`Thread.Start()` 之后立刻读结果，等于读了一个还没写完的变量。** 凡是要"等子线程就绪"的
   布尔属性，都得配 `ManualResetEventSlim` 之类的握手，而不是靠 `Thread.Sleep` 撞运气。
2. **兜底路径必须和主路径一样可靠。** 当初的写法是"托盘起不来就什么都别做"，结果一个竞态
   直接把"关闭 = 隐藏"整条功能删掉了。正确做法是：托盘失败只降级"怎么唤回"，不降级"要不要留住"。
3. **动画的计时起点决定它是否存在。** 在"事件入口"起表，等于把首帧延迟算进动画时长；
   第一次渲染慢一点，180ms 的动画就会被吞成 0 帧。计时永远从"第一帧真的到了"开始。
4. **任何"逐帧推进"的东西都必须有一个"立刻到终点"的出口。** 定时器会被丢、窗口会被藏、进程会退出；
   没有 `Settle()`，界面就会永久停在中间态 —— 而且这种中间态是"看起来只是小了一点"，
   很难被人第一时间发现，却能实实在在让点击坐标算错。

---

## 四、项目结构

```
HeliumTodo.slnx                   解决方案（XML 新格式，不再是老 .sln）
├─ global.json                    锁定 .NET 10 SDK（编译用；目标框架仍是 net8.0）
├─ NuGet.config                   包源（含本地缓存兜底）
├─ Directory.Build.props          全局编译设置、版本号 0.3.1-dev
├─ Directory.Packages.props       集中管理 NuGet 包版本
│
├─ HeliumTodo.Core/               领域内核（不依赖任何界面框架）
│   ├─ Models/                    日程、待办、专注、自习室
│   ├─ Services/
│   │   ├─ DayTimelineBuilder.cs  ★ 冲突检测 + 错位分列 + 去重
│   │   ├─ RecurrenceExpander.cs  重复规则（daily / weekly:1,3,5）
│   │   ├─ FocusTimerService.cs   专注计时器
│   │   └─ StudyRoomService.cs    自习室
│   ├─ Theming/                   ★ 扩展点 1：主题注册表
│   ├─ Plugins/                   ★ 扩展点 2：插件接口 + P2P 接口
│   └─ Configuration/             可分离的 JSON 配置（含便携模式兜底）
│
├─ HeliumTodo.Data/               EF Core + SQLite
│   ├─ HeliumDbContext.cs         6 张表
│   ├─ *Repository.cs             四个仓储
│   ├─ SeedData.cs                示例数据（故意造了两处冲突）
│   └─ LocalOnlyPeerTransport.cs  联机的占位实现
│
├─ HeliumTodo.App/                Avalonia + FluentAvaloniaUI
│   ├─ App.axaml(.cs)             ★ 依赖注入组装入口 + ViewLocator 注册
│   ├─ Controls/
│   │   ├─ TimelineGrid.cs        自绘 24 小时网格 + 整点刻度 + 「现在」红线
│   │   ├─ ClockControls.cs       钟表盘 + 圆环进度条
│   │   └─ WidthObserverPanel.cs  把界面宽度报给 ViewModel
│   ├─ Converters/                绑定转换器（优先级、时段、布尔）
│   ├─ Styles/
│   │   ├─ DesignTokens.axaml     ★ Fluent 2 设计令牌
│   │   └─ Controls.axaml         通用样式
│   ├─ ViewModels/                6 个页面 VM + AppState + TimelineEntryViewModel
│   ├─ Views/                     6 个页面 + 主窗口 + 外壳
│   └─ Services/
│       ├─ ThemeService.cs        Core 主题 → Avalonia 明暗/强调色
│       └─ PluginHost.cs          插件加载器
│
├─ HeliumTodo.Tests/              46 个 xunit 测试
├─ HeliumTodo.SelfTest/           自检运行器（反射调测试，不依赖 VSTest）
├─ HeliumTodo.RenderCheck/        无头渲染自检
├─ output/render/                 渲染出来的 6 张页面截图
└─ tools/capture-screenshot.ps1   真窗口截图脚本（本机环境会卡，留着备用）
```

---

## 五、关键设计决定（改代码前先看这里）

### 1. 三大核心功能是怎么「互相联系」的

需求里最关键的一条：

```
待办 ──「安排时间」──▶ 设置 PlannedStart ──▶ 自动出现在时间轴上
待办 ──「开始专注」──▶ 专注结束 ──▶ 时长累加到该待办的 FocusedDuration
日程 ──占用时段──▶ 时间轴变忙 ──▶ 可能和待办截止时间/预订专注撞车 → 冲突提醒
```

统一由 `AppState.DataChanged` 事件广播：任何页面改了数据，
其它页面收到通知自动刷新。**不要**让页面之间直接互相引用。

### 2. 错位展示（用户特别要求的）

算法在 `DayTimelineBuilder.AssignLanes`：

1. 用**并查集**把所有互相重叠的条目分到同一组
2. 组内按开始时间排序，用**贪心**分配列号（哪列空闲塞哪列）
3. 列数 = 组内**最大并发数**（不是组内条目数，这样最省横向空间）

界面侧在 `TimelineEntryViewModel.Resize` 换算成像素：
```
X    = 第几列 × (可用宽度 / 总列数)
宽度 = 可用宽度 / 总列数
Y    = 开始时刻 / 24h × 时间轴高度
高度 = 持续时长 / 24h × 时间轴高度
```

**这块测试写得最全**（`DayTimelineBuilderTests` 共 24 个），改算法前后都要跑。

实测效果：19:00 的「晚自习」和 19:30 的「预订专注」自动左右并排，
21:00 的「复习词汇」又回到左列复用空位 —— 完全符合预期。

### 3. 两个扩展点（需求第 8 条）

| 扩展点 | 接口 | 怎么用 |
| --- | --- | --- |
| 主题 | `IHeliumTheme` + `ThemeRegistry.Register()` | 插件启动时注册，设置页自动出现 |
| 插件 | `IHeliumPlugin` + `PluginHost` | dll 丢进 `%AppData%\Helium Todo\plugins`，重启加载 |
| 联机 | `IPeerTransport` | 换掉 `LocalOnlyPeerTransport` 即可 |

`HeliumTodo.Core` **故意不引用 Avalonia** —— 所以主题里用 `ThemeKind` 枚举和
`#RRGGBB` 字符串，由界面层的 `ThemeService` 翻译成 Avalonia 的对象。

### 4. 配置文件与数据放哪

默认 `%AppData%\Helium Todo\`（`config.json` + `helium.db` + `backups\` + `plugins\`）。

**写不进去时会自动退到「程序旁边的 Data 文件夹」**（便携模式），不会因为没权限就起不来。
这是特意做的健壮性设计。

---

## 六、踩过的坑（重要，避免重复踩）

### 环境类

| 坑 | 现象 | 解决 |
| --- | --- | --- |
| 编译节点复用 | 报 **0 错误但构建失败**，没有任何错误信息 | 加 `-m:1 -nodeReuse:false` |
| NuGet 还原 | 沙箱内 TLS 握手被拒（`SEC_E_NO_CREDENTIALS`） | 在沙箱外做还原；`NuGet.config` 加了本地缓存源 |
| SDK 首次运行标记 | 报 `x.y.z.*.sentinel` 写入被拒 | 预先在 `~/.dotnet` 建 3 个空标记文件（`.toolpath.sentinel` / `.dotnetFirstUseSentinel` / `.aspNetCertificateSentinel`） |
| **.slnx 需要新 MSBuild** | 用 .NET 8 SDK 编译 .slnx 报「无法识别元素 `<Solution>`」 | 编译改用 .NET 10 SDK（`global.json`），目标框架保持 net8.0 不变 |
| `dotnet test` | 测试宿主附加父进程被拒（Win32 错误 5） | 写了 `HeliumTodo.SelfTest` 用反射直接调 |
| 后台进程 | 执行环境回收进程树 | 启动+截图要在**同一条命令**里完成 |
| **误判 Visual Studio 未安装** | 只查了 `C:\Program Files\Microsoft Visual Studio` 就下结论，实际 VS 2026 装在 **E 盘** | **别猜路径**：用官方 `vswhere.exe` 查询（详见下方「本机开发环境实况」） |

### 本机开发环境实况（2026-10-12 用 vswhere 核实）

> 这一节是**排查工具装没装**的正确姿势记录。
> 之前判断「Visual Studio 未安装」是错的 —— 它装在 E 盘，
> 而我只查了 C 盘，且目录结构猜错了（新版是 `Microsoft Visual Studio\18\Community`）。

| 工具 | 版本 / 位置 |
| --- | --- |
| **Visual Studio Community 2026** | 18.10.3 · `E:\Program Files\Microsoft Visual Studio\18\Community` |
| **MSVC 工具集** | 14.51.36231（`cl.exe` 已实测可编译 C++20） |
| **Windows SDK** | 10.0.26100 · `E:\Windows Kits\10\` |
| VS 自带 CMake / Ninja | 4.3.1-msvc1 / 1.13.2 |
| w64devkit（GCC） | GCC 16.2.0 · CMake 4.4.3 · Ninja 1.13.2 |
| .NET SDK | 10.0.401（编译用）+ 8.0.425 |
| Qt / vcpkg / Conan / clang++ | 未安装 |

**正确的检测命令**（以后要用这个，别猜路径）：

```powershell
# 找出所有版本、所有盘符的 VS 安装
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -all -products * -format json

# 确认 C++ 桌面开发工作负载是否装了
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -products * -requires Microsoft.VisualStudio.Workload.NativeDesktop -property installationPath
```

**双 C++ 工具链都已实测跑通**（真的编译并运行了 64 位 C++20 程序）：
MSVC 14.51（`_MSC_VER=1951`）与 GCC 16.2 均正常。
C++ 方向的详细规划见 `Helium todo cpp\PORTING_TO_CPP.md`（已随 C++ 项目搬走）。


### 框架类（Avalonia / FluentAvalonia）

| 坑 | 现象 | 解决 |
| --- | --- | --- |
| FluentAvalonia 版本 | 模板装的是 Avalonia **12**，FluentAvalonia 只支持 **11** | 手动锁 11.3.2 + FluentAvaloniaUI 2.4.0 |
| **Avalonia 程序集混版** ⚠️ | 输出目录里 `Avalonia.Controls.DataGrid.dll` 是 **11.2.5**，其余全是 11.3.2。**IDE 语法分析跨版本解析会失败，表现成莫名其妙的标红（编译却又能过）** | 见下 |


### ⚠️ 重点坑：Avalonia 程序集混版（会让 IDE 误报语法错误）

这个坑值得单独写一节，因为**它不影响编译，只影响 IDE** ——
`dotnet build` 一直是 0 警告 0 错误，所以很难往这个方向想。

**症状**：Rider 里一堆莫名其妙的「无法解析符号」标红，但编译完全正常。

**根因**：FluentAvaloniaUI 2.4.0 声明依赖 Avalonia **11.2.5**，
而我们把 Avalonia 主包设成了 **11.3.2**。NuGet 对间接依赖的处理是
「只有被更高版本顶掉时才升级，否则保留原样」，于是：

```
Avalonia.Base.dll                11.3.2.0     一致
Avalonia.Controls.dll            11.3.2.0     一致
Avalonia.Controls.DataGrid.dll   11.2.5.0     ← 只有它混版
```

`DataGrid` 和 `ColorPicker` **我们一行代码都没直接用**，
纯粹是 FluentAvaloniaUI 拖进来的传递依赖。

**排查过程**（记下来，因为下次遇到同样的症状能少走弯路）：

1. 先排除了源生成器 —— 用 `EmitCompilerGeneratedFiles` 把
   CommunityToolkit.Mvvm 的产物落盘看，139 处 `[ObservableProperty]`
   全部正常生成，Avalonia 的 XAML 名字生成器也在跑
2. 排除了 C# 版本不匹配 —— Rider 2026.2 + .NET SDK 8/10 都在，
   C# 12 语法（`record`、集合表达式、`[GeneratedRegex]`）都没问题
3. 排除了 XAML 命名空间 —— 默认命名空间和所有 `using:` 前缀都完整
4. **最后去看输出目录里各程序集的真实版本**，一眼就看出 DataGrid 是 11.2.5

**修复**（两处，缺一不可）：

```xml
<!-- Directory.Packages.props -->
<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>

<PackageVersion Include="Avalonia.Controls.DataGrid" Version="11.3.2" />
<PackageVersion Include="Avalonia.Controls.ColorPicker" Version="11.3.2" />
```

⚠️ 第一条最容易漏：`CentralPackageTransitivePinningEnabled` 本来是 **false**，
意思是「中央版本只管**直接**引用的包」——
光加上 `PackageVersion` 而不打开这个开关，**传递依赖根本不受约束**，
DataGrid 照样停在 11.2.5。（我第一版就是这么改的，改完发现没生效。）

**验证**：输出目录里 24 个 `Avalonia*.dll` 全部变成 11.3.2.0。

**以后怎么防**：升级 Avalonia 时用
`dotnet list HeliumTodo.App package` 加 `--include-transitive` 开关
（注意：MSBuild 的 XML 注释里**不能写连续两个减号**，
我因为这个把 `Directory.Packages.props` 写坏过一次，
MSBuild 报出一堆看不懂的 NU1010），检查所有 `Avalonia.*` 版本号是否一致。

### ⚠️ 本轮新踩的坑

**坑 A：`Border.card` 样式的 Padding 把 Double 塞进 Thickness**

`Styles/Controls.axaml` 里原来写着
`<Setter Property="Padding" Value="{DynamicResource SpaceL}" />`，
而 `SpaceL` 是 `sys:Double`。样式里的 Double **不会**自动转 Thickness，
等到应用时才炸：

```
InvalidCastException: Unable to cast object of type 'System.Double'
  to type 'Avalonia.Thickness'.
   at Avalonia.PropertyStore.ValueStore.ReevaluateEffectiveValues
   at StyledElement.OnAttachedToLogicalTreeCore
```

**为什么潜伏了这么久**：以前每一处 `Border Classes="card"` 都自己又写了
`Padding="14,12"`，本地值优先级更高，坏掉的样式值根本轮不到转换。
新加的那张卡片没覆盖 Padding，才露出来。已改成 `Value="16,16,16,16"`。

**坑 B：无头渲染里 `DispatcherTimer` 根本不会触发**

给动图写的自检一开始是「等 260ms 看当前帧有没有换」，结果报失败。
写了个探针才确认：无头模式下 `Dispatcher.UIThread.RunJobs()` 能跑，
但定时器一次都不触发（400ms 里 0 次）。
**这是测试环境的限制，不是产品缺陷。**

于是把推帧逻辑抽成 `WallpaperService.AdvanceFrame()`，
自检改成手动推帧并验证「能循环回第一帧」；
真机另开临时探针验证过 8 秒 65 次 tick（GIF 每帧 90ms，量级吻合）。

**坑 C：改配置文件时键名大小写错了，导致误判成"功能坏了"**

`config.json` 里是 **PascalCase** 键（`ThemeId` / `AccentSource` / `CustomAccentHex`…）。
我先用 camelCase 键写测试配置，配置根本没被读进去，
主题一直是默认蓝 —— 于是得出了"强调色切换从来没生效过"的错误结论，
还差点去改不该改的地方。用正确键名重测，一切正常。

**教训：改外部配置文件之前，先把真实的文件打印出来看一眼。**

### 另一个顺手修的：源文件编码

`.cs` / `.axaml` 一共 69 个文件**全都没有 BOM**。
C# 编译器默认按 UTF-8 读，所以编译没问题；
但 Rider 的 `encodings.xml` 里写的是
`addBOMForNewFiles="with BOM under Windows"` —— 偏好和现状不一致。
源码里中文注释很多，统一加 BOM 更稳妥（不影响编译，纯粹让 IDE 少出岔子）。
| 设置卡片类名 | `SettingsCard` **不存在** | 是 `SettingsExpanderItem`，且内容属性是 **`Content`** 不是 `Header` |
| `Application.Styles` 解析顺序 | 样式里用 `StaticResource` 取 `Application.Resources` 里的东西会崩 | 样式里一律用 **`DynamicResource`** |
| `TryFindResource` | 在 `Application` 上找不到 | 加 `using Avalonia.Controls;`（扩展方法在 `ResourceNodeExtensions`） |
| `NavigationViewItem` 图标 | `Icon` 属性用不了 | 用 `IconSource` + `FontIconSource` |
| `ComboBox` 可编辑 | 没有 `IsEditable` 属性 | 用「TextBox + 常用值按钮」代替 |
| `ComboBoxItem Tag=` | 不参与 `SelectedValue` 绑定，下拉框显示空白 | 改用 `ItemsSource` + `SelectedValueBinding` |
| **Canvas 在 ItemsControl 里** | 拿不到尺寸 → 高度算成 0 → 内容全被裁掉、一个都不显示 | 要么在 Canvas 上显式再写一次 `Height`，要么**改用自绘控件**（推荐，见 `TimelineGrid`） |
| `AssemblyLoadContext` | 想重写 `LoadFromAssemblyName` 报错 | 应该重写 **`Load`** |
| 无头截图 | `CaptureRenderedFrame` 抛 `NotSupportedException` | 必须 `.UseSkia()` + `UseHeadlessDrawing = false` |
| `App` 命名冲突 | `HeliumTodo.App` 既是命名空间又是类名 | 写全名 `HeliumTodo.App.App` |

---

## 七、需求对照（自查用）

| # | 需求 | 状态 |
| --- | --- | --- |
| 1 | C# + .NET 8 | ⚠️ **已改成 `net10.0`** —— FluentAvaloniaUI 3 只发布了 `net10.0`，被它逼的 |
| 2 | 前端 Avalonia | ✅ Avalonia **12.1.2** |
| 3 | Fluent 2 / FluentAvalonia | ✅ FluentAvaloniaUI **3.1.0** + 自建设计令牌 |
| 4 | 日程 / 专注 / 待办三大核心且互相联系 | ✅ 已打通，见第五节第 1 条 |
| 5 | 自习室（专注+自控）、预订专注时长 | ✅ 完成；**多人联机自习已实现**（局域网 P2P，无中心服务器，IPv4+IPv6） |
| 6 | SQLite 本地存储 | ✅ EF Core + SQLite（另有 `backups\` 自动备份） |
| 7 | 每日工作流（冲突）、优先级、24h 时间轴、时钟专注 | ✅ 全部实现并渲染验证 |
| 8 | 易扩展：主题、插件接入点 | ✅ 三个扩展点已预留并可用 |
| — | 背景壁纸（PNG/JPG/GIF/APNG） | ✅ 静态 + 动图，主色调自动取色 |
| — | 主题色跟随壁纸 | ✅ 三种来源：跟随主题 / 跟随壁纸 / 自定义 |
| — | 自定义调色板 | ✅ 方形选色 + RGB 滑块 + 十六进制 + 预设色 |
| 9 | 可分离的配置文件 | ✅ JSON，可单独拷走，且带便携模式兜底 |
| — | 本科生能看懂的代码 | ✅ 全部中文注释，解释「为什么」而不只是「是什么」 |

---

## 八、下一步可以做什么（按建议顺序）

1. **两台真机 / 双进程跑一次联机**：现在的自动化测试是同一进程内开两个
   127.0.0.1 端口互连（覆盖了握手、房间码校验、聊天转发），
   但"两台电脑真的连得上"还没实测过。Windows 防火墙大概率会拦第一个入站连接，
   需要放行 47470 端口。
2. **壁纸取色的"跟随主题亮度"**：目前 `MakeUsable` 按主题浅/深选安全区，
   但没有考虑壁纸本身很亮/很暗的情况。
3. **人工点一遍交互**（最该做的）：渲染能验证「画得对不对」，
   但点按钮、拖滚动条这些要真人操作。建议手动跑一次完整流程：
   新建日程 → 把待办安排到时间轴 → 开始专注 → 看时长有没有累加到那条待办上。
4. **深色主题渲染验证**：设置页切换主题的功能没在渲染里测过。
   可以在 `RenderCheck` 里加一个「用深色主题再渲染一遍」的开关。
5. **多渲染几个日期**：现在的截图都是「今天」。
   可以改 `RenderCheck` 渲染前几天/后几天，确认滚动和空状态都正常。
6. **补 UI 自动化测试**（可选）：Avalonia.Headless 支持模拟输入，
   可以给关键流程写几个自动化用例。
7. **房间发现列表做进界面**：传输层已经会收 UDP 广播并有
   `DiscoveredRooms` / `DiscoveryChanged`，但目前界面上只做了"手填 IP"。
   加一个"附近的自习室"列表会更好用。

### 2026-10-25 · 外观与调色板合并 + 主题色全局统一

**合并**：原来「自定义调色板」是独立一张卡片，和「外观」并列。
现在并进「外观」展开面板，成为其中一项（`Views/SettingsView.axaml`），
少一个顶级分区，改颜色不用在两处之间来回看。

> 试过用 `FASettingsExpander.Footer` 承载调色板，实测 Footer 会被排到
> 标题右侧当成「操作区」，不是想要的位置，改回普通 item。
> 另：FA 3 的 `FASettingsExpanderItem` 一旦放了子元素，`Content` 里写的标题
> 会被子元素顶掉（其余几项也有这个毛病，暂未动），所以这一项把标题写进了内容里。

**主题色全局统一**：改颜色以后有几处仍是蓝的，根因是这些地方把 `#0F6CBD` 写死了：

| 原来的写死值 | 现在 |
| --- | --- |
| `PriorityNormalFill/Border/Text` | 跟随强调色（派生浅底 / 强调色 / 派生深字） |
| `ScheduleEntryBrush` / `ScheduleEntryBorderBrush` | 同上（日程块、总览页紧凑条） |
| `ConflictInfoBrush` / `ConflictInfoFillBrush` | 同上（冲突提示里的「信息」级别） |

做法：`ThemeService.ApplyDerivedAccentTokens` 按强调色算出
`AccentSoftFill`（混 86% 背景色）与 `AccentSoftText`（压暗/提亮），
写进应用资源；`DesignTokens.axaml` 里那几个画刷改成
`Color="{DynamicResource ...}"`，这样换色时是**画刷内部颜色变**，
`StaticResource` 引用它的地方（如 `DashboardView` 的紧凑条）也能跟着动。

验证：临时把配置改成自定义红 `#C42B1C` 渲染一轮，
保存按钮、单选圆点、「普通」优先级标签、日程块边框全部变红；改回默认后恢复蓝色。

**再调整（按用户意见）**：强调色相关的三项（来源 / 当前色 / 调色板）合成一项，
最上面选「用哪套颜色」（跟随主题 / 跟随壁纸 / 自定义颜色），
**只有选了自定义，调色盘才在下面展开**，不再默认占一大块。

方块取色、色相条、RGB 三条滑块、十六进制改的都是同一个 `ColorPaletteViewModel` 里的
同一个颜色，任取一处调整其余自动同步 —— 这就是"两种取色方式保持统一"的实现方式
（不是两套状态互相镜像，而是压根只有一个状态）。

验证：`AccentSource=custom` 时调色盘展开、`=theme` 时收起，两种分支都截图确认。

---

## 2026-10-30 · 用了壁纸以后，窗口顶上那条白带

用户报了一句很具体的话：**「在使用壁纸时上方还会存在空白，疑似图片覆盖逻辑是填充而不是适应」**。
先排除了「图片没铺满」这个猜测 —— `MainWindow.axaml` 里的 `Image` 一直是
`Stretch="UniformToFill"` 加四边 `Stretch` 对齐，图片本身是铺满的。
真正的原因在**客户区有多大**，不在图片。

### 怎么量出来的

这个会话里屏幕截图抓不到东西（`CopyFromScreen` 出来几乎全黑，非黑像素 12.8%），
所以改用 Win32 的矩形关系来量：`GetWindowRect` − `GetClientRect` = 系统保留的非客户区。
`tools/probe-window-frame.ps1` 就是干这个的（脚本必须存成**带 BOM 的 UTF-8**，
否则 Windows PowerShell 按 GBK 读会把中文注释读成乱码，报 `Unexpected token`）。

| 窗口装饰 | 窗口 | 客户区 | 客户区起点 | 非客户区 |
| --- | --- | --- | --- | --- |
| `BorderOnly`（原来） | 1295×835 | 1280×820 | (7, 7) | 上 7 / 左 7 / 右 8 / 下 8 |
| `BorderOnly` + `ExtendClientAreaToDecorationsHint` | 1295×828 | 1280×820 | (7, 1) | 上 **1** / 左 7 / 右 8 / 下 7 |
| `None`（最终） | 1280×820 | 1280×820 | (0, 0) | **全 0** |

（150% 缩放，DPI 144。用户截图里那条带子看起来 8–10px，和这里的 7–8px 对得上。）

`BorderOnly` 只去掉了系统标题栏，**可调整大小的那一圈边框还归系统管**（Win32 的 `WS_THICKFRAME`）。
它由 DWM 用浅灰色画，壁纸铺不进去。`ExtendClientAreaToDecorationsHint` 只把顶上那条从 7px 压到 1px，
左右两条竖条还在 —— `PrintWindow` 抓图确认：`x < 8` 的像素根本不是我们画的。

### 为什么最后选 None

只有 `WindowDecorations="None"` 能让客户区和窗口矩形完全重合。
代价是系统不再提供边框、投影，**也不再提供拖边改大小**：

- 实测窗口样式从 `0x16870000`（含 `WS_THICKFRAME`）变成 `0x16030000`（没有）；
- 对右边缘发 `WM_NCHITTEST`，返回 **1（HTCLIENT）** 而不是 11（HTRIGHT）——
  系统认为整块都是客户区，鼠标在边缘只是普通点击；
- `Window.BeginResizeDrag()` 也救不了：它最终依赖那个只对 `WS_THICKFRAME` 生效的系统循环。
  用 `PostMessage` 投递「按住右边缘往左拖 150px」，窗口纹丝不动，
  但辅助日志证明抓手确实收到了点击（`edge=East state=Normal canResize=True`）。

### 所以补了一层「改大小抓手」

`MainWindow.axaml` 里加了 `Grid Name="ResizeGrips"`：四条边各 6px、四个角各 10px 的透明 `Border`，
`Cursor` 换成 `TopSide` / `LeftSide` / `TopLeftCorner` 这些形状，`Tag` 记着是哪个方向。
按下时记下窗口位置（物理像素）与大小（DIP），拖动时按鼠标位移改 `Position` / `Width` / `Height`，
往左/往上拉时右下角保持不动。纯托管代码，**没有引入任何 P/Invoke**。

验证（`tools/probe-resize-post.ps1`，用 `PostMessage` 投递鼠标消息，避免窗口被别的窗口盖住）：

```
拖动前 : 1280x820
拖动后 : 1130x820      ← 拖了 150px，正好少 150
```

另外确认了最大化仍然只占工作区、不盖任务栏（窗口 1707×1019 = 工作区尺寸，屏幕 1707×1067）。

### 教训

1. **「图片没铺满」和「客户区不够大」是两个问题**，先量矩形再改布局。
2. `BorderOnly` ≠ 无边框，它只是「没有标题栏」；那圈 `WS_THICKFRAME` 还在，而且**会露出来**。
3. 屏幕截图不可用时，`GetWindowRect` / `GetClientRect` 这类矩形关系比猜更可靠；
   想知道「某个像素是不是我们画的」，用 `PrintWindow(PW_RENDERFULLCONTENT)` 比截屏稳。
4. 去掉系统边框就同时去掉了「拖边改大小」，这是要自己补的，不是免费得到的。

## 2026-10-30 · 圆角回来了：一行附加属性，外加一个"差点多画一条线"的判断

上一节把窗口改成 `WindowDecorations="None"`，壁纸终于铺到边了，
但代价是 Windows 11 的两样东西一起消失：**窗口圆角**和**那圈 1px 边框**。
用户的原话是「win11的圆角适配边框没了」。

### 一、修法其实只有一行

Avalonia 12 自带一个附加属性，不用写任何 P/Invoke：

```xml
<Window WindowDecorations="None"
        Win32Properties.WindowCornerPreference="Round">
```

`Avalonia.Controls.Win32Properties.WindowCornerPreference` 有四个取值
`Default` / `DoNotRound` / `Round` / `RoundSmall`，内部就是
`DWMWA_WINDOW_CORNER_PREFERENCE`。XML 注释写得很清楚：
「This value is supported starting with Windows 11 Build 22000.
It is ignored on earlier Windows version」—— 所以老系统上写它无害。

### 二、然后我差点多画一条线

第一反应该是"系统不画边了，我自己画"。于是我加了一层
`Border` 覆盖全窗：圆角 8、1px 描边、`IsHitTestVisible="False"`。

但"应该"不等于"是"。量一下就知道 DWM 到底画没画：

```
[DllImport("dwmapi.dll")] DwmGetWindowAttribute(hwnd, /*37*/ DWMWA_VISIBLE_FRAME_BORDER_THICKNESS, ...)
```

实测结果：

| 属性 | 读回值 | 含义 |
| --- | --- | --- |
| `DWMWA_VISIBLE_FRAME_BORDER_THICKNESS`（37） | **2 px** @150% 缩放 ≈ 1 DIP | DWM 自己画了边框 |
| `DWMWA_WINDOW_CORNER_PREFERENCE`（33） | **2**（Round） | DWM 会裁圆角 |

也就是说设了 `WindowCornerPreference="Round"` 之后，DWM 是**连边框带圆角一起给的**。
自绘那层 Border 纯属多余，而且会变成"内外两条线"，比原生窗口还脏。

→ 把自绘 Border 连同它引用的 `WindowEdgeBrush`、`RadiusWindow` 令牌一起删掉了。
这段经历写进了 `MainWindow.axaml` 的注释里，省得下次有人再想着画一遍。

### 三、两个坑

**坑 1：`Name` 会把枚举类型遮住。**

自绘那层 Border 我一开始叫 `Name="WindowEdge"`。XAML 编译器会给它生成一个字段：

```csharp
internal Border WindowEdge;      // 来自 Name="WindowEdge"
```

而 `MainWindow.axaml.cs` 里改大小时用的是枚举 `WindowEdge.East` / `WindowEdge.NorthWest`……
于是 `WindowEdge` 这个名字指向了**字段**，编译直接报 5 个错：

```
error CS1061: "Border"未包含"East"的定义
error CS1061: "Border"未包含"NorthEast"的定义
...（SouthEast / West / NorthWest 同理）
```

改成 `WindowEdgeLine` 就好了。教训：**给控件起名先查一下有没有同名类型**，
尤其是 `WindowEdge`、`WindowState`、`Orientation` 这种天天用的枚举。

**坑 2：脚本插令牌插重了，程序启动就崩。**

用脚本往三个主题字典里各插一条 `WindowEdgeBrush`，结果 Light 字典被插了两遍、
Default 字典一条都没插。编译毫无问题（XAML 是运行时才建 ResourceDictionary），
一启动就：

```
Unhandled exception. System.ArgumentException:
  An item with the same key has already been added. Key: WindowEdgeBrush
   at Avalonia.Controls.ResourceDictionary.AddDeferred(...)
   at Populate:/Styles/DesignTokens.axaml(..., ResourceDictionary)
   in HeliumTodo.App\Styles/DesignTokens.axaml:line 48
```

退出码 `-532462766`（`0xE0434352`）。教训：**往 `ThemeDictionaries` 里批量插内容，
插完要数一遍"每个字典各几条"**，别只看 `dotnet build` 绿了就以为对。

### 四、验证

| 项目 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **119 个测试全绿** |
| `HeliumTodo.RenderCheck --shell` | 7 项全通过 |
| `--shell-check` / `--refresh-check` / `--room-check` / `--backup-check` / `--folder-check` | 全部通过 |
| 圆角偏好读回 | `2 (Round)`，`HRESULT=0x00000000` |
| DWM 可见边框 | `2 px`（150% 缩放） |
| 客户区 | 1280×820 = 窗口矩形，偏移 (0, 0) —— **壁纸仍然铺满整窗** |

最后一行是这一节的重点：圆角回来了，但上一节修好的"壁纸铺到边"不能回退。
两个诉求同时满足，靠的是 `WindowDecorations="None"` 清掉 WS_THICKFRAME、
`WindowCornerPreference="Round"` 让 DWM 在无边框窗口上补圆角。

### 五、教训

1. **"系统不给，我自己画"之前，先量一下系统到底给没给。** 一个 `DwmGetWindowAttribute`
   就能回答，比截图对比靠谱得多（何况这个会话里截图全黑）。
2. **`Name` 生成的字段会参与名字查找。** 字段和类型同名，编译器只会告诉你
   "Border 未包含 East"，不会告诉你"因为你给 Border 起了个和枚举一样的名字"。
3. **XAML 资源字典的重复键是运行时炸弹。** 编译期不查，启动即崩；
   批量改主题字典后最好立刻跑一次 GUI，别只看编译结果。

## 2026-11-01 · 版本号升到 0.3.0-dev，并打一份能直接双击运行的 0.3.0

### 一、为什么一步跨到 0.3

0.1.0-dev 之后又落下四块东西：待办/日程提醒、托盘常驻与后台运行、IPC 契约预留、
以及「打开文件夹」改用 Avalonia Launcher。按语义化版本的说法，
这些里面有**新增能力**也有**行为变更**（关窗口不再退出程序），
所以第二段跳两格，直接给到 **0.3.0-dev**。

### 二、版本号的唯一源头

`Directory.Build.props` 里改三个值，其余全部跟着走：

| 属性 | 值 |
| --- | --- |
| `<Version>` | `0.3.0-dev` |
| `<AssemblyVersion>` | `0.3.0.0` |
| `<FileVersion>` | `0.3.0.0` |

> `Version` 允许带 `-dev`（会进 `AssemblyInformationalVersion`），
> `AssemblyVersion` / `FileVersion` 只能四段数字 —— 两者不能混。

顺带把注释和文档里"当前版本是 0.1.0-dev"的说法改掉（`VersionInfo.cs`、`ShellViewModel.cs`、
`SettingsViewModel.cs`、`BackupManifest.cs`、`VersionInfoTests.cs`、`ARCHITECTURE.md`、
`PROJECT_OVERVIEW.md`、`WINDOWS_DEPENDENCIES.md`、`AVALONIA12_FEASIBILITY.md`）。

> ⚠️ 但**历史小节不能改**：`PROGRESS.md` 里「版本号 0.0.2-dev → 0.1.0-dev」那一段
> 记录的是当时发生的事，跟着现在一起改就成了假历史。上次批量替换踩过这个坑，这次先圈定范围再改。

### 三、打出来的东西

```powershell
dotnet publish HeliumTodo.App/HeliumTodo.App.csproj -c Release -r win-x64 --self-contained true -o dist/HeliumTodo-0.3.0-win-x64
```

| 项目 | 数值 |
| --- | --- |
| 文件数 | 252 → **247**（删了 5 个 `.pdb`） |
| 体积 | 219 MB → **119 MB** |
| 压缩包 | `dist/HeliumTodo-0.3.0-win-x64.zip`，**50 MB / 247 条目** |
| exe 元数据 | `FileVersion 0.3.0.0`、`ProductVersion 0.3.0-dev` |

`.pdb` 占了一半以上体积，其中 `libSkiaSharp.pdb` 一个就 81 MB、`libHarfBuzzSharp.pdb` 20 MB
—— 都是原生库的调试符号，运行完全不需要。

### 四、冒烟测试里顺带验证了"便携模式"这条退路

从 `dist` 目录直接双击 `HeliumTodo.App.exe`，进程起得来、窗口出现。它没能用
`%AppData%\Helium Todo`（这个沙盒会话对那里没有写权限），于是**自动退到程序旁边的 `Data\`**：

```
dist\HeliumTodo-0.3.0-win-x64\Data\config.json
dist\HeliumTodo-0.3.0-win-x64\Data\helium.db
dist\HeliumTodo-0.3.0-win-x64\Data\background.log
```

这正是 `ConfigService` 设计的行为：`%AppData%` 写不了就退到 `AppContext.BaseDirectory\Data`，
并置 `IsPortableMode = true`，保证程序**永远能启动**。
在你自己的电脑上它会正常用 `%AppData%`，不会在程序目录里拉东西。

（冒烟测试的产物已在打包前删掉，压缩包里是干净的。）

### 五、验证

| 检查 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **141 / 141** |
| `RenderCheck --shell` | 7 / 7，界面顶部打印「当前版本：0.3.0-dev」 |
| 设置 → 关于 | 显示 `0.3.0-dev`；左栏底部 `v0.3.0-dev` |
| 指纹基线 | **139 个文件**一致 |

### 六、教训

- **版本号在四个地方"看起来都像源头"**：`<Version>`、`AssemblyVersion`、`FileVersion`、
  以及界面里那句硬编码。真正能自动带走的只有前三个 —— 界面那句必须自己查。
- **`.pdb` 不是"小零头"**：这次 252 个文件里 5 个 pdb 占了 100 MB。发布脚本里删掉它们是性价比最高的一步。
- **便携模式不是坏消息**：它在"没有 AppData 权限"的环境里救了命，冒烟测试反而把它验了一遍。

## 2026-11-01 · 「打开文件夹」改用 Avalonia 自带的 Launcher

用户问：能不能用 Avalonia 自带的 Launcher 服务打开文件夹？

### 一、先认个错：之前那句结论是错的

前几轮我在日志里写过「Avalonia 12 的 `ILauncher` 只有 `LaunchUriAsync` / `LaunchFileAsync`，
**没有**打开目录的方法」，所以自己写了 `explorer.exe` 那套进程降级。

这次没靠记忆，直接把 `~/.nuget/packages/avalonia/12.1.2/lib/net10.0/Avalonia.Base.xml`
翻出来查，两个方法都在：

| 方法 | 文档原文 |
| --- | --- |
| `LauncherExtensions.LaunchDirectoryInfoAsync(ILauncher, DirectoryInfo)` | Starts the default app associated with the specified storage directory (folder). |
| `ILauncher.LaunchFileAsync(IStorageItem)` | Starts the default app associated with the specified storage file **or folder**. |

`TopLevel` 上就有 `Launcher` 属性（用临时控制台反射确认过）。
所以「没有这个 API」是错的，只是**当时没找到**。

### 二、为什么它比硬编码 `explorer.exe` 好

1. **交给系统外壳决定用哪个文件管理器** —— 用户把默认文件管理器换成第三方时，
   硬编码 `explorer.exe` 会绕过他的选择。
2. **有返回值**。`Process.Start` 在很多路径下是"开了但你看不出来"，而
   `LaunchDirectoryInfoAsync` 返回 `Task<bool>`，成没成一目了然。
3. **不用拼命令行引号**，少一类"路径里有空格/中文就炸"的隐患。

### 三、改法：它排第一，老的四级降级保留

- 新增 `HeliumTodo.App/Services/FolderLauncher.cs`：
  `IFolderLauncher.OpenAsync(string, CancellationToken) → Task<bool>`；
  `AvaloniaFolderLauncher` 从 `Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime`
  取 `MainWindow` 当 `TopLevel`（窗口藏到托盘时它依然在，所以后台也能用），
  然后 `await top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path))`；
  任何异常吞掉返回 false，让调用方继续降级。
- `SettingsViewModel.OpenInExplorer` 改成 `async void`：先 `await _folderLauncher.OpenAsync(path)`，
  成功直接返回；失败就把原因塞进 `reasons`，再走原来的
  「explorer.exe → 目录 + Verb=open → explorer.exe 直启 → cmd /c start」。
- `App.axaml.cs` 注册 `services.AddSingleton<IFolderLauncher, AvaloniaFolderLauncher>();`。

> ⚠️ 编译期坑：`LaunchDirectoryInfoAsync` 是**扩展方法**，定义在
> `Avalonia.Platform.Storage` 命名空间里。不写 `using Avalonia.Platform.Storage;`
> 报的是 `error CS1061: “ILauncher”未包含“LaunchDirectoryInfoAsync”的定义`
> —— 看起来像方法不存在，其实是 using 少了一行。

### 四、验证

`--folder-check` 开头新增一段：从 DI 解析 `IFolderLauncher`，对配置目录调一次，
把返回值打出来。无头平台没有真正的系统外壳，实测返回

```
── Avalonia Launcher ──
  ✓ 解析到 IFolderLauncher；对配置目录的返回值 = False
    → 无头环境预期如此，会走进程降级路径
```

`False` 正是"这条路走不通就降级"的证明 —— 报错或抛异常才是问题。

| 检查 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **141 / 141** |
| `RenderCheck --shell` | 7 / 7 |
| `--folder-check` | 0 失败项（4 个按钮可点、Launcher 已接） |
| `--timeline-check` / `--reminder-check` / `--shell-check` / `--refresh-check` / `--room-check` / `--backup-check` | 全 0 失败项 |

### 五、教训

- **"这个 API 不存在"这种结论，一定要落到文档/反射证据上。** 这次只是翻了个 XML 文档，
  就把半年前"必须自己起进程"的前提推翻了 —— 而基于错误前提写的四级降级，倒也不算白写：
  它正好补上了 Launcher 返回 false 的那些情况。
- **扩展方法找不到时，先想 using。** `CS1061` 的措辞（"未包含…的定义"）很容易让人
  以为签名对不上，而真正的原因是命名空间没进来。

## 2026-11-01 · 在 Visual Studio 里跑出来的三个问题

在 VS 里调起来之后一口气冒出三件事：托盘没起来、打开文件夹没反应、导出备份直接崩。
前两个查到最后都是**环境**，第三个是真 bug —— 记一下。

### 一、导出崩在 `UnauthorizedAccessException`

现象：挑"桌面"导出，弹一个未处理异常对话框，指到 `BackupPackageWriter.Write` 里那句
`new FileStream(tempPath, ...)`：

```
System.UnauthorizedAccessException: "Access to the path
'E:\17121\desktops\HeliumTodo-backup-20261031-230435.zip.tmp' is denied."
```

根因有两层：

1. **那个目录确实不让写**。Windows 安全中心的「受控文件夹访问」默认保护桌面 / 文档 / 图片，
   未签名的自己编译的 exe 一写就吃 `UnauthorizedAccessException`。
2. **代码把异常漏出去了**。`UnauthorizedAccessException` 不是 `IOException` 的子类
   （它是 `SystemException` 的直接子类），所以任何 `catch (IOException)` 都兜不住它。

修法分三段：

| 位置 | 改法 |
| --- | --- |
| `Core/Backup/BackupPackageWriter.cs` | 新增 `BackupWriteDeniedException`；建目录 / 开临时文件 / 改名三处失败都转成它，并带一句人话（提到受控文件夹访问） |
| `Data/DataBackupService.cs` | 捕获它 → 自动改存到**应用自己的 backups 文件夹**（重名加序号）→ 返回 `FellBack = true` 的结果 |
| `App/ViewModels/SettingsViewModel.cs` | 状态栏写「原位置不能写入，已改存到备份文件夹：xxx」，提示条说明原因 |

**为什么是"改存"而不是"报错"**：备份是保命的东西，用户点了导出就该拿到一个 zip。
路径问题不该让整件事失败 —— 换个地方放、把新位置说清楚就行了。

### 二、托盘图标加不上（环境问题，但现在是可诊断的）

`Shell_NotifyIconW(NIM_ADD)` 返回 FALSE、`err=5`（拒绝访问）。检查过：
`cbSize = 976`（x64 正确值）、图标句柄非 0、explorer 在别的 session 里跑着。

为了以后一眼看出原因，`TrayService` 现在会把这行写进 `background.log`：

```
[环境] 会话=9 管理员=False 资源管理器=没有运行
托盘图标：window=0xE00DA icon=0x2B097D add=False err=5 cbSize=976
托盘图标加不上（错误码 5）：拒绝访问：当前会话没有可用的通知区域
（远程桌面 / 无交互会话常见），或者程序以管理员身份运行而资源管理器不是。
```

**错误码 5 的两个典型原因**（都不是代码能绕过的）：
① 当前会话没有可交互的通知区域（远程桌面断开、服务会话、无头会话）；
② 程序以管理员身份运行，而资源管理器是普通权限 —— 两者完整性级别不同，托盘区不互通。

### 三、打开文件夹没反应：多给一条退路

原来的三级降级（`explorer.exe` + 外壳 / 系统默认 / 直接 CreateProcess）在"外壳被挡"的情况下
会全军覆没。加了第四条：**经 `cmd /c start` 打开** —— 它走的是另一条创建进程的路径。
四条都失败时，提示条会把完整路径打出来，用户至少能手动粘到资源管理器地址栏。

### 四、验证

| 项目 | 结果 |
| --- | --- |
| `dotnet build`（Core / Data / Tests / SelfTest / RenderCheck） | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **141 / 141**（新增 2 个覆盖降级保存） |
| `RenderCheck --shell` | 7 / 7 |
| `--backup-check` | 全部通过，新增 3 项断言（不卡忙状态 / 说明改存到哪儿 / 提示条给出原因） |

### 五、教训

- **`UnauthorizedAccessException` 不继承 `IOException`**。写文件的地方如果只 `catch (IOException)`，
  权限错误会一路冒到用户脸上。这个坑值得单独记一条。
- **"导出到用户挑的位置"是外部输入**：路径可能是只读盘、网络位置、受控文件夹、甚至一个目录本身。
  所有失败都要有一条"那我放这儿"的兜底。
- **`Shell_NotifyIcon` 的错误码要当天写进日志**，否则用户反馈"托盘没有了"时，
  只能靠猜是权限、会话还是结构体大小。
- **调试时别让程序一直开着**：`HeliumTodo.App.exe` 会锁住 `bin` 里的 dll，
  `dotnet build` 报 `MSB3027 超出了重试计数 10`、`文件被 Microsoft Visual Studio 锁定`。
  要么先停止调试，要么 `-o` 到临时目录。

## 2026-11-01 · 提醒、托盘常驻、IPC 预留，以及时间轴首尾那半个字

这一轮做的是四件互不相干的事，但它们都得往同一个 App 里塞，所以一起记。

### 一、时间轴 0 点和 24 点被切掉半个字

原来 `TimelineGrid` 直接把时刻映成坐标：`top = -StartHour * HourHeight`。
于是 00:00 那条线正好在 y = 0、24:00 在 y = 912，而刻度文字是**以那条线为垂直中心**
画的 —— 上面那个 "00:00" 有一半在控件外面，下面那个 "24:00" 有一半被裁掉。

修法是把"线的位置"和"文字的位置"分开：新增 `VerticalPadding`（默认 14），
线画在 `VerticalPadding - StartHour * HourHeight`，内容高度从 912 变成
`24 × HourHour + 2 × 14 = 940`。方块的高度也要按去掉上下留白之后的可画区算，
否则会发生"线在 14，方块还在 0"的错位。

| 位置 | 修之前 | 修之后 |
| --- | --- | --- |
| 00:00 那条线 | y = 0（文字上半截在外面） | y = 14（文字完整） |
| 24:00 那条线 | y = 912（文字下半截被裁） | y = 926，文字底部 934.3 ≤ 940 |
| 内容高度 | 912 | 940 |

验证靠新写的 `--timeline-check`（7 项，其中"14 ≥ 半个字高 8.3"是防以后有人把留白调小）。

### 二、提醒：从"算得对"到"真的弹出来"

分三层，每层都能单独测：

1. **Core · 什么时候该提醒**（`ReminderPlanner.CollectDue`）
   判定窗口是 `[now - 容差, now]` 而不是 `now`：后台 30 秒扫一次，
   如果只认"提醒时刻正好等于当前这一毫秒"，那这一轮永远扫不到东西。
   重复日程要先过 `RecurrenceExpander.OccursOn`，命中了再把 `Start` 挪到今天同一时刻 ——
   否则"每周二的课"在第三周就再也不提醒了。
2. **Core · 弹什么**（`ReminderItem` + `INotificationService`）
   `ReminderItem.Key` 里带了日期，所以同一个待办今天弹过、明天还能再弹；
   但同一天里反复扫描不会重复弹（`ReminderService` 的 `HashSet<string>` 记着）。
3. **App · 谁去弹**（`ReminderService` 定时器 30 秒 → `WindowsToastNotifier` → 托盘气泡）
   Win10 之后，托盘气泡会被系统统一收进通知中心，所以不需要 WinRT toast
   （那要注册 AppUserModelID + 开始菜单快捷方式，为一个提醒不值当）。

界面上日程、待办共用同一个下拉（`ReminderPicker`）：不提醒 / 开始时 / 提前 5、10、30 分钟 /
提前 1、2 小时 / 自定义…。选「自定义…」才展开小时和分钟两个数字框。
这样 95% 的情况一次点选就完，不用为了"提前 10 分钟"去打两个数字。

### 三、托盘常驻：关窗口不等于退出

`TrayService` 是纯 P/Invoke 写的（`Shell_NotifyIconW` + 自建隐藏窗口 + 独立 STA 线程消息循环），
没有引 WinForms —— 引了就得把目标框架改成 `net10.0-windows`，而本项目的无头渲染工具
是 `net10.0`，一改就得连带改一堆东西。

行为：点 × → 窗口 `Hide()`、进程继续跑（提醒照常弹）；托盘菜单「退出 Helium Todo」才真退出。
菜单里还有「立即备份一次」，对应需求里"后台处理必要的数据"。

**关键判断**：如果托盘图标加不上（比如资源管理器没跑），那 `Closing` 就**不拦**，
窗口按系统默认行为关掉。否则用户会得到一个关不掉也叫不回来的进程。

### 四、IPC：只留代码，不留界面

按你说的「暂时无需在应用界面内声明预留了 IPC 接口」，这一块只有三个文件：
`IIpcChannel`（含 `StartAsync/StopAsync/SendAsync/RequestReceived`）、
`IpcContracts`（`IpcRequest` / `IpcResponse` / `IpcActions` / `IpcJson`）、
以及一个什么都不做的 `NullIpcChannel`，在 DI 里注册成 `IIpcChannel`。
将来换成命名管道实现，界面一行都不用改。设置页里**没有**任何 IPC 字样。

### 五、两个环境限制（不是代码问题，但得记下来）

1. **托盘图标在本会话加不上**：`Shell_NotifyIconW(NIM_ADD)` 返回 FALSE，
   `GetLastWin32Error() = 5`（ERROR_ACCESS_DENIED）。结构体大小核对过是 976（x64 正确值），
   explorer.exe 也在同一个 session 里。这个会话没有可交互的通知区域
   （和之前"截屏全黑、真实窗口点不到"是同一个原因）。
   → 加了个自检开关 `HELIUM_FAKE_TRAY=1`，假装托盘已就绪，用来验证"关窗口之后进程还在"。
2. **`%TEMP%` 写不了**（早前就发现的那条）：所有自检沙盒都放程序目录下。

### 六、验证

| 项目 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **139 / 139**（原 119 + 提醒相关 20） |
| `RenderCheck --shell` | 7 / 7 |
| `--timeline-check` | 7 项全通过 |
| `--reminder-check` | 11 项全通过 |
| `--shell-check` / `--refresh-check` / `--room-check` / `--backup-check` | 11 / 6 / 5 / 15 全通过 |
| `tools/probe-lifecycle.ps1` | 关窗口后进程仍在、窗口已隐藏 |
| 后台提醒端到端 | 插入 25 秒后到点的待办，`background.log` 里 25 秒后确实出现了「弹出」 |

### 七、教训

- **`const int?` 不合法**（`CS0283`）。可空类型没有编译期常量，只能 `static readonly`。
- **DisplayName 里别嵌英文双引号**：`[Fact(DisplayName = "…"今天"…")]` 直接把字符串截断，
  报的是 `error CS1003: 语法错误，应输入","`，看不出跟引号有关。
- **`weekly:1` 是周一不是周二**（0 = 周日）。测试里那个 `Noon` 明明写着 2026-03-10（星期二），
  规则写成 `weekly:1` 就永远不命中。
- **后台定时器必须 `catch` 一切**：`Timer` 回调里抛出去的异常会静默带走进程，
  跟"提醒不弹"长得一模一样。
- **WinExe 没有控制台**：后台事件发生在窗口关掉之后，除了自己写日志文件没有第二个观测口。
  这次的 `background.log` 一次就定位了托盘失败和提醒不到两个问题。
- **DI 里没有 ViewModel**：页面是导航项惰性创建的，`GetRequiredService<TodoViewModel>()` 一定抛，
  得走 `item.GetOrCreatePage() as TodoViewModel`。

## 2026-10-31 · 还是跳变，于是我们自己写这 180 毫秒

上一节把 `WS_THICKFRAME` 借了回来，理论上窗口已经"像个正常窗口"，
但实测点最大化**仍然是瞬间跳变**。

### 先排除"是不是被谁关掉了过渡"

新增 `tools/probe-dwm-attrs.ps1`，直接问 DWM：

| 属性 | 值 | 结论 |
| --- | --- | --- |
| `DWMWA_NCRENDERING_ENABLED`（1） | 1 | DWM 在画非客户区，正常 |
| `DWMWA_TRANSITIONS_FORCEDISABLED`（3） | 读不出来（`0x80070057` E_INVALIDARG） | 不是被强制关掉的 |
| `DWMWA_WINDOW_CORNER_PREFERENCE`（33） | 2 (Round) | 圆角生效 |
| `DWMWA_VISIBLE_FRAME_BORDER_THICKNESS`（37） | 2px | 描边在 |

也就是说：没有哪个开关被关掉，就是 DWM 不认这个窗口。
继续往下挖要动的是 `WM_NCCALCSIZE` 和非客户区的交互细节，
那不是"再试一次就能中"的东西 —— 与其猜，不如自己实现。

### 做法：把 180 毫秒握在自己手里

新增 `HeliumTodo.App/Views/MainWindow.Animation.cs`（201 行，纯托管，不碰任何平台 API）：

- `ToggleMaximizeAnimated()`：唯一入口。动画进行中直接忽略点击，避免两个定时器互相打架。
- `MaximizeAnimated()`：记下当前矩形当"还原尺寸"，终点取显示器工作区（`Screens.ScreenFromWindow(this).WorkingArea`），起动画，结束时 `WindowState = Maximized`。
- `RestoreAnimated()`：先把窗口切回 `Normal`（系统会把它摆到还原尺寸），**同一帧内**立刻把窗口按回最大化的样子，再从这里往回收 —— 屏幕上不会闪。
- `AnimateWindow(from, to)`：`DispatcherTimer`，16 帧 / 180ms ≈ 11ms 一帧，每帧 `ApplyRect()`。
- `EaseOut(t) = 1 - (1-t)³`：起步快收尾慢。线性插值会让窗口"匀速撞"到屏幕边缘，很硬。
- `Interpolate()`：在**物理像素**里插值再取整。用 DIP 插值的话 150% 缩放下会出现半像素抖动。
- `ApplyRect()`：先 `Position` 再 `Width/Height`。反过来的话窗口会先长到旧位置外面一帧。

`ShellView.axaml.cs` 的 `ToggleMaximize()` 改成先认 `MainWindow` 并调 `ToggleMaximizeAnimated()`，
认不出来（比如渲染检查里的小窗口）才退回系统行为。

### 怎么证明"真的在动"

这是本节最要紧的一点：**"我看它好像在动"不算证据**。
因为动画是我们自己一帧帧改 `Position/Width/Height` 的，
从窗口外面 `GetWindowRect` 就能采到中间尺寸 —— 于是可以客观验证。

`tools/probe-maximize-anim.ps1`：给程序设 `HELIUM_MAXIMIZE_TEST=3000`（毫秒），
它启动 3 秒后自己点一次最大化，1.5 秒后再还原一次；探针每 10ms 采一次窗口矩形。

实测输出：

```
采样次数   : 506
不同尺寸数 : 25
尺寸序列   : 1293x833 -> 1379x879 -> 1443x908 -> 1498x933 -> 1545x954 -> 1619x987
           -> 1647x999 -> 1668x1009 -> 1685x1017 -> 1698x1022 -> 1707x1027
           -> 1713x1029 -> 1719x1031 -> 1720x1032        ← 长到工作区
           -> 1583x971 -> 1529x946 -> 1481x925 -> 1441x907 -> 1407x892
           -> 1380x879 -> 1359x870                       ← 再收回去
```

去程 14 个中间尺寸、回程 11 个，步长先大后小（正是 ease-out 的形状）。
跳变的话这个数只会是 1~2。

### 顺手加的测试钩子

`MainWindow.Animation.cs` 里的 `ScheduleMaximizeTestIfRequested()`：
只有 `HELIUM_MAXIMIZE_TEST` 存在时才挂定时器，正常使用完全不会执行。
`MainWindow.axaml.cs:170` 在 `OnOpened` 末尾调用它。

### 教训

1. **"系统应该会"和"系统确实会"是两件事。** 样式位补齐换来的是"窗口看起来正常"，
   换来不了 DWM 的动画 —— 这两者的判据不一样。
2. **能被外部量测的行为，才算验证过。** 这次的关键不是"我实现了动画"，
   而是"我找到了一个从窗口外面就能量到中间帧的办法"。
3. **缓动曲线不是装饰。** 线性插值在 180ms 的动画里看起来就是"弹一下"。
4. **同一帧里做完"切换状态 + 摆回原状"**，是消除闪烁的通用手法（还原动画就靠这个）。
5. 量 DWM 属性之前，先确认属性本身可读 —— `DWMWA_TRANSITIONS_FORCEDISABLED`
   读出来是 `E_INVALIDARG`，这个返回值本身也是信息（它说明"没人强制关过"）。

## 2026-10-31 · 最大化没有动画：把 WS_THICKFRAME 借回来，再把非客户区抹掉

圆角修好之后冒出一个新问题：**点最大化窗口是"跳"过去的，没有 Windows 那个缩放动画了**。
（`WindowDecorations="BorderOnly"` 的时候是有动画的，换成 `None` 之后才没的。）

### 一、先看样式位，别猜

`tools/probe-window-frame.ps1` 打印的 `GWL_STYLE` 直接给出了答案：

| 窗口装饰 | 样式值 | 拆开看 |
| --- | --- | --- |
| `BorderOnly`（**有**动画） | `0x16870000` | `WS_VISIBLE \| WS_CLIPSIBLINGS \| WS_CLIPCHILDREN \| WS_BORDER \| **WS_THICKFRAME** \| WS_MINIMIZEBOX \| WS_MAXIMIZEBOX` |
| `None`（**没**动画） | `0x16030000` | 少了 `WS_THICKFRAME` 和 `WS_BORDER`，只剩 MIN/MAXBOX |

也就是说：Windows 判断"这是不是一个能最大化的普通窗口"时，看的是 `WS_THICKFRAME`
（可调整边框）而不是 `WS_MAXIMIZEBOX`。我们把边框去掉换壁纸铺满，顺手把这个也算丢了。

### 二、矛盾的解法：样式借回来，非客户区抹掉

要动画就得有 `WS_THICKFRAME`，可有了它系统就会长出那圈 7px 的灰色非客户区 ——
又回到「壁纸顶上留白」。这两件事其实可以同时满足：

1. **样式位加回去**（`WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX`）；
2. **挂钩 `WM_NCCALCSIZE`**，把 `rgrc[0]`（窗口矩形）原样留着，等于告诉系统
   "这个窗口的非客户区是 0"。

样式上它是普通可调整窗口（有动画、有阴影、有圆角），视觉上客户区等于整个窗口。

Avalonia 12 已经给了钩子，**不用自己子类化窗口**：

```csharp
Win32Properties.AddWindowStylesCallback(
    this,
    static (style, exStyle) => (style | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX, exStyle));

Win32Properties.AddWndProcHookCallback(this, OnNativeMessage);
```

代码在 `HeliumTodo.App/Views/MainWindow.Win32.cs`（单独一个 partial 文件，
`MainWindow.axaml.cs` 只留一行 `EnableNativeWindowFrame();`），非 Windows 平台直接返回。

### 三、`WM_NCCALCSIZE` 的两个细节

**细节 1：wParam 是 TRUE 还是 FALSE，含义完全不同。**
`TRUE` 表示"要算客户区大小"，`lParam` 是 `NCCALCSIZE_PARAMS*`；`FALSE` 表示
"要算客户区矩形"，`lParam` 是 `RECT*`。我们只处理前一种，把结构体原样写回去、
返回 0（"接受这个客户区"）。

**细节 2：最大化时"窗口矩形"是整个屏幕，不是工作区。**
如果原样接受，客户区会把任务栏盖住。所以最大化（`IsZoomed`）时换成显示器的工作区：

```csharp
if (IsZoomed(hWnd))
{
    var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
    var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
    if (GetMonitorInfo(monitor, ref info)) parameters.WindowRect = info.Work;
}
```

实测（150% 缩放，屏幕 1707x1067，工作区 1707x1019）：

| 状态 | 窗口矩形 | 客户区 |
| --- | --- | --- |
| 还原态 | 1293x833 @(153,59) | = 窗口矩形，偏移 (0,0) |
| 最大化后 | 1720x1032 @(-7,-7) | 扣掉两侧各 7px 的框 = **1706x1018 ≈ 工作区** |

最大化后窗口矩形比工作区大 7px/边，是正常现象 —— 系统把"框"放到工作区外面去，
好让**客户区**正好等于工作区。任务栏没被盖住。
还原（`SC_RESTORE`）之后尺寸精确回到 1293x833 @(153,59)。

圆角和描边也没丢：`DWMWA_WINDOW_CORNER_PREFERENCE = 2 (Round)`、
`DWMWA_VISIBLE_FRAME_BORDER_THICKNESS = 2px`。

### 四、一个坑：样式回调返回的是元组

`CustomWindowStylesCallback` 的签名不是"返回新的 style"，而是返回一个二元组：

```csharp
(style, exStyle) => (style | WS_THICKFRAME, exStyle)
```

照直觉写 `(style, exStyle) => style | WS_THICKFRAME` 会报一个很难懂的错：

```
error CS0029: 无法将类型"uint"隐式转换为"(uint style, uint exStyle)"
error CS1662: 无法将 lambda 表达式转换为预期委托类型…
```

### 五、验证

| 项目 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **119 个测试全绿** |
| `HeliumTodo.RenderCheck --shell` | 7 项全通过 |
| `--shell-check` / `--refresh-check` / `--room-check` / `--backup-check` / `--folder-check` | 全部通过 |
| 窗口样式 | `0x160F0000`（含 `WS_THICKFRAME \| WS_SYSMENU`） |
| 客户区 | = 窗口矩形，偏移 (0,0) —— 壁纸仍然铺满 |
| 最大化 / 还原 | 客户区 = 工作区；任务栏没被盖住；还原精确回到原尺寸 |
| DWM 圆角 / 描边 | `Round` / 2px，都还在 |

动画本身在无桌面会话里没法用截图证明，只能靠"样式位和有动画时完全一致"来推断；
真机上点一下最大化就知道。

### 六、教训

1. **删样式位之前想想它兼着什么职责。** `WS_THICKFRAME` 不只是"那圈边框"，
   它还决定动画、阴影、圆角、任务栏避让。一次删掉，四处退化。
2. **两个矛盾的需求往往有一个"系统本来就提供了"的解。**
   这里就是 `WM_NCCALCSIZE`：让样式和维护的东西分开 —— 样式留着，画面自己说了算。
3. **框架给的钩子先查文档再动手。** Avalonia 12 已经有 `AddWindowStylesCallback`
   和 `AddWndProcHookCallback`，比自己去 `SetWindowLongPtr` + `CallWindowProc` 稳得多。

## 2026-10-30 · 界面改版：从「六张平铺表单」改成「三列卡片工作台」

按仓库根目录的 `UI_REDESIGN_BRIEF.md` 分四个阶段做的。**四个阶段全部完成**，
每个阶段结束都跑了 `dotnet build` + `SelfTest` + `RenderCheck --shell` 三项。

### 一、改成了什么样

改版前：每个页面都是「28px 大标题 + 副标题 + 一堆平铺表单/列表」，左右边距 24，
换成壁纸之后文字直接压在图片上，六页看起来像六张说明书。

改版后：

```
┌──────────────────────────────────────────────────────────────┐
│ 标题栏 48px（自绘：品牌 + 状态 + 最小化/最大化/关闭）        │
├──────┬────────────────────────────────────────┬──────────────┤
│ 图标 │             卡片区（可换页）           │  右侧栏 360  │
│ 轨   │  ┌──────────────────────────────────┐  │  ┌────────┐  │
│ 56px │  │ 卡片圆角 18 · 半透明 · 带阴影    │  │  │ 上下文 │  │
│      │  └──────────────────────────────────┘  │  └────────┘  │
└──────┴────────────────────────────────────────┴──────────────┘
```

- **外壳**：自绘标题栏 + 左侧 56px 纯图标轨（图标是 Fluent 字形，不是 emoji）+ 卡片区。
  窗口用 `WindowDecorations="BorderOnly"`，边框还在所以还能拖边改大小。
- **卡片**：统一 `Border.card`（圆角 18、`#CCFFFFFF` 半透明、1px `#66FFFFFF` 描边、
  阴影 `0 8 24 0 #14000000`）。页面里不再有 28px 大标题，标题长在卡片头上（`TextBlock.cardHeader`，16 SemiBold）。
- **模糊**：壁纸默认模糊半径 8 → **24**，滑块范围 0–40 → **0–60**。
  卡片是半透明的，壁纸糊得不够狠，卡片背后的花纹会"透"出来和文字打架。
- **六个页面**：
  - 今日工作流：4 张统计小卡压成时间轴卡片头上的一行数字，省下的高度全给时间轴；
    右栏 360 放「冲突提醒 / 今日重点」。
  - 日程：一张卡片搞定 —— 卡片头（概览 + 「＋ 新建」开关）→ 可折叠编辑区 → 按天分组的列表。
  - 待办事项：卡片头（三个指标）→ 快速添加行（「更多」展开优先级/分类/截止）→ 筛选行 → 列表。
  - 专注：表盘挪到右栏 360（它是 1:1 的**展示**，不是操作区），左列放表单与记录，表单终于有横向空间。
  - 自习室：去掉页面标题，其余沿用「不在房间 / 在房间」两态布局。
  - 设置：去掉 28px 标题（每个 `FASettingsExpander` 的分组标题就是卡片头），边距与其它页对齐成 12。

### 二、四个阶段各自做了什么

| 阶段 | 内容 | 结果 |
| --- | --- | --- |
| 1 · 令牌与卡片 | 三个主题字典各补 `CardSurfaceBrush`/`CardBorderBrush`/`CardInsetBrush`/`ChipSurfaceBrush`/`SegmentedTrackBrush`/`CardShadow`；新尺寸 `RailWidth=56`/`RailItemSize=44`/`TitleBarHeight=48`/`RightRailWidth=360`；圆角 `RadiusRailItem=12`/`RadiusCard=18`；`IconFont` 字体族 | ✓ |
| 2 · 外壳三列 | `ShellView` 重写成「标题栏 + 图标轨 + 内容区」；`MainWindow` 加 `WindowDecorations="BorderOnly"`；导航图标换成 Fluent 字形并加 `IsSelected` | ✓ |
| 3 · 页面迁移 | 六页逐个搬进卡片；`TextBlock.cardHeader` 取代页面大标题 | ✓ |
| 4 · 模糊打磨 | 默认模糊 24、滑块 0–60、卡片半透明兜底（不做真 backdrop blur，见下） | ✓ |

**关于"真 backdrop blur"**：brief 第 10 节把它列为可选。没做的原因很实在 ——
要做就得再解一张强模糊的半分辨率图、塞进每个卡片、用 `LayoutUpdated` 反向平移抵消卡片滚动，
卡片里超过 20 个的话 1080p 动图下很难守住 30 fps。现在这套（半透明卡片 + 壁纸整图模糊）
已经能拿到 80% 的观感，剩下的 20% 不值得拿性能换。

### 三、踩到的坑（都记一笔）

1. **`ExtendClientAreaChromeHints` 在 Avalonia 12 里没了。**
   照旧文档写 `ExtendClientAreaChromeHints="NoChrome"` 会直接编译失败：
   `AVLN2000: Unable to resolve suitable regular or attached property ExtendClientAreaChromeHints`。
   12 里表达"不要系统标题栏"的方式是 `WindowDecorations="BorderOnly"`。
2. **`GetVisualParent()` 需要 `using Avalonia.VisualTree;`**，否则报
   `CS1061: "Control"未包含"GetVisualParent"的定义` —— 提示信息完全不提扩展方法所在的命名空间。
3. **无头渲染里 `Loaded` 事件不触发。** 只挂 `Loaded` 的话图标轨内容区会是空白；
   真机上 `Loaded` 和 `OnAttachedToVisualTree` 都会走，所以两个都挂、方法幂等。
4. **`$"{a == 0 ? "全部通过" : $"{n} 项失败"}"` 编译不过**（`CS8076`/`CS8361`）：
   条件表达式里嵌套含冒号的内插字符串，编译器分不清哪层是格式化。先算成变量再打印。
5. **`c.ItemTemplate is DataTemplate { }` 报 `CS0246`**，即使加了
   `using Avalonia.Controls.Templates;` —— 最后改成按 `ItemsSource` 类型判断，绕开这个检查。
6. **`--backup-check` 把临时 zip 写在 `%TEMP%`** → 这台机器上 .NET 进程被拒（`UnauthorizedAccessException`），
   自检报出来像是"导出功能坏了"。改成 `AppContext.BaseDirectory` 下的目录后 15 项全过。

### 四、验证

| 项目 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **119 个测试全绿** |
| `RenderCheck --shell` | 7 项全通过（六页 + 外壳） |
| `--shell-check` | 11 项全通过（几何 5 + 点击 6 + 选中唯一） |
| `--refresh-check` | 6 项全通过 |
| `--room-check` | 5 项全通过 |
| `--backup-check` | 15 项全通过 |
| `--folder-check` | 通过（提示条停在 y=48，正好在新标题栏下方） |

`--shell-check` 是这次新写的：它用 `window.MouseMove/MouseDown/MouseUp` 真的去点那六个图标，
断言「页面换了」且「选中态只有一个」——光看截图看不出点击到底通没通。

---

## 2026-11-01 · 版本号升到 0.3.1-dev

小版本号 +1，内容没变：0.3.0 是「先打一份能双击运行的包」的那个提交，
之后又落了「打开文件夹走 Avalonia Launcher」「VS 里的三个问题（导出降级 / 托盘诊断 / 第四条退路）」
「后台常驻 + 打开动效 + 窗口代码封装」这几拨，所以次版本号往前走一格。

版本号的**唯一源头**还是 `Directory.Build.props`，另外三处是它的影子：

| 位置 | 改成 |
| --- | --- |
| `Directory.Build.props` | `<Version>0.3.1-dev</Version>`、`<AssemblyVersion>0.3.1.0</AssemblyVersion>`、`<FileVersion>0.3.1.0</FileVersion>` |
| 各文件的注释示例 | `SettingsViewModel.cs`、`ShellViewModel.cs`、`BackupManifest.cs`、`VersionInfo.cs`、`VersionInfoTests.cs` 里拿版本号举例的地方（不改的话注释会跟实际对不上，下次看代码的人会被误导） |
| 文档里的「当前版本」 | `ARCHITECTURE.md` 快照表与目录树、`PROJECT_OVERVIEW.md` 表头、`WINDOWS_DEPENDENCIES.md`、`AVALONIA12_FEASIBILITY.md`、本文件顶部与目录树 |

**没有动的**：上面那节「版本号升到 0.3.0-dev，并打一份能直接双击运行的 0.3.0」里所有 `0.3.0`
字样 —— 那是当时的事实记录，包括 `dist/HeliumTodo-0.3.0-win-x64` 这个路径。
（这条规矩是上次踩出来的：批量替换把历史小节也改了，读起来像「0.3.0 的包叫 0.3.1」。）

验证：

| 检查 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | 141 / 141 |
| 程序集元数据 | `FileVersion 0.3.1.0`、`ProductVersion 0.3.1-dev` |
| `--shell` 渲染 7 页 | 7 / 7，界面顶部打印「当前版本：0.3.1-dev」 |
| 设置 → 关于 → 版本 | 走同一个 `VersionInfo.Current`，随之变成 `0.3.1-dev` |

---

## 2026-11-01 · 扫描技术栈、优化内存、修遗留 bug（0.3.1 内容不变）

三件事：先把 0.3.1 同步到 `D:\heliumtodo dev` 并打一份包，然后把内存从"170 MB 感"往下压，
最后静态自检一遍遗留问题。**版本号没动**——这次改的是运行时行为，不是功能面。

### 一、同步与打包

| 动作 | 结果 |
| --- | --- |
| 同步到 `D:\heliumtodo dev` | 程序文件 + 依赖 DLL 全量覆盖，`config.json` / `*.db` / `data` / `backups` / `wallpapers` 一律不动（实测该目录本来没有数据） |
| 打包 | `dist/HeliumTodo-0.3.1-win-x64.zip`，49.5 MB / 247 条目，元数据 `0.3.1.0` / `0.3.1-dev` |

### 二、内存：先分清三个数字

同一时刻能读出三个不同的"内存"，混着说就会吵架：

| 名字 | 含义 | 本应用（空配置、软件渲染） |
| --- | --- | ---: |
| 工作集 | 当前映射的物理页 | 178.2 MB |
| 私有字节 | 已提交的私有内存 | 78.2 MB |
| **私有工作集** | 任务管理器「内存」列 | **47.7 MB** |

"跑起来 170 MB"说的是**工作集**。启动曲线 1 s 105 → 5 s 146 → 8 s 178 → 20 s 持平，无泄漏。

### 三、优化①：默认改成软件渲染（这轮最大一笔）

查模块表才看明白 ANGLE 干了什么——它把整套 NVIDIA 驱动映射进进程：

| 模块 | 大小 | 是什么 |
| --- | --- | ---: |
| `nvgpucomp64.dll` | 94.1 MB | GPU 着色器编译器 |
| `nvwgf2umx.dll` | 85.6 MB | D3D 用户态驱动 |
| `nvppex.dll` | 7.2 MB | 后处理扩展 |
| `av_libGLESv2.dll` | 5.2 MB | ANGLE 本体 |
| `d3dcompiler_47.dll` | 4.5 MB | HLSL 编译器 |

界面是纯 2D 卡片 + 180 ms 动画，Skia 的 CPU 光栅化足够。对照：

| 后端 | 工作集 | 私有字节 | 私有工作集 | 线程 | 句柄 |
| --- | ---: | ---: | ---: | ---: | ---: |
| ANGLE（原默认） | 208.1 MB | 256.9 MB | 68.6 MB | 84 | 846 |
| **Software（现默认）** | **178.2 MB** | **78.2 MB** | **47.7 MB** | **19** | **481** |

省下 **−177.8 MB 私有字节、−65 个线程、−364 个句柄**。视觉上等价（两种后端各抓一张图，
像素差 > 8 的只占 0.45%，全在文字抗锯齿上）。想换回去给个环境变量就行：

```powershell
$env:HELIUM_RENDER = "angle"    # software（默认）/ angle / wgl / vulkan / auto
```

不写进 `config.json` 的原因：渲染后端必须在 Avalonia 初始化之前定，那时配置还没读。

### 四、优化②：动态壁纸限额

40 帧 GIF 在 1920 长边下，每帧 8.3 MB × 二十多帧全驻留。改成"动图 1280 长边 + 96 MB 帧预算"：

| 场景 | 工作集 | 私有字节 |
| --- | ---: | ---: |
| 无壁纸 | 178.2 MB | 78.2 MB |
| 1920×1080 / 40 帧（改前） | 332.3 MB | 236.6 MB |
| **1280 / 最多 26 帧（改后）** | **237.3 MB** | **144.6 MB** |

壁纸额外开销 +154 MB → **+63 MB**。静态图仍是 1920（只占一帧，不心疼）。

### 五、试过但没用（写下来免得后人再试）

| 手段 | 结果 |
| --- | --- |
| `DOTNET_GCConserveMemory=7/9` | 稳定值完全相同 |
| `InvariantGlobalization` | 本机根本没加载 ICU |
| 关掉提醒/托盘线程 | 只省几百 KB + 2 线程，而它们是后台常驻的本体 |

### 六、自检修掉的两个隐患

| 位置 | 问题 | 修法 |
| --- | --- | --- |
| `HeliumTodo.App/ViewModels/FocusViewModel.cs:375` | `async void OnFocusCompleted` 没有 try/catch，写库抛异常会直接带走进程 | 包 try/catch + 日志 |
| `HeliumTodo.App/Services/WallpaperService.cs` → `App.axaml.cs` | 隐藏到托盘后壁纸逐帧定时器仍在跑，后台空耗 | 隐藏时 `Stop()`，`RestoreMainWindow()` 里 `Start()` |

静态扫描结果：`TODO`/`FIXME`/`HACK`/`XXX` 零命中，空 `catch {}` 零命中，
`async void` 6 处全是事件处理器（其中只有上面那处缺 try/catch）。

### 七、新增工具

| 工具 | 用途 |
| --- | --- |
| `tools/probe-memory.ps1` | `GetProcessMemoryInfo` 采三个内存数字 + 线程/句柄 + 模块明细 |
| `tools/capture-window.ps1` | `PrintWindow(hwnd, hdc, 2)` 抓真实窗口（不传 flag 2 会全黑） |
| `TECH_STACK.md` | 技术栈总表、依赖图、运行时画像、质量体系、已知限制 |

两个 PS 脚本都踩到同一类坑：`$hit = $x | Where-Object {...}` 在**只有一个匹配**时返回字符串
而不是数组，`$hit[0]` 取到的是**首字符**（`"libEGL"[0]` 得到 `l`），输出成 `✓ l (0 MB)`。
正确写法 `@($x | Where-Object {...})`。

### 八、验证

| 检查 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | 141 / 141 |
| 九个 `--check`（shell/shell-check/refresh/room/backup/folder/timeline/reminder/window） | 全部 `exit=0` |
| `--shell` 渲染 7 页 | 7 / 7 |
| 内存采样 | 20 秒持平，无泄漏 |
| `--tree` / 模块明细 | 软件渲染下无 `libEGL` / `d3d11` / `opengl32` |

> 沙箱里托盘图标加不上（`Shell_NotifyIconW` 恒返回错误码 5，该会话没有可交互通知区域），
> 真实桌面上才看得到，这条一直只靠代码路径与降级逻辑保证。

---

## 2026-11-01 · 渲染后端可选 + MP4 动态壁纸 + 30 帧/秒上限

两个需求：①「渲染后端改成 software 之后，干脆在设置里加一个"底层设置"，能选 OpenGL /
Avalonia 自带渲染器 / 软件 / Vulkan」②「动图支持 MP4，并且实际渲染时把帧率锁在 30 帧以下」。

### 一、渲染后端的可配置化

之前渲染后端是**写死的**（`App/Program.cs` 里默认 `Software`，想换要设环境变量
`HELIUM_RENDER`）。现在变成「配置文件 + 环境变量」两级：

| 层 | 位置 | 优先级 |
| --- | --- | --- |
| 环境变量 | `HELIUM_RENDER` | 高（调试/排障用，会盖掉界面选择） |
| 配置文件 | `config.json` 的 `RenderBackend` | 中（界面里选的就是写这里） |
| 兜底 | `RenderBackendSettings.Default` = `software` | 低 |

四档取值与界面上显示的文字：

| 值 | 界面文字 | 实际后端 |
| --- | --- | --- |
| `wgl` | OpenGL | `Win32RenderingMode.Wgl` |
| `auto` | Avalonia 自带渲染器（默认） | `Win32RenderingMode.AngleEgl` —— Avalonia 自己的默认选择 |
| `software` | 软件渲染 | `Win32RenderingMode.Software` |
| `vulkan` | Vulkan | `Win32RenderingMode.Vulkan` |

注意「Avalonia 自带渲染器（默认）」指的**不是**本程序的默认（本程序默认软件渲染），
而是 Avalonia 这个框架自己的默认方案（ANGLE → D3D11）。文案按用户要求保留「（默认）」字样。

界面：设置页新增可折叠的「底层设置」，描述写「如非遇到渲染问题，请勿调整！」，里面只有一个
下拉框，Footer 注明「改完需要重启程序才生效。」当前生效值旁边会显示来源：

- 被环境变量强制时：`当前由环境变量 HELIUM_RENDER=angle 指定，配置文件里的选择暂不生效。`
- 正常时：`当前正在使用：软件渲染。选择"Vulkan"后需要重启程序。`

### 二、为什么渲染后端没法"热切换"

Avalonia 的渲染后端必须在**平台初始化之前**确定，而那时 `App.OnFrameworkInitializationCompleted`
还没跑、`ConfigService` 还没读配置。所以 `Program.cs` 里的 `CreateWin32Options()` 只能：

1. 先看环境变量；
2. 没有的话用 `RenderBackendChoice.PeekConfigured()` —— 只把 `config.json` 当纯 JSON
   读一个字段（`JsonDocument.Parse`），**不**走 `ConfigService.Load()`（那个会在首次运行
   时写配置文件，等于在启动路径上多一次 IO 和副作用）；
3. 都没有就用 `software`。

读取失败（文件不存在、JSON 坏了、字段是别的类型）一律静默回落，绝不在启动路径上抛异常。

### 三、四个后端的实测对照

用 `tools/probe-render-backend.ps1` 起真进程、8 秒后查模块加载情况：

| 配置 | 关键模块 | 线程 / 句柄 |
| --- | --- | --- |
| `software` | 只有 `libSkiaSharp`；`av_libGLESv2`、`libEGL`、`d3dcompiler_47`、`opengl32`、`nvgpucomp64` 全未加载 | 19 / 478 |
| `auto` | `av_libGLESv2` ✓、`d3dcompiler_47` ✓、`nvgpucomp64` ✓、驱动 `nvwgf2umx.dll`/`nvppex.dll` | 84 / 852 |
| `wgl` | `opengl32` ✓、`nvgpucomp64` ✓ | — |
| `vulkan` | `opengl32` ✓、`nvgpucomp64` ✓、Intel `igdgmm2_64.dll`/`igddxvacommon64.dll` | — |
| `software` + `HELIUM_RENDER=angle` | ANGLE 模块全部加载 | 84 / 851 |

最后一行是**环境变量优先级**的实测证明：配置文件写 `software`，环境变量设 `angle`，
起来的是 ANGLE。

### 四、MP4 动态壁纸

原来壁纸只走 `SKCodec`（PNG / GIF / APNG / JPG / WebP）。MP4 得另找解码器：

- **没有引第三方库**（FFmpeg、libmpv 都要么大要么得自己编）；
- 走 **Windows Media Foundation**（系统自带 `mfplat.dll` / `mfreadwrite.dll`），
  零打包体积、零授权问题，代价是只能在 Windows 上用 —— 非 Windows 走
  `PlatformNotSupportedException("视频壁纸目前只支持 Windows 系统。")`，界面会显示这条错误。

`App/Services/MediaFoundationVideo.cs`（约 690 行，`internal static`，纯 COM Interop）：

1. `MFStartup(0x00020070, 0)` 一次；`MFCreateAttributes` 上开
   `MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING`（不开的话源读取器只会吐 NV12，
   自己写 YUV→RGB 既慢又容易偏色）+ 关 DXVA（软解更稳，且这里只要帧不要速度）；
2. `MFCreateSourceReaderFromURL` 打开文件，选出第一条视频流；
3. 请求 `RGB32` 输出类型 —— 这一步踩了三个坑，见下面「踩坑」；
4. `ReadSample` 循环取帧，每帧拷进 `SKBitmap`（RGB32 是**自下而上**的 DIB，
   要按 `(height-1-y)*stride` 倒着拷；第 4 个字节 alpha 统一写 255，否则整张壁纸透明）；
5. 按 `MinFrameGap` 跳帧、按预算压帧数，算出每帧的停留时长。

`WallpaperDecoder.DecodeFile()` 是新的统一入口：视频走 MF，其它走原来的
`SKCodec` 路径，`WallpaperService` 只调这一个方法。

### 五、30 帧/秒上限放在哪

上限落在两个地方，都用同一组常量（`Core/Media/PlaybackLimits.cs`）：

| 常量 | 值 | 为什么是这个数 |
| --- | --- | --- |
| `MaxFramesPerSecond` | 30 | 需求 |
| `MinFrameIntervalMs` | **34** | 1000/30 = 33.33，取 33 会变成 30.3 帧/秒，必须向上取整 |
| `MaxFrameIntervalMs` | 10 000 | 防止素材里写了个"停 1 小时"的帧把定时器挂死 |
| `FallbackFrameIntervalMs` | 100 | 间隔缺失/为 0 时用它 |

1. **解码侧**（`MediaFoundationVideo`）：距上一保留帧不足 34 ms 的直接丢掉，不浪费内存；
2. **播放侧**（`WallpaperService.ScheduleNextFrame()`）：`NextFrameDelayMs` 把解码出来的
   原始间隔夹一遍再交给定时器。这一层才是真正兜底的 —— GIF 里写 20 ms、2000 ms 都拦得住。

`PlaybackLimits` 是纯常量 + 纯函数，所以能直接单测（5 个用例）；`RenderCheck` 里还会打印
「原始间隔 X–Y ms → 实际播放 A–B ms」，一眼能看出夹没夹。

### 六、踩坑

| # | 症状 | 根因 | 修法 |
| --- | --- | --- | --- |
| 1 | `CS0246: 未能找到类型或命名空间名"Win32RenderingMode"` | 它在 `Avalonia` 命名空间下，不在 `Avalonia.Win32`（直觉错了） | `using Avalonia;` |
| 2 | MP4 打开就 `E_INVALIDARG`（"解析视频失败：参数错误"） | 开了 `MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING`，本机媒体管线不认 | 不开；只要普通的 `ENABLE_VIDEO_PROCESSING` |
| 3 | 想让 MF 顺便缩放到 1280 长边，`SetCurrentMediaType` 恒返回 `0xC00D36B4`（`MF_E_INVALIDMEDIATYPE`） | 视频处理器不接受"换分辨率"的输出类型（改当前类型、新建类型两种写法都不行） | 只把子类型改成 RGB32、尺寸保持原样，拿到帧后自己用 Skia 缩 |
| 4 | 4500×3000 的 mp4 当壁纸，解码期工作集冲到 **3007 MB** | 每帧 `IMFSample` 等 GC 回收，MF 的画面池越积越多 | 每帧 `Marshal.ReleaseComObject(sample)`（`try/finally`）→ 峰值降到 **1510 MB** |
| 5 | `Guid` 传不进 COM 方法（`ref` 实参编译不过） | 我把它写成了 `static Guid X { get; }` 只读属性 —— 属性永远接不了 `ref` | 改成可写 `static Guid X = new("…")` 字段 |
| 6 | 自检里 `--wallpaper 路径` 完全不生效、像没跑 | 参数解析只认 `--wallpaper=路径`，空格分隔被静默忽略 | 用等号；顺手在自检里把壁纸段的结果打得更明显 |
| 7 | 探针脚本报「字符串缺少终止符」 | 含中文的 `.ps1` 存成了 UTF-8 无 BOM，Windows PowerShell 5.1 按 GBK 读 | 存 UTF-8 **带 BOM**（`encoding='utf-8-sig'`） |
| 8 | `HELIUM_CONFIG_DIR` 指到 `%TEMP%` 时启动即 `SQLite Error 14: 'unable to open database file'` | 本机沙箱对子进程写 `%TEMP%` 有限制（PowerShell 能写、程序不能），非产品 bug | 探针改用脚本旁边的目录 |

### 七、实测数据

| 场景 | 结果 |
| --- | --- |
| 测试视频（`oobe-intro.mp4`，源 4500×3000） | 输出 1280×852、保留 15 帧、帧间隔 133 ms（素材本身 7.5 帧/秒，够不着 30 上限） |
| 造一个 20 ms/帧的 GIF | 「原始间隔 20–20 ms → 实际播放 34–34 ms（最快约 29.4 帧/秒）」= 上限生效 |
| MP4 壁纸稳态内存 | 253 / 149 / 47 MB（工作集 / 私有 / 私有工作集），比无壁纸多约 75 MB |
| MP4 壁纸解码期峰值 | 1510 MB（MF 画面池 + 15 帧 1280×852 + 模糊副本）；素材越大越高，见「已知限制」 |
| 真实窗口截图 | `docs/screenshot-mp4-wallpaper.png`，均值 231.2 vs 无壁纸 239.2 → 背景确实画出来了 |

### 八、已知限制

- **解码期峰值内存与源尺寸正相关**：4500×3000 的素材峰值 1.5 GB。壁纸是可选功能，
  且限了"最长边 1280 + 96 MB 帧预算"，但 MF 自己的画面池不受我们控制；
  真遇到再考虑「先降到 720p 再解码」。
- **视频帧的强调色偶尔取不到**：`ExtractAccent` 对深色渐变画面会判定失败、回落 `#4B6B8B`。
  不影响显示，只是壁纸取色那条路对视频不太灵。
- **视频壁纸只在 Windows 可用**（Media Foundation 是 Windows 组件）。

### 九、验证

| 检查 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **153 / 153**（141 + 渲染后端 7 + 播放限制 5） |
| `--shell` 渲染 7 页 | 7 / 7 |
| 九个 `--check` | 全部 `exit=0` |
| `tools/probe-render-backend.ps1` | 四档后端模块加载全部符合预期，环境变量优先级已验证 |

---

## 2026-11-01 · 改渲染后端先问一句「要不要现在重启」，工作流方块让出刻度

### 需求原文

> 再增加切换后端渲染时重启应用的确认选项，提示语"更改此项需要重启，是否立即重启"
> 并排两个选项"确认"和"取消更改"
> 并缩进工作流中显示日程的部分，便于查看时间轴对应的时间

第二条是看了截图才明白的：工作流里排到下午的日程方块**整行压在刻度文字上**，
14:00 / 16:00 / 18:00 被盖掉一半，看不出方块对应的是几点。

### 一、改后端 → 弹窗 → 重启

| 文件 | 改动 |
| --- | --- |
| `Core/Configuration/RenderBackendSettings.cs` | 新增 `NeedsRestart(current, selected)`，两个值 `Normalize` 之后比较 |
| `App/ViewModels/SettingsViewModel.cs` | 选中后端时**不写配置**；记下 `_pendingRenderBackendKey` 并弹浮层；新增 `ConfirmBackendRestart` / `CancelBackendChange` 两个命令 |
| `App/App.axaml.cs` | 新增 `TryRestart()`：放锁 → 起新进程 → 关自己 |
| `App/Views/SettingsView.axaml` | 页内遮罩浮层：提示语 + 「确认」「取消更改」并排两个按钮 |
| `Tests/RenderBackendSettingsTests.cs` | 新增 2 个测试（同值不需要重启 / 不同值需要重启） |

三条语义，写清楚免得以后改回去：

1. **选中的那一刻不写配置。** 写下去就撤回不了，用户点「取消更改」时配置已经是新值了。
2. **「确认」= 保存 + 立刻重启。** 保存走 `_configService.Save()` 而不是 `ScheduleSave()`，
   后者有 500 ms 防抖，进程都要退了，防抖还没到点。
3. **「取消更改」= 一个字都不写。** 下拉框退回原值，状态文字刷新，仅此而已。

### 二、时间轴方块缩进 54 像素

刻度文字和日程方块本来是两个 `Canvas`，横坐标一个是 XAML 里的字面量、一个是
ViewModel 算出来的数 —— 于是就有了「方块压住刻度」。这次把它们收到一个来源：

| 常量（`App/ViewModels/TimelineMetrics.cs`） | 值 |
| --- | ---: |
| `LabelWidth` | 46 |
| `AxisGap` | 8 |
| `LeftInset` | **54** |

`DashboardView.axaml` 的 `LabelMargin` 改成 `{x:Static vm:TimelineMetrics.LabelWidth}`，
`DashboardViewModel` 调 `Resize(..., TimelineMetrics.LeftInset)` —— 网格画的宽和方块让的宽
从此是同一个数，改一处两边一起动。

`--timeline-check` 加了 5 条横向断言：刻度宽度 = `LabelWidth`、第一列 X = 54、
列宽 = `(1000 - 54) / 2 = 473`、真实方块最小 X = 54（没压住刻度）、
最大右边缘 = 画布宽（没溢出）。

### 三、`--restart-prompt-check`：十四项断言

专治"看起来弹了、其实逻辑是错的"：

| 断言 | 抓什么 |
| --- | --- |
| 初始不显示弹窗 | 页面一打开就弹 = 吓人 |
| 改后端会弹重启提示 | 主路径 |
| **弹窗出现时配置还没被改写** | 这条是灵魂：先写配置的实现在这里就露馅 |
| 有「确认」按钮 / 有「取消更改」按钮 | 按钮没被 XAML 写丢 |
| 两个按钮并排 | 用 `Bounds.Y` 相同、`Bounds.X` 不同来判断，不靠截图 |
| 取消后弹窗关闭 | |
| 取消后下拉框退回原值 | 回填逻辑 |
| 取消后配置没被改写 | 和上面那条互相印证 |
| 选回原值不弹窗 | `NeedsRestart` 判等没写反 |

「确认」那一支一开始是不敢验的 —— 它会真的开新进程，自检里干这个只会开出一堆实例。
后来加了一个只给自检用的开关：`HELIUM_NO_RESTART=1` 时 `RestartApplication()`
直接返回"成功"，于是也能把这条主路径压到断言里：

| 断言 | 抓什么 |
| --- | --- |
| 再选不同后端仍会弹提示 | 取消过一次之后状态没被弄脏 |
| 确认后弹窗关闭 | |
| 确认后配置写成了新后端 | |
| **确认后配置已落盘（不是等防抖）** | 直接读 `config.json` 找 `"RenderBackend": "wgl"`；走 `ScheduleSave` 的实现会在这里挂掉 |

`SaveConfigNow()`（停防抖 + 立刻 `Save()`）因此被单独提了出来，
「确认重启」和「自检复原」两处共用。

### 四、本轮踩的坑

| 坑 | 现象 | 根因 | 修法 |
| --- | --- | --- | --- |
| `RenderCheck` 里用 `RenderBackendSettings` 报 `CS0103` | 「当前上下文中不存在名称"RenderBackendSettings"」 | 文件里没 `using HeliumTodo.Core.Configuration;`（之前 `PlaybackLimits` 用的是全限定名，把这个坑盖住了） | 补 `using` |
| `--output` 传了等于没传 | 输出目录还是默认的 `output/render` | 参数解析只认 `--名字=值`，空格分隔的形式被静默忽略（`--wallpaper` 也一样） | 写成 `--output=output/plain` |
| 遮罩「看起来没生效」 | 截图里页面像没压暗 | 其实是压暗了：同一像素 251 → 151（`#66` 黑 ≈ ×0.6），缩略图看走了眼 | 用 PIL 取像素对比，不靠眼睛 |
| 造测试日程插入失败 | `NOT NULL constraint failed: Schedules.RecurrenceRule` | 表结构与记忆不符：`RecurrenceRule` 非空、时长列叫 `Duration`（`'01:40:00'`） | 给 `''`、列名改对 |

### 五、回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **155 / 155** |
| `--shell` 渲染 7 页 | 7 / 7 |
| 九个 `--check`（含新增的 `--restart-prompt-check`） | 全部 `exit=0` |
| `--timeline-check` 横向断言 | 5 / 5 通过 |
| `架构.md` §19 | 新增「时间轴横向」与「改后端重启」两节 |

---

## 2026-11-01 · 在 Visual Studio 里点「重启」，把 exe 占住了（MSB3027）

### 现象（用户报的）

> 在 visual studio 中调试修改部分描述文件时，第一次编译完成后修改了后端并重启软件后，
> visual studio 显示调试已经关闭，但软件窗口正常弹出，且检查后未在系统统一展示后台应用的地方发现软件，
> 第二次尝试编译发生如下错误

错误列表里两条：

```
MSB3027 无法将 obj\Debug\net10.0\apphost.exe 复制到 bin\Debug\net10.0\HeliumTodo.App.exe。
        超出了重试计数 10。失败。文件被"HeliumTodo.App (17564)"锁定。
MSB3021 ... 文件正由另一进程使用，因此该进程无法访问此文件。
```

### 根因

不是"重启功能坏了"，是**在调试器里自助重启**这件事本身不成立：

1. VS 调试时，那个进程是**调试器管的子进程**；
2. 我们的 `TryRestart()` 又 `Process.Start` 了一个新的 —— 这个新进程跟调试器没关系；
3. 旧进程（调试器的子进程）退出，VS 认为"调试结束了"；
4. 新进程还活着，并且抓着 `bin\Debug\net10.0\HeliumTodo.App.exe` 和同目录的 dll；
5. 于是下一次编译重试十次都拿不到文件，报 MSB3027。

更要命的是第 4 步那个进程**藏起来了**：用户点过窗口的 ×（或者窗口本来就被关掉了），
我们的关闭拦截把它送到后台，只有托盘图标能叫回来 ——
而用户在"系统统一展示后台应用的地方"（启动应用列表）里找，那里本来就不会有它。

现场核对（本机实测）：

```
Id    : 17564
Start : 2026/11/1 19:55:59
Path  : D:\...\HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe
Parent: 12504（已经退出了，所以这是个"孤儿"）
```

杀掉之后 `dotnet build` 立刻回到 0 警告 0 错误 —— 与代码无关，纯占用。

### 修法

**调试器在场时一律不自己重启**，把这件事交回给调试器的「重新启动」（Ctrl+Shift+F5）。

| 文件 | 改动 |
| --- | --- |
| `Core/Lifecycle/RestartPolicy.cs`（新增） | `RestartOutcome { Started, BlockedByDebugger, Failed }` + 纯函数 `Decide(bool) => debuggerAttached ? BlockedByDebugger : Started` |
| `App/App.axaml.cs` | `TryRestart()` 改成返回 `RestartOutcome`；第一件事判断 `Debugger.IsAttached`（另给自检留了 `HELIUM_FAKE_DEBUGGER=1`），命中就记一行日志、原样返回，不起进程、不放锁、不退出 |
| `App/ViewModels/SettingsViewModel.cs` | `RestartApplication()` 返回 `RestartOutcome`；`ConfirmBackendRestart` 按三种结果分别给提示 —— 调试器那一档明说"自动重启会把调试器甩掉、还会占住 exe 让下次编译失败"，并指到 Ctrl+Shift+F5 |
| `Tests/RestartPolicyTests.cs`（新增） | 3 个测试：调试器在场必须 Blocked、不在场必须 Started、`Failed` 不能被合并进另外两档 |
| `RenderCheck` `--restart-prompt-check` | 增加调试器分支，三条断言（见下） |

`--restart-prompt-check` 现在 17 项：

| 新增断言 | 抓什么 |
| --- | --- |
| 调试器里确认后配置照样存盘 | 用户的"确认"不能因为不能重启就白点 |
| 调试器里不会偷偷开新进程 | 数 `HeliumTodo.App` 进程数，前后必须相等（实测 0 → 0） |
| 调试器里提示要用调试器的「重新启动」 | 提示语里必须有「调试」，否则用户不知道该干嘛 |

### 顺手记两条经验

- **`MSB3027` 先查占用进程，再看代码。** 报错信息里已经写了 PID，
  `Get-CimInstance Win32_Process -Filter "Name='HeliumTodo.App.exe'"` 能看到 `ParentProcessId`
  —— 父进程没了（12504）、子进程还活着，就是"孤儿"，直接说明问题出在自助重启。
- **"系统统一展示后台应用的地方"指的是「启动应用」列表**，那里放的是开机自启项；
  托盘程序不在那儿。要证明进程还在，只能看任务管理器或进程列表 —— 这也是这次
  最迷惑人的一点：界面上哪儿都看不到它，但文件确实被它锁着。

### 回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误（杀掉孤儿进程之后） |
| `HeliumTodo.SelfTest` | **158 / 158** |
| `--restart-prompt-check` | 17 / 17 |
| 其余八个 `--check` + `--shell` | 全部 `exit=0` / 7 页正常 |

## 2026-11-01 · 设置页提示条压住内容 / 设置卡片圆角统一

用户报了两个界面问题（原话见下），都是"看起来像层级错乱"的观感问题：

> 「另外，窗口层级也出现了问题，设置界面上方横幅的界面优先级出现了问题」
> 「在修完层级的bug之后将设置界面选项的R角与前三项调整至一致」

### 一、提示条压住内容（真问题）

**现象**：设置页顶部那条绿色提示（"已取消更改，渲染后端仍是…"）浮在页面上，
底下的「外观」卡片标题和说明文字透了出来，两行字叠在一起。

**根因**：上一版把它做成了"叠在内容上方的浮层"——
`Grid` 里和 `ScrollViewer` 并列、`VerticalAlignment="Top"`。
但 `FAInfoBar` 的背景刷子本身带透明通道（Fluent 的提示条是半透明的），
浮层下面有文字时就会一层层透上来。**层级其实是对的，错的是"叠"这个做法本身**：
FluentAvalonia 自己在 `FAInfoBar` 的注释里就写着
"will take up space in a layout and will not cover up other content"，
提示条天生是占位的，不是浮层。

**修法**：`Grid RowDefinitions="Auto,*"` —— 提示条放第 0 行、滚动内容放第 1 行。
两件事一起做才成立：

| 做法 | 解决什么 |
| --- | --- |
| 放在 `ScrollViewer` 外面 | 页面滚到底时点按钮，提示仍然可见（这是更早那版的需求） |
| 自己占一行（`Auto`） | 加上这一行之后内容被往下推，谁也不盖谁 |

`Auto` 行还有个好处：`FAInfoBar` 没打开时自身高度为 0，整行塌成 0，
不会在页面顶部留一条空白 —— 这一点写进了自检断言。

### 二、设置页卡片圆角统一成 18

**现象**：设置页那些"卡片"（外观 / 背景壁纸 / 专注 …）是直角，
和待办 / 专注 / 日程页用 `Border.card` 画出来的 18 圆角卡片不是一套。

**根因**：设置页用的是 `FASettingsExpander`，它的模板里没有圆角，
控件本身也**没有 `CornerRadius` 属性**（查过 `FluentAvalonia.xml`：
公开属性只有 `Description` / `IconSource` / `Footer` / `IsExpanded` / `ActionIconSource` /
`IsClickEnabled` / `Command` / `CommandParameter`）。想靠一个 setter 改圆角是改不动的。

**修法**：外面套一层 `<Border CornerRadius="{DynamicResource RadiusCard}" ClipToBounds="True">`，
10 个 expander 全部套上（含「关于」）。三点经验：

- `ClipToBounds="True"` 是必须的 —— 不裁剪的话 expander 自己的直角背景会从圆角外壳里冒出来。
- 提示条没有 `CornerRadius` 属性，同一个套路：`Border` + `ClipToBounds`。
- 圆角一律用 `DynamicResource RadiusCard`（18），和 `Border.card` 共用同一个令牌，
  以后调圆角只改 `DesignTokens.axaml` 一处。

### 三、新增自检 `--notice-check`（5 项）

几何断言比肉眼看图靠谱 —— 缩略图里"透上来的字"和"正常渲染的字"几乎分不出来：

| 断言 | 判据 |
| --- | --- |
| 设置页里有提示条 | 按 `DataContext is SettingsViewModel` 找，外壳自己那条不算 |
| 收起时不占高度 | 高度 `< 0.5`（`Auto` 行必须塌成 0） |
| 能正常显示 | 高度 `> 8` |
| 不压住任何滚动内容 | 对页面里每个可见 `ScrollViewer` 做矩形相交测试，重叠面积必须为 0 |
| 内容被推到下面 | 滚动容器顶边 ≥ 提示条底边（实测两边都是 116） |

**坑**：`window.GetVisualDescendants().OfType<FAInfoBar>()` 第一个找到的是**外壳**里的提示条
（`ShellView.axaml` 标题栏那一行），按 `DataContext` 过滤才是设置页这一个。
第一次跑就是因为它而报"高度 0"，白白多查了一轮。

### 回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **158 / 158** |
| `--notice-check` | 5 / 5（新增） |
| 其余九个 `--check` + `--shell` | 全部 `exit=0` / 7 页正常 |
| 截图 | `docs/screenshot-settings-notice.png`（提示条在独立一行、四角与下面卡片同半径） |

---

## 2026-11-02 · 六个页面统一分区：模块名移出功能区

### 需求原话

> 统一所有界面的分区，以主界面为模板分区修改，将模块名称那一行移出功能区，独立显示在最上方（如主界面那种）

### 改前的样子

只有「今日工作流」是**标题在卡片外**的：页面顶部单独一行，左边是 16px 标题 + 日期 + 概览，
右边是翻页按钮，下面才是两张卡。其余五个页面都把标题写在**卡片头**里：

| 页面 | 改前标题位置 |
| --- | --- |
| 待办事项 | 卡片头第一行，右侧跟三个统计 |
| 日程 | 卡片头左侧，概览文字在标题下面另起一行 |
| 专注 | 完全没有页面标题，每张卡自己带「本次专注」「预订专注时长」等卡头 |
| 自习室 | 没有页面标题，顶上是联机状态提示条 |
| 设置 | 没有页面标题，十个 expander 各自带分组名 |

切页时标题的位置、字号所在的层级、"标题离内容多远"全都不一样。

### 改后的统一结构

```xml
<Grid RowDefinitions="Auto,*" Margin="12,4,12,12">
  <Grid Grid.Row="0" ColumnDefinitions="*,Auto" Margin="4,0,4,10">  <!-- 模块名那一行 -->
    <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="10">
      <TextBlock Text="待办事项" Classes="cardHeader" />
      <TextBlock Text="{Binding ...}" Classes="caption" />           <!-- 概览，可不写 -->
    </StackPanel>
    <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="14">
      …统计数字 / 动作按钮…
    </StackPanel>
  </Grid>
  <Border Grid.Row="1" Classes="card">…功能区…</Border>
</Grid>
```

六个页面各自的标题行内容：

| 页面 | 左侧 | 右侧 |
| --- | --- | --- |
| 今日工作流（模板，未改） | 今日工作流 + 日期 + 概览 | ◀ 今天 ▶ + 刷新 |
| 待办事项 | 待办事项 | 未完成 / 今天完成 / 全部 三个数字 |
| 日程 | 日程 + 概览 | 今天 / 未来 7 天 / 今天占用 + 「＋ 新建」 |
| 专注 | 专注 + 今天 N 分钟 · N 次 | — |
| 自习室 | 自习室 + N 人在线 | — |
| 设置 | 设置 + 一句话说明 | — |

### 三个必须一起改的细节

1. **卡片头的行号要整体上移一格。**
   待办页卡片原来是 `RowDefinitions="Auto,Auto,Auto,*"`（卡头 / 快速添加 / 筛选 / 列表），
   卡头搬走后变成 `"Auto,Auto,*"`，原来的 `Grid.Row="2"` `Row="3"` 依次减一。
   日程页同理（编辑区 `1→0`、列表 `2→1`）。
2. **快速添加行去掉上边框。**
   它原来是 `BorderThickness="0,1,0,1"` —— 上面那条线是"卡头与添加行之间的分隔线"，
   卡头搬走后这条线就贴在卡片顶边上，看着像卡片被切开。改成 `"0,0,0,1"`。
3. **概览数字统一横排。**
   日程页原来是「数字在上、单位在下」的两行小卡，把标题行撑到 40px 高；
   改成和待办页一样 `数字（metric 16）+ 单位（caption）` 的水平 `StackPanel Spacing="5"`，
   六个页面切来切去卡片顶边才在一条线上。

### 提示条收起时别留空白

专注页和设置页的提示条现在在「模块名」和「功能区」之间占一行。只绑
`FAInfoBar.IsOpen` 的话，条子高度确实塌成 0 了，但**外层那个 `Margin="0,0,0,8"` 还在**，
页面顶部会永远空出 8px。做法是外面套一层带 `IsVisible` 的 `Panel`：

```xml
<Panel Grid.Row="1" Margin="0,0,0,8" IsVisible="{Binding IsNoticeVisible}">
  <fa:FAInfoBar IsOpen="{Binding IsNoticeVisible}" … />
</Panel>
```

`IsVisible=false` 的面板不参与布局，它自己的外边距也一起消失。

### 像素实测（`--shell` 渲染 1400×900 后逐列扫描）

| 页面 | 标题文字 y | 卡片顶边 y | 标题左边 x |
| --- | --- | --- | --- |
| 今日工作流（模板） | 13–27 | 46 | 16 |
| 待办事项 | 8–22 | 36 | 16 |
| 日程 | 12–26 | 45 | 18 |
| 专注 | 8–22 | 36 | 16 |
| 自习室 | 8–22 | 36 | 18 |
| 设置 | 8–22 | 36 | 150 |

- 标题行里放了按钮的页面（今日工作流、日程）行高被按钮撑到 32，卡片自然低十来像素 —— 这是内容决定的，不是没对齐。
- 设置页标题从 **x=150** 起：它的内容块限宽 `MaxWidth="1100"` 并居中，
  **标题跟本页内容的左边缘对齐**，而不是跟页面的物理左边对齐。

### 回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **158 / 158** |
| 十个 `--check`（shell / refresh / room / backup / folder / timeline / reminder / window / restart-prompt / notice） | 全部 `exit=0` |
| `--shell` | 7 / 7 页渲染正常 |
| 文档 | `ARCHITECTURE.md` §19 改成「七个界面细节的约定」并加 §19.5；快照刷新为 **29 142 行 / 123 源文件**；`UI_TEXT_MAP.md` 重生成 |

---

## 2026-11-02 · 功能区下移 30px、标题字号加大、版本升到 0.3.3-alpha

### 需求原话

> 功能区整体下移20~30px,顺带修改版本号为0.3.3-alpha

> 还有功能区标题字幕字体变大，包括那一整行的

### 一、功能区整体下移

标题行（模块名那一行）的下边距从 `10` 改成 `30`，六个页面一起改。
同时给标题行加 `MinHeight="32"` —— 不只是"往下推"，而是让六个页面的**行高**也一致：

| 页面 | 改前卡片顶边 | 改后卡片顶边 |
| --- | ---: | ---: |
| 今日工作流 | 46 | **66** |
| 待办事项 / 专注 / 自习室 / 设置 | 36 | **66** |
| 日程 | 45 | **66** |

66 = `4`（页面外边距）+ `32`（标题行 MinHeight）+ `30`（标题行下边距）。
改前那 36/45/46 的差别来自"行高由内容撑"：标题行里放了按钮的页面行高 32，
只放文字的页面行高 22。加了 `MinHeight="32"` 之后行高统一，卡片顶边才对齐。

### 二、标题行字号加一档

`Styles/Controls.axaml` 新增三个类，只给"模块名那一行"用：

| 类名 | 字号 | 用在哪 |
| --- | --- | --- |
| `pageHeader` | **20** SemiBold | 模块名 |
| `pageCaption` | **14**（透明度 0.62） | 标题右边的概览/字幕 |
| `pageMetric` | **20** SemiBold | 标题行右侧的统计数字 |

原来的 `cardHeader`（16）保留给卡片自己的头。两者拉开一档是有原因的：
模块名代表"整个页面"，卡片头只代表"一张卡"，一样大就看不出层级。

改完实测：六页标题文字都落在 y=11–30（20px 字高），卡片顶边都从 y=66 开始。

### 三、版本号 0.3.3-alpha

`Directory.Build.props` 是唯一源头：

```xml
<Version>0.3.3-alpha</Version>
<AssemblyVersion>0.3.3.0</AssemblyVersion>
<FileVersion>0.3.3.0</FileVersion>
```

同步了 `HeliumTodo.Core/VersionInfo.cs`（含注释里的示例）、`BackupManifest.cs`、
`SettingsViewModel.cs`、`ShellViewModel.cs`、`VersionInfoTests.cs`、`ARCHITECTURE.md`、
`PROJECT_OVERVIEW.md`、`TECH_STACK.md`、`WINDOWS_DEPENDENCIES.md`，以及这份文档
顶部的「当前版本」。

**这条规矩再说一次**：批量替换版本号必须排除 `PROGRESS.md` 的历史小节
（`AVALONIA12_FEASIBILITY.md` 里也有一句"项目还在 0.3.1-dev"，那是当时的实况记录，
同样不动）—— 否则读起来像"0.3.3 的功能写在 0.3.1 的小节里"。

### 回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **158 / 158** |
| 十个 `--check` | 全部 `exit=0` |
| `--shell` | 7 / 7 页渲染正常 |
| 像素实测 | 六页标题 y=11–30、卡片顶边 y=66 |

---

## 2026-11-02 · 设置页宽度与其他页同步、功能区再下移、标题行再加大

### 需求原话

> 将设置功能区的UI宽度设置与其他功能区同步，且再次下移功能区，调大标题行

### 一、设置页的宽度同步

设置页的标题行、提示条、内容 `StackPanel` 三处都自带 `MaxWidth="1100"`，
在 1400 宽的窗口里卡片右边缘落到 x≈1122，比其他页（x=1387）窄一圈，
切换页面能明显看到卡片"缩回去"。

三处 `MaxWidth` 全部删掉，宽度只由页面外边距 `Margin="12,4,12,12"` 决定：

| 页面 | 卡片 x 范围（y=300 处实测） |
| --- | --- |
| 待办事项 | 12 – 1387 |
| 日程 | 12 – 1387 |
| 设置 | **13 – 1386**（改前 13 – 1122） |

顺带把设置页标题从 x=150 拉回 x=16，与其他五页同一条竖线。

### 二、功能区再次下移

标题行 `Margin="4,0,4,30"` → `Margin="4,0,4,44"`，卡片顶边从 **66 → 88**。
六个页面一起改，`MinHeight` 32 → 40（字号抬了，行高也得抬，否则 24px 的字
会把 32px 的行顶满、标题和卡片又贴在一起）。

88 = 4（页面外边距）+ 40（标题行）+ 44（下边距）。

### 三、标题行字号 20 → 24

| 类名 | 改前 | 改后 |
| --- | ---: | ---: |
| `pageHeader` | 20 | **24**（`FontTitleLarge`） |
| `pageCaption` | 14 | **15** |
| `pageMetric` | 20 | **24** |
| `cardHeader` | 16 | 16（不动） |

20px 在 1400 宽的窗口里仍然"缩"在左上角，看不太出这是页面标题；24px 才和
整块功能区配得起来。

### 实测

| 页面 | 标题文字 y | 卡片顶边 y | 标题左边 x |
| --- | --- | --- | --- |
| 今日工作流（模板） | 14–36 | **88** | 16 |
| 待办事项 | 14–36 | **88** | 16 |
| 日程 | 14–36 | **88** | 20 |
| 专注 | 14–36 | **88** | 16 |
| 自习室 | 14–36 | **88** | 19 |
| 设置 | 15–36 | **88** | 16 |

### 一处容易忘的连带修改

`--notice-check` 里的断言「设置内容被推到提示条下面」的实测值也跟着变了：
改前「提示条底边 116，内容顶边 116」，改后「提示条底边 192，内容顶边 200」——
标题行下移让提示条整体下移，断言是相对比较，所以不用改，但看日志时别以为串了。

### 回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **158 / 158** |
| 十个 `--check` | 全部 `exit=0` |
| `--shell` | 7 / 7 页渲染正常 |

---

## 2026-11-02 · 外观三件套：明暗模式 / 强调色优先 / 功能区玻璃浓度

### 需求原话

> 功能区半透明玻璃与顶部标题仅保持一行小三宋体字大小的空隙，支持动态调整控件不透明度
> （无壁纸时默认不透明），主题颜色直接取强调色（即合并主题与强调色且直接以强调色为优先），
> 加入亮色与暗色模式（暗色模式下壁纸自动打上灰色蒙版（10%~20%不透明度），
> 并支持设置中数据修改时的数据动态保存。

### 一、空隙压到一行字

标题行下边距 `44` → **`20`**（小三宋体 15pt ≈ 20px），六个页面一起改，
卡片顶边从 88 回到 **64**。

### 二、主题与强调色合并

以前是"主题下拉（浅色/深色/高对比度…）+ 强调色来源（跟随主题/壁纸/自定义）"两套东西，
互相打架：选了主题又选自定义强调色，到底听谁的说不清。

现在只剩一件事：**颜色由强调色决定**，主题只管明暗。

| 改动 | 位置 |
| --- | --- |
| 新增 `ColorSchemes`（light/dark，纯字符串 + 归一化） | `HeliumTodo.Core/Configuration/ColorSchemes.cs` |
| `AppConfig.ColorScheme`，`ThemeId` 降级为兼容字段 | `HeliumTodo.Core/Configuration/AppConfig.cs` |
| `AccentSource` 默认 `theme` → **`custom`** | 同上 |
| 老配置迁移：没有 `ColorScheme` 字段就按 `ThemeId` 推 | `ConfigService.Load()` |

**迁移那段不是可选优化**：老配置里没有 `ColorScheme`，直接按默认值走就是"亮色"，
用着暗色的老用户升级后会被闪一下白屏。

### 三、功能区玻璃浓度

`AppConfig.CardOpacity`（0–100，默认 100）。`App.ApplyCardOpacity(percent, isDark)`
把 `CardSurfaceBrush` 换成带 alpha 的实色刷子：亮色底 `White`、暗色底 `#1C1C1C`，
alpha = `percent * 255 / 100`。

打开壁纸时自动降到 **82%**（`AppConfig.WallpaperCardOpacity`），关掉再回到 100%；
只在"用户从没动过滑块"时才自动改，他自己设过的值不覆盖。

**这条坑值得单独记**：`CardSurfaceBrush` 原来定义在 `DesignTokens.axaml` 的
`ThemeDictionaries` 三处（Light/Default/Dark）。主题字典的优先级**高于**应用级同名资源，
所以代码里 `Application.Current.Resources["CardSurfaceBrush"] = ...` 写了也白写 ——
界面纹丝不动，而且不报任何错。

修法：主题字典里那三处全删，改在字典外面放一个默认刷子，让代码成为唯一来源。

### 四、暗色模式与灰蒙版

- `AppConfig.DarkWallpaperScrim`（10–20，默认 15），只在暗色 + 有壁纸时生效。
- `App.ApplyDarkWallpaperScrim(percent, isDark)` 写资源 `DarkWallpaperScrimOpacity`；
  亮色时写 `0`，越界值夹回 10~20。
- `MainWindow.axaml` 蒙版层之后多一条 `Background="#808080"` 的 Border 绑这个资源。

### 五、设置项改动即时存盘

- 防抖从 400ms 压到 **150ms**（拖滑块时连续写盘的次数够少、又不至于明显滞后）。
- 新增 `SettingsViewModel.FlushPendingSave()`，并在 `SettingsView` 的
  `OnDetachedFromVisualTree` 里调用 —— 拖完滑块立刻切页时把最后一次改动补写。

### 验证

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **168 / 168**（新增 `ColorSchemesTests` 10 个） |
| 新增 `--appearance-check` | **14 / 14** 通过 |
| 其余十个 `--check` | 全部 `exit=0` |
| `--shell` | 7 / 7 页渲染正常 |

`--appearance-check` 里最关键的一条不是"资源字典里的值对不对"，而是
**页面上真正画出来的那个 `Border.card` 用的刷子 alpha 是不是也跟着变** ——
只看字典是查不出"被主题字典盖住"这种问题的。

像素实测（`HELIUM_SHELL_PAGE=设置` + `--wallpaper`，1400×900 缩到 1280×820 输出）：

| 场景 | 卡片内部 | 壁纸缝隙 |
| --- | --- | --- |
| 亮色 + 壁纸 + 82% | `(236,234,243)`（白 82% 压蓝壁纸） | `(168,168,221)` |
| 暗色 + 壁纸 + 82% + 灰蒙版 | `(87,81,109)` | `(54,54,107)` |

归档：`docs/screenshot-glass-light.png`、`docs/screenshot-glass-dark.png`。

---

## 2026-11-02 · 设置页被"落下"：十张小卡合并成一整张 + 版面自检

### 用户报告

> 「你在修改的时候老是将设置界面给拉下」

连着提了两次。之前几轮我都按"某项数值没跟上"去查（顶边、宽度、字号都量过、都对得上），
这次不再猜，直接把六页截图的**整行像素**拉出来对比。

### 根因：不是参数错，是结构错

`HeliumTodo.App/Views/SettingsView.axaml` 里十个分组各自是一张独立小卡：

```xml
<StackPanel Spacing="12">
  <Border CornerRadius="{DynamicResource RadiusCard}" ClipToBounds="True">
    <fa:FASettingsExpander Header="外观" … />
```

其他五页都是**一整块**功能区卡片。切到设置页时，卡片之间会露出 12px 页底色，
一眼就能看出"这不是同一个页面的排版"。像素剖面证据（x=700 竖线，改前）：

| 页面 | 页底色段 |
| --- | --- |
| 待办 / 日程 / 专注 / 工作流 | `(60,63)`（只有标题与卡片之间那一条） |
| 设置 | 卡片之间有 9 段 12px 的页底色 |

### 修法

`ScrollViewer` 外面套一张卡，十处内层外壳的 `CornerRadius` 全部归零：

```xml
<Border Grid.Row="2" Classes="card" Padding="0" ClipToBounds="True">
  <ScrollViewer>
    <StackPanel Margin="0" Spacing="0">
```

改后设置页的页底色段只剩 `(60,63)`，与别页完全一致；卡片 x 范围 12–1387。

### 顺带修掉的两处

1. **自习室卡片低 60px**：它把"局域网联机"状态做成常驻 `FAInfoBar` 单独占一行，
   卡片顶端落在 y=124（别页 64）。改成把状态收进卡片第一行（`cardHeader` + `caption`）后齐平。
2. **自习室"没进房间"时卡片断在半空**：创建/加入卡原来 `VerticalAlignment="Top"`，
   到 y=262 就结束、下面一大片页底色。去掉 `VerticalAlignment` 让它跟着行高拉伸。

### 新增 `--layout-check`（6 页 × 6 项）

| # | 断言 | 判据 |
| --- | --- | --- |
| 1 | 左边缘 12 | 顶行并集 `Min(X) == 12` |
| 2 | 右边缘 宽-12 | 顶行并集 `Max(X+W) == 1388` |
| 3 | 顶端 64 | `Min(Y) == 64` |
| 4 | 顶行只有列间距那一条缝 | `Σ间隙 == 12 × (卡数-1)` |
| 5 | 页面标题字号 24 | `TextBlock.pageHeader.FontSize == 24` |
| 6 | 标题与卡片左边缘差 4 | `|标题X-卡片X| == 4` |

外加"卡片之间没有漏出底色"：整行 95% 以上是页底色且连续 ≥3 行才算缝，
且只统计"下面还有卡"的缝。

### 两个坑

- **单点竖线扫描判缝会误报**：输入框/内嵌面板底色可能正好等于页底色。
  自习室一开始被判了 2 处假缝，改成"整行采样 24 点、95% 命中"才干净。
- **`CopyPixels` 要非托管指针**，而这个项目没开 `AllowUnsafeBlocks` ——
  用 `Marshal.AllocHGlobal` + `CopyPixels(..., pointer, length, stride)` + `Marshal.Copy` 中转。

### 回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **168 / 168** |
| 十二个 `--check`（新增 `--layout-check`） | 全部 `exit=0` |
| `--shell` | 7 / 7 页渲染正常 |

归档：`docs/screenshot-settings.png`（一整张卡）、`docs/screenshot-studyroom.png`（卡片通到底）。

---

## 2026-11-02 · 构建为什么慢（诊断结论）+ 采用 Skills for Real Engineers

### 一、现象

`User said (m11922): 「diagnose 一下这个构建为什么慢」`

主观感受是"每次改一点都要等一会儿"。先确认一件事：**这个目录不是 Git 仓库**
（`git status` → `fatal: not a git repository`），所以没有"某次提交之后变慢"这种线索，
只能横向量：不同改动类型各要多久。

### 二、实测（`-clp:PerformanceSummary` + `date +%s%N` 夹时间）

| 场景 | 时间 |
| --- | --- |
| 无改动（MSBuild server 热态） | **1.05–1.42 s** |
| 无改动（空闲 3 小时后 / `dotnet build-server shutdown` 之后） | **3.6–6.9 s** |
| 改 `HeliumTodo.Core` 一个 `.cs` | 1.1–1.6 s |
| 改 `HeliumTodo.App` 一个 `.cs` | 2.6–3.2 s |
| 改 `HeliumTodo.App` 一个 `.axaml` | 2.7–3.2 s |
| 删 `obj`/`bin` 全新构建（server 热） | **6.1 s** |
| 同上 + 冷 server | **8.3 s** |
| `dotnet msbuild -version`（纯 CLI 开销） | 215 ms |

冷构建的任务级账（6 个项目）：

| 任务 | 耗时 |
| --- | --- |
| `Csc` | 4 234 ms |
| `CompileAvaloniaXamlTask` | 1 131 ms |
| `Copy` | 1 090 ms / 21 次 |
| `ResolvePackageAssets` | 422 ms |
| `GenerateDepsFile` | 437 ms |
| `RestoreTask` | 316 ms |
| `CreateAppHost` | 46 ms / 3 次 |

App 单次改动（约 3.2 s）的账：`CompileAvaloniaXamlTask` **1 158 ms**、`Delete` 599 ms / 4 次、
`Copy` 560 ms / 9 次、`Csc` 340 ms、`CallTarget` 1 192 ms。

### 三、结论：编译本身不慢，慢在三处

1. **冷启动惩罚**：MSBuild server 活着时 1.0 s，睡着之后 3.6–6.9 s（差 3–5 倍）。这部分跟代码无关。
2. **改 App 就得重做 Avalonia XAML IL 织入**：实测只要 `HeliumTodo.App` 自己的 `.cs` 变了，
   哪怕只加一行注释，也要重跑 `CompileAvaloniaXamlTask`（1.16 s）。
   而**只改 `HeliumTodo.Core` 时，App 只跑 `Csc` 108 ms、完全不碰 XAML 任务** ——
   这是"改逻辑比改界面快"的真正原因。
3. **我们的验证回路比编译贵得多**：十二项 `--check` 串行跑完 **57.5 s**（`--no-build` 状态），
   带构建检查 70 s+；单次 `dotnet run` 光构建检查就 1.0–1.9 s。
   再加上三个工具项目各自持有一整套依赖树（`App` 121 / `Tests` 138 / `SelfTest` 142 / `RenderCheck` 127 个文件），
   磁盘与启动开销都不小。

### 四、给了五条提速建议（前两条待批）

1. 验证回路统一加 `--no-build`（已构建过就不要再检查一遍）；
2. 把 12 次进程启动合并成单进程 `--all-checks`（省掉 12 次 Avalonia 初始化）；
3. 构建加 `--no-restore`；
4. 给 Defender 加 `bin/`、`obj/` 排除项；
5. 合并三个工具项目（`SelfTest` / `RenderCheck` 共用一份依赖树）。

用户当时没有答复是否实施 1+2，直接给了下一个任务，所以这里只留结论、不改流水线。

### 五、采用 Skills for Real Engineers

`User said (m12004): 「在之后的开发中追加使用Skills for Real Engineers这个skill集（排除agent无法使用的部分），保证开发流程中代码的规范性，写入开发文档，以便交接」`
`User said (m12019): 「抱歉我的意思是本地已经安装了这个skill集」`

本机 `C:/Users/17121/.dsh/skills` 下已装 **41 个**技能。逐个分类后落成两份文档：

| 分类 | 数量 | 内容 |
| --- | --- | --- |
| 默认启用 | 8 | `tdd`、`diagnosing-bugs`、`research`、`prototype`、`domain-modeling`、`codebase-design`、`code-review`、`writing-for-agents` |
| 降级使用 | 9 | `to-spec`/`to-tickets`/`triage`/`wayfinder`（需 issue tracker）、`grill-me`/`grilling`/`loop-me`（需人在场）、`retro`、`handoff`、`teach`、`implement(-spec)`、`ask-matt` |
| 明确排除 | 16 | TypeScript 专用（`migrate-to-shoehorn`、`setup-ts-deep-modules`）、JS 生态钩子（`setup-pre-commit`）、需 Git 仓库（`git-guardrails-claude-code`、`pr`）、Web/移动前端（`design-taste-frontend`、`impeccable`）、教学与写作类、`office*`、多 Agent 编排（`chief-of-staff`、`claude-handoff`）等 |

产出：

- **`DEVELOPMENT_PLAYBOOK.md`**（新建）—— 每轮开发固定七步（立题 → 设计 → 验证先行 → 实现 → 排障 → 收尾 → 交付）、
  采用/降级/排除三张清单（逐条写清理由）、质量门禁（构建 + SelfTest + 7 页渲染 + 12 项 `--check` + 指纹基线）、
  写作规范（md 无 BOM、ps1 带 BOM、`UI_TEXT_MAP` 重生成）、交接清单、已知缺口（**进 Git**、`GLOSSARY.md`、`docs/adr/`、构建提速）。
- **`AGENTS.md`**（新建）—— 只列不许破的规则：分层铁律（`Core` 不许引用 Avalonia/EF）、界面铁律（一页一张卡、
  卡片顶边 64 / 左边 12、`CardSurfaceBrush` 不许进 `ThemeDictionaries`、提示条不浮层）、测试铁律、禁止项、常用命令与环境变量。
- `PROJECT_OVERVIEW.md`：头部加两份新文档的指引；「构建与验证流水线」补第 ⑤ 步（12 项 `--check` 的循环命令）；
  顺手把过期数字改掉（46 个用例 → **168**、116 个文件 → **158**）。
- `ARCHITECTURE.md`：头部指向 playbook 与 `AGENTS.md`；架构图与目录树里的旧数字（116 测试 / 9 文件 / 1863 行 / 116 文件基线）全部更正。

### 六、回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **168 / 168** |
| 十二个 `--check` | 全部 `exit=0` |
| `--shell` | 7 / 7 页渲染正常 |
| `tools/check-file-integrity.ps1` | **160 文件**（原 158 + `AGENTS.md` + `DEVELOPMENT_PLAYBOOK.md`）复查一致 |

### 七、教训

- **含 Windows 路径的文本替换必须用 raw string**。这次往 `PROJECT_OVERVIEW.md` 里写
  `HeliumTodo.RenderCheck\bin\Debug\...` 时，heredoc 里的 `\b` 被 python 当成了退格、`\n` 当成了换行，
  生成出 `RenderCheck<退格>in\Debug⏎et10.0\...` 这种鬼东西。同一个坑 2026-11-01 已经在 patch 脚本里踩过一次
  （`CS1056` / `CS1002`），这次是因为"看起来只是往 .md 里贴一行命令"而放松了。
  → 涉及反斜杠一律写脚本文件 + `r"..."`，不要用 shell heredoc。

---

## 2026-11-02 · 五条界面细节：透明分组 / 可展开图标轨 / 一层蒙版 / 叠层提示 / 空白文案

用户一次提了五条（原话见下），共同点是"都是观感问题，但根因分布在不同层"：
一条在控件默认样式、一条在布局骨架、一条在资源字典、一条在计时策略、一条纯粹是漏填。

> ①「设置的界面效果在上上上轮修改后效果仍未与其他界面同步（表现上就是原本的控件下面垫了一层）」
> ②「将左侧功能栏的设置按钮上移至联机功能下方，并在功能栏上侧添加展开按钮，点击后将会常驻显示各功能的名称，再点一下缩回，要求在切换的过程中有拉伸非线性动效。」
> ③「将壁纸的蒙版和暗色蒙版合并，将蒙版颜色的判断改为调整亮暗色时随设置自动调整，增加暗色模式下的蒙版强度（仅使壁纸变暗）」
> ④「设置界面更改时显示的绿色横幅通知改为跟随亮暗色，且直接叠在功能区上方而不是与设置界面错开，且显示3秒后自主消失。」
> ⑤「每日工作流的设置区出现文字空白」

### 一、五条各自改了什么

| # | 根因 | 改法 | 证据 |
| --- | --- | --- | --- |
| ① | `FASettingsExpander` 模板自带一块底色，十个分组叠在一张卡上就是"垫了一层" | 设置页 10 个分组全部 `Background="Transparent"`，底色只由外层 `Border.card` 提供 | `--rail-check` 断言 10 个背景 alpha 全 0；截图里卡片内同一列 77 个采样点只剩 2 种颜色 |
| ② | 图标轨固定在 `RailWidth=56`，设置挤在贴底的 `FooterItems` 里 | 新增 `RailMetrics`（56 / 188 / 44 / 176），`ShellViewModel.IsRailExpanded` + `ToggleRailCommand`；轨道 `DoubleTransition Width 260ms CubicEaseOut`；设置移入 `MenuItems`（紧跟自习室）；顶部加 `Button.railToggle` | `--rail-check` 16 项；截图像素实测白区 x=8…64 → 8…196 |
| ③ | 两层蒙版叠着（亮白/暗黑 + 独立"暗色灰蒙版"），暗色下像两张半透明纸 | 合并成一层：`WallpaperScrim.ComputeStrength`（暗色 ×1.6、上限 0.95）、`ColorHex`（亮白 / 暗黑）；删掉 `AppConfig.DarkWallpaperScrim` 与 ②b 那层 Border | `--appearance-check` 14 项；整图平均亮度 238.0 → 46.8 |
| ④ | `FAInfoBar` 的底板带透明通道，浮层会透出底下的字；占一行又会被用户说"错开" | 改用自绘 `Border Name="NoticeBar" ZIndex="10"` 叠在卡片上方，底板 `NoticeSurfaceBrush` 跟随明暗；`NoticePolicy.DisplayMilliseconds=3000` + 200ms 轮询自动收起 | `--notice-check` 13 项（含"与滚动容器重叠"这条证明是叠层） |
| ⑤ | 两行 `FASettingsExpanderItem` 的 `Content` 是空串 | 补回「超载阈值（小时）」「时间轴上显示已完成」并各配一句说明 | 截图 + `UI_TEXT_MAP.md` 收录 |

### 二、三个值得记住的坑

1. **`Panel.ZIndex` 在 Avalonia 12 不存在**。写 `Panel.ZIndex="10"` 报
   `AVLN3000: Unable to find suitable setter or adder for property ZIndex of type Avalonia.Base:Avalonia.Visual`，
   直接写 `ZIndex="10"` 才对（`ZIndex` 就在 `Visual` 上，不是附加属性）。
2. **`HELIUM_DARK` 原来只写在 `RenderPage` 里**，`--shell` 拍出来的"暗色图"和亮色一模一样 ——
   而玻璃/蒙版效果只有 `--shell`（带壁纸层）才拍得到，等于这个开关在最需要它的路径上失效。
   现在提到主流程，全局生效。**这类"开关只在一个分支里实现"是自检最容易自欺的地方**。
3. **`Visual.VisualChildren` 是 protected**，自检里想按顺序判断兄弟节点只能
   `(bar.GetVisualParent() as Panel)?.Children`；`DoubleTransition` 的命名空间是
   `Avalonia.Animation`（不是 `Avalonia.Animations`），比较属性用 `t.Property?.Name == "Width"`。

### 三、回归

| 项 | 结果 |
| --- | --- |
| `dotnet build HeliumTodo.slnx -c Debug` | 0 警告 0 错误 |
| `HeliumTodo.SelfTest` | **179 / 179**（新增 `WallpaperScrimTests` 5 + `NoticePolicyTests` 3 + `RailMetricsTests` 3） |
| 十三个 `--check` | 全部 `exit=0`（新增 `--rail-check` 16 项；`--notice-check` 13 项 / `--appearance-check` 14 项已按新语义重写） |
| `--shell` | 7 / 7 页渲染正常 |
| 指纹基线 | 见 `tools/file-hashes.json`（本轮新增 4 个源文件 + 3 张截图） |

归档截图：`docs/screenshot-settings-glass-light.png`、`docs/screenshot-rail-expanded.png`、
`docs/screenshot-settings-glass-dark.png`。

### 补记：设置页那"一层"其实有两层

上面 ① 只改对了外层（`FASettingsExpander`），用户看截图后立刻指出**没修**。
用一层临时探针把每个分组内部所有会画底色的元素打出来，才看到真正的原因：

```
FASettingsExpander      bg=Transparent        1306x672
  ToggleButton          bg=#b3ffffff          1306x68   ← 分组标题那一行
  FASettingsExpanderItem bg=#b3ffffff         1304x54   ← 每个设置行
    Border name=Root    bg=#b3ffffff          1304x54
```

`FASettingsExpanderItem` 的控件主题把 `Background` 写死成 `#B3FFFFFF`，
十个分组一共 **34 行**，每行都在卡片上再刷一层白。修法是加四条样式
（`HeliumTodo.App/Styles/Controls.axaml`，选择器用 `fa|类型名`，前缀在 `Styles` 根上声明）：
`fa|FASettingsExpanderItem`、`fa|FASettingsExpanderItem /template/ Border#Root`、
`fa|FASettingsExpander ToggleButton`、`fa|FASettingsExpander ToggleButton /template/ Border#Root`。

像素证据（卡片右侧 x=1150–1380、y=230–500 的平均亮度）：修前 **252.2** → 修后 **245.9**
（多出来的那 6 个点就是白层）；`--rail-check` 也补了一条断言，
现在会数着 34 行说"行全是透明"。

教训：**"背景不是透明"这件事要按控件的模板实际颜色验证，不能只看自己设的那个属性。**
`Background="Transparent"` 只代表"我没设"，模板里的 `Border` 照样可以自己刷一层。

### 设置卡：内外圆角一致 + 内容内缩 8px

用户反馈"保持设置功能区内外 R 角大小一致，并且将内侧控件（显示内容的）向内缩"。

原因很简单：卡是 `RadiusCard=18`，但里面 `Padding=0`，滚动内容顶在卡的边上。
分组标题的悬停底色是一块**直角**矩形，正好压在圆角上 —— 看起来就是"卡是圆的、内容方着"。

改法（`HeliumTodo.App/Views/SettingsView.axaml`）：

```xml
<Border Classes="card" Padding="8" ClipToBounds="True">
  <Border CornerRadius="{DynamicResource RadiusCard}" ClipToBounds="True" Background="Transparent">
    <ScrollViewer …> … </ScrollViewer>
  </Border>
</Border>
```

内层实测落在 `x=9, y=9`、`1291x755`（卡的 1px 边框占掉 1px），与卡同半径 18。
`--rail-check` 从 17 项加到 **20 项**，新增三条：设置卡 8px 内边距、内层圆角与卡片一致、内层裁边界；
以后再有人把 `Padding` 改回 0，自检会当场变红。

门禁：build 0/0、`SelfTest` 179/179、13 项 `--check` 全 `exit=0`、`--shell` 7/7。

### 版本号升到 0.3.5-alpha

用户要求（0.3.3-alpha → 0.3.5-alpha）。版本号只有**一个源头** `Directory.Build.props`：

```xml
<Version>0.3.5-alpha</Version>
<AssemblyVersion>0.3.5.0</AssemblyVersion>
<FileVersion>0.3.5.0</FileVersion>
```

同步到的位置（都是"讲当前版本"的地方，历史叙述一律不动）：

- 代码注释里的示例：`HeliumTodo.Core/VersionInfo.cs`（4 处）、`HeliumTodo.Core/Backup/BackupManifest.cs:59`、
  `HeliumTodo.App/ViewModels/ShellViewModel.cs:86`、`HeliumTodo.App/ViewModels/SettingsViewModel.cs:186-187`、
  `HeliumTodo.Tests/VersionInfoTests.cs`（3 处，含断言里的提示文案）。
- 文档当前版本行：`ARCHITECTURE.md:17`、`ARCHITECTURE.md:1093`（目录树）、`DEVELOPMENT_PLAYBOOK.md:8`、
  `PROJECT_OVERVIEW.md:5`、`TECH_STACK.md:3`、`WINDOWS_DEPENDENCIES.md:8`、`PROGRESS.md:6`。
- **保留不动**：`PROGRESS.md` 里 0.3.3-alpha 那一整节、以及代码里"从 0.3.3 起…""0.3.3 之前…"这类**历史说明**——
  它们描述的是"哪一版改了什么"，改掉就变成假话。

验证：`HeliumTodo.Core.dll` 的 `AssemblyInformationalVersion` = `0.3.5-alpha`、`Version` = `0.3.5.0`；
`SelfTest` 179/179（含"版本号格式应合法""显示用版本号带 v 前缀"）；十三项 `--check` 全 `exit=0`；`--shell` 7/7；
build 0 警告 0 错误。

## 2026-11-02 · 启动 / 最大化 / 拖拽改大小：动效为什么卡，以及卡在哪儿

用户原话：**「修复启动以及最大化和调整窗口大小时时动效卡顿的问题」**。

### 先说结论

卡的不是定时器、不是缓动曲线、也不是布局。是**软件渲染下"换一次窗口尺寸"这件事本身要 45~58 ms**，
180 毫秒的动画最多挤出 4~5 帧。我们把能砍的都砍了（省掉一半），但只要还在软件渲染，
2560×1440 这个尺寸就上不到 60 fps —— 想真的丝滑，只能换 GPU 后端（下面有实测对照）。

### 怎么量的（新增 `--perf-check`）

`HeliumTodo.RenderCheck` 里新增 `--perf-check`：无头真绘图（`UseHeadlessDrawing = false`）建一个
1400×900 的 `MainWindow`，塞一张 1600×1000 的渐变色壁纸（纯色会被缓存，量不准），
然后循环"改窗口尺寸 → `CaptureRenderedFrame()`"计时，把成本拆成十档：

| 变体 | 平均 ms/帧 | 折合帧率 | 说明 |
| --- | --- | --- | --- |
| A 改尺寸 + 高品质壁纸（改动前线上） | 53~58 | 18 | 基线 |
| B 改尺寸 + 低品质壁纸 | 35~38 | 27~29 | 壁纸重采样值 18~20 ms |
| C 改尺寸 + 不画壁纸 | 28~31 | 33~35 | |
| D 不改尺寸，纯重绘 | 1.4~2.0 | 500~700 | 说明"重绘一帧"本身很便宜 |
| E 改尺寸，只布局不画 | **0.0~0.1** | — | **布局几乎不花钱** |
| F 只改整窗不透明度（开场动画就是这么干的） | 14~16 | 63~69 | |
| G 只改内容缩放 | 13.9~15.6 | 64~72 | |
| H 改尺寸但什么都不画 | 13.3~15.2 | 66~75 | 剩下的都是"换尺寸"的固定开销 |
| I 改尺寸，只剩壁纸 | 13.5~15.5 | 74 | |
| **J 低品质壁纸 + 无卡片阴影（现在的线上做法）** | **22.9~25.8** | **39~44** | 比基线快 **57%** |

- 六页各测一遍（出图 / 只布局）：今日工作流 49.5/0.0、日程 51.0/0.1、待办 50.3/0.0、
  专注 50.5/0.0、自习室 52.8/0.0、设置 50.7/0.1 ms → **和页面内容多少无关**，进一步说明是固定成本。
- 试过但没用：把文字抗锯齿关掉（`TextRenderingMode.Alias`）→ 24.6 vs 25.6 ms，反而更慢，已放弃
  （`RenderOptions.GetTextRenderingMode` 本身也已废弃，要改得用 `TextOptions`）。
- 试过但没用（真机）：**动画期间把界面拍成一张位图、只拉伸这张图**。无头里看着有希望（H/I 那两档只要 13~15 ms），
  但真机上一点没快（45 ms 还是 45 ms）—— 因为开销不在"画内容"，而在"换尺寸"本身。这段代码写完量完就删了，
  没留在仓库里（`WindowSnapshot.cs` 已删除）。

### 真机三次对照（同一个 2560×1440 最大化动画，`HELIUM_ANIM_TRACE=1` 打帧间隔）

| 配置 | 帧间隔（ms） | 帧数 |
| --- | --- | --- |
| 软件渲染 + 无降质（改动前） | 18.1 / **91.6** / 64.2 / 57.9 | 5 |
| 软件渲染 + 降质（现状） | 20.7 / 48.3 / 37.2 / 45.8 / 51.3 | 6 |
| GPU（`HELIUM_RENDER=angle`） | 15 / 24 / 18.9 / 11.8 / 11.9 / 12.2 / 11.8 / 12.6 / 17.9 / 19 / 11.4 / 12.3 / 23.4 | **14** |

最后一行就是"为什么 GPU 不卡"的全部证据：同样的代码、同样的动画，GPU 后端 12 ms 一帧。

### 改了什么

1. **窗口正在动的时候自动降质**（新增 `HeliumTodo.App/Windowing/InteractionQuality.cs`）：
   引用计数的 `Begin()`/`Dispose()`，进入时做三件事——壁纸 `BitmapInterpolationMode` 降到 `LowQuality`、
   卡片阴影临时关掉、动图壁纸暂停播放；退出时逐项还原。
   `WindowAnimator` 的开场动画与最大化动画、`WindowResizeGrips` 的按住拖拽都包在里面。
   `HELIUM_NO_INTERACTION_QUALITY=1` 可整体关掉（做 A/B 对照用）。
2. **`CardShadow` 移出 `ThemeDictionaries`**（`HeliumTodo.App/Styles/DesignTokens.axaml:92`）：
   这是第**三**次踩同一个坑——主题字典优先于应用级同名资源，代码改 `Resources["CardShadow"]`
   会被静默无视。移出来之后才能"动画期间临时无阴影"，`App.ApplyCardShadow(bool isDark)` /
   `App.SetCardShadowSuppressed(bool)` 两个入口负责在亮暗两种阴影之间切。
3. **拖拽改大小按帧合并**（`HeliumTodo.App/Windowing/WindowResizeGrips.cs`）：
   原来 `OnPointerMoved` 每次都直接改窗口，鼠标 1000 Hz 时一帧内能改十几次；
   现在只记下 `_pendingPosition/_pendingWidth/_pendingHeight`，用
   `TopLevel.RequestAnimationFrame` 合并到下一帧改一次。
4. 壁纸 `Image` 起名 `Name="WallpaperImage"`（`InteractionQuality` 靠名字找它）。

### 经验

- **"卡"要先分成"布局 / 光栅化 / 换尺寸"三段分别量**，不然会一直在错误的地方优化
  （这次 E 档 0.0 ms 直接排除了布局，H 档 15 ms 说明有一半开销和内容无关）。
- 无头数字只用来**比较**，不能用绝对值代表真机：同一个 J 档无头 23 ms、真机 45 ms。
- 一个"看起来很有道理"的优化（快照拉伸）在真机上可能完全无效——量了再下结论，量完就删掉。
- 主题字典那个坑值得单独立一条规矩：**凡是要用代码改的资源，都不许写进 `ThemeDictionaries`**。

### 本轮验证

`dotnet build HeliumTodo.slnx -c Debug` 0 警告 0 错误；`SelfTest` **179/179**；
`--perf-check` 5 条断言全绿（含"交互降质方案比基线快 30% 以上"，实测 53.2 → 23.0 ms）；
十四项 `--check` 全 `exit=0`；`--shell` 7/7。

---

## 2026-11-02 · 并入版本控制：`git init` + 分支 + 源码快照

### 为什么做这件事

到这一轮为止，项目已经 179 个测试、31 500 行代码，但**没有版本管理**：
`git status` 一直是 `fatal: not a git repository`，唯一的"历史"是这份 `PROGRESS.md`。
用户要求「备份所有目前代码，创建分支」，于是把这两件事一起做掉。

### 做了什么

| 事项 | 做法 |
| --- | --- |
| 仓库初始化 | `git init -b main`，首个提交 `f14f0a5`（194 个文件 / 43 013 行） |
| 分支 | `main` = 发布线（就停在这一个提交上），`dev` = 开发线（当前所在） |
| 标签 | `v0.3.5-alpha`（annotated，指向首个提交） |
| 忽略规则 | `.gitignore`：`bin/`、`obj/`、`dist/`、`Data/`、`backups/`、`*.db*`、`output/`、`_probe-*/`、`.vs/`、`.idea/`、`docs/_shots/` |
| 换行规则 | `.gitattributes`：`* text=auto eol=lf`；`*.png`/`*.dll`/`*.exe`/`*.zip`/`*.db` 标 binary；`core.autocrlf=false` |
| 整包备份 | `git archive --format=zip` 导出到仓库外：`D:\代码\dsh project\HeliumTodo-backups\HeliumTodo-0.3.5-alpha-20261102.zip`（1.9 MB，只含受版本管理的源码与文档） |

为什么把 `Data/` 也忽略：那是**运行时数据**（`helium.db` + `config.json` + `backups/`），
复制来复制去会互相污染，而且之前已经踩过"自检写真实库"的坑。代码仓库只放代码。

为什么 `dist/` 不进球：`dist/HeliumTodo-0.3.1-win-x64/` 之类的目录加起来 454 MB，
全是可从源码重建的产物。要发版就重新 `dotnet publish`。

### 备份内容核对

- 194 个受管理文件：6 个项目的全部 `.cs` / `.axaml` / `.csproj`、14 张文档截图、
  9 份 Markdown、`tools/` 里 9 个脚本、`.gitignore` / `.gitattributes` / `global.json` 等配置。
- `git archive` 出来的 zip 解压后目录名带前缀 `HeliumTodo-0.3.5-alpha/`，和源码树同构。
- 大小对比：源码快照 1.9 MB vs 整个工作目录 2.4 GB —— 差额基本是 `bin/`、`obj/`、`dist/`。

### 顺带修正

- `AGENTS.md` 新增「版本管理」一节（分支约定、每轮一个提交、交付前 `git status` 必须干净），
  常用命令里补了 `git status --short` 与 `git add -A && git commit`。
- `DEVELOPMENT_PLAYBOOK.md`：`code-review` 那段从"本仓库还没进 Git，只能降级"改成
  "用 `git diff main...HEAD` 当 fixed point + 指纹差异当第二把尺子"；已知缺口第 1 条
  "进 Git"改成"已就位"。
- `PROJECT_OVERVIEW.md` 的环境表里 `Git 仓库` 一行改为记录当前分支与标签。

### 经验

1. **先 `.gitignore` 再 `git add`**。这个仓库有 2.4 GB，其中 99% 是构建产物；
   顺序反了就是"提交 45 万行二进制"的事故。
2. **`core.autocrlf=false` + `.gitattributes` 里写 `eol=lf`**：本机 `git add` 默认会
   报警"LF 将被 CRLF 取代"，那是 `core.autocrlf=true` 的行为。两者定死一端，diff 才不会每行都变。
3. **备份放仓库外面**。`git archive` 导出的 zip 如果放在工作区里，下次 `git status`
   就是脏的（除非也忽略它）；放 `D:\代码\dsh project\HeliumTodo-backups\` 最省心。
