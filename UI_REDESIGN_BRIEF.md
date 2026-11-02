# Helium Todo 界面改版指令书（可直接交给 AI 执行）

> 本文是一份**可执行规格**，不是灵感板。里面的数字、文件名、类名、命令都是给执行者照做的。
> 与本文冲突的旧代码注释以本文为准；与本文冲突的用户偏好以用户为准（先问）。

---

## 0. 一句话目标

把 Helium Todo 从「Fluent 设置面板式的整页布局」改成「**壁纸上的浮层卡片**」：
自绘标题栏 + 左侧 56px 图标轨 + 中间圆角卡片（列表）+ 右侧 360px 详情卡片，
卡片半透明、背景壁纸透上来并带模糊，**背景壁纸仍可用户更换**（含动图、模糊半径、蒙版、主题色跟随）。

---

## 1. 硬约束（违反任何一条都算失败）

| # | 约束 | 怎么验证 |
| --- | --- | --- |
| C1 | 6 个页面功能与交互**不减**：今日工作流 / 日程 / 待办事项 / 专注 / 自习室 / 设置 | 手工点一遍 |
| C2 | 背景壁纸可换，且支持 PNG / APNG / JPG / GIF；模糊半径、蒙版不透明度、跟随壁纸取色**全部保留** | 设置页 → 背景壁纸 |
| C3 | 编译 **0 警告 0 错误**（本项目对警告零容忍） | `dotnet build HeliumTodo.slnx -c Debug` |
| C4 | **119 个测试全绿** | `dotnet run --project HeliumTodo.SelfTest` |
| C5 | 7 项无头渲染通过（6 页 + 整个外壳） | `dotnet run --project HeliumTodo.RenderCheck -- --shell` |
| C6 | 三个行为自检通过 | `--refresh-check` / `--backup-check` / `--room-check` |
| C7 | **不新增 NuGet 包**，**不新增图片资源**（图标一律用字体字形） | 检查 `*.csproj`、`Assets/` |
| C8 | 不改数据库结构；不用 EF Migrations；老库靠 `EnsureColumns()` 补列 | 见 `HeliumDbContext` |
| C9 | 注释写中文，且解释**为什么**这么做（不是复述代码） | 人读 |
| C10 | 主题/插件扩展点保留：换主题、插件注册页面仍能进导航栏 | `ThemeRegistry`、`PluginHost` |

---

## 2. 参考图解剖

参考界面（Starcloud 启动器）在本项目里的可迁移特征，只有 5 件事：

```
┌───────────────────────────────────────────────────────────────────────────┐
│  ▣ Starcloud  开发版 V0.3.9              ○ ○ ○        ─   □   ✕        │ ← 自绘标题栏 48px
├────┬──────────────────────────────────────────────┬───────────────────────┤
│ ☰  │ ┌──────────────────────────────────────────┐ │ ┌───────────────────┐ │
│ ▣  │ │ 已安装版本 (5)              ⌃   ⟳        │ │ │ 正版 | 外置 | 离线│ │
│ ≡  │ │──────────────────────────────────────────│ │ │                   │ │
│ ⤓  │ │ [icon] 橙服26.2优化整合包   Fabric   上次…│ │ │      (头像)       │ │
│ ▤  │ │ [icon] SkyExplorer         NeoForge  从未…│ │ │      Baigei_hei   │ │
│ ◕  │ │ [icon] Closing Song1.6.6    Forge    从未…│ │ │                   │ │
│ ⚗  │ │ [icon] 26.3-Fabric 0.20.1   Fabric   从未…│ │ │                   │ │
│ ⚙  │ │ [icon] 1.21.1-NeoForge…     NeoForge  从未…│ │ │  ┌─────────────┐  │ │
│ ↻  │ │                                           │ │ │  │ ▶ 启动游戏  │⌄ │ │
│ ◔  │ │                                           │ │ │  └─────────────┘  │ │
│ ▦  │ └──────────────────────────────────────────┘ │ └───────────────────┘ │
│ ★  │                                              │                       │
│ ▤  │                                              │                       │
│ ⚙  │                                              │                       │
└────┴──────────────────────────────────────────────┴───────────────────────┘
 56px            ~860px（弹性）                            360px（固定）
```

