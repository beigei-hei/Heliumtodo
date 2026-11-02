# 用 PostMessage 往窗口里"点一下右边缘再往左拖"，看窗口能不能变小。
# 为什么不用真鼠标：这个会话里窗口可能被别的窗口盖住，真鼠标点不到它；
# PostMessage 直接投递到窗口消息队列，跟 Z 序无关。
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;

public class WinPost {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wp, IntPtr lp);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const int MK_LBUTTON = 0x0001;

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

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
    $hwnd = [WinPost]::FindWindow([uint32]$proc.Id)
    if ($hwnd -ne [IntPtr]::Zero) { break }
}
if ($hwnd -eq [IntPtr]::Zero) { throw "没等到窗口" }

Start-Sleep -Seconds 4

$r = New-Object WinPost+RECT
[void][WinPost]::GetWindowRect($hwnd, [ref]$r)
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
Write-Host ("拖动前 : {0}x{1}" -f $w, $h)

# 客户区坐标：(宽-3, 高/2) 落在右边缘 6px 的抓手条里
$edgeX = $w - 3
$y = [int]($h / 2)

[void][WinPost]::PostMessage($hwnd, [WinPost]::WM_MOUSEMOVE, [IntPtr]::Zero, [WinPost]::MakeLParam($edgeX, $y))
Start-Sleep -Milliseconds 200
[void][WinPost]::PostMessage($hwnd, [WinPost]::WM_LBUTTONDOWN, [IntPtr]1, [WinPost]::MakeLParam($edgeX, $y))
Start-Sleep -Milliseconds 300

for ($i = 1; $i -le 15; $i++) {
    $x = $edgeX - [int](150 * $i / 15)
    [void][WinPost]::PostMessage($hwnd, [WinPost]::WM_MOUSEMOVE, [IntPtr]1, [WinPost]::MakeLParam($x, $y))
    Start-Sleep -Milliseconds 50
}

[void][WinPost]::PostMessage($hwnd, [WinPost]::WM_LBUTTONUP, [IntPtr]::Zero, [WinPost]::MakeLParam(($edgeX - 150), $y))
Start-Sleep -Milliseconds 600

$r2 = New-Object WinPost+RECT
[void][WinPost]::GetWindowRect($hwnd, [ref]$r2)
$w2 = $r2.Right - $r2.Left
Write-Host ("拖动后 : {0}x{1}" -f $w2, ($r2.Bottom - $r2.Top))
if ($w2 -lt $w - 20) {
    Write-Host ("结论   : 收窄成功（{0} → {1}）" -f $w, $w2)
} else {
    Write-Host ("结论   : 没变化（{0} → {1}）" -f $w, $w2)
}

if (-not $proc.HasExited) { $proc.Kill(); Start-Sleep -Milliseconds 600 }
Write-Host "程序已关闭"
