# 能否迁移到 Avalonia 12 + FluentAvaloniaUI 3？

> ## ✅ 这份文档已经执行完毕（2026-10-24）
>
> 下面记录的是**当初的可行性验证过程**（在副本上跑的）。
> 之后确认要做，迁移**已经正式落到主项目上**。
>
> 最终结果：
>
> | 项目 | 结果 |
> | --- | --- |
> | 编译 | **0 警告 0 错误** |
> | 单元测试 | **46 / 46 通过**（74 毫秒） |
> | 界面渲染 | **7 / 7 通过**（6 个页面 + 外壳） |
> | 目标框架 | `net8.0` → `net10.0` |
> | Avalonia | 11.3.2 → **12.1.2**（24 个程序集全对齐） |
> | FluentAvaloniaUI | 2.4.0 → **3.1.0** |
>
> 实际执行用的脚本留在 `tools/migrate-av12.ps1`，19 个文件的改动全部固化在里面，
> 支持 `-WhatIf` 空跑。改动明细见 [`PROGRESS.md`](PROGRESS.md) 第三节 A。
>
> ### 迁移时新发现的一个真 bug（这份文档当初没提到）
>
> 迁移完成后补齐了 `--shell` 渲染（渲染整个主窗口而不只是单个页面），
> 第一次看到外壳 —— **内容区一片空白**。
>
> 原因：`ShellViewModel` 构造时设了 `SelectedItem`，但导航栏控件没被同步选中，
> `SelectionChanged` 从不触发，`CurrentPage` 一直是 `null`。
>
> **这个 bug 和迁移无关，Avalonia 11 时代就有** —— 只是六个页面单独渲染的自检
> 绕过了外壳，所以一直没暴露。已在 `ShellView.axaml.cs` 里修掉。
>
> 教训：**自检覆盖范围决定了能发现什么**。

> **结论：能，而且我已经真的迁移成功并跑通了。**
>
> 但有一个**硬性代价**：`TargetFramework` 必须从 `net8.0` 升到 `net10.0`。
> 这不是选择题 —— FluentAvaloniaUI 3.x **只提供 net10.0 目标**。
>
> 验证日期：2026-10-20　·　验证方式：在项目副本上实际执行移植，编译 + 测试 + 渲染全跑

---

## 一、结论速览

| 问题 | 答案 |
| --- | --- |
| 技术上能否迁移？ | ✅ **能**，已实测跑通 |
| 需要改多少代码？ | 很少 —— 实际动了 19 个文件，但真正改代码的只有 4 个 XAML（FA 改名）+ 2 个 XAML（`Watermark`）+ 2 个 `.cs`，其余都是版本号 |
| 最大阻碍是什么？ | ⚠️ **必须升到 .NET 10**（FA 3 的硬约束） |
| 算法 / 数据层要动吗？ | ❌ **完全不用**。Core 和 Data 只改了 TargetFramework 一行 |
| 风险在哪？ | 控件包名全变了（`FA` 前缀）、UI 视觉有细微变化、`Avalonia.Diagnostics` 没了 |

---

## 二、实测结果（不是推测）

迁移在 `D:\av12-migrate` 副本上完成，**没有动你的原项目**。

```
┌──────────────────────────────────────────────────────────┐
│  构建          ✅ 已成功生成    0 错误（1 个无害过时警告） │
│  单元测试      ✅ 46 / 46 全通过（68 毫秒）                │
│  界面渲染自检  ✅ 6 / 6 页面全部渲染正常                   │
│  程序集版本    ✅ 24 个 Avalonia 全部 12.1.2，无混版       │
│  FluentAvalonia ✅ 3.1.0                                  │
└──────────────────────────────────────────────────────────┘
```

渲染出来的界面和 Avalonia 11 版**几乎一模一样**：
标题栏、时间轴错位布局、冲突标红、时钟表盘、设置页的 Expander 全部正常。

---

## 三、版本矩阵（实测确认可用）

| 包 | 版本 | 说明 |
| --- | --- | --- |
| **.NET SDK** | 10.0.402 | 你机器上已有；`net10.0` 运行时也齐 |
| **Avalonia** | **12.1.2** | 12.1.x 最新可用补丁 |
| **FluentAvaloniaUI** | **3.1.0** | 12.x 生态里最新稳定版 |
| **CommunityToolkit.Mvvm** | 8.4.0 | 不变，兼容 |
| **EF Core / SQLite** | 8.0.11 | 不变，能跑在 net10 上 |
| **xunit** | 2.9.2 | 不变（46 个测试直接用 `[Fact]` 特性，没依赖 Avalonia.Headless 的 xunit 适配器，所以不用升 xunit v3） |

