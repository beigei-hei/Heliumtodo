# Windows 平台专属依赖检查

> 问题：程序里有没有用到 Windows 专属的依赖库？
>
> **结论：没有「依赖库」层面上的 Windows 锁定。**
> 但找到了 **3 处 Windows 专属的配置/API**，其中 **1 处会影响跨平台运行**。
>
> 检查日期：2026-10-20　·　项目版本：0.3.5-alpha

---

## 一、结论速览

> **2026-11-01 修订**：本文件最初写于只有"打开文件夹"一处 Windows 写法的时候。
> 之后陆续加了托盘（`Shell_NotifyIconW`）、Windows 圆角属性、渲染后端选择、MP4 视频壁纸
> （Media Foundation），所以下面"完全没有 P/Invoke"的说法**已经过时**，
> 准确清单见文末第十一节；第一节表格按当时状态保留，用来对照演变。

| 检查层面 | 结果 |
| --- | --- |
| NuGet 包（依赖库） | ✅ **无 Windows 专属库** |
| 原生 DLL / P/Invoke | ⚠️ **有 2 处**（托盘 `Shell_NotifyIconW`、视频壁纸 Media Foundation）—— 见第十一节 |
| 注册表 / Win32 API | ✅ **没有注册表**；`Win32Properties`（圆角）与 `Win32RenderingMode`（渲染后端）是 Avalonia 的 API，非自己调 |
| 平台后端 | ✅ **Win32 / X11 / macOS / FreeDesktop 全都有** |
| 构建配置 | ⚠️ **2 处 Windows 专属属性**（不影响其他平台编译） |
| 代码 API | ⚠️ **1 处 Windows 写法，其他平台行为待验证** |
| 字体 | ⚠️ **回退链里有 Windows 字体**（有 Inter 兜底） |

**一句话**：这个程序**本来就是跨平台的**，只有 1 处代码用了 Windows 写法（`Verb = "open"`），而且它**可能在不改的情况下也能工作** —— 需要实机验证才能确定。

---

## 二、依赖库层面：完全干净

### 直接依赖

| 包 | 是否 Windows 专属 |
| --- | --- |
| Avalonia / Avalonia.Desktop | ❌ 跨平台 |
| Avalonia.Themes.Fluent | ❌ 跨平台 |
| Avalonia.Fonts.Inter | ❌ 跨平台 |
| **FluentAvaloniaUI 2.4.0** | ❌ 跨平台 |
| CommunityToolkit.Mvvm | ❌ 跨平台 |
| EF Core + Sqlite | ❌ 跨平台 |
| Microsoft.Extensions.* | ❌ 跨平台 |
| xunit | ❌ 跨平台 |

### Core 层是零依赖的

```xml
<!-- HeliumTodo.Core.csproj 里没有任何 PackageReference -->
```

只有 `<TargetFramework>net8.0</TargetFramework>`。
**任何平台相关的东西都进不了这一层** —— 这是分层架构的直接好处。

### FluentAvaloniaUI 也是跨平台的

我特意查了它的 nuspec 和程序集引用表，确认它没有依赖任何 Windows 专属库：

```
依赖：Avalonia, Avalonia.Controls.ColorPicker, Avalonia.Controls.DataGrid,
      Avalonia.Skia, MicroCom.Runtime
引用：Avalonia.Base, Avalonia.Controls, Avalonia.Markup, SkiaSharp,
      MicroCom.Runtime, System.Text.Json ...
```

> 注：`MicroCom.Runtime` 名字看着像 COM（Windows 概念），
> 但它其实是**跨平台的 COM 接口封装**，在 Linux/macOS 上走不同的底层实现。
> FluentAvalonia 用它来支持 WinUI 风格的控件接口，不是 Windows 专属。

---

## 三、平台后端：四个平台全都带上了

`Avalonia.Desktop` 会同时引入所有桌面后端。实测输出目录里有：

```
Avalonia.Win32.dll               ← Windows
Avalonia.Win32.Automation.dll    ← Windows（无障碍支持）
Avalonia.X11.dll                 ← Linux (X11)
Avalonia.FreeDesktop.dll         ← Linux（跨桌面环境辅助）
Avalonia.Native.dll              ← macOS
```

**这不是负担**，而是设计：运行时由 `UsePlatformDetect()` 自动挑对应的那个。
其余没用到的 DLL 只是躺在目录里，不加载、不占内存。

