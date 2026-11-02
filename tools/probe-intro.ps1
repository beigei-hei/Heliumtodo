# 验证「窗口出现时的淡入 + 轻微放大」在真实窗口里确实逐帧播了。
#
# 为什么需要这个脚本：
#   无头渲染环境里 DispatcherTimer 一次都不触发（实测），所以 --window-check 只能
#   断言"起点是 0 透明度 / 0.96 缩放"和缓动函数本身；帧有没有按时到，必须看真窗口。
#   窗口透明度在 Win32 那边读不出来，于是让程序自己把每帧写进 background.log。
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$exe = Join-Path $PSScriptRoot '..\HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe'
$exe = (Resolve-Path $exe).Path

$sandbox = Join-Path $PSScriptRoot '..\.tmp-intro'
if (Test-Path $sandbox) { Remove-Item $sandbox -Recurse -Force }
New-Item -ItemType Directory -Path $sandbox | Out-Null
$sandbox = (Resolve-Path $sandbox).Path

$env:HELIUM_CONFIG_DIR = $sandbox
$env:HELIUM_INTRO_TRACE = '1'

Write-Host '── 开场动画 ──'
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 7

$log = Join-Path $sandbox 'background.log'
if (-not (Test-Path $log)) {
    Write-Host '  [失败] 没有生成 background.log'
    $proc.Kill(); exit 1
}

$frames = Get-Content $log -Encoding UTF8 | Where-Object { $_ -like '*[开场动画]*' }
$opacities = @()
foreach ($line in $frames) {
    if ($line -match 'opacity=([0-9.]+)') { $opacities += [double]$Matches[1] }
}

$distinct = ($opacities | Sort-Object -Unique).Count
$last = if ($opacities.Count -gt 0) { $opacities[-1] } else { -1 }

Write-Host ("  帧数：{0}，不同不透明度：{1}，最后一帧不透明度：{2}" -f $frames.Count, $distinct, $last)

$ok = $true
if ($frames.Count -lt 5) { Write-Host '  [失败] 帧太少（至少 5 帧才说明是一帧一帧来的）'; $ok = $false }
if ($distinct -lt 4)    { Write-Host '  [失败] 不透明度几乎没变化（说明是瞬间出现）'; $ok = $false }
if ($last -lt 0.99)     { Write-Host '  [失败] 最后一帧没有回到完全体'; $ok = $false }

$proc.Kill()
Start-Sleep -Milliseconds 800
Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item Env:\HELIUM_CONFIG_DIR -ErrorAction SilentlyContinue
Remove-Item Env:\HELIUM_INTRO_TRACE -ErrorAction SilentlyContinue

if ($ok) { Write-Host '  ✔ 开场动画逐帧播出，并收尾到完全体' }
exit ([int](-not $ok))