**尺寸表（以 1400×900 窗口为基准；参考图是 1924×1143，按比例缩放得来）**

| 元素 | 值 | 说明 |
| --- | --- | --- |
| 自绘标题栏高 | 48 | 图标 20、标题 13 SemiBold、版本胶囊 10 |
| 左图标轨宽 | 56 | 条目 44×44、圆角 12、条目间距 4 |
| 卡片外边距 | 12 | 与窗口边缘的距离；卡片之间 12 |
| 卡片圆角 | 18 | 参考图约 24@1924 → 18@1400 |
| 卡片内边距 | 20 | 列表卡上下 12、左右 20 |
| 列表行高 | 64 | 图标 40×40（圆角 10）、标题 14、副标题 12 |
| 右侧栏宽 | 360 | 窄屏（<1180）自动隐藏 |
| 分段控件高 | 36 | 三个等分 tab，选中项胶囊填充强调色 |
| 主按钮高 | 48 | 圆角 12，强调色底 + 深色文字 |
| 卡片阴影 | `0 8 24 0 #14000000` | 深色主题用 `#40000000` |
| 卡片描边 | 1px `#66FFFFFF` | 深色主题 `#1AFFFFFF` |

> 关键视觉语言：**外层（壁纸）模糊、里层（卡片）不模糊**，靠半透明 + 描边 + 阴影把卡片"托"起来。
> 卡片内部**不要**再用大面积色块，靠留白和字号层级区分。

---

## 3. 目标结构

```
MainWindow（Grid，三层）
├── ① Image           壁纸（Stretch=UniformToFill，IsVisible={Binding IsWallpaperVisible}）
├── ② Border          蒙版（WallpaperScrimBrush + WallpaperScrimOpacity）
└── ③ ShellView       背景透明
    ├── TitleBar（48，自绘：拖拽/双击最大化/最小化/最大化/关闭）
    └── Grid（ColumnDefinitions="56,*,360"）
        ├── Rail        左图标轨（导航；可展开成 228 带文字）
        ├── CardHost    中列：一个圆角卡片，里面放当前页面
        └── RightRail   右列：每页可选的详情/操作卡片（IsVisible 由页面决定）
```

**要改的文件**

| 文件 | 改什么 |
| --- | --- |
| `HeliumTodo.App/Views/MainWindow.axaml` | 加 `ExtendClientAreaToDecorationsHint`、`TransparencyLevelHint`；窗口背景透明 |
| `HeliumTodo.App/Views/MainWindow.axaml.cs` | 标题栏拖拽、双击最大化、三个窗口按钮命令 |
| `HeliumTodo.App/Views/ShellView.axaml` | 三列布局 + 卡片宿主 + 右侧栏骨架（**重写**） |
| `HeliumTodo.App/Views/ShellView.axaml.cs` | 只保留 `Loaded` 时同步选中项（见坑 K1） |
| `HeliumTodo.App/Styles/DesignTokens.axaml` | 新增卡片/模糊/阴影令牌（见 §4） |
| `HeliumTodo.App/Styles/Controls.axaml` | 新增/替换 `card`、`railItem`、`chip`、`segmented`、`primaryButton` 样式 |
| `HeliumTodo.App/ViewModels/ShellViewModel.cs` | 图标改 Fluent 字形（§11）、新增 `IsRightRailVisible`/`RightRailTitle` 等透明属性 |
| `HeliumTodo.App/Views/*.axaml`（6 页） | 去掉页面自己画的 `pageTitle` 大标题（标题移到卡片头），内容宽度自适应卡片 |
| `HeliumTodo.App/Services/WallpaperService.cs` | 仅当走 Phase 2 时才改（额外产出一张强模糊图层） |
| `HeliumTodo.App/ViewModels/SettingsViewModel.cs` | 模糊半径范围从 0–30 提到 0–60（配合更"毛玻璃"的外观） |

---

## 4. 设计令牌（在 `DesignTokens.axaml` 里新增）