### ⚠️ 各包的版本上限不一致（踩过）

我一开始想统一用 `12.1.4`，结果还原失败：

```
error NU1102: 找不到版本为 (>= 12.1.4) 的包 Avalonia.Controls.DataGrid
              在 nuget.org 中找到 140 个版本 [最接近版本: 12.1.2]
```

**`Avalonia.Controls.DataGrid` 和 `ColorPicker` 只到 12.1.2**，
而主包 `Avalonia` 已经到 12.1.4。所以整套必须对齐到 **12.1.2**（取交集）。

> 上次修「程序集混版」时留下的经验在这里派上了用场：
> 一定要去实际的 NuGet 索引核对每个包的可用版本，不能假设它们同步发版。

---

## 四、必须做的改动清单

一共 **12 个文件**，其中 6 个只是改一行 TargetFramework。

### 4.1 目标框架（6 个 .csproj）

```xml
- <TargetFramework>net8.0</TargetFramework>
+ <TargetFramework>net10.0</TargetFramework>
```

**没有商量余地**：FluentAvaloniaUI 3.1.0 的 nuspec 里只有 `net10.0` 一个目标框架组。
（有意思的是 Avalonia 12 **自己**仍然支持 net8.0 —— 卡住的是 FA 3。）

### 4.2 包版本（`Directory.Packages.props`）

```xml
<PackageVersion Include="Avalonia" Version="12.1.2" />
<PackageVersion Include="Avalonia.Desktop" Version="12.1.2" />
<PackageVersion Include="Avalonia.Themes.Fluent" Version="12.1.2" />
<PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.2" />
<PackageVersion Include="Avalonia.Controls.DataGrid" Version="12.1.2" />
<PackageVersion Include="Avalonia.Controls.ColorPicker" Version="12.1.2" />
<PackageVersion Include="Avalonia.Headless" Version="12.1.2" />
<PackageVersion Include="Avalonia.HarfBuzz" Version="12.1.2" />   <!-- 新增，见 4.5 -->
<PackageVersion Include="FluentAvaloniaUI" Version="3.1.0" />

- <PackageVersion Include="Avalonia.Diagnostics" Version="11.3.2" />   <!-- 删除 -->
```

### 4.3 ⚠️ FluentAvalonia 所有控件都加了 `FA` 前缀

**这是最容易低估的一步。** 实测 FA 3.1.0 导出 252 个类型，其中 **224 个（88%）以 `FA` 开头**。

我们项目用到的 7 个，有 6 个改名：

| 2.x（现在用的） | 3.x（迁移后） | 命名空间 |
| --- | --- | --- |
| `NavigationView` | → **`FANavigationView`** | `FluentAvalonia.UI.Controls` |
| `NavigationViewItem` | → **`FANavigationViewItem`** | `FluentAvalonia.UI.Controls` |
| `NavigationViewSelectionChangedEventArgs` | → **`FANavigationViewSelectionChangedEventArgs`** | `FluentAvalonia.UI.Controls` |
| `InfoBar` | → **`FAInfoBar`** | `FluentAvalonia.UI.Controls` |
| `SettingsExpander` | → **`FASettingsExpander`** | `FluentAvalonia.UI.Controls` |
| `SettingsExpanderItem` | → **`FASettingsExpanderItem`** | `FluentAvalonia.UI.Controls` |
| `FontIconSource` | → **`FAFontIconSource`** | `FluentAvalonia.UI.Controls` |
| `FluentAvaloniaTheme` | **未改名** | `FluentAvalonia.Styling` |

**实际改动**：4 个 XAML 文件里共 **48 处** `<fa:xxx>` 开标签、闭标签、**附加属性**，
外加 `ShellView.axaml.cs` 里 2 处 C# 引用。

