
<#
  内存探针：启动真实 exe，按时间采样工作集 / 私有内存 / GC 堆，
  最后按体积列出已加载的模块（找出谁在吃内存）。

  用法：
    powershell -File tools\probe-memory.ps1 -ExePath "...\HeliumTodo.Desktop.exe" [-ConfigDir ...] [-Seconds 20]
#>
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$ConfigDir = "",
    [int]$Seconds = 20
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 任务管理器「内存」那一列显示的是"私有工作集"，跟 PrivateMemorySize64 不是一回事，
# 所以单独取一次，免得跟用户看到的数字对不上。
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class MemProbe
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COUNTERS_EX
    {
        public uint cb;
        public IntPtr PageFaultCount;
        public IntPtr PeakWorkingSetSize;
        public IntPtr WorkingSetSize;
        public IntPtr QuotaPeakPagedPoolUsage;
        public IntPtr QuotaPagedPoolUsage;
        public IntPtr QuotaPeakNonPagedPoolUsage;
        public IntPtr QuotaNonPagedPoolUsage;
        public IntPtr PagefileUsage;
        public IntPtr PeakPagefileUsage;
        public IntPtr PrivateUsage;
        public IntPtr PrivateWorkingSetSize;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref COUNTERS_EX counters, uint size);

    public static long PrivateWorkingSet(IntPtr handle)
    {
        var c = new COUNTERS_EX();
        c.cb = (uint)Marshal.SizeOf<COUNTERS_EX>();
        return GetProcessMemoryInfo(handle, ref c, c.cb) ? c.PrivateWorkingSetSize.ToInt64() : 0;
    }
}
"@

if ($ConfigDir -ne "") {
    $env:HELIUM_CONFIG_DIR = $ConfigDir
}

$started = Get-Date
$proc = Start-Process -FilePath $ExePath -PassThru
Write-Host ("启动 PID={0}" -f $proc.Id)

$samples = @()
foreach ($t in @(1, 2, 3, 5, 8, 12, 16, 20, 25, 30)) {
    if ($t -gt $Seconds) { break }
    while (((Get-Date) - $started).TotalSeconds -lt $t) { Start-Sleep -Milliseconds 50 }
    $proc.Refresh()
    if ($proc.HasExited) { Write-Host "✗ 进程退出了"; break }
    $samples += [pscustomobject]@{
        秒   = $t
        工作集MB = [math]::Round($proc.WorkingSet64 / 1MB, 1)
        私有MB   = [math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
        私有工作集MB = [math]::Round(([MemProbe]::PrivateWorkingSet($proc.Handle)) / 1MB, 1)
        峰值MB   = [math]::Round($proc.PeakWorkingSet64 / 1MB, 1)
        线程     = $proc.Threads.Count
        句柄     = $proc.HandleCount
    }
}
$samples | Format-Table -AutoSize | Out-String | Write-Host

# 等进程稳定，再抓一次模块清单（按内存映射大小排序）
$proc.Refresh()
if (-not $proc.HasExited) {
    Write-Host "── 已加载模块（按大小，前 15）──"
    $proc.Modules | Sort-Object -Property ModuleMemorySize -Descending |
        Select-Object -First 15 @{n = 'MB'; e = { [math]::Round($_.ModuleMemorySize / 1MB, 1) } }, ModuleName |
        Format-Table -AutoSize | Out-String | Write-Host

    Write-Host "── 关键模块是否加载 ──"
    $names = $proc.Modules | ForEach-Object { $_.ModuleName }
    foreach ($key in @('icudt', 'icudtl', 'libSkiaSharp', 'libHarfBuzzSharp', 'libEGL', 'libGLESv2', 'd3d11', 'd3d9', 'opengl32', 'av_libglesv2')) {
        # 注意：单个匹配时 Where-Object 会返回字符串而不是数组，
        # 直接写 $hit[0] 取到的是"字符串的第一个字符"（"libEGL"[0] => "l"），
        # 所以这里必须用 @() 强制成数组再取第一个。
        $hit = @($names | Where-Object { $_ -like "*$key*" })
        if ($hit.Count -gt 0) {
            $name = $hit[0]
            $size = (@($proc.Modules | Where-Object { $_.ModuleName -eq $name }) | Select-Object -First 1).ModuleMemorySize
            Write-Host ("  ✓ {0}  ({1} MB)" -f $name, [math]::Round($size / 1MB, 1))
        } else {
            Write-Host ("  ✗ {0}（没加载）" -f $key)
        }
    }
}

$proc.Refresh()
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
Write-Host "（已结束进程）"