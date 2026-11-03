# 验证图标轨展开在真实窗口里是不是逐帧来的、有没有卡。
#
# 为什么需要这个脚本：
#   无头渲染里 --perf-check 能算出"改一次宽度要 20 ms"，但那是我们自己的计时循环，
#   不是真正的动画时钟。真实窗口里 260 ms 到底出了几帧、帧间隔匀不匀，只能看日志。
#   程序在 HELIUM_RAIL_TRACE=1 时会把每帧写进 background.log，
#   HELIUM_RAIL_TEST=1 时界面稳定后自己展开一次图标轨（生产里这两个变量都不存在）。
#
# 只统计"动画窗口内"的帧（已用 <= 260 ms），并丢掉第一帧：
#   第一帧的间隔是从"按下按钮"算起的，里面含这次点击本身的开销；
#   动画结束后的空转帧帧率由屏幕刷新率决定，跟动画无关，混进来只会污染统计。
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

. (Join-Path $PSScriptRoot 'probe-common.ps1')

$durationMs = 260

$exe = Join-Path $PSScriptRoot '..\HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe'
$exe = (Resolve-Path $exe).Path

$sandbox = Join-Path $PSScriptRoot '..\.tmp-rail'
if (Test-Path $sandbox) { Remove-Item $sandbox -Recurse -Force }
New-Item -ItemType Directory -Path $sandbox | Out-Null
$sandbox = (Resolve-Path $sandbox).Path

Write-Host '── 图标轨展开 ──'
$proc = Start-HeliumApp -Exe $exe -Environment @{
    HELIUM_CONFIG_DIR = $sandbox
    HELIUM_RAIL_TRACE = '1'
    HELIUM_RAIL_TEST  = '1'
}
[void](Wait-HeliumWindow -Process $proc)
Start-Sleep -Seconds 6

$log = Join-Path $sandbox 'background.log'
if (-not (Test-Path $log)) {
    Write-Host '  [失败] 没有生成 background.log'
    $proc.Kill(); exit 1
}

$lines = Get-Content $log -Encoding UTF8 | Where-Object { $_ -like '*[图标轨]*' }
$samples = @()
foreach ($line in $lines) {
    if ($line -match '间隔\s*([\d.]+)\s*ms\s*已用\s*([\d.]+)\s*ms') {
        $samples += [pscustomobject]@{ Gap = [double]$Matches[1]; Elapsed = [double]$Matches[2] }
    }
}

$inWindow = @($samples | Where-Object { $_.Elapsed -le $durationMs })
# 丢掉第一帧：它的间隔从点击那一刻算起，含点击本身的开销。
if ($inWindow.Count -gt 1) { $inWindow = $inWindow[1..($inWindow.Count - 1)] }
$intervals = @($inWindow | ForEach-Object { $_.Gap })

$frames = $intervals.Count
$mean = 0.0
$p50 = 0.0
$p95 = 0.0
$worst = 0.0
if ($frames -gt 0) {
    $sorted = @($intervals | Sort-Object)
    $mean = ($intervals | Measure-Object -Average).Average
    $p50 = $sorted[[int][Math]::Floor(($frames - 1) / 2)]
    $p95 = $sorted[[int][Math]::Floor(($frames - 1) * 0.95)]
    $worst = $sorted[$frames - 1]
}

Write-Host ("  动画窗口内 {0} 帧，平均间隔 {1:0.0} ms，P50 {2:0.0} ms，P95 {3:0.0} ms，最慢 {4:0.0} ms" -f $frames, $mean, $p50, $p95, $worst)
Write-Host ("  折合 {0:0} 帧/秒" -f (1000.0 / [Math]::Max(0.1, $mean)))

# 判据用 P50 / P95，不用"最慢一帧"：
#   260 ms 的过渡里第一次切换 IsExpanded 会让整页重新排版，那一帧必然偏慢；
#   单帧尖峰只要不是连续的，肉眼感知不到，用它判红只会让这条断言永远红。
#   真正体感是"常态帧"和"尾部分布"，所以看 P50（60 Hz 预算 16.7 ms）和 P95（30 fps 预算 33.3 ms）。
$ok = $true
if ($frames -lt 10) {
    Write-Host '  [失败] 动画窗口内帧太少（260 ms 该有 10 帧以上）'
    $ok = $false
}
if ($frames -gt 0 -and $p50 -gt 16.7) {
    Write-Host ("  [失败] 常态帧就超了 60 Hz 的预算（P50 {0:0.0} ms > 16.7 ms）" -f $p50)
    $ok = $false
}
if ($frames -gt 0 -and $p95 -gt 33.3) {
    Write-Host ("  [失败] 尾部帧掉到 30 fps 以下（P95 {0:0.0} ms > 33.3 ms）" -f $p95)
    $ok = $false
}
if ($frames -gt 0 -and $worst -gt 2 * $p50) {
    Write-Host ("  [提示] 最慢一帧 {0:0.0} ms 是常态的两倍以上（P50 {1:0.0} ms）—— 单帧尖峰，仅记录" -f $worst, $p50)
}

$proc.Kill()
Start-Sleep -Milliseconds 800
Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
Clear-HeliumEnvironment -Names @('HELIUM_CONFIG_DIR', 'HELIUM_RAIL_TRACE', 'HELIUM_RAIL_TEST')

if ($ok) { Write-Host '  ✔ 图标轨展开逐帧推进，常态帧在 60 Hz 预算内，尾部帧不低于 30 fps' }
exit ([int](-not $ok))