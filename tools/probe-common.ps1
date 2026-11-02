# 探针脚本共用的两件事：启动应用（不经过 shell）、等主窗口句柄。
#
# 为什么要自己起进程：
#   Start-Process 默认 UseShellExecute=$true，会走 ShellExecuteEx —— 而 ShellExecuteEx
#   依赖正在运行的资源管理器/桌面外壳。这台机器上实测出现过"资源管理器=没有运行"的状态
#   （会话里没有桌面外壳，托盘图标也因此加不上），这时 Start-Process 会**直接卡住不返回**。
#   用 ProcessStartInfo + UseShellExecute=$false 就绕开了外壳，纯 CreateProcess。

function Start-HeliumApp {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [hashtable]$Environment = @{}
    )

    foreach ($key in $Environment.Keys) {
        Set-Item -Path ("Env:" + $key) -Value $Environment[$key]
    }

    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Exe
    $info.WorkingDirectory = Split-Path -Parent $Exe
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true

    # 必须把孩子的输出接过来再丢掉：否则它继承我们的 stdout 句柄，
    # 脚本结束了管道也不会 EOF —— 外面 `| tail` 会一直等（踩过：探针"卡住"其实是这个）。
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true

    return [System.Diagnostics.Process]::Start($info)
}

function Wait-HeliumWindow {
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [int]$TimeoutMs = 15000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 200
        $Process.Refresh()
        if ($Process.HasExited) { return [IntPtr]::Zero }
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) { return $Process.MainWindowHandle }
    }

    return [IntPtr]::Zero
}

function Clear-HeliumEnvironment {
    param([string[]]$Names)

    foreach ($name in $Names) {
        Remove-Item ("Env:" + $name) -ErrorAction SilentlyContinue
    }
}