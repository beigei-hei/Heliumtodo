# 在「同一个 PowerShell 进程」里完成：启动 → 等窗口 → 截图 → 关闭。
# 不依赖子进程存活（有些执行环境会在命令结束时回收整个进程树）。

param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$OutPng,
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinCap {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

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

    public static string TitleOf(IntPtr h) {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }
}
"@

[void][WinCap]::SetProcessDPIAware()

$proc = Start-Process -FilePath $ExePath -PassThru
Write-Host "已启动，PID = $($proc.Id)"

# ── 等窗口 ──
$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 400
    if ($proc.HasExited) { throw "程序退出，退出码 $($proc.ExitCode)" }
    $hwnd = [WinCap]::FindWindow([uint32]$proc.Id)
    if ($hwnd -ne [IntPtr]::Zero) { break }
}
if ($hwnd -eq [IntPtr]::Zero) { throw "没等到窗口" }
Write-Host "窗口: $hwnd  标题: $([WinCap]::TitleOf($hwnd))"

Start-Sleep -Seconds 3
[void][WinCap]::ShowWindow($hwnd, 9)
[void][WinCap]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 1200

$rect = New-Object WinCap+RECT
[void][WinCap]::GetWindowRect($hwnd, [ref]$rect)
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
Write-Host "区域: ${w}x${h} @ ($($rect.Left),$($rect.Top))"
if ($w -le 0 -or $h -le 0) { throw "窗口尺寸异常" }

$outDir = Split-Path $OutPng -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# ── 直接截取屏幕对应区域（最可靠：截到的就是屏幕上真实显示的内容）──
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$g.Dispose()
$bmp.Save($OutPng, [System.Drawing.Imaging.ImageFormat]::Png)

# 顺便统计一下非黑像素比例，判断是不是真的画出了东西
$nonBlack = 0
$total = 0
for ($y = 0; $y -lt $h; $y += 12) {
    for ($x = 0; $x -lt $w; $x += 12) {
        $c = $bmp.GetPixel($x, $y)
        $total++
        if ($c.R -gt 20 -or $c.G -gt 20 -or $c.B -gt 20) { $nonBlack++ }
    }
}
$bmp.Dispose()

Write-Host "截图已保存: $OutPng"
Write-Host ("非黑像素比例: {0:P1} （{1}/{2}）" -f ($nonBlack / [Math]::Max(1, $total)), $nonBlack, $total)

if (-not $proc.HasExited) { $proc.Kill(); Start-Sleep -Milliseconds 800 }
Write-Host "程序已关闭"
