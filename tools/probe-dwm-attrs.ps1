# 查 DWM 对这个窗口的"额外开关"：
#   attr 1 = DWMWA_NCRENDERING_ENABLED   —— DWM 有没有在画非客户区（阴影/边框/圆角都靠它）
#   attr 3 = DWMWA_TRANSITIONS_FORCEDISABLED —— 这个窗口是不是被强制关掉了过渡动画
# 后者为 TRUE 的话，最大化一定跳变，而且跟我们自己的代码没关系。
param(
    [string]$ExePath = "HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe"
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class DWM2 {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr param);
    public delegate bool EnumProc(IntPtr hWnd, IntPtr param);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);

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

$hwnd = [DWM2]::Find([uint32]$proc.Id)
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "没找到窗口"; $proc.Kill(); exit 1 }

$names = @{ 1 = "NCRENDERING_ENABLED"; 2 = "NCRENDERING_POLICY"; 3 = "TRANSITIONS_FORCEDISABLED"; 33 = "WINDOW_CORNER_PREFERENCE"; 37 = "VISIBLE_FRAME_BORDER_THICKNESS" }
foreach ($attr in 1, 2, 3, 33, 37) {
    $v = 0
    $hr = [DWM2]::DwmGetWindowAttribute($hwnd, $attr, [ref]$v, 4)
    Write-Host ("{0,-30} = {1,-6} (HRESULT=0x{2:X8})" -f $names[$attr], $v, $hr)
}

$proc.Kill()
Write-Host "程序已关闭"