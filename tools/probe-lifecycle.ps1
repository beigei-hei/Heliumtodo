# 生命周期探针：验证「关掉主界面不退出、进程还在后台跑」。
#
# 做法：启动真实程序 → 给它主窗口发一条 WM_CLOSE（等价于点右上角的 ×）
# → 等 3 秒看进程还在不在 → 再结束它并清理临时配置目录。
#
# 注意：这个脚本带中文注释，必须存成「带 BOM 的 UTF-8」，
# 否则 Windows PowerShell 会按 GBK 去读，直接报 Unexpected token。

param(
    [string]$ExePath = "HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe",
    [int]$WaitSeconds = 6
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms | Out-Null

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LifecycleProbe {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    public const uint WM_CLOSE = 0x0010;
}
'@

$root = (Get-Location).Path
$configDir = Join-Path $root ".tmp-lifecycle"
if (Test-Path $configDir) { Remove-Item $configDir -Recurse -Force }
New-Item -ItemType Directory -Path $configDir | Out-Null

$env:HELIUM_CONFIG_DIR = $configDir
# 这台机器上托盘图标加不上（无头会话里 Shell_NotifyIcon 返回访问被拒），
# 用自检开关把"托盘已就绪"这条分支打开，好验证关窗口之后的常驻行为。
$env:HELIUM_FAKE_TRAY = "1"
$exe = Join-Path $root $ExePath

Write-Host "── 启动 ──"
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds $WaitSeconds

if ($process.HasExited) {
    Write-Host "  ✗ 进程没撑到窗口出现就退出了（退出码 $($process.ExitCode)）"
    exit 1
}
$process.Refresh()
Write-Host "  ✓ 进程在跑：PID $($process.Id)"

$handle = $process.MainWindowHandle
if ($handle -eq [IntPtr]::Zero) {
    Write-Host "  ✗ 没找到主窗口句柄"
    Stop-Process -Id $process.Id -Force
    exit 1
}
Write-Host "  ✓ 主窗口标题：$($process.MainWindowTitle)"

Write-Host "── 发 WM_CLOSE（等价于点 ×）──"
[LifecycleProbe]::PostMessage($handle, [LifecycleProbe]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 3

$process.Refresh()
if ($process.HasExited) {
    Write-Host "  ✗ 关窗口之后进程就没了 —— 后台常驻没生效"
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Write-Host "── 后台日志 ──"
    if (Test-Path (Join-Path $configDir "background.log")) {
        Get-Content (Join-Path $configDir "background.log") | ForEach-Object { "   $_" }
    }
    exit 1
}

Write-Host "  ✓ 关窗口之后进程仍在：PID $($process.Id)"
Write-Host "  ✓ 窗口已隐藏：$($process.MainWindowHandle -eq [IntPtr]::Zero)"

Write-Host "── 再双击一次程序（第二个实例应当只负责把窗口叫回来）──"
$second = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 5

$second.Refresh()
if (-not $second.HasExited) {
    Write-Host "  ✗ 第二个实例没有自己退出（单实例守卫没起作用）"
    Stop-Process -Id $second.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "  ✓ 第二个实例已自动退出"

# 窗口被 Show 出来要一点时间，轮询等它出现
$restored = $false
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $restored = $true; break }
}
if (-not $restored) {
    Write-Host "  ✗ 第一个实例的窗口没有被叫回来"
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "  ✓ 窗口被叫回来了：$($process.MainWindowTitle)"

Write-Host "── 收尾 ──"
Stop-Process -Id $process.Id -Force
Remove-Item $configDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ 生命周期检查通过"