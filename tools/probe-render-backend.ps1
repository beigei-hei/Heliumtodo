# 验证「设置 → 底层设置 → 渲染后端」真的生效。
#
# 思路：渲染后端是"启动时读一次"的，光看界面看不出来。
#   不同后端的差别在"进程里加载了哪些模块"：
#     · Avalonia 自带（ANGLE）→ 会加载 av_libGLESv2 / libEGL / d3dcompiler_47，
#                                还可能把整套显卡驱动映射进来（nvgpucomp64 之类）
#     · OpenGL（WGL）        → 会加载 opengl32.dll
#     · 软件渲染            → 上面这些一个都不该出现，只有 libSkiaSharp
#   所以：写一份 config.json 指定后端 → 启动 → 看模块 → 关掉。
#
# 用法：
#   powershell -File tools\probe-render-backend.ps1 -Backend software
#   powershell -File tools\probe-render-backend.ps1 -Backend auto
#   powershell -File tools\probe-render-backend.ps1 -Backend wgl
#   powershell -File tools\probe-render-backend.ps1 -Backend vulkan
#   powershell -File tools\probe-render-backend.ps1 -EnvironmentOverride angle   # 测环境变量优先级
param(
    [string]$ExePath = "HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe",
    [string]$Backend = "software",
    [string]$EnvironmentOverride = "",
    [int]$WaitSeconds = 8
)

$ErrorActionPreference = "Stop"

$exe = (Resolve-Path $ExePath).Path
# 配置目录必须放在"程序自己能写的地方"。
#
# 踩过的坑：最早用的是 %TEMP%，结果程序一启动就报
#   SQLite Error 14: 'unable to open database file'
# 排查半天才确认不是路径拼错 —— 是这台机器上沙箱环境不允许该进程往 %TEMP% 建文件
# （同一目录里 PowerShell 能写、程序写不了；换成工作区里的目录立刻正常）。
# 所以固定放在脚本旁边，跑完删掉。
$configDir = Join-Path $PSScriptRoot ("_probe-render-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $configDir -Force | Out-Null

# 只写一个字段：其它配置项留空会走默认值，不影响这次判断
$configJson = @{ RenderBackend = $Backend } | ConvertTo-Json
$configPath = Join-Path $configDir "config.json"
Set-Content -Path $configPath -Value $configJson -Encoding UTF8

Write-Host "配置目录：$configDir"
Write-Host "本次指定：$Backend $(if ($EnvironmentOverride) { "（环境变量 HELIUM_RENDER=$EnvironmentOverride）" })"

$startInfo = [System.Diagnostics.ProcessStartInfo]::new($exe)
$startInfo.UseShellExecute = $false
$startInfo.Environment["HELIUM_CONFIG_DIR"] = $configDir
if ($EnvironmentOverride) {
    $startInfo.Environment["HELIUM_RENDER"] = $EnvironmentOverride
}

$process = [System.Diagnostics.Process]::Start($startInfo)
try {
    Start-Sleep -Seconds $WaitSeconds
    if ($process.HasExited) {
        Write-Host "程序已经退出（ExitCode=$($process.ExitCode)）—— 说明这个后端起不来" -ForegroundColor Red
        return
    }

    $modules = @($process.Modules | ForEach-Object { $_.ModuleName })
    $checks = @(
        @{ Name = "libSkiaSharp"; Expect = $true; Note = "任何后端都该有" },
        @{ Name = "av_libGLESv2"; Expect = $false; Note = "ANGLE 才有" },
        @{ Name = "libEGL"; Expect = $false; Note = "ANGLE 才有" },
        @{ Name = "d3dcompiler_47"; Expect = $false; Note = "ANGLE 才有" },
        @{ Name = "opengl32"; Expect = $false; Note = "OpenGL/WGL 才有" },
        @{ Name = "nvgpucomp64"; Expect = $false; Note = "显卡驱动着色器编译器（ANGLE 才会拉进来）" }
    )

    Write-Host ""
    Write-Host "── 关键模块 ──"
    foreach ($check in $checks) {
        $hit = @($modules | Where-Object { $_ -like "*$($check.Name)*" })
        $loaded = $hit.Count -gt 0
        $mark = if ($loaded) { "✓" } else { "✗" }
        $state = if ($loaded) { "已加载" } else { "未加载" }
        Write-Host ("  {0} {1,-18} {2}  ({3})" -f $mark, $check.Name, $state, $check.Note)
    }

    # 显卡驱动模块（ANGLE 才会带进来，软件渲染里出现就说明没生效）
    $gpu = @($modules | Where-Object { $_ -match "^(nvgpucomp|nvwgf2umx|nvppex|igd|atio)" })
    Write-Host ("  显卡驱动模块：{0}" -f $(if ($gpu.Count -gt 0) { $gpu -join ", " } else { "无" }))
    Write-Host ("  线程 {0} · 句柄 {1}" -f $process.Threads.Count, $process.HandleCount)
}
finally {
    if (-not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit(5000) | Out-Null
    }
    Remove-Item -Recurse -Force $configDir -ErrorAction SilentlyContinue
}