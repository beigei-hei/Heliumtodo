param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$OutPath,
    [string]$ConfigDir = "",
    [int]$WaitSeconds = 7
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if ($ConfigDir -ne "") { $env:HELIUM_CONFIG_DIR = $ConfigDir }

Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class WinCap
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

# 不用 Start-Process：本机会话没有资源管理器，Start-Process 默认走 ShellExecuteEx 会卡死。
# 统一走 probe-common.ps1 里的 ProcessStartInfo 版（顺带重定向子进程输出，免得外部管道等不到 EOF）。
. (Join-Path $PSScriptRoot 'probe-common.ps1')

$proc = Start-HeliumApp -Exe $ExePath
Write-Host ("启动 PID={0}" -f $proc.Id)
$handle = Wait-HeliumWindow -Process $proc -TimeoutMs ($WaitSeconds * 1000)
if ($handle -eq [IntPtr]::Zero) { Write-Host "✗ 没找到主窗口"; Stop-Process -Id $proc.Id -Force; exit 1 }
Start-Sleep -Milliseconds 800

$hwnd = $handle
[void][WinCap]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 600

$rect = New-Object WinCap+RECT
[void][WinCap]::GetWindowRect($hwnd, [ref]$rect)
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
Write-Host ("窗口 {0}x{1}" -f $w, $h)

$bmp = New-Object System.Drawing.Bitmap $w, $h
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $gfx.GetHdc()
# flag 2 = PW_RENDERFULLCONTENT：不传这个标志，DirectComposition 的内容会是全黑
$ok = [WinCap]::PrintWindow($hwnd, $hdc, 2)
$gfx.ReleaseHdc($hdc)
$gfx.Dispose()
Write-Host ("PrintWindow = {0}" -f $ok)

$dir = Split-Path -Parent $OutPath
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host ("已保存 {0}（{1} 字节）" -f $OutPath, (Get-Item $OutPath).Length)

Stop-Process -Id $proc.Id -Force
Write-Host "（已结束进程）"