```xml
<!-- 卡片表面：半透明，壁纸透上来 -->
<ResourceDictionary x:Key="Light">
  <SolidColorBrush x:Key="CardSurfaceBrush" Color="#CCFFFFFF" />
  <SolidColorBrush x:Key="CardSurfaceHoverBrush" Color="#E6FFFFFF" />
  <SolidColorBrush x:Key="CardBorderBrush" Color="#66FFFFFF" />
  <SolidColorBrush x:Key="CardInsetBrush" Color="#0F000000" />   <!-- 列表行 hover -->
</ResourceDictionary>
<ResourceDictionary x:Key="Dark">
  <SolidColorBrush x:Key="CardSurfaceBrush" Color="#B31E1E1E" />
  <SolidColorBrush x:Key="CardSurfaceHoverBrush" Color="#CC262626" />
  <SolidColorBrush x:Key="CardBorderBrush" Color="#1AFFFFFF" />
  <SolidColorBrush x:Key="CardInsetBrush" Color="#14FFFFFF" />
</ResourceDictionary>

<!-- 没开壁纸时用不透明底，避免"半透明压在纯色上"显得脏 -->
<ResourceDictionary x:Key="Light">
  <SolidColorBrush x:Key="CardSurfaceOpaqueBrush" Color="#FFFFFFFF" />
</ResourceDictionary>

<x:Double   x:Key="RailWidth">56</x:Double>
<x:Double   x:Key="TitleBarHeight">48</x:Double>
<x:Double   x:Key="CardGap">12</x:Double>
<x:Double   x:Key="RightRailWidth">360</x:Double>
<CornerRadius x:Key="RadiusCard">18</CornerRadius>
<CornerRadius x:Key="RadiusChip">6</CornerRadius>
<CornerRadius x:Key="RadiusRailItem">12</CornerRadius>
<BoxShadows x:Key="CardShadow">0 8 24 0 #14000000</BoxShadows>
```

**规则**：卡片表面用 `DynamicResource`（换主题要跟着变），且**必须**有"壁纸关掉时换不透明底"的兜底——用一个继承 `IsWallpaperVisible` 的转换器或直接在 `ShellView` 里切两套样式类（推荐后者：`Classes.wallpaper`）。

---

## 5. 自绘标题栏

```xml
<Window ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaChromeHints="NoChrome"
        ExtendClientAreaTitleBarHeightHint="48"
        Background="Transparent"
        TransparencyLevelHint="Mica, AcrylicBlur, Blur">
```

要点：

1. 标题栏那段 `Grid` 绑 `PointerPressed` → `BeginMoveDrag(e)`；`DoubleTapped` → 切换 `WindowState`。
2. 右侧窗口按钮：最小化 `WindowState = Minimized`、最大化/还原 `Normal`/`Maximized`、关闭 `Close()`。
   关闭按钮 hover 用 `#C42B1C` 底 + 白字（Windows 习惯）。
3. **可拖动区域**要避开按钮：给右侧按钮区设 `IsHitTestVisible` 正常、但标题栏的拖动只挂在空白处（用一个 `Border` 包住左侧标题部分）。
4. **兜底**：如果 `ExtendClientAreaToDecorationsHint` 在本机出现"最大化后内容溢出/穿透点击异常"，
   立刻退回原生标题栏（删掉该属性 + 隐藏自绘标题栏），**不要**在窗口样式上死磕。这条兜底写进 PR 说明。

---

## 6. 左侧图标轨

**首选（改动小、风险低）**：继续用 `FANavigationView`，改成紧凑模式：

```xml
<fa:FANavigationView PaneDisplayMode="LeftCompact"
                     IsPaneOpen="False"
                     OpenPaneLength="228"
                     CompactPaneLength="56"
                     IsPaneToggleButtonVisible="True"
                     IsSettingsVisible="False"
                     Background="Transparent" />
```

紧凑模式下条目只剩图标，鼠标悬停/点击会弹出带文字的浮层，`PaneHeader` 收成品牌方块。
选中项的高亮色改成本项目强调色（FA 内部用 `NavigationViewItemBackgroundSelected` 等主题资源，按 §4 覆盖即可）。

**备选（要完全复刻参考图的"图标在 44×44 方块里、选中是实心强调色"）**：自己写 `ItemsControl`：

