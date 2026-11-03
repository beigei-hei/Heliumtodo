# 验证「最小化 / 最大化」是 DWM 在播过渡动画，而不是硬切。
#
# 为什么需要这个脚本：
#   DWM 的原生过渡**只改视觉，不改窗口矩形** —— 所以以前那个采样 GetWindowRect
#   数中间尺寸的探针（probe-maximize-anim.ps1）对原生动画会给出假阴性。
#   唯一可靠的证据是"屏幕上肉眼看到的东西在变"：这里直接抓屏，
#   数每一帧里"离终态还差多少像素"，看这个数字是不是连续递减。
#
# 两个场景用同一个思路（都和终态比）：
#   · 最小化：终态 = 窗口消失后的桌面。参考帧在最小化完成后拍，然后把窗口捡回来再最小化，
#             逐帧数"还属于窗口"的像素 → 递减。
#   · 最大化：终态 = 铺满工作区的窗口。参考帧在普通大小时拍，然后最大化，
#             逐帧数"还不一样"的像素 → 递减。
#
# 判据：不同取值 >= 12 个；大体单调（>= 80% 的相邻帧在下降，允许 3% 抖动）。
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

. (Join-Path $PSScriptRoot 'probe-common.ps1')

Add-Type -AssemblyName System.Drawing

Add-Type -Namespace HeliumProbe -Name Screen -CompilerOptions '/unsafe' -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

public static void Minimize(IntPtr h) { ShowWindow(h, 6); }   // SW_MINIMIZE
public static void Maximize(IntPtr h) { ShowWindow(h, 3); }   // SW_MAXIMIZE
public static void Restore(IntPtr h) { ShowWindow(h, 9); }    // SW_RESTORE

public static RECT Rect(IntPtr h) { RECT r; GetWindowRect(h, out r); return r; }

// 和参考帧比，数"还不一样"的像素。
public static unsafe int DiffCount(IntPtr reference, IntPtr current, int width, int height, int stride, int threshold)
{
    int count = 0;
    byte* r = (byte*)reference.ToPointer();
    byte* c = (byte*)current.ToPointer();
    for (int y = 0; y < height; y++)
    {
        byte* rp = r + (long)y * stride;
        byte* cp = c + (long)y * stride;
        for (int x = 0; x < width; x++)
        {
            int dr = Math.Abs(rp[x * 4] - cp[x * 4]);
            int dg = Math.Abs(rp[x * 4 + 1] - cp[x * 4 + 1]);
            int db = Math.Abs(rp[x * 4 + 2] - cp[x * 4 + 2]);
            if (dr + dg + db > threshold) { count++; }
        }
    }
    return count;
}
'@

function Capture-Bytes([System.Drawing.Bitmap]$bitmap) {
    $rect = New-Object System.Drawing.Rectangle 0, 0, $bitmap.Width, $bitmap.Height
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $bytes = New-Object byte[] ($data.Stride * $data.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        return [pscustomobject]@{ Bytes = $bytes; Stride = $data.Stride }
    }
    finally {
        $bitmap.UnlockBits($data)
    }
}

