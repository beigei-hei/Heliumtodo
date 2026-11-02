# 量一下真实窗口的「非客户区」有多大：
#   窗口矩形 - 客户区矩形 = 系统保留的区域（标题栏 / 边框 / 阴影）。
# 界面上的壁纸如果只铺满客户区，这块就会露出来 —— 用户看到的「上方空白」。
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinProbe {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr hWnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    public static IntPtr FindWindow(uint targetPid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid != targetPid) return true;
            if (!IsWindowVisible(h)) return true;
            if (GetWindowTextLength(h) == 0) return true;
            found = h;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$proc = Start-Process -FilePath $ExePath -PassThru
Write-Host "已启动，PID = $($proc.Id)"

$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 400
    if ($proc.HasExited) { throw "程序退出，退出码 $($proc.ExitCode)" }
    $hwnd = [WinProbe]::FindWindow([uint32]$proc.Id)
    if ($hwnd -ne [IntPtr]::Zero) { break }
}
if ($hwnd -eq [IntPtr]::Zero) { throw "没等到窗口" }

Start-Sleep -Seconds 3

$wr = New-Object WinProbe+RECT
$cr = New-Object WinProbe+RECT
[void][WinProbe]::GetWindowRect($hwnd, [ref]$wr)
[void][WinProbe]::GetClientRect($hwnd, [ref]$cr)

$origin = New-Object WinProbe+POINT
$origin.X = 0; $origin.Y = 0
[void][WinProbe]::ClientToScreen($hwnd, [ref]$origin)

$dpi = [WinProbe]::GetDpiForWindow($hwnd)
$scale = $dpi / 96.0

$winW = $wr.Right - $wr.Left
$winH = $wr.Bottom - $wr.Top
$cliW = $cr.Right - $cr.Left
$cliH = $cr.Bottom - $cr.Top

Write-Host ("DPI          : {0} ({1:P0} 缩放)" -f $dpi, $scale)
Write-Host ("窗口矩形     : {0}x{1}" -f $winW, $winH)
Write-Host ("客户区       : {0}x{1}" -f $cliW, $cliH)
Write-Host ("客户区起点   : 窗口内偏移 ({0}, {1})" -f ($origin.X - $wr.Left), ($origin.Y - $wr.Top))
Write-Host ("非客户区     : 上 {0} px / 左 {1} px / 右 {2} px / 下 {3} px" -f `
    ($origin.Y - $wr.Top), ($origin.X - $wr.Left), `
    ($winW - $cliW - ($origin.X - $wr.Left)), ($winH - $cliH - ($origin.Y - $wr.Top)))
Write-Host ("窗口样式     : 0x{0:X8}" -f [WinProbe]::GetWindowLong($hwnd, -16))

# DWMWA_WINDOW_CORNER_PREFERENCE = 33，读回来验证"圆角"这个请求真的生效了
$corner = 0
$hr = [WinProbe]::DwmGetWindowAttribute($hwnd, 33, [ref]$corner, 4)
$cornerName = switch ($corner) { 0 { "Default（交给系统）" } 1 { "DoNotRound" } 2 { "Round ✔" } 3 { "RoundSmall" } default { "未知($corner)" } }
Write-Host ("圆角偏好     : {0}  (HRESULT=0x{1:X8})" -f $cornerName, $hr)

if (-not $proc.HasExited) { $proc.Kill(); Start-Sleep -Milliseconds 600 }
Write-Host "程序已关闭"