```xml
<ItemsControl ItemsSource="{Binding MenuItems}" Background="Transparent">
  <ItemsControl.ItemTemplate>
    <DataTemplate x:DataType="vm:NavigationItemViewModel">
      <Button Classes="railItem" Tag="{Binding}" Classes.active="{Binding IsSelected}"
              ToolTip.Tip="{Binding Title}"
              Command="{Binding $parent[ItemsControl].((vm:ShellViewModel)DataContext).SelectItemCommand}"
              CommandParameter="{Binding}">
        <TextBlock FontFamily="{StaticResource IconFont}" Text="{Binding Icon}" FontSize="18" />
      </Button>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`railItem` 样式：`Width/Height=44`、`CornerRadius=12`、`Background=Transparent`；
`:pointerover` → `CardInsetBrush`；`.active` → `SystemAccentColor` 底 + **深色/白色图标按对比度自动选**
（强调色可能是浅黄，图标要用 `#1A1A1A`；这条参考图里很明显：黄色底 + 黑色图标）。

> 无论选哪种，**底部固定项**（设置）要和主分组分开：中间插 1px 分隔线（`#1A000000`，左右各留 8）并 `Spacing=8`。

---

## 7. 卡片系统

一个卡片 = `Border Classes="card"`：

```xml
<Border Classes="card">
  <DockPanel>
    <Grid DockPanel.Dock="Top" Classes="cardHeader">…标题 + 计数胶囊 + 右侧小按钮…</Grid>
    <ScrollViewer>…列表 / 内容…</ScrollViewer>
  </DockPanel>
</Border>
```

`Controls.axaml` 里：

```xml
<Style Selector="Border.card">
  <Setter Property="Background" Value="{DynamicResource CardSurfaceBrush}" />
  <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}" />
  <Setter Property="BorderThickness" Value="1" />
  <Setter Property="CornerRadius" Value="{StaticResource RadiusCard}" />
  <Setter Property="BoxShadow" Value="{StaticResource CardShadow}" />
  <Setter Property="ClipToBounds" Value="True" />
</Style>
```

**列表行**（参考图的"图标 + 标题 + 标签胶囊 + 右对齐两行小字"）：

```xml
<Grid ColumnDefinitions="40,12,*,Auto" Height="64" Margin="0,2">
  <Border Width="40" Height="40" CornerRadius="10" Background="{DynamicResource CardInsetBrush}">
    <TextBlock Text="{Binding Icon}" FontSize="18" HorizontalAlignment="Center" VerticalAlignment="Center" />
  </Border>
  <TextBlock Grid.Column="2" Text="{Binding Title}" FontSize="{StaticResource FontBody}"
             TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
  <StackPanel Grid.Column="3" VerticalAlignment="Center" HorizontalAlignment="Right">
    <TextBlock Text="{Binding MetaLine1}" FontSize="{StaticResource FontCaption}" Opacity="0.55" HorizontalAlignment="Right" />
    <TextBlock Text="{Binding MetaLine2}" FontSize="{StaticResource FontCaption}" Opacity="0.45" HorizontalAlignment="Right" />
  </StackPanel>
</Grid>
```

标签胶囊 `/chip`：`Padding="8,2"`、`CornerRadius=RadiusChip`、`FontSize=10`、`Background=CardInsetBrush`、`Opacity=0.8`。

---

## 8. 右侧栏

- 宽度固定 360；`Margin` 与卡片一致；内容同样是 `Border Classes="card"`。
- **归属**（建议）：

| 页面 | 右侧栏内容 | 是否常显 |
| --- | --- | --- |
| 今日工作流 | 今日重点 + 冲突清单 + 空闲时段 | 是 |
| 专注 | 时钟表盘 + 本日累计 + 快捷开始 | 是（表盘搬到右侧，"时钟样式的专注时长"要求不变） |
| 待办事项 | 筛选 + 完成率 | 可折叠 |
| 日程 | 无（整宽列表更像参考图的列表卡） | 隐藏 |
| 自习室 | 房间信息 + 成员 | 进房间时显示，否则隐藏 |
| 设置 | 无（设置页自身是两列卡片） | 隐藏 |

- 控制方式：`ShellViewModel` 暴露 `bool IsRightRailVisible` + `object? RightRailContent`，
  页面切换时由 `ShellViewModel.NavigateAsync()` 设置；窄屏（`Bounds.Width < 1180`）时列宽收敛为 0。
  用 `Grid.ColumnDefinitions` + 触发器/代码后置改 `Width`，**不要**用两套 XAML 复制粘贴。

---

## 9. 各页映射（只改外观，不改行为）

