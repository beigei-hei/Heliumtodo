# 量一下"最大化"以后窗口到底占了多大。
# 关键点：最大化必须停在工作区（不能盖住任务栏），否则自定义边框就玩坏了。
param(
    [string]$ExePath = "HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe"
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class MaxProbe {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int Size; public RECT Monitor; public RECT Work; public int Flags; }

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr param);
    public delegate bool EnumProc(IntPtr hWnd, IntPtr param);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

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

    public static string Describe(IntPtr hwnd) {
        RECT r;
        GetWindowRect(hwnd, out r);
        return string.Format("{0}x{1} @({2},{3})", r.R - r.L, r.B - r.T, r.L, r.T);
    }

    public static string WorkArea(IntPtr hwnd) {
        IntPtr mon = MonitorFromWindow(hwnd, 2);
        MONITORINFO info = new MONITORINFO();
        info.Size = Marshal.SizeOf(typeof(MONITORINFO));
        GetMonitorInfo(mon, ref info);
        return string.Format("{0}x{1} @({2},{3})", info.Work.R - info.Work.L, info.Work.B - info.Work.T, info.Work.L, info.Work.T);
    }
}
"@

$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 4

$hwnd = [MaxProbe]::Find([uint32]$proc.Id)
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "没找到窗口"; $proc.Kill(); exit 1 }

Write-Host ("还原态   : {0}" -f [MaxProbe]::Describe($hwnd))
Write-Host ("显示器工作区 : {0}" -f [MaxProbe]::WorkArea($hwnd))

# WM_SYSCOMMAND = 0x0112, SC_MAXIMIZE = 0xF030
# 用系统命令而不是直接改 WindowState —— 这样走的正是"用户点了最大化"那条路径
[void][MaxProbe]::PostMessage($hwnd, 0x0112, [IntPtr]0xF030, [IntPtr]::Zero)
Start-Sleep -Milliseconds 800
Write-Host ("最大化后 : {0}" -f [MaxProbe]::Describe($hwnd))

# SC_RESTORE = 0xF120
[void][MaxProbe]::PostMessage($hwnd, 0x0112, [IntPtr]0xF120, [IntPtr]::Zero)
Start-Sleep -Milliseconds 800
Write-Host ("还原后又 : {0}" -f [MaxProbe]::Describe($hwnd))

$proc.Kill()
Write-Host "程序已关闭"