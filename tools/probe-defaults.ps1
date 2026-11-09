# 验证出厂默认值：窗口开出来是 1920×1080、渲染后端默认走显卡（自动）。
#
# 为什么需要这个脚本：
#   这两条都是"配置里没写就按默认走"的行为，无头渲染一套都验不到 ——
#     · 窗口尺寸由 AppConfig.WindowWidth/Height 决定，无头里窗口尺寸是我们自己写死的；
#     · 渲染后端在 Avalonia 初始化之前就定下来了，无头走的是 Skia CPU 那条路。
#   所以只能用真窗口 + 客户区尺寸 + 启动日志里的那行"渲染后端="来验。
#   换台机器、换个 DPI，或者谁把默认值改回去，这里立刻红。
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

. (Join-Path $PSScriptRoot 'probe-common.ps1')

Add-Type -Namespace HeliumProbeDefaults -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(int action, int param, ref RECT rect, int winIni);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@

# PowerShell 默认是 DPI 不感知的，Windows 会把坐标除以缩放再交给它 —— 量出来会小一圈。
[void][HeliumProbeDefaults.Native]::SetProcessDpiAwarenessContext([IntPtr](-4))

$workArea = New-Object HeliumProbeDefaults.Native+RECT
[void][HeliumProbeDefaults.Native]::SystemParametersInfo(0x0030, 0, [ref]$workArea, 0)
$workW = $workArea.Right - $workArea.Left
$workH = $workArea.Bottom - $workArea.Top
Write-Host ("  屏幕工作区 {0}x{1} px" -f $workW, $workH)

$exe = Join-Path $PSScriptRoot '..\HeliumTodo.Desktop\bin\Debug\net10.0\HeliumTodo.Desktop.exe'
$exe = (Resolve-Path $exe).Path

$sandbox = Join-Path $PSScriptRoot ('_probe-defaults-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sandbox | Out-Null

$proc = Start-HeliumApp -Exe $exe -Environment @{ HELIUM_CONFIG_DIR = $sandbox }
$hwnd = Wait-HeliumWindow -Process $proc

$ok = $true

if ($hwnd -eq [IntPtr]::Zero) {
    Write-Host '  [失败] 没找到主窗口句柄'
    $ok = $false
}
else {
    Start-Sleep -Milliseconds 1500

    $rect = New-Object HeliumProbeDefaults.Native+RECT
    [void][HeliumProbeDefaults.Native]::GetClientRect($hwnd, [ref]$rect)
    $clientW = $rect.Right - $rect.Left
    $clientH = $rect.Bottom - $rect.Top

    # 配置里写的是 DIP（1920×1080），窗口给的是物理像素，用窗口自己所在屏的 DPI 折回去。
    $dpi = [HeliumProbeDefaults.Native]::GetDpiForWindow($hwnd)
    if ($dpi -le 0) { $dpi = 96 }
    $scaling = $dpi / 96.0
    $dipW = $clientW / $scaling
    $dipH = $clientH / $scaling

    Write-Host ("  客户区 {0}x{1} px　DPI {2}（缩放 {3:0.##}）　折合 {4:0}x{5:0} DIP" -f $clientW, $clientH, $dpi, $scaling, $dipW, $dipH)

    # 期望 = min(1920×1080, 工作区折成 DIP)。装得下就是 1920×1080；装不下（这台
    # 2560×1600 @150% 就是）应当铺满工作区 —— 那是 Avalonia 建窗口时自己收的。
    # 容差 5%：无边框窗口带 DWM 描边，取整也会差几个像素。
    $expectedW = [Math]::Min(1920.0, $workW / $scaling)
    $expectedH = [Math]::Min(1080.0, $workH / $scaling)
    $tolW = [Math]::Max(4.0, $expectedW * 0.05)
    $tolH = [Math]::Max(4.0, $expectedH * 0.05)

    if ([Math]::Abs($dipW - $expectedW) -le $tolW -and [Math]::Abs($dipH - $expectedH) -le $tolH) {
        Write-Host ("  ✔ 默认窗口尺寸对得上（期望 {0:0}×{1:0} DIP）" -f $expectedW, $expectedH)
    }
    else {
        Write-Host ("  [失败] 期望 {0:0}×{1:0} DIP，实际 {2:0}×{3:0} DIP" -f $expectedW, $expectedH, $dipW, $dipH)
        $ok = $false
    }

    # 启动日志里那行"渲染后端="就是这次真正生效的后端
    $log = Join-Path $sandbox 'background.log'
    if (Test-Path $log) {
        $line = Get-Content $log -Encoding UTF8 | Where-Object { $_ -like '*[环境]*渲染后端=*' } | Select-Object -Last 1
        if ($line) {
            Write-Host ("  启动日志：{0}" -f $line.Trim())
            if ($line -like '*Avalonia 自带渲染器*') {
                Write-Host '  ✔ 默认渲染后端是自动（显卡）'
            }
            else {
                Write-Host '  [失败] 默认渲染后端不是自动（显卡），检查 RenderBackendSettings.Default'
                $ok = $false
            }
        }
        else {
            Write-Host '  [失败] 启动日志里没有"渲染后端="这一行（是不是把这行日志删了？）'
            $ok = $false
        }
    }
    else {
        Write-Host ("  [失败] 找不到启动日志：{0}" -f $log)
        $ok = $false
    }
}

if (-not $proc.HasExited) {
    $proc.Kill()
    Start-Sleep -Milliseconds 800
}

Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue

exit ([int](-not $ok))