> ⚠️ 我用脚本批量替换时**漏了附加属性**，编译报：
> `The 'fa:FASettingsExpander.Items' start tag does not match the end tag of 'fa:SettingsExpander.Items'`
>
> 因为 `<fa:SettingsExpander.Items>` 这种写法里，`fa:` 后面跟着的是 `SettingsExpander.Items`，
> 简单替换 `fa:SettingsExpander` 会只改一半。
> **要改的三种形态**：`<fa:Name`、`</fa:Name>`、`fa:Name.Property`。

### 4.4 `App.axaml.cs` —— 删掉 BindingPlugins 相关代码

Avalonia 12 把 `BindingPlugins` 整个**删掉了**（因为编译期绑定用不上它，
而且它和 CommunityToolkit.Mvvm 一直打架）。

我们项目里这段代码原本是为了「关掉反射式绑定校验插件，避免同一属性被通知两次」：

```csharp
// ❌ Avalonia 12 里这段编译不过
var toRemove = BindingPlugins.DataValidators
    .OfType<DataAnnotationsValidationPlugin>()
    .ToArray();
foreach (var plugin in toRemove)
    BindingPlugins.DataValidators.Remove(plugin);
```

**直接删掉即可** —— Avalonia 12 里这些插件默认就是禁用的，不再需要手动关。

### 4.5 `Avalonia.HarfBuzz` —— 需要新增

Avalonia 12 把「文本整形」从渲染后端里独立出来了。
如果你用 `UsePlatformDetect()`（本项目就是），HarfBuzz 会被自动带上；
但**显式声明更保险**，否则某些配置下会抛
`InvalidOperationException: No text shaping system configured`。

### 4.6 `SystemDecorations` → `WindowDecorations`

```csharp
// RenderCheck/Program.cs
- SystemDecorations = SystemDecorations.None,
+ WindowDecorations = WindowDecorations.None,
```

`Window.SystemDecorations` 在 12 里标了 `[Obsolete]`（不是删掉，所以不致命）。

### 4.7 `Avalonia.Diagnostics` 没了

这个包在 12 里被移除，官方要求改用 **AvaloniaUI.DiagnosticsSupport**（需要 Avalonia Plus 授权）。

我们项目里它只是 Debug 配置下的调试工具，**直接删掉不影响功能**。
如果还想要 DevTools，得考虑付费授权。

---

## 五、不需要改的部分（好消息）

我逐项核查了官方 [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes) 里所有条目，**以下这些我们都用到了「不受影响」的写法**：

| 变更项 | 我们的情况 |
| --- | --- |
| `IBinding` / `InstancedBinding` 移除 | ❌ 没用到（XAML 里的 `{Binding}` 不受影响） |
| 编译期绑定改为默认开启 | ✅ 我们本来就手动开了，行为一致 |
| `TopLevel` 强转失效 | ❌ 没做强制转换 |
| 剪贴板 `IDataObject` → `IAsyncDataTransfer` | ❌ 没用到剪贴板 |
| `GotFocus/LostFocus` 事件参数类型变化 | ❌ 没订阅这两个事件 |
| `Gestures.` 前缀移除 | ❌ 没用到手势 |
| `FuncMultiValueConverter` 参数类型变化 | ❌ 没用到多值转换器 |
| `WindowState` 变 direct 属性 | ✅ 仅代码里读写，仍可用 |
| `Screen` 变抽象类 | ❌ 没自己 new |
| 自定义渲染后端接口重构 | ❌ `ClockControls` / `TimelineGrid` 只用公开绘图 API |

**Core 层和 Data 层一行代码都没改**（除了 TargetFramework）。
46 个测试原样跑过 —— 这是分层架构的直接回报。

---

## 六、视觉差异（需要你知道）

渲染是成功的，但 FluentAvaloniaUI 3 的**控件样式有变化**。对比同一页面的 PNG：

| 控件 | Avalonia 11 + FA 2.4 | Avalonia 12 + FA 3.1 |
| --- | --- | --- |
| `ComboBox`（设置页主题选择） | 带彩色色块的圆角框 | 更朴素的细边框框 |
| `ToggleSwitch` | 小圆点滑块 | 更大的胶囊滑块 |
| `InfoBar` 图标 | `!` 感叹号 | `×` 叉（严重级别图标变了） |
| 整体留白 | — | 略微更紧凑 |

这些不是 bug，是上游设计更新。**如果你对当前视觉很满意，迁移会带来这些变化**；
如果想保留原样，可以靠 `DesignTokens.axaml` + 控件样式覆写去调回来（工作量中等）。

---

