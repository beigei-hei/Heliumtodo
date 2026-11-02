# 量一下 DWM 到底有没有给这个窗口画"可见边框"。
# DWMWA_VISIBLE_FRAME_BORDER_THICKNESS = 37，Win11 才有：
#   返回 0  = DWM 没画边框，也就不会替窗口裁圆角 → 得自己画描边
#   返回 1  = DWM 画了 1px 边框，圆角也归它 → 我们不该再画一层
param(
    [string]$ExePath = "HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe"
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class DwmProbe {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr param);
    public delegate bool EnumProc(IntPtr hWnd, IntPtr param);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);

    public static IntPtr Find(uint targetPid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) => {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == targetPid && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 4
$hwnd = [DwmProbe]::Find([uint32]$proc.Id)

if ($hwnd -eq [IntPtr]::Zero) {
    Write-Host "没找到窗口"
    $proc.Kill(); exit 1
}

$thickness = 0
$hr = [DwmProbe]::DwmGetWindowAttribute($hwnd, 37, [ref]$thickness, 4)
Write-Host ("DWM 可见边框厚度 : {0} px  (HRESULT=0x{1:X8})" -f $thickness, $hr)

$corner = 0
$hr2 = [DwmProbe]::DwmGetWindowAttribute($hwnd, 33, [ref]$corner, 4)
Write-Host ("圆角偏好         : {0}  (HRESULT=0x{1:X8})" -f $corner, $hr2)

$proc.Kill()
Write-Host "程序已关闭"