### 原生资源包也是全平台

`project.assets.json` 里同时记录了：

```
SkiaSharp.NativeAssets.Win32 / Linux / macOS / WebAssembly
HarfBuzzSharp.NativeAssets.Win32 / Linux / macOS / WebAssembly
Avalonia.Angle.Windows.Natives              ← 这个只有 Windows
```

> `Avalonia.Angle.Windows.Natives` 是 Windows 上的 ANGLE（OpenGL ES 转 Direct3D）后端，
> **只会在 Windows 上被用到**，其他平台会忽略它。属于「带了但不用」。

---

## 四、⚠️ 真正的 Windows 专属代码：1 处

### `SettingsViewModel.cs` —— 用 `explorer.exe` 打开文件夹

```csharp
// HeliumTodo.App/ViewModels/SettingsViewModel.cs:243
private void OpenInExplorer(string path)
{
    Process.Start(new ProcessStartInfo
    {
        FileName = path,
        UseShellExecute = true,
        Verb = "open",        // ← 这个 Verb 属性只有 Windows 才认
    });
}
```

**调用点 3 处**：打开配置文件夹、打开插件文件夹、打开数据库文件夹。

**影响**：
- Windows：正常（用资源管理器打开）
- Linux / macOS：**不确定，需要实机验证**

  `Verb` 这个属性是 Windows ShellExecute 的概念。.NET 在 Unix 上的实现是：
  `UseShellExecute = true` 时走 `xdg-open`（Linux）/ `open`（macOS），
  `Verb` 很可能被直接忽略而不是抛异常。

  也就是说：**这个功能在 Linux/macOS 上有可能「本来就能用」**，
  只是 `Verb = "open"` 那行是多余的。

  ⚠️ 我没有 Linux/macOS 环境可以实测，所以这里**不下结论**。
  如果真要跨平台发布，第一步应该是**先在目标平台点一下这个按钮**看实际行为，
  而不是急着改代码。

**修法**（改 1 个方法即可）：

```csharp
private void OpenInExplorer(string path)
{
    try
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        if (OperatingSystem.IsWindows())
        {
            // Windows：UseShellExecute + Verb
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open",
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", path);
        }
        else
        {
            Process.Start("xdg-open", path);
        }
    }
    catch (Exception ex) { /* 原有错误处理 */ }
}
```

> **关于 Avalonia 自带的跨平台方案（我验证过，Avalonia 11 用不了）**
>
> 直觉上应该用 Avalonia 的 `ILauncher` 服务，但实测 Avalonia 11.3.2 的
> `Avalonia.Platform.Storage.ILauncher` **只有两个方法**：
>
> ```
> Task<bool> LaunchUriAsync(Uri uri)
> Task<bool> LaunchFileAsync(IStorageItem storageItem)
> ```
>
> **没有** `LaunchDirectoryInfoAsync` / `OpenFolderAsync` 之类打开文件夹的方法
> —— 那个是 Avalonia **12** 才加的。
> 而 `LaunchFileAsync` 需要 `IStorageItem`，Avalonia 11 也没有公开 API
> 能把一个路径转成 `IStorageFolder`。
>
> 所以结论是：**Avalonia 11 上想打开文件夹，只能自己判断平台**。
> 上面那段按平台分支的写法就是正确解法，不是「偷懒」。

---

## 五、⚠️ 构建配置里的 2 处 Windows 专属属性

```xml
<!-- HeliumTodo.App.csproj -->
<BuiltInComInteropSupport>true</BuiltInComInteropSupport>
<ApplicationManifest>app.manifest</ApplicationManifest>
```

| 属性 | 作用 | 在非 Windows 上 |
| --- | --- | --- |
| `BuiltInComInteropSupport` | 启用 .NET 内置的 COM 互操作（供 Avalonia.Win32 用） | **被忽略**，不影响编译 |
| `ApplicationManifest` | 嵌入 app.manifest（DPI 感知声明） | **被忽略**，用默认清单 |

**这两项都不会阻止跨平台构建** —— .NET SDK 在其他平台直接跳过它们。
只是「配了但没用」，属于无害残留。

如果要做跨平台发布，可以把它们包进条件：