## 七、迁移代价评估

| 项目 | 工作量 | 风险 |
| --- | --- | --- |
| TargetFramework 升 net10.0 | 改 6 行 | 低（你的环境已就绪） |
| 包版本升级 | 改 1 个文件 | 低 |
| FA 控件加 `FA` 前缀 | 48 处 XAML + 2 处 C# | **中**（易漏附加属性） |
| 删 BindingPlugins 代码 | 删约 15 行 | 低 |
| `WindowDecorations` 改名 | 1 处 | 低 |
| 删 Avalonia.Diagnostics | 删 1 个引用 | 低（但失去 DevTools） |
| 视觉回归对比 | 人工看 6 张图 | 中（样式有变化） |
| **总计** | **约半天**（我已实测走通一遍） | — |

---

## 八、我的建议

### 要迁移的话，理由是什么？

- **.NET 10 是 LTS**（2025 年 11 月发布，支持到 2028 年），net8.0 支持到 2026 年 11 月
- Avalonia 12 性能更好、剪贴板/窗口装饰等底层重构更健壮
- 早迁比晚迁便宜 —— 项目还在 0.3.1-dev，代码量不大

### 不迁移的理由？

- **当前一切正常**：46 测试全绿、6 页渲染正常、0 警告
- **视觉会变**，需要重新调校
- **失去免费 DevTools**（`Avalonia.Diagnostics`），要付费才有替代
- **FA 3 还很新**（3.x 发布不久），生态和文档可能还不完善

### 如果决定迁移

我已经在 `D:\av12-migrate` 把整条路走通了。建议的做法：

1. 先把当前状态（`net8.0` + Avalonia 11.3.2）**打个 tag / 备份**，确保可回滚
2. 照着上面第四节的清单改（顺序：TargetFramework → 包版本 → 删 BindingPlugins → XAML 改名）
3. 每改完一步就 `dotnet build` 一次，不要把错误攒在一起
4. 最后跑三件套验证：`SelfTest`（46 测试）、`RenderCheck`（6 页渲染）、人工看 6 张图
5. 重点检查 4 个视觉有变化的控件（ComboBox / Switch / InfoBar / 整体间距）

---

## 九、验证产物位置

| 内容 | 路径 | 状态 |
| --- | --- | --- |
| 迁移验证用的项目副本 | `D:\av12-migrate` | 已清理 |
| 类型探测工具 | `D:\av12-probe` | 已清理 |
| **迁移后的 6 张界面渲染图** | [`docs/avalonia12-render/`](docs/avalonia12-render/) | **已保留** |

保留那 6 张图是因为它们是「迁移后界面长什么样」的唯一证据 ——
尤其是第六节提到的控件视觉差异，看图比看文字直观得多。

> ⚠️ **关于验证过程的一个坦白**
>
> 验证时我本该只在副本上操作，但中途有 3 个文件的写入落到了**原项目**上
> （`App.axaml.cs` 的 BindingPlugins 代码被删、`Program.cs` 的换行符被改、
> `Directory.Packages.props` 的版本号被改）。
>
> 发现后已全部还原，并用 SHA256 指纹校验逐个确认与原基线**字节级一致**。
> 现在原项目状态：`net8.0` + Avalonia 11.3.2 + FluentAvaloniaUI 2.4.0，
> 46 个测试全绿、6 页渲染正常、84 个基线文件全部一致。
>
> 这件事本身也说明了一件事：**那个指纹校验工具确实有用** ——
> 正是它在第一时间指出了「有文件被改了」。

---

## 十、附：探测过程中确认的事实

用 `D:\av12-probe` 这个独立小项目（`net10.0` + Avalonia 12.1.4 + FA 3.1.0）实测：

```
Avalonia.Base        12.1.4.0
FluentAvalonia       3.1.0.0

FluentAvalonia 3 导出 252 个类型，224 个以 FA 开头（88%）
UI.Controls 命名空间里全部是 FA 开头的类型：
    FABreadcrumbBar, FAContentDialog, FAFrame, FAInfoBar, FANavigationView,
    FANumberBox, FAProgressRing, FASettingsExpander, FATabView, FATeachingTip ...
```

这也解释了为什么第一次直接写 `using FluentAvalonia.UI.Controls;` + `NavigationView`
会编译失败 —— 类型确实换了名字，不是命名空间搬家。