| 页面 | 现在 | 改完 |
| --- | --- | --- |
| 今日工作流 | 整页 `ScrollViewer` + 多个大卡片 | 卡片里分两列：左"时间轴"（保留 24h 网格与冲突红框），右"今日重点/冲突"放进 RightRail |
| 日程 | 顶部"新建日程"表单很占地方 | 表单收进卡片头的「+ 新建」弹出层或展开区，列表成为主视觉 |
| 待办事项 | 输入行 + 列表 | 输入行做成卡片内的"快速添加"行（无边框、底部 1px 分隔），列表行套 §7 模板 |
| 专注 | 大表盘居中 | 表盘移到 RightRail 卡片，左卡片放"预设/历史/统计" |
| 自习室 | 创建/加入卡 + 房间内两列 | 卡片化，沿用现有"不在房间/在房间"两种状态（见 `--room-check`） |
| 设置 | `FASettingsExpander` 长列表 | 保持 `FASettingsExpander`（功能密度高、别硬套参考图），但外观换成 `Border.card` 包裹，去掉页面大标题 |

> 统一动作：所有页面**删掉页面自己的 `pageTitle` 28px 大标题**（参考图标题在卡片头，14–16px）。
> 标题统一由卡片头承担：`FontSize=16 / SemiBold`。

---

## 10. 模糊效果（分两步，先做能交付的）

### Phase 1（必做，风险低）：全窗模糊 + 半透明卡片

参考图的观感来自"**背景模糊 + 卡片半透明**"，不一定是逐卡片的 backdrop blur。

1. `WallpaperService` 本来就在解码时按 `WallpaperBlurRadius` 做模糊 —— 把这个默认值从 `8` 提到 **`24`**，
   设置页滑块范围改成 `0–60`（`AppConfig.WallpaperBlurRadius` 的类型不用改）。
2. 卡片表面用 §4 的半透明 `CardSurfaceBrush`（浅色 `#CCFFFFFF` ≈ 80% 白）。
3. 蒙版保持用户可调（`WallpaperScrimOpacity`）。
4. **没开壁纸时**：卡片换成不透明 `CardSurfaceOpaqueBrush`，视觉上退化成普通浅色卡片，不会"脏"。

> 这一阶段**不写代码后置、不动布局数学**，纯样式 + 一个默认值，就能达到参考图 80% 的观感。

### Phase 2（可选，真·背景模糊）：把壁纸再画一遍、裁进卡片

只有当用户明确说"要卡片内部也糊"时才做。做法：

```xml
<!-- 卡片内：底下一层和窗口同尺寸的模糊壁纸，往上抵消卡片自身偏移 -->
<Border CornerRadius="18" ClipToBounds="True">
  <Panel>
    <Image Name="BackdropImage" Source="{Binding BlurredWallpaper}"
           Stretch="Fill" Width="{Binding $parent[Window].Bounds.Width}"
           Height="{Binding $parent[Window].Bounds.Height}"
           RenderTransform="{Binding $parent[Window].((vm:ShellViewModel)DataContext).BackdropOffset}" />
    <Border Background="{DynamicResource CardSurfaceBrush}" />
    <!-- 真正的内容 -->
  </Panel>
</Border>
```

- `BackdropOffset` 是 `TranslateTransform`，值 = `-card.TranslatePoint(new Point(0,0), window)`，
  在 `LayoutUpdated` 里更新（低频、只在尺寸/位置变化时算一次，别每帧算）。
- `BlurredWallpaper` 由 `WallpaperService` 额外解出一张**强模糊**（半径 40）+ **半分辨率**的图，
  内存代价约 1/4 张原图；关闭壁纸时不生成。
- 注意：动图壁纸每帧都要更新两张图，帧预算要复核（当前是 192 MB 上限，别撞破）。

**性能红线**：Phase 2 做完后，用 1080p 动图壁纸连续播放 5 分钟，工作集增长不超过 100 MB，帧率不低于 30。

---

## 11. 图标表（现在是 emoji，改成 Fluent 字形）

`ShellViewModel.BuildNavigation()` 里 6 个条目：

