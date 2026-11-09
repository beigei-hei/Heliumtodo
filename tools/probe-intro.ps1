# 验证「窗口出现时的淡入 + 轻微放大」在真实窗口里确实逐帧播了。
#
# 为什么需要这个脚本：
#   无头渲染环境里 DispatcherTimer 一次都不触发（实测），所以 --window-check 只能
#   断言"起点是 0 透明度 / 0.96 缩放"和缓动函数本身；帧有没有按时到，必须看真窗口。
#   窗口透明度在 Win32 那边读不出来，于是让程序自己把每帧写进 background.log。
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

. (Join-Path $PSScriptRoot 'probe-common.ps1')

$exe = Join-Path $PSScriptRoot '..\HeliumTodo.Desktop\bin\Debug\net10.0\HeliumTodo.Desktop.exe'
$exe = (Resolve-Path $exe).Path

$sandbox = Join-Path $PSScriptRoot '..\.tmp-intro'
if (Test-Path $sandbox) { Remove-Item $sandbox -Recurse -Force }
New-Item -ItemType Directory -Path $sandbox | Out-Null
$sandbox = (Resolve-Path $sandbox).Path

Write-Host '── 开场动画 ──'
$proc = Start-HeliumApp -Exe $exe -Environment @{ HELIUM_CONFIG_DIR = $sandbox; HELIUM_INTRO_TRACE = '1' }
[void](Wait-HeliumWindow -Process $proc)
Start-Sleep -Seconds 5

$log = Join-Path $sandbox 'background.log'
if (-not (Test-Path $log)) {
    Write-Host '  [失败] 没有生成 background.log'
    $proc.Kill(); exit 1
}

$frames = Get-Content $log -Encoding UTF8 | Where-Object { $_ -like '*[开场动画]*' }
$alphas = @()
foreach ($line in $frames) {
    # 0.3.5 起开场动画改成"遮罩淡出"：每帧记的是遮罩的 Alpha（255 = 全遮，0 = 全透）。
    if ($line -match '遮罩=(\d+)') { $alphas += [double]$Matches[1] }
}

$distinct = ($alphas | Sort-Object -Unique).Count
$last = if ($alphas.Count -gt 0) { $alphas[-1] } else { -1 }
$first = if ($alphas.Count -gt 0) { $alphas[0] } else { -1 }

Write-Host ("  帧数：{0}，不同遮罩 Alpha：{1}，首帧 {2} → 末帧 {3}" -f $frames.Count, $distinct, $first, $last)

$ok = $true
if ($frames.Count -lt 5) { Write-Host '  [失败] 帧太少（至少 5 帧才说明是一帧一帧来的）'; $ok = $false }
if ($distinct -lt 4)     { Write-Host '  [失败] 遮罩几乎没变化（说明是瞬间出现）'; $ok = $false }
if ($first -lt 200)      { Write-Host '  [失败] 首帧遮罩不够厚（应当几乎全遮住，再淡出）'; $ok = $false }
if ($last -gt 5)         { Write-Host '  [失败] 最后一帧遮罩没有完全透掉'; $ok = $false }

$proc.Kill()
Start-Sleep -Milliseconds 800
Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
Clear-HeliumEnvironment -Names @('HELIUM_CONFIG_DIR', 'HELIUM_INTRO_TRACE')

if ($ok) { Write-Host '  ✔ 开场遮罩逐帧淡出，并收尾到完全透明' }
exit ([int](-not $ok))
