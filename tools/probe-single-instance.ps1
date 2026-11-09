# 验证"第二个实例只喊一声就干净退出"：退出码必须是 0，第一个实例必须还活着。



#



# 为什么需要这个脚本：



#   0.3.5 之前这里有个真 bug —— 第二个实例在 OnFrameworkInitializationCompleted 里



#   直接 desktop.Shutdown()，而 Avalonia 的消息循环要到这一句返回之后才起来，



#   于是 StartCore 进 MainLoop 时抛



#     InvalidOperationException: Cannot perform requested operation because the Dispatcher shut down



#   进程以 0xE0434352（未处理异常）退出。用户看到的就是"程序是异常结束的"，



#   而"再双击一次程序"恰恰是最常见的操作路径。



#   无头渲染碰不到这条链路（它不建窗口、也不起消息循环），只能真机验。



$ErrorActionPreference = 'Stop'



[Console]::OutputEncoding = [System.Text.Encoding]::UTF8







. (Join-Path $PSScriptRoot 'probe-common.ps1')







$exe = Join-Path $PSScriptRoot '..\HeliumTodo.App\bin\Debug\net10.0\HeliumTodo.App.exe'



$exe = (Resolve-Path $exe).Path







$sandbox = Join-Path $PSScriptRoot ('_probe-single-' + [guid]::NewGuid().ToString('N'))



New-Item -ItemType Directory -Path $sandbox | Out-Null







# 每次跑用一个独立 app id：否则上一次没清干净的实例（或用户自己开着的 HeliumTodo）
# 会占着同一个互斥量，害得这一次的"第一份"发现自己不是第一个 —— 不建窗口，
# 脚本就卡在等窗口那一步，而且现象完全不像"名字撞了"。
$appId = 'HeliumTodoProbe' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$env_ = @{ HELIUM_CONFIG_DIR = $sandbox; HELIUM_APP_ID = $appId }



$ok = $true







$first = Start-HeliumApp -Exe $exe -Environment $env_



$hwnd = Wait-HeliumWindow -Process $first -TimeoutMs 30000







if ($hwnd -eq [IntPtr]::Zero) {



    Write-Host '  [失败] 第一份实例没开出窗口'



    $ok = $false



}



else {



    Start-Sleep -Milliseconds 1200







    $second = Start-HeliumApp -Exe $exe -Environment $env_



    $exited = $second.WaitForExit(40000)







    if (-not $exited) {



        Write-Host '  [失败] 第二份实例 40 秒还没退出（单实例守卫没生效？）'



        $second.Kill()



        $ok = $false



    }



    else {



        $code = $second.ExitCode



        Write-Host ("  第二份实例退出码 {0}（0x{1:X8}）" -f $code, ($code -band 0xFFFFFFFF))



        if ($code -eq 0) {



            Write-Host '  ✔ 第二份实例干净退出（退出码 0）'



        }



        else {



            Write-Host '  [失败] 第二份实例不是干净退出：0xE0434352 表示 Dispatcher 已关还在起消息循环'



            $ok = $false



        }



    }







    # 第一份必须还活着（第二份只是请它把窗口显示出来）



    if ($first.HasExited) {



        Write-Host '  [失败] 第一份实例跟着退出了，后台常驻被破坏'



        $ok = $false



    }



    else {



        Write-Host '  ✔ 第一份实例仍在运行（窗口被唤到前面）'



    }







    $log = Join-Path $sandbox 'background.log'



    if (Test-Path $log) {



        $hit = Get-Content $log -Encoding UTF8 | Where-Object { $_ -like '*[单实例]*' }



        if ($hit) {



            Write-Host ("  日志：{0}" -f @($hit)[-1].Trim())



        }



        else {



            Write-Host '  [失败] 日志里没有"[单实例]"这一行'



            $ok = $false



        }



    }



}







if (-not $first.HasExited) {



    $first.Kill()



    Start-Sleep -Milliseconds 800



}







Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue







exit ([int](-not $ok))