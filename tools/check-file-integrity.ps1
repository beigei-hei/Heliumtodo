# Helium Todo · 源文件完整性校验
#
# 为什么需要这个？
#   有一次脚本批量改文件时，误在 AppConfig.cs 开头插进了一个字母 S
#   （把 using 变成了 Susing），编译才发现。
#   如果被改坏的是注释或字符串内容，编译器是**看不出来**的 —— 那才危险。
#
#   所以用它给所有源文件算一份指纹（哈希）：
#     · 改完代码跑一次 -Update，把当前状态记为「正确的样子」
#     · 以后怀疑文件被动过，跑一次校验，它对不上的文件会指出来
#
# 用法：
#   pwsh tools/check-file-integrity.ps1            # 校验
#   pwsh tools/check-file-integrity.ps1 -Update    # 更新指纹（确认当前状态是正确的之后再用）

param(
    # 把当前的文件指纹记录/更新为最新状态
    [switch]$Update
)

$ErrorActionPreference = 'Stop'

# 解决方案根目录（本脚本在 tools/ 下面）
$root = Split-Path $PSScriptRoot -Parent
$manifestPath = Join-Path $PSScriptRoot 'file-hashes.json'

# 要被校验的文件类型
$include = @('*.cs', '*.axaml', '*.csproj', '*.props', '*.slnx', '*.json', '*.md', '*.ps1', '*.manifest')

# 不参与校验的目录（编译产物、数据库、渲染输出）
#
# ⚠️ `cpp` 也要排除 —— 那是后来加的 C++ 移植项目，是**独立**的一份东西，
#    有自己的构建目录（里面还有 FetchContent 下载的第三方源码）。
#    不排除的话，本来 84 个文件的基线会膨胀到近千个，
#    而且 C++ 那边一改就会让 C# 的校验报「文件被改动」—— 完全是误报。
$excludeDirs = @('bin', 'obj', 'output', 'Data', '.git', '.vs', 'cpp', 'dist', 'node_modules')

Write-Host "══════════════════════════════════════════════════"
Write-Host "  Helium Todo · 源文件完整性校验"
Write-Host "  根目录：$root"
Write-Host "══════════════════════════════════════════════════"
Write-Host ""

# 不被校验的文件（这些是本工具自己产出的，会自己改自己）
$excludeFiles = @('file-hashes.json')

# ── 收集所有要校验的文件 ──
$files = Get-ChildItem -Path $root -Recurse -File -Include $include | Where-Object {
    $relative = $_.FullName.Substring($root.Length + 1)
    $parts = $relative -split '[\\/]'
    -not ($parts | Where-Object { $excludeDirs -contains $_ }) -and
    -not ($excludeFiles -contains $_.Name)
}

Write-Host "找到 $($files.Count) 个源文件。"
Write-Host ""

# ── 算指纹 ──
$hashes = [ordered]@{}
foreach ($f in $files | Sort-Object FullName) {
    $relative = $f.FullName.Substring($root.Length + 1).Replace('\', '/')

    # ⚠️ 必须用 -LiteralPath。
    #    文件名里只要有 [ ] 这样的字符，PowerShell 就会当成通配符，
    #    报 "The specified wildcard character pattern is not valid: operator[].md"。
    #    （这个是 C++ 那边文档里的一个文件名，扫到它就炸了。）
    $hash = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
    $hashes[$relative] = $hash
}

# ── 更新模式 ──
if ($Update) {
    $payload = [ordered]@{
        generatedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        fileCount   = $hashes.Count
        files       = $hashes
    }

    $json = $payload | ConvertTo-Json -Depth 5
    # 明确用「不带 BOM 的 UTF-8」写：带 BOM 的 JSON 会让某些工具读起来多出奇怪字符
    [System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host "已更新指纹文件：$manifestPath" -ForegroundColor Green
    Write-Host "共记录 $($hashes.Count) 个文件。"
    exit 0
}

# ── 校验模式 ──
if (-not (Test-Path $manifestPath)) {
    Write-Host "还没有指纹文件。先跑一次带 -Update 的来建立基线。" -ForegroundColor Yellow
    exit 1
}

$saved = Get-Content $manifestPath -Raw | ConvertFrom-Json
$expected = @{}
foreach ($prop in $saved.files.PSObject.Properties) {
    $expected[$prop.Name] = $prop.Value
}

Write-Host "指纹基线建立于：$($saved.generatedAt)（$($saved.fileCount) 个文件）"
Write-Host ""

$changed = @()
$missing = @()
$added = @()

foreach ($relative in $expected.Keys) {
    $full = Join-Path $root ($relative.Replace('/', '\'))

    if (-not (Test-Path $full)) {
        $missing += $relative
        continue
    }

    $actual = (Get-FileHash -Path $full -Algorithm SHA256).Hash
    if ($actual -ne $expected[$relative]) {
        $changed += $relative
    }
}

foreach ($relative in $hashes.Keys) {
    if (-not $expected.ContainsKey($relative)) {
        $added += $relative
    }
}

# ── 报告 ──
if ($changed.Count -eq 0 -and $missing.Count -eq 0 -and $added.Count -eq 0) {
    Write-Host "所有文件都和基线一致，没有被动过 ✔" -ForegroundColor Green
    exit 0
}

if ($changed.Count -gt 0) {
    Write-Host "内容变了（$($changed.Count) 个）：" -ForegroundColor Yellow
    $changed | ForEach-Object { Write-Host "  ~ $_" }
    Write-Host ""
}

if ($missing.Count -gt 0) {
    Write-Host "文件不见了（$($missing.Count) 个）：" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
}

if ($added.Count -gt 0) {
    Write-Host "新增文件（$($added.Count) 个）：" -ForegroundColor Cyan
    $added | ForEach-Object { Write-Host "  + $_" }
    Write-Host ""
}

Write-Host "如果这些改动都是你有意做的，跑一次 -Update 把基线更新掉。" -ForegroundColor Yellow
exit 1