| 页面 | 现在 | 改成（`Segoe Fluent Icons` / `Segoe MDL2 Assets` 字形） |
| --- | --- | --- |
| 今日工作流 | `📅` | `\uE80F`（Home）或 `\uE787`（Calendar） |
| 日程 | `🕐` | `\uE787`（Calendar） |
| 待办事项 | `☑` | `\uE73A`（CheckboxComposite）或 `\uE9D5`（CheckList） |
| 专注 | `⏱` | `\uE916`（Stopwatch） |
| 自习室 | `👥` | `\uE716`（People） |
| 设置 | `⚙` | `\uE713`（Setting） |

配套：

```xml
<FontFamily x:Key="IconFont">Segoe Fluent Icons, Segoe MDL2 Assets, Segoe UI Symbol</FontFamily>
```

> Windows 10 有 `Segoe MDL2 Assets`、Windows 11 有 `Segoe Fluent Icons`；两个都写上做兜底。
> 插件注册的页面沿用插件自己给的 `Icon`，**不要**强制改（扩展点 C10）。

---

## 12. 交互与状态规范

| 状态 | 规范 |
| --- | --- |
| 导航项 hover | 背景 `CardInsetBrush`，圆角同条目，**不要**改文字色 |
| 导航项 selected | 强调色实心 + 图标按对比度选深/浅色（见 §6） |
| 导航项 pressed | 在 hover 基础上 `Opacity=0.9` + `ScaleTransform 0.98`（可选） |
| 列表行 hover | `CardInsetBrush`，圆角 10，左右外边距 -8（让悬停块比内容宽一点，参考图就是这个感觉） |
| 卡片焦点 | `:focus-visible` 时 2px 强调色描边（键盘可达性） |
| 分页 tab（右侧栏顶部） | 选中项：强调色胶囊 + 深色文字；未选中：透明底 + 60% 透明度文字 |
| 主按钮 | 高 48、圆角 12、强调色底、文字按对比度自动选深浅；hover 提亮 8%、press 压暗 6% |
| 空态 | 卡片内居中：图标 40 + 一句说明 + 一个主按钮；**不要**用整页空态 |
| 滚动 | 卡片内部滚动（卡片本身不动）；滚动条用细条（`Width=6`、圆角 3、透明度 40%） |

---

## 13. 实施顺序（每个阶段结束都必须能编译、能跑、测试全绿）

**阶段 1 · 令牌与卡片**（不改布局）
1. §4 令牌全部加进 `DesignTokens.axaml`；
2. `Controls.axaml` 加 `Border.card` / `railItem` / `chip` / `segmented` / `primaryButton`；
3. 把 6 个页面里最外层的 `Border.card` 换用新样式（外观变化、结构不变）。
   验收：构建 0/0、119 测试、`--shell` 7 项渲染（截图对比）。

**阶段 2 · 外壳三列**
4. `MainWindow` 加无边框 + 透明背景；`ShellView` 改成 标题栏 + 56 / * / 360 三列；
5. 导航改紧凑模式（或自绘 rail）；窗口按钮接线。
   验收：窗口能拖动/双击最大化/最小化/关闭；导航 6 项可达；`--shell` 渲染通过。

**阶段 3 · 页面迁移**
6. 逐页把内容塞进卡片：先 今日工作流（有右侧栏），再 待办、日程、专注、自习室、设置；
7. 删除各页 `pageTitle`，标题交给卡片头。
   验收：6 页功能全走一遍；`--refresh-check`、`--room-check` 通过。

**阶段 4 · 模糊与打磨**
8. 模糊半径默认 24、滑块 0–60；卡片半透明兜底；
9. （可选）Phase 2 真背景模糊；性能红线验收。
   验收：壁纸开/关两种状态都好看；动图壁纸 5 分钟不涨内存。

---

## 14. 已知坑（这个项目已经踩过，别再踩）

