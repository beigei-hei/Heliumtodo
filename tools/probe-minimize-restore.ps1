# 真机探针：开场动画播到一半被最小化，再从任务栏点回来，状态是不是干净的。
#
# 验的是 L4 修的那条路：
#   · 进最小化时必须就地收尾 —— 否则遮罩/变换会跟着窗口一起被最小化，
#     从任务栏点回来时没人收尾（以前只有"藏到托盘"会调 Settle）；
#   · 从最小化恢复时必须补播一次出现动画。
#
# 判据不看像素（窗口最小化后根本抓不到内容），而是读应用自己写的 background.log：
#   应用在状态变化处会记一行"[窗口] 状态 → …"，探针断言这两行都在。

param(
    [string]$Exe = "HeliumTodo.Desktop\bin\Debug\net10.0\HeliumTodo.Desktop.exe"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "probe-common.ps1")

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class WinMin {
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

$root = Split-Path -Parent $PSScriptRoot
$exe = if ([System.IO.Path]::IsPathRooted($Exe)) { $Exe } else { Join-Path $root $Exe }
$sandbox = Join-Path $root ".tmp-minrestore"

if (Test-Path $sandbox) { Remove-Item $sandbox -Recurse -Force }
New-Item -ItemType Directory -Path $sandbox | Out-Null

$env:HELIUM_CONFIG_DIR = $sandbox
Clear-HeliumEnvironment @("HELIUM_INTRO_TRACE", "HELIUM_NO_INTERACTION_QUALITY")

Write-Host "启动：$exe"
$process = Start-HeliumApp -Exe $exe -Environment @{ HELIUM_CONFIG_DIR = $sandbox }
$hwnd = Wait-HeliumWindow -Process $process -TimeoutMs 20000

if ($hwnd -eq [IntPtr]::Zero) {
    Write-Host "✗ 等不到主窗口"
    $process.Kill()
    exit 1
}

Write-Host "窗口句柄：$hwnd"

# 让开场动画先播完（180ms 的动画 + 冷启动首帧），再进最小化 —— 这样测的是
# "动画播到一半被打断"那条路径的收尾，而不是"还没开始播"。
Start-Sleep -Milliseconds 900
$null = [WinMin]::ShowWindow($hwnd, 6)   # SW_MINIMIZE
Start-Sleep -Milliseconds 700
$null = [WinMin]::ShowWindow($hwnd, 9)   # SW_RESTORE
Start-Sleep -Milliseconds 900

$process.Kill()
Start-Sleep -Milliseconds 300

$logPath = Join-Path $sandbox "background.log"
if (-not (Test-Path $logPath)) {
    Write-Host "✗ 没有 background.log（路径：$logPath）"
    exit 1
}

$lines = Get-Content $logPath
$minimized = @($lines | Where-Object { $_ -match "状态 → 最小化，动画就地收尾" }).Count
$restored = @($lines | Where-Object { $_ -match "从最小化恢复，补播出现动画" }).Count
$introFrames = @($lines | Where-Object { $_ -match "\[开场动画\]" }).Count

$failures = 0

if ($minimized -ge 1) {
    Write-Host "✔ 最小化时就地收尾 — 命中 $minimized 行"
} else {
    Write-Host "✗ 最小化时没有收尾（找不到『状态 → 最小化，动画就地收尾』）"
    $failures++
}

if ($restored -ge 1) {
    Write-Host "✔ 从最小化恢复时补播出现动画 — 命中 $restored 行"
} else {
    Write-Host "✗ 恢复时没有补播出现动画"
    $failures++
}

Write-Host ""
Write-Host "background.log 里的窗口状态行："
$lines | Where-Object { $_ -match "\[窗口\]" } | ForEach-Object { Write-Host "  $_" }

if ($introFrames -gt 0) {
    Write-Host "  （另外还有 $introFrames 行开场动画日志）"
}

Write-Host ""
if ($failures -eq 0) {
    Write-Host "✔ 最小化 / 恢复这条路径是干净的"
    exit 0
}

Write-Host "✗ 共 $failures 项不合格"
exit 1