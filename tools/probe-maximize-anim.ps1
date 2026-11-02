# 验证"最大化是不是真的在动"。
#
# 思路：自己写的动画是每帧改 Position/Width/Height，
# 所以从外面（GetWindowRect）能采到中间尺寸。
# 如果是瞬间跳变，采样里只会出现 1~2 个不同尺寸。
#
# 触发方式：给程序设 HELIUM_MAXIMIZE_TEST=1，它启动 1.5 秒后自己点一次最大化。
param(
    [string]$ExePath = "HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe"
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class AnimProbe {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr param);
    public delegate bool EnumProc(IntPtr hWnd, IntPtr param);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

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

# 让程序 3 秒后再自己点最大化，给我们留出"找窗口"的时间
$env:HELIUM_MAXIMIZE_TEST = "3000"
$proc = Start-Process -FilePath $ExePath -PassThru

# 轮询等窗口出现（最多 8 秒）
$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(8)
while ((Get-Date) -lt $deadline -and $hwnd -eq [IntPtr]::Zero) {
    Start-Sleep -Milliseconds 50
    $hwnd = [AnimProbe]::Find([uint32]$proc.Id)
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "没找到窗口"; $proc.Kill(); exit 1 }

# 再采 6 秒，每 10ms 一次（覆盖 3 秒触发点 + 180ms 动画）
$sizes = New-Object System.Collections.Generic.List[string]
$rect = New-Object AnimProbe+RECT
$deadline = (Get-Date).AddSeconds(8)
while ((Get-Date) -lt $deadline) {
    [void][AnimProbe]::GetWindowRect($hwnd, [ref]$rect)
    $sizes.Add("$($rect.R - $rect.L)x$($rect.B - $rect.T)")
    Start-Sleep -Milliseconds 10
}

$distinct = $sizes | Select-Object -Unique
Write-Host ("采样次数   : {0}" -f $sizes.Count)
Write-Host ("不同尺寸数 : {0}" -f $distinct.Count)
Write-Host ("尺寸序列   : {0}" -f (($distinct | Select-Object -First 22) -join " -> "))

if ($distinct.Count -ge 4) {
    Write-Host "结论       : 尺寸逐帧变化，动画在跑 ✔"
} else {
    Write-Host "结论       : 尺寸几乎没中间值，看起来是瞬间跳变 ✗"
}

$proc.Kill()
Write-Host "程序已关闭"