$exe = (Resolve-Path (Join-Path $PSScriptRoot '..\HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe')).Path
# 沙盒放系统临时目录：以前放在仓库里，探针中途崩掉就会在 tools/ 下留一堆垃圾目录。
$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ('helium-probe-native-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $sandbox

$proc = Start-HeliumApp -Exe $exe -Environment @{ HELIUM_CONFIG_DIR = $sandbox }
$hwnd = Wait-HeliumWindow -Process $proc

$ok = $true

if ($hwnd -eq [IntPtr]::Zero) {
    Write-Host '  [失败] 没找到主窗口句柄'
    $ok = $false
}
else {
    Start-Sleep -Milliseconds 1500

    # 抓屏区域：窗口中间一块。最小化时窗口缩小滑向任务栏，最大化时内容铺满，
    # 中间这块在两个场景里都能覆盖整个变化过程；而且比整窗便宜（整窗每帧 20 ms 太慢）。
    $rect = [HeliumProbe.Screen]::Rect($hwnd)
    $winW = $rect.Right - $rect.Left
    $winH = $rect.Bottom - $rect.Top
    $sampleW = [Math]::Min(480, $winW - 40)
    $sampleH = [Math]::Min(320, $winH - 40)
    $sx = $rect.Left + [int](($winW - $sampleW) / 2)
    $sy = $rect.Top + [int](($winH - $sampleH) / 2)

    Write-Host ("── 抓屏区域 {0}x{1} @ ({2},{3})，窗口 {4}x{5} ──" -f $sampleW, $sampleH, $sx, $sy, $winW, $winH)

    $bitmap = New-Object System.Drawing.Bitmap $sampleW, $sampleH
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    # 连拍一段，返回每次都和参考帧比出来的"差异像素数"序列。
    function Measure-Sequence([int]$durationMs) {
        $areas = New-Object System.Collections.Generic.List[int]
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        while ($watch.ElapsedMilliseconds -lt $durationMs -and $areas.Count -lt 80) {
            $graphics.CopyFromScreen($sx, $sy, 0, 0, $bitmap.Size)
            $shot = Capture-Bytes $bitmap
            $ptrShot = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($shot.Bytes.Length)
            [System.Runtime.InteropServices.Marshal]::Copy($shot.Bytes, 0, $ptrShot, $shot.Bytes.Length)
            try {
                $areas.Add([HeliumProbe.Screen]::DiffCount(
                        $script:refPtr, $ptrShot, $sampleW, $sampleH, $shot.Stride, 40))
            }
            finally {
                [System.Runtime.InteropServices.Marshal]::FreeHGlobal($ptrShot)
            }
        }
        return , $areas
    }

    function Assert-Sequence([string]$label, $areas) {
        $distinct = @($areas | Sort-Object -Unique)
        $first = if ($areas.Count -gt 0) { $areas[0] } else { 0 }
        $last = if ($areas.Count -gt 0) { $areas[-1] } else { 0 }
        Write-Host ("  {0}：{1} 帧，不同取值 {2} 个，首帧 {3} → 末帧 {4} 像素" -f `
                $label, $areas.Count, $distinct.Count, $first, $last)

        if ($distinct.Count -lt 12) {
            Write-Host '  [失败] 不同取值少于 12 个 —— 说明是硬切，DWM 没有播过渡'
            return $false
        }

        $decreasing = 0
        for ($i = 1; $i -lt $areas.Count; $i++) {
            if ($areas[$i] -le $areas[$i - 1] * 1.03) { $decreasing++ }
        }
        $ratio = $decreasing / [Math]::Max(1, $areas.Count - 1)
        Write-Host ("    单调下降比例：{0:P0}" -f $ratio)

        if ($ratio -lt 0.8) {
            Write-Host '  [失败] 序列不是单调下降的，采到的可能不是过渡动画'
            return $false
        }

        return $true
    }

    # ── ① 最小化 ──
    Write-Host '── ① 最小化 ──'
    [HeliumProbe.Screen]::Minimize($hwnd)
    Start-Sleep -Milliseconds 800
    $graphics.CopyFromScreen($sx, $sy, 0, 0, $bitmap.Size)
    $refShot = Capture-Bytes $bitmap
    $script:refPtr = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($refShot.Bytes.Length)
    [System.Runtime.InteropServices.Marshal]::Copy($refShot.Bytes, 0, $script:refPtr, $refShot.Bytes.Length)

    try {
        [HeliumProbe.Screen]::Restore($hwnd)
        Start-Sleep -Milliseconds 900
        [HeliumProbe.Screen]::Minimize($hwnd)
        $areas = Measure-Sequence -durationMs 700
        if (-not (Assert-Sequence -label '最小化' -areas $areas)) { $ok = $false }
        else { Write-Host '  ✔ 最小化走的是 DWM 过渡（可见面积连续递减）' }
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::FreeHGlobal($script:refPtr)
    }

    # ── ② 最大化 ──
    Write-Host '── ② 最大化 ──'
    [HeliumProbe.Screen]::Restore($hwnd)
    Start-Sleep -Milliseconds 900

    # 先量一次"最大化之后画面长什么样"，作为终态参考帧。
    [HeliumProbe.Screen]::Maximize($hwnd)
    Start-Sleep -Milliseconds 900
    $graphics.CopyFromScreen($sx, $sy, 0, 0, $bitmap.Size)
    $maxShot = Capture-Bytes $bitmap

    [HeliumProbe.Screen]::Restore($hwnd)
    Start-Sleep -Milliseconds 900

    # 普通大小的画面：最大化过程中这块会逐渐变成终态，差异数递减。
    $graphics.CopyFromScreen($sx, $sy, 0, 0, $bitmap.Size)
    $normalShot = Capture-Bytes $bitmap

    $ptrNormal = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($normalShot.Bytes.Length)
    $ptrMax = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($maxShot.Bytes.Length)
    [System.Runtime.InteropServices.Marshal]::Copy($normalShot.Bytes, 0, $ptrNormal, $normalShot.Bytes.Length)
    [System.Runtime.InteropServices.Marshal]::Copy($maxShot.Bytes, 0, $ptrMax, $maxShot.Bytes.Length)
    try {
        $baseline = [HeliumProbe.Screen]::DiffCount(
            $ptrNormal, $ptrMax, $sampleW, $sampleH, $normalShot.Stride, 40)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::FreeHGlobal($ptrNormal)
        [System.Runtime.InteropServices.Marshal]::FreeHGlobal($ptrMax)
    }

    Write-Host ("  普通大小与最大化终态的差异：{0} 像素" -f $baseline)

    if ($baseline -lt 2000) {
        Write-Host '  - 跳过：这块区域在两种状态下几乎一样，测不出过渡（窗口本来就已经接近满屏）'
    }
    else {
        $script:refPtr = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($maxShot.Bytes.Length)
        [System.Runtime.InteropServices.Marshal]::Copy($maxShot.Bytes, 0, $script:refPtr, $maxShot.Bytes.Length)
        try {
            [HeliumProbe.Screen]::Maximize($hwnd)
            $areas = Measure-Sequence -durationMs 700
            if (-not (Assert-Sequence -label '最大化' -areas $areas)) { $ok = $false }
            else { Write-Host '  ✔ 最大化走的是 DWM 过渡（与终态的差异连续递减）' }
        }
        finally {
            [System.Runtime.InteropServices.Marshal]::FreeHGlobal($script:refPtr)
        }
    }

    $graphics.Dispose()
    $bitmap.Dispose()
}

if (-not $proc.HasExited) {
    $proc.Kill()
    Start-Sleep -Milliseconds 600
}

Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
Clear-HeliumEnvironment -Names @('HELIUM_CONFIG_DIR')

exit ([int](-not $ok))