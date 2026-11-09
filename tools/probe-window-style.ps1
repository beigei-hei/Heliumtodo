# 验证窗口样式位里确实有 WS_CAPTION，且窗口矩形仍然等于客户区。
#
# 为什么需要这个脚本：
#   DWM 愿不愿意给"最小化 / 最大化 / 还原"播过渡动画，看的是窗口有没有 WS_CAPTION
#   （= WS_BORDER | WS_DLGFRAME）。我们的窗口为了铺满壁纸用了 WindowDecorations=None，
#   Avalonia 给出来的样式位里恰好没有这一段，于是过渡变成硬切。
#   补上之后必须同时确认两件事：
#     ① 样式位里真的有 WS_CAPTION（否则 DWM 还是不给动画）；
#     ② 窗口矩形仍然等于客户区（否则说明那圈 7px 的系统边框又长回来了，壁纸铺不满）。
#   这两条只能问 Windows 本体，无头渲染里没有 HWND，--anim-check 验不到。
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

. (Join-Path $PSScriptRoot 'probe-common.ps1')

Add-Type -Namespace HeliumProbe -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@

$exe = Join-Path $PSScriptRoot '..\HeliumTodo.Desktop\bin\Debug\net10.0\HeliumTodo.Desktop.exe'
$exe = (Resolve-Path $exe).Path

$sandbox = Join-Path $PSScriptRoot ('_probe-style-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sandbox | Out-Null

$proc = Start-HeliumApp -Exe $exe -Environment @{ HELIUM_CONFIG_DIR = $sandbox }
$hwnd = Wait-HeliumWindow -Process $proc

$ok = $true

if ($hwnd -eq [IntPtr]::Zero) {
    Write-Host '  [失败] 没找到主窗口句柄'
    $ok = $false
}
else {
    Start-Sleep -Milliseconds 1200

    $GWL_STYLE = -16
    $style = [HeliumProbe.Native]::GetWindowLong($hwnd, $GWL_STYLE)
    $WS_CAPTION = 0x00C00000
    $hasCaption = ($style -band $WS_CAPTION) -eq $WS_CAPTION

    Write-Host ('  GWL_STYLE = 0x{0:X8}' -f $style)

    $windowRect = New-Object HeliumProbe.Native+RECT
    $clientRect = New-Object HeliumProbe.Native+RECT
    [void][HeliumProbe.Native]::GetWindowRect($hwnd, [ref]$windowRect)
    [void][HeliumProbe.Native]::GetClientRect($hwnd, [ref]$clientRect)

    $windowW = $windowRect.Right - $windowRect.Left
    $windowH = $windowRect.Bottom - $windowRect.Top
    $clientW = $clientRect.Right - $clientRect.Left
    $clientH = $clientRect.Bottom - $clientRect.Top

    Write-Host ("  窗口 {0}x{1}　客户区 {2}x{3}" -f $windowW, $windowH, $clientW, $clientH)

    if ($hasCaption) {
        Write-Host '  ✔ 样式位含 WS_CAPTION（DWM 会播最小化/最大化过渡）'
    }
    else {
        Write-Host '  [失败] 样式位缺 WS_CAPTION，DWM 不会给过渡动画'
        $ok = $false
    }

    # 客户区比窗口小 2px 以内算"一样大"（边框 0，实测只有 1px 的 DWM 描边在视觉上存在）。
    $deltaW = [Math]::Abs($windowW - $clientW)
    $deltaH = [Math]::Abs($windowH - $clientH)
    if ($deltaW -le 2 -and $deltaH -le 2) {
        Write-Host '  ✔ 窗口矩形仍等于客户区（壁纸照样铺满，没有把系统边框长回来）'
    }
    else {
        Write-Host ("  [失败] 窗口比客户区大了 {0}x{1}，系统边框又回来了" -f $deltaW, $deltaH)
        $ok = $false
    }
}

if (-not $proc.HasExited) {
    $proc.Kill()
    Start-Sleep -Milliseconds 800
}

Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item Env:\HELIUM_CONFIG_DIR -ErrorAction SilentlyContinue

exit ([int](-not $ok))