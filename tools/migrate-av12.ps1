#requires -Version 5.1
<#
    migrate-av12.ps1 —— 把项目从 Avalonia 11.3.2 + FluentAvaloniaUI 2.4.0
                        迁移到 Avalonia 12.1.2 + FluentAvaloniaUI 3.1.0

    为什么写成脚本而不是手工一个个改？
      要改 16 个文件、几十处替换，手工改容易漏；
      脚本还能【保留每个文件原有的 BOM 和行尾】——
      这一点很关键：edit 工具会把 LF 文件写成 CRLF，
      而项目的指纹基线是按字节算的，行尾一变哈希就全变了。

    用法：
      powershell -ExecutionPolicy Bypass -File tools\migrate-av12.ps1 -WhatIf   # 只看会改什么
      powershell -ExecutionPolicy Bypass -File tools\migrate-av12.ps1          # 真的改
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Root = ''
)

$ErrorActionPreference = 'Stop'

# ⚠️ $PSScriptRoot 不能写在 param 的默认值里 ——
#    PowerShell 5.1 在解析 param 块时它还是空字符串，会直接报错：
#    Cannot bind argument to parameter 'Path' because it is an empty string.
#    所以挪到函数体里算。
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent $PSScriptRoot
}
$Root = (Resolve-Path $Root).Path
$script:changed = @()
$script:skipped = @()

# ─────────────────────────────────────────────────────────────
#  工具函数：按「原文件的编码 + 行尾」写回去
# ─────────────────────────────────────────────────────────────

function Get-FileInfo {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($hasBom) { $text = $text.Substring(1) }   # 去掉 BOM 字符 U+FEFF
    $crlf = ([regex]::Matches($text, "`r`n")).Count
    $lfOnly = ([regex]::Matches($text, "(?<!`r)`n")).Count
    $eol = if ($crlf -gt 0 -and $lfOnly -eq 0) { "`r`n" }
           elseif ($lfOnly -gt 0 -and $crlf -eq 0) { "`n" }
           else { "`n" }   # 混合或单行文件：按 LF 处理
    return [pscustomobject]@{ Text = $text; HasBom = $hasBom; Eol = $eol }
}