```xml
<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

### `app.manifest` 本身

内容全是 Windows 专属的（`urn:schemas-microsoft-com:asm.v1` + `dpiAware`）。
**这个文件本身没法跨平台**，但它只影响 Windows 上的 DPI 表现，
其他平台不读它。所以留着没问题。

---

## 六、⚠️ 字体回退链里有 Windows 字体

```xml
<!-- Styles/DesignTokens.axaml:112 -->
<FontFamily x:Key="AppFontFamily">Inter, Microsoft YaHei UI, Segoe UI, sans-serif</FontFamily>
```

另外几处硬编码（等宽字体）：

```xml
<FontFamily="Inter, Consolas, monospace" />   <!-- FocusView -->
<FontFamily="Consolas, monospace" />          <!-- StudyRoomView ×2 -->
```

| 字体 | 平台 | 影响 |
| --- | --- | --- |
| `Inter` | **随程序打包**（`WithInterFont()`） | ✅ 全平台可用 |
| `Microsoft YaHei UI` | Windows 中文 | Linux/macOS 没有 → 跳过 |
| `Segoe UI` | Windows | Linux/macOS 没有 → 跳过 |
| `Consolas` | Windows 等宽 | Linux/macOS 没有 → 落到 `monospace` |

**实际影响：很小。** 因为：
1. 第一顺位 `Inter` 是**随程序打包的**，任何平台都能用
2. 中文字符在 Linux/macOS 上会落到最后的 `sans-serif`（系统默认中文字体），**不会显示成方块**
3. 等宽字体那一串最后有 `monospace` 兜底

**唯一的小瑕疵**：Linux/macOS 上中文会用系统默认字体，和 Windows 上的微软雅黑视觉上略有差异。
想更严谨可以补上 `Noto Sans CJK SC`（Linux 常见）和 `PingFang SC`（macOS）。

---

## 七、自控模块：确认没有用 Windows 钩子（重要）

「专注期间切出窗口算打扰」这类功能**最容易**依赖 Windows API（全局钩子、`GetForegroundWindow`）。
我特意查了，**本项目没有这么做**：

```csharp
// HeliumTodo.Core/Services/FocusTimerService.cs
public void RegisterInterruption()    // ← 由界面手动调用
{
    InterruptionCount++;
}
```

- `FocusTimerService` 里**没有任何** `Environment` / `Process` / `Window` / 钩子调用
- 打扰次数是用户**手动点按钮**登记的（界面上那个「标记打扰」）
- `FocusView.axaml` 里只有**显示**打扰次数的 TextBlock，没有监听窗口状态

**这意味着自控模块是纯逻辑、完全跨平台的。**

> 这是当初分层设计的功劳：真实现在 Core 里，Core 又零依赖，
> 所以它天然没法碰平台 API。

---

## 八、其余全部检查项（都没问题）

| 检查项 | 结果 |
| --- | --- |
| `DllImport` / `LibraryImport` | ❌ 没有 |
| `user32` / `kernel32` / `advapi32` / `shell32` / `dwmapi` / `gdi32` / `ole32` | ❌ 没有 |
| `Microsoft.Win32.*` 命名空间 | ❌ 没有 |
| `Registry` / `RegistryKey` | ❌ 没有 |
| `System.Windows.Forms` / `UseWPF` / `UseWindowsForms` | ❌ 没有 |
| `#if WINDOWS` 条件编译 | ❌ 没有 |
| `NativeLibrary` 手动加载 | ❌ 没有 |
| `RuntimeIdentifier` 锁定 | ❌ 没有（所有 csproj 都没锁 RID） |
| 硬编码 `C:\` 或 `\` 分隔符 | ❌ 没有（全用 `Path.Combine`） |
| `OperatingSystem.IsWindows()` 判断 | ❌ 没有（所以也没做平台适配） |

### 文件路径处理是跨平台写法

```csharp
// AppConfig.cs —— 全用 API，不拼字符串
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)   // 各平台各自解析
Path.Combine(AppContext.BaseDirectory, "Data")
Directory.CreateDirectory(directory)
File.WriteAllText(probe, "probe")    // 用试写探测权限
```

注意 `SpecialFolder.ApplicationData` 的实际值：

| 平台 | 解析成 |
| --- | --- |
| Windows | `C:\Users\<你>\AppData\Roaming` |
| Linux | `~/.config` |
| macOS | `~/Library/Application Support` |

**这个 API 本身是跨平台的**，只是 Windows 上叫 `%AppData%`。
代码注释里写 `%AppData%` 只是因为它主要在 Windows 上开发，不是限制。

---

## 九、要真正跑在 Linux/macOS 上，需要做什么

按重要性排序：

| # | 改动 | 必要性 |
| --- | --- | --- |
| 1 | 先在目标平台实测「打开文件夹」按钮 | **必须**（可能无需改代码） |
| 2 | `BuiltInComInteropSupport` / `ApplicationManifest` 加 Windows 条件 | 建议（不改也能编译） |
| 3 | 字体链补 Linux/macOS 中文字体 | 可选（不影响功能） |
| 4 | 若实测失败，再按平台分支改写 `OpenInExplorer` | 视情况 |

**不需要做的**：
- ❌ 换掉任何 NuGet 包
- ❌ 改 Core / Data 层任何代码
- ❌ 处理 `Avalonia.Win32.dll` 之类的多平台后端（它们本来就在，按需加载）

---

## 十、怎么验证我的结论

全部检查都可以重跑：

```powershell
# 查 Win32 P/Invoke
Get-ChildItem -Recurse -File -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
  Select-String -Pattern 'DllImport|LibraryImport|user32|kernel32|Registry'