| # | 坑 | 现象 | 做法 |
| --- | --- | --- | --- |
| K1 | `FANavigationView` 内容区空白 | 启动后中间是空的 | `ShellView` 的 `Loaded` 里补 `Nav.SelectedItem = shell.SelectedItem;`（只改字段不选中不会触发 `SelectionChanged`） |
| K2 | `FASettingsExpanderItem` 同时写 `Content` 又塞子元素 | 23 处 `The property 'Content' is assigned more than once.`（**只有 VS 的 XAML 分析器报，`dotnet build` 不报**） | 控件放 `<fa:FASettingsExpanderItem.Footer>` |
| K3 | 自定义控件属性默认 OneWay | 拖了没反应、也不报错 | `AvaloniaProperty.Register(..., defaultBindingMode: BindingMode.TwoWay)` |
| K4 | 强调色写 `Application.Resources["SystemAccentColor"]` | 被 FA 主题字典覆盖，不生效 | 写 `FluentAvaloniaTheme.CustomAccentColor` |
| K5 | `StaticResource` 引用强调色画刷 | 换色后不变 | 画刷的 `Color` 用 `DynamicResource` |
| K6 | `required property` 的模板里用 `index` | `ReferenceError: index is not defined` | 显式 `required property int index` |
| K7 | 自定义属性名撞控件 FINAL 属性 | `Cannot override FINAL property`，整页不可用 | 换名（如 `selectedMinutes`） |
| K8 | `ScrollViewer` 里卡片宽度被压窄 | 卡片只占一半 | 用 `HorizontalAlignment="Stretch"` + 明确 `MaxWidth`，避免双向绑定循环 |
| K9 | `[ObservableProperty]` 字段被直接赋值 | `warning MVVMTK0034` | 走属性，或用 `_isInitialized` 闸门 |
| K10 | Windows 下 .NET 进程写 `%TEMP%` 被拒 | `UnauthorizedAccessException` / `SQLite Error 14` | 自检沙盒一律放**程序目录下** |
| K11 | 页面用 `HasFlag` 判断数据变更 | 单条待办变化时页面不刷新 | `AppDataTypeExtensions.Includes()` |

---

## 15. 验收清单（照抄执行）

```powershell
cd "D:\代码\dsh project\Helium todo demo"

dotnet build HeliumTodo.slnx -c Debug                     # 0 警告 0 错误
dotnet run --project HeliumTodo.SelfTest                  # 119 / 119
dotnet run --project HeliumTodo.RenderCheck -- --shell    # 7 项渲染
dotnet run --project HeliumTodo.RenderCheck -- --refresh-check
dotnet run --project HeliumTodo.RenderCheck -- --backup-check
dotnet run --project HeliumTodo.RenderCheck -- --room-check
powershell tools\check-file-integrity.ps1 -Update         # 指纹基线
```

渲染截图落在 `output/render/`，逐张对照参考图检查：

- [ ] 卡片是"浮"在壁纸上的（有描边 + 阴影 + 半透明），不是贴边的平面块
- [ ] 左侧是纯图标轨，选中项是实心强调色方块，图标颜色随强调色对比度自动切换
- [ ] 标题栏能拖动、双击最大化、三个按钮都有效
- [ ] 壁纸关掉后界面依然干净（卡片不透明、无半透明白雾）
- [ ] 壁纸开着时卡片内文字对比度够（深色壁纸 + 浅色主题也不糊）
- [ ] 窄窗口（1080）下右栏自动收起，主卡片不被挤压
- [ ] 键盘 Tab 能走遍导航与列表，焦点框可见

---

## 16. 明确不做

- 不做 Mica / Acrylic 系统材质（`ExperimentalAcrylicBorder` 与自绘壁纸冲突，且跨平台不一致）；
- 不做窗口阴影外扩/圆角窗口（Windows 上视觉收益小、兼容性问题多）；
- 不引入 Fluent 图标字体文件（用系统自带 `Segoe Fluent Icons` / `Segoe MDL2 Assets`）；
- 不改数据库、不改 Core 层算法、不动联机协议；
- 不为改版新增页面或删减功能（包括自习室的联机开关、备份导入导出）。

---

## 附：给执行 AI 的第一句话（可直接粘贴）

> 请按仓库根目录的 `UI_REDESIGN_BRIEF.md` 执行界面改版，**逐阶段**推进：每完成一个阶段先跑
> `dotnet build HeliumTodo.slnx -c Debug`、`HeliumTodo.SelfTest`、`HeliumTodo.RenderCheck -- --shell`，
> 三者全绿再进入下一阶段；每阶段结束时用 `-- --shell` 重新渲染 `output/render/` 并自查第 15 节清单。
> 严格遵守第 1 节硬约束与第 14 节已知坑；拿不准的视觉细节（宽度、圆角、透明度）在第 2、4 节给出的数值上微调，
> 不要自创新结构。