function Save-FileInfo {
    param([string]$Path, [string]$Text, [bool]$HasBom)
    $enc = New-Object System.Text.UTF8Encoding($HasBom)
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

# 在指定文件里做一串「字面量替换」，任何一处没匹配上都报错 —— 避免静默漏改
function Edit-Text {
    param(
        [string]$Path,
        [hashtable[]]$Replacements,     # 每项：@{ Old=...; New=... } 或 @{ Old=...; New=...; Optional=$true }
        [string]$Note = '',
        # 整段都匹配不上时不报错，只提示。
        # 用于「脚本重复跑时本来就该匹配不上」的那些步骤 ——
        # 比如「删掉 Avalonia.Diagnostics 引用」，第一次跑删掉了，第二次自然找不到。
        # 注意：这和单条替换的 Optional 不是一回事，那个是「同一批里某一条可缺」。
        [switch]$AllowNoMatch
    )
    $full = Join-Path $Root $Path
    if (-not (Test-Path $full)) { throw "文件不存在：$full" }

    $info = Get-FileInfo -Path $full
    $text = $info.Text
    $touched = $false

    foreach ($r in $Replacements) {
        $old = $r.Old
        $new = $r.New
        $optional = [bool]$r.Optional

        # 统一行尾：先把替换串统一成 LF，再换成这个文件自己的行尾。
        #
        # ⚠️ 这两步都必须【无条件执行】，不能像最初那样只在外壳文件不是 LF 时才对替换串动手。
        #    踩过的坑：脚本本身被我转成了 CRLF，于是 here-string 里的换行也变成 CRLF；
        #    而目标 .csproj 是 LF —— 两边对不上，Contains() 直接返回 false，
        #    脚本报「找不到这段内容」。空跑（-WhatIf）正好把这个错抓了出来。
        $old = ($old -replace "`r`n", "`n") -replace "`n", $info.Eol
        $new = ($new -replace "`r`n", "`n") -replace "`n", $info.Eol

        if ($text.Contains($old)) {
            $text = $text.Replace($old, $new)
            $touched = $true
        }
        elseif (-not $optional) {
            if ($AllowNoMatch) {
                Write-Host ("  [略] {0,-52} {1}" -f $Path, "$Note（本来就没有）") -ForegroundColor DarkGray
                return
            }
            throw "在 $Path 里找不到这段内容（迁移脚本写的匹配串和文件对不上）：`n$($old.Substring(0, [Math]::Min(200, $old.Length)))..."
        }
    }

    if ($touched) {
        if ($PSCmdlet.ShouldProcess($Path, '改写')) {
            Save-FileInfo -Path $full -Text $text -HasBom $info.HasBom
            $script:changed += $Path
            Write-Host ("  [改] {0,-52} {1}" -f $Path, $Note) -ForegroundColor Green
        }
    }
    else {
        $script:skipped += $Path
        Write-Host ("  [略] {0,-52} {1}" -f $Path, $Note) -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Avalonia 11 → 12 迁移" -ForegroundColor Cyan
Write-Host "  项目根：$Root" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan

# ─────────────────────────────────────────────────────────────
#  ① 中央包版本
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "① 中央包版本（Directory.Packages.props）" -ForegroundColor Yellow

$propsPath = Join-Path $Root 'Directory.Packages.props'
if (-not (Test-Path $propsPath)) { throw "找不到 $propsPath" }
$propsInfo = Get-FileInfo -Path $propsPath
$E = $propsInfo.Eol

$newProps = @'
<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>

    <!--
      ⚠️ 这一项必须是 true，否则上面的 PackageVersion 对【传递依赖】不生效。

      踩过的坑：本来是 false，以为在 PackageVersion 里写
      「Avalonia.Controls.DataGrid 12.1.2」就能把它提上去，结果提不上去。
      因为 false 的含义是「中央版本只用于直接引用的包」；
      DataGrid 是 FluentAvaloniaUI 拖进来的间接依赖，不归它管。

      症状：输出目录里只有 DataGrid 是旧版本、其余全是新版本。
      这种混版会让 IDE 的语法分析跨版本解析类型时出错
      （表现就是莫名其妙标红，但编译能过），运行时也可能加载失败。

      改成 true 之后，中央版本对传递依赖同样有约束力，整棵树就统一了。
    -->
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <!--
      ===== 界面：Avalonia 12 =====

      ⚠️ 为什么是 12.1.2 而不是最新的 12.1.4？

        因为 Avalonia.Controls.DataGrid 只发到 12.1.2，
        而主包已经到 12.1.4。整套必须取【交集】——
        否则还原时直接报 NU1102「找不到版本为 (>= 12.1.4) 的包」。

      这不是第一次踩这个坑了：升 Avalonia 时一定要去 NuGet 索引
      逐个核对可用版本，不能假设所有子包同步发版。
    -->
    <PackageVersion Include="Avalonia" Version="12.1.2" />
    <PackageVersion Include="Avalonia.Desktop" Version="12.1.2" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="12.1.2" />
    <PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.2" />

    <!--
      Avalonia 12 把文字排版（HarfBuzz 整形）拆成了独立包，
      不显式引用的话运行时会在渲染文字时找不到实现。
      Avalonia 11 时代它埋在 Skia 包里，不需要单独引用。
    -->
    <PackageVersion Include="Avalonia.HarfBuzz" Version="12.1.2" />

    <!--
      下面这两个我们【没有直接用】，但必须显式钉住版本。
      原因见上面 CentralPackageTransitivePinningEnabled 的注释 ——
      它们是 FluentAvaloniaUI 拖进来的间接依赖。

      DataGrid 的 12.1.2 也是「整套只能到 12.1.2」的那个卡点。

      ⚠️ 以后升级 Avalonia 时记得【同时改这几行】，否则又会悄悄退回混版。

      ⚠️ 还有一条 XML 的坑要记住：**注释里不能出现连续两个减号**。
         曾经在这里写过一句检查命令，那两个减号让整个文件的 XML 直接非法，
         MSBuild 报出一堆莫名其妙的 NU1010（说所有包都没有对应的 PackageVersion）。
    -->
    <PackageVersion Include="Avalonia.Controls.DataGrid" Version="12.1.2" />
    <PackageVersion Include="Avalonia.Controls.ColorPicker" Version="12.1.2" />

    <!-- 无头渲染：不开窗口也能把界面渲染成图片，用来自动检查界面有没有画错 -->
    <PackageVersion Include="Avalonia.Headless" Version="12.1.2" />

    <!--
      ===== Fluent 2 设计系统（FluentAvaloniaUI 3）=====

      ⚠️ 3.x 有一个硬限制：它【只有 net10.0 一个目标框架】。
         所以整个解决方案必须跟着升到 net10.0 ——
         这就是这次迁移要改 TargetFramework 的根本原因，
         不是我们想升，是被 FluentAvaloniaUI 3 逼的。

      另一个大变化：3.x 给几乎所有公开类型加了 FA 前缀，
      用来避开和 Avalonia 官方同名控件的冲突。
        2.4.0                       3.1.0
        NavigationView            → FANavigationView
        NavigationViewItem        → FANavigationViewItem
        InfoBar                   → FAInfoBar
        SettingsExpander          → FASettingsExpander
        SettingsExpanderItem      → FASettingsExpanderItem
        FontIconSource            → FAFontIconSource
        252 个导出类型里有 224 个（88%）改了名。

      例外：FluentAvaloniaTheme 没改名，命名空间 FluentAvalonia.Styling 也没变。
    -->
    <PackageVersion Include="FluentAvaloniaUI" Version="3.1.0" />

    <!-- ===== MVVM 工具包：源生成器写法，省掉大量样板代码 ===== -->
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />

    <!-- ===== 数据库：EF Core + SQLite ===== -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11" />

    <!-- ===== 依赖注入 / 日志 ===== -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Debug" Version="8.0.1" />

    <!-- ===== 测试 ===== -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <!--
    升级 Avalonia 之后怎么检查有没有混版？

      用 dotnet list 命令列出传递依赖（开关名见下面那行纯文本），
      输出里所有 Avalonia 开头的程序集版本号应该完全一致。

    开关名的写法：include 前面加两个减号，再写 transitive。
    （之所以不直接写出来，是因为 XML 注释里禁止连续两个减号。）
  -->

</Project>
'@

if ($propsInfo.Eol -ne "`n") { $newProps = $newProps -replace "`r`n", "`n" -replace "`n", $propsInfo.Eol }

if ($PSCmdlet.ShouldProcess('Directory.Packages.props', '整体重写')) {
    Save-FileInfo -Path $propsPath -Text $newProps -HasBom $propsInfo.HasBom
    $script:changed += 'Directory.Packages.props'
    Write-Host ("  [改] {0,-52} {1}" -f 'Directory.Packages.props', 'Avalonia 12.1.2 + FA 3.1.0') -ForegroundColor Green
}

# ─────────────────────────────────────────────────────────────
#  ② 目标框架 net8.0 → net10.0
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "② 目标框架 net8.0 → net10.0（6 个 csproj）" -ForegroundColor Yellow

$csprojs = @(
    'HeliumTodo.App\HeliumTodo.App.csproj'
    'HeliumTodo.Core\HeliumTodo.Core.csproj'
    'HeliumTodo.Data\HeliumTodo.Data.csproj'
    'HeliumTodo.RenderCheck\HeliumTodo.RenderCheck.csproj'
    'HeliumTodo.SelfTest\HeliumTodo.SelfTest.csproj'
    'HeliumTodo.Tests\HeliumTodo.Tests.csproj'
)

$tfNote = @'
<!--
      ⚠️ 这里必须是 net10.0。

      不是「想用新版本」，而是 FluentAvaloniaUI 3.x 只发布了 net10.0 一个目标框架，
      引用它就等于要求整个解决方案都跑在 net10.0 上。
      Avalonia 12 本身支持 net8.0 / net10.0 两个，卡点只在于 FA 3。
    -->
'@

foreach ($p in $csprojs) {
    Edit-Text -Path $p -Note 'net8.0 → net10.0' -Replacements @(
        # Optional：脚本要能重复跑。已经是 net10.0 的话这里什么都不做，不算失败。
        @{ Old = '<TargetFramework>net8.0</TargetFramework>'; New = '<TargetFramework>net10.0</TargetFramework>'; Optional = $true }
    )
}

# ─────────────────────────────────────────────────────────────
#  ③ 移除 Avalonia.Diagnostics（12 里这个包没了）
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "③ 移除 Avalonia.Diagnostics（Avalonia 12 已删除该包）" -ForegroundColor Yellow

Edit-Text -Path 'HeliumTodo.App\HeliumTodo.App.csproj' -Note '删 Diagnostics' -AllowNoMatch -Replacements @(
    @{
        Old = @'
  <ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <PackageReference Include="Avalonia.Diagnostics" />
  </ItemGroup>

'@
        New = @'
  <!--
    ⚠️ Avalonia 12 删掉了 Avalonia.Diagnostics 这个包（开发者工具窗口）。
       我们已经把它从引用里去掉。
       反正代码里从来没调用过 AttachDevTools()，等于只是个挂在依赖里的空引用。

       以后要调试界面，Avalonia 12 改用 Avalonia DevTools 独立工具。
  -->

'@
    }
)

# ─────────────────────────────────────────────────────────────
#  ④ 加 Avalonia.HarfBuzz 显式引用
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "④ 加 Avalonia.HarfBuzz 显式引用" -ForegroundColor Yellow

foreach ($p in @('HeliumTodo.App\HeliumTodo.App.csproj', 'HeliumTodo.RenderCheck\HeliumTodo.RenderCheck.csproj')) {
    # 脚本要能重复跑：已经有 HarfBuzz 引用就直接跳过，不然会加两遍
    $full = Join-Path $Root $p
    if ((Get-FileInfo -Path $full).Text.Contains('Avalonia.HarfBuzz')) {
        Write-Host ("  [略] {0,-52} {1}" -f $p, '已有 HarfBuzz') -ForegroundColor DarkGray
        continue
    }
    Edit-Text -Path $p -Note '加 HarfBuzz' -Replacements @(
        @{
            Old = '    <PackageReference Include="Avalonia.Fonts.Inter" />'
            New = @'
    <PackageReference Include="Avalonia.Fonts.Inter" />
    <!--
      Avalonia 12 把文字排版（HarfBuzz 整形）拆成了独立包。
      Avalonia 11 时代它埋在 Skia 里，所以老代码里看不到这个引用。
    -->
    <PackageReference Include="Avalonia.HarfBuzz" />
'@
        }
    )
}

# ─────────────────────────────────────────────────────────────
#  ⑤ App.axaml.cs：BindingPlugins 整段删掉
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "⑤ App.axaml.cs：移除 BindingPlugins 相关代码" -ForegroundColor Yellow

Edit-Text -Path 'HeliumTodo.App\App.axaml.cs' -Note '删 using' -AllowNoMatch -Replacements @(
    @{ Old = "using Avalonia.Data.Core.Plugins;`n"; New = '' }
)

Edit-Text -Path 'HeliumTodo.App\App.axaml.cs' -Note '删调用' -AllowNoMatch -Replacements @(
    @{
        Old = @'
    public override void OnFrameworkInitializationCompleted()
    {
        // ── 关掉 Avalonia 自带的「用反射找属性」的绑定方式 ──
        // 我们全部用「编译期绑定」（编译时就能查出拼错的属性名），
        // 两者同时开会导致同一个属性被绑定两次，所以这里去掉旧的。
        RemoveDuplicateValidationPlugins();

        // ── ① 配置 ──
'@
        New = @'
    public override void OnFrameworkInitializationCompleted()
    {
        // ── 注意：这里以前有一行 RemoveDuplicateValidationPlugins()，迁移到 Avalonia 12 时删掉了 ──
        //
        // 那行代码的作用是：把 Avalonia 自带的「用反射找属性」的校验插件从
        // BindingPlugins.DataValidators 里摘掉，因为我们全用编译期绑定，
        // 两种方式同时开会让同一个属性被通知两次。
        //
        // Avalonia 12 做了两件事，让这段代码既编译不过、也没必要存在了：
        //   1. BindingPlugins 这个静态类被删掉了（连同 DataAnnotationsValidationPlugin）；
        //   2. 那些校验插件默认就是不启用的，只在真正需要时才会注册。
        // 所以直接删掉即可，不需要任何替代写法。

        // ── ① 配置 ──
'@
    }
)

Edit-Text -Path 'HeliumTodo.App\App.axaml.cs' -Note '删方法' -AllowNoMatch -Replacements @(
    @{
        Old = @'
    /// <summary>
    /// 去掉 Avalonia 默认的反射式绑定校验插件。
    /// 不去掉的话，同一个属性会被通知两次，界面上会看到重复刷新甚至报错。
    /// </summary>
    private static void RemoveDuplicateValidationPlugins()
    {
        var toRemove = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>()
            .ToArray();

        foreach (var plugin in toRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

'@
        New = ''
    }
)

# ─────────────────────────────────────────────────────────────
#  ⑥ ShellView.axaml.cs：C# 里的 FA 类型名
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "⑥ ShellView.axaml.cs：FA 前缀（C# 侧）" -ForegroundColor Yellow

Edit-Text -Path 'HeliumTodo.App\Views\ShellView.axaml.cs' -Note 'FA 类型名' -AllowNoMatch -Replacements @(
    @{ Old = 'NavigationViewSelectionChangedEventArgs e)'; New = 'FANavigationViewSelectionChangedEventArgs e)' }
    @{ Old = 'if (e.SelectedItem is NavigationViewItem { Tag: NavigationItemViewModel item })'
       New = 'if (e.SelectedItem is FANavigationViewItem { Tag: NavigationItemViewModel item })' }
    @{
        Old = @'
    /// 说明：这里用事件而不是命令，是因为 FluentAvalonia 的 NavigationView
    /// 在模板里生成的是 NavigationViewItem 控件，
    /// 用事件拿到它的 Tag（我们存了对应的 ViewModel）最直接、最不容易出错。
'@
        New = @'
    /// 说明：这里用事件而不是命令，是因为 FluentAvalonia 的 NavigationView
    /// 在模板里生成的是 NavigationViewItem 控件，
    /// 用事件拿到它的 Tag（我们存了对应的 ViewModel）最直接、最不容易出错。
    ///
    /// ⚠️ 迁移到 FluentAvaloniaUI 3 时，下面两个类型名都要加 FA 前缀：
    ///      NavigationView                 → FANavigationView
    ///      NavigationViewItem             → FANavigationViewItem
    ///      NavigationViewSelectionChangedEventArgs
    ///                                     → FANavigationViewSelectionChangedEventArgs
    ///    加前缀是为了避开和 Avalonia 官方同名控件的冲突（3.x 里 88% 的类型都加了）。
'@
    }
)

# ─────────────────────────────────────────────────────────────
#  ⑦ XAML 里的 fa: 控件名加 FA 前缀
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "⑦ XAML：fa: 控件名加 FA 前缀（5 个文件）" -ForegroundColor Yellow

# 顺序很重要：先长后短。
#   SettingsExpanderItem 要先于 SettingsExpander 替换，
#   否则 fa:SettingsExpanderItem 会先被改成 fa:FASettingsExpanderItem 的第一步
#   —— 其实那样也对，但保险起见还是按长度从长到短走一遍。
# 注意 fa: 后面的 FA 是连在一起的，所以替换完长名之后，
# 短名的匹配串（fa:SettingsExpander）不会再命中已经变成 fa:FASettingsExpanderItem 的地方。
# ⚠️ 每一项都标记 Optional —— 因为不是每个 XAML 都有全部控件
#    （ShellView 只用 NavigationView，SettingsView 只用 SettingsExpander）。
#    强制要求每项都命中反而会误报。真正的一致性由后面的「残留检查」来保证。
$xamlRenames = @(
    @{ Old = 'fa:SettingsExpanderItem'; New = 'fa:FASettingsExpanderItem'; Optional = $true }
    @{ Old = 'fa:SettingsExpander';     New = 'fa:FASettingsExpander';     Optional = $true }
    @{ Old = 'fa:NavigationViewItem';   New = 'fa:FANavigationViewItem';   Optional = $true }
    @{ Old = 'fa:NavigationView';       New = 'fa:FANavigationView';       Optional = $true }
    @{ Old = 'fa:InfoBar';              New = 'fa:FAInfoBar';              Optional = $true }
    @{ Old = 'fa:FontIconSource';       New = 'fa:FAFontIconSource';       Optional = $true }
)

$xamlFiles = @(
    'HeliumTodo.App\Views\ShellView.axaml'
    'HeliumTodo.App\Views\SettingsView.axaml'
    'HeliumTodo.App\Views\FocusView.axaml'
    'HeliumTodo.App\Views\StudyRoomView.axaml'
)

foreach ($x in $xamlFiles) {
    Edit-Text -Path $x -Note 'fa: 前缀' -Replacements $xamlRenames
}

# ── 残留检查：所有 fa: 后面应该都跟着 FA ──
#    这一步是「保险丝」：万一将来 FA 又改了某个名字，这里会立刻报出来，
#    而不是等到编译时报一个看不懂的 XAML 错误。
Write-Host ""
Write-Host "⑦b 残留检查：fa: 后面是否都带了 FA" -ForegroundColor Yellow

$bad = @()
foreach ($x in $xamlFiles) {
    $full = Join-Path $Root $x
    $content = (Get-FileInfo -Path $full).Text

    # ⚠️ 先在【内存里】跑一遍替换，再检查替换后的结果。
    #    不能直接查磁盘上的文件 —— 因为 -WhatIf 空跑时文件根本没被写，
    #    查到的还是原名，会满屏误报（第一版就栽在这里）。
    #    这样写还有个好处：空跑也能真正验证「改名规则是否覆盖完整」。
    foreach ($r in $xamlRenames) {
        $content = $content.Replace($r.Old, $r.New)
    }

    # 抓出所有 fa:Name 形式的标识符，凡是不以 FA 开头的都算漏网
    foreach ($m in [regex]::Matches($content, 'fa:([A-Za-z][A-Za-z0-9]*)')) {
        $name = $m.Groups[1].Value
        if (-not $name.StartsWith('FA')) {
            $bad += "$x → fa:$name"
        }
    }
}
if ($bad.Count -gt 0) {
    throw ("以下控件名还没有加 FA 前缀：`n  " + ($bad -join "`n  "))
}
Write-Host "  ✓ 没有漏网的" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────
#  ⑧ 更新 global.json 的注释（版本号本身不用改）
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "⑧ global.json 注释同步" -ForegroundColor Yellow

Edit-Text -Path 'global.json' -Note '注释同步' -AllowNoMatch -Replacements @(
    @{
        Old = @'
  //  为什么是 10 而不是 8？
  //    项目的**目标框架仍然是 net8.0**（见各 .csproj 的 TargetFramework），
  //    也就是说编译出来的程序照样跑在 .NET 8 上，用户机器只装 .NET 8 运行时就行。
  //    这里锁的只是「拿哪个 SDK 来编译」。
'@
        New = @'
  //  为什么是 10？
  //    ① 目标框架已经是 net10.0（见各 .csproj 的 TargetFramework），
  //       因为 FluentAvaloniaUI 3 只发布了 net10.0 一个目标框架。
  //    ② 解决方案文件用的是 .slnx 新格式，需要 MSBuild 17.13+ 才认识。
  //       用 .NET 8 的 SDK 去编译 .slnx 会报「无法识别元素 <Solution>」。
'@
    }
    @{
        Old = @'
  //  换电脑时如果只装了 .NET 8 SDK，两条路：
  //    a) 装一个 .NET 10 SDK（推荐）
  //    b) 把下面的 version 改成 8.0.x，并把解决方案文件换回 .sln 格式
'@
        New = @'
  //  换电脑时必须装 .NET 10 SDK（运行时也要 10）。
  //  只装 .NET 8 SDK 已经不够了 —— 因为目标框架是 net10.0。
'@
    }
)

# ─────────────────────────────────────────────────────────────
#  ⑨ TextBox.Watermark → TextBox.PlaceholderText
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "⑨ TextBox.Watermark → PlaceholderText（11 处）" -ForegroundColor Yellow

# ⚠️ 只替换 Watermark，不要碰 ComboBox 已经在用的 PlaceholderText。
#    匹配串带上等号（Watermark="）最保险，不会误伤别的东西。
$wmFiles = @(
    'HeliumTodo.App\Views\FocusView.axaml'
    'HeliumTodo.App\Views\ScheduleView.axaml'
    'HeliumTodo.App\Views\StudyRoomView.axaml'
    'HeliumTodo.App\Views\TodoView.axaml'
)
foreach ($x in $wmFiles) {
    Edit-Text -Path $x -Note 'Watermark→PlaceholderText' -Replacements @(
        @{ Old = 'Watermark="'; New = 'PlaceholderText="'; Optional = $true }
    )
}

# ─────────────────────────────────────────────────────────────
#  ⑩ RenderCheck：SystemDecorations → WindowDecorations、Bitmap.Save 新重载
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "⑩ RenderCheck：两处 Avalonia 12 API 变化" -ForegroundColor Yellow

Edit-Text -Path 'HeliumTodo.RenderCheck\Program.cs' -Note 'WindowDecorations' -Replacements @(
    @{
        Old = @'
        var window = new Window
        {
            Width = width,
            Height = height,
            SystemDecorations = SystemDecorations.None,
'@
        New = @'
        var window = new Window
        {
            Width = width,
            Height = height,
            // ⚠️ Avalonia 12 的改名：SystemDecorations → WindowDecorations。
            //    枚举类型本身也换了位置，现在就叫 Avalonia.Controls.WindowDecorations
            //    （Avalonia 11 里是 Avalonia.Controls.SystemDecorations）。
            //    属性名要跟着一起改，否则虽然能编译，但用的是标了 [Obsolete] 的旧属性。
            WindowDecorations = WindowDecorations.None,
'@
    }
    @{
        Old = @'
        var path = Path.Combine(outputDirectory, $"{Sanitize(item.Title)}.png");
        frame.Save(path);
'@
        New = @'
        var path = Path.Combine(outputDirectory, $"{Sanitize(item.Title)}.png");

        // ⚠️ Avalonia 12 把 frame.Save(path) 标成过时了，现在要显式传编码参数。
        //    BitmapEncoderOptions 是抽象类，PNG 用它的子类 PngBitmapEncoderOptions，
        //    直接拿内置的 Default 实例就等于「按默认参数编码 PNG」。
        frame.Save(path, PngBitmapEncoderOptions.Default);
'@
    }
)

# ─────────────────────────────────────────────────────────────
#  汇总
# ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ("  改了 {0} 个文件，{1} 个没动" -f $script:changed.Count, $script:skipped.Count) -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Cyan