# 查平台条件编译
Get-ChildItem -Recurse -File -Filter *.cs |
  Select-String -Pattern '#if WINDOWS|OperatingSystem\.IsWindows|RuntimeIdentifier'

# 查实际落地的平台后端
Get-ChildItem HeliumTodo.App\bin\Debug\net8.0 -Filter 'Avalonia*.dll' |
  Select-Object Name

# 查原生资源包
(Get-Content HeliumTodo.App\obj\project.assets.json -Raw |
  ConvertFrom-Json).libraries.PSObject.Properties |
  Where-Object { $_.Name -match 'Native' } | Select-Object Name
```

---

## 附：一个容易误解的点

**「包含了 Avalonia.Win32.dll」不等于「依赖 Windows」。**

所有基于 Avalonia 的桌面程序，发布后都会同时带上四个平台的后端 DLL。
这是**多平台支持**的表现，不是 Windows 锁定。

判断一个 Avalonia 程序有没有 Windows 依赖，正确的看法是：
1. 有没有 `DllImport` 到 Win32 DLL
2. 有没有用 `Microsoft.Win32.*` 注册表
3. 有没有硬编码 Windows 路径
4. 有没有用 `explorer.exe` 之类的外部程序

本项目前 3 项**完全没有**，第 4 项**有 1 处**（就是第五节说的 `OpenInExplorer`）。

---

## 十一、2026-11-01 增补：Windows 专属代码的准确清单

| # | 位置 | 用了什么 | 其他平台怎么办 |
| --- | --- | --- | --- |
| 1 | `HeliumTodo.App/Services/FolderLauncher.cs` | `explorer.exe` / `ProcessStartInfo` 的 `Verb = "open"` | 降级链会依次尝试 Avalonia 的 `Launcher`，理论上能工作，未实机验证 |
| 2 | `HeliumTodo.App/Services/TrayService.cs` | `Shell_NotifyIconW`（纯 P/Invoke，未引 WinForms） | 托盘功能整体不可用；程序启动时会检测返回值，失败仍可正常用（关窗口会真的退出） |
| 3 | `HeliumTodo.App/Services/MediaFoundationVideo.cs` | `mfplat.dll` / `mfreadwrite.dll` 的 COM 接口 | 视频壁纸不可用：`OperatingSystem.IsWindows()` 前置判断直接抛 `PlatformNotSupportedException("视频壁纸目前只支持 Windows 系统。")`；GIF / APNG / 图片壁纸不受影响 |
| 4 | `HeliumTodo.App/Program.cs` + `Services/RenderBackendChoice.cs` | `Win32PlatformOptions` / `Win32RenderingMode`（Avalonia 的 API） | 属于"Windows 后端配置"，其他平台不会走到这段；`AVALONIA_RENDER` 等环境变量同理 |
| 5 | `HeliumTodo.App/Views/MainWindow.axaml` 附近 | `Win32Properties.WindowCornerPreference = "Round"`（Avalonia 的附加属性） | 其他平台忽略，窗口用系统默认圆角 |

**结论**：核心功能（日程 / 专注 / 待办 / 备份 / 提醒）仍然是**跨平台**的；
Windows 专属只集中在"托盘、文件夹、视频壁纸"这三个**周边增强**上，且都有降级路径。
