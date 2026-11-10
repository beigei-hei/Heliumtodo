# SPDX-License-Identifier: GPL-3.0-only
# 发布脚本：自包含 win-x64 包 + 许可证 + SOURCE.txt + zip。
# 用法：pwsh tools\publish.ps1 [-SkipPublish]
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$SkipPublish,
    # 单文件发布：把托管程序集与原生库都塞进一个 exe（运行期自解压到临时目录）。
    # 想回到目录式发布用 -SingleFile:$false。
    [bool]$SingleFile = $true
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $props = Get-Content Directory.Build.props -Raw
    if ($props -notmatch '<Version>([^<]+)</Version>') { throw '无法从 Directory.Build.props 读取 Version' }
    $version = $Matches[1]
    $zipVersion = $version -replace '-.*$', ''
    $name = "HeliumTodo-$zipVersion-$Runtime"
    $staging = Join-Path $root "dist\$name"
    $zip = Join-Path $root "dist\$name.zip"

    if (-not $SkipPublish) {
        $env:TMP = Join-Path $root '.tmp-publish'
        $env:TEMP = $env:TMP
        New-Item -ItemType Directory -Force $env:TMP | Out-Null
        $singleFileArgs = @()
        if ($SingleFile) {
            $singleFileArgs = @(
                '-p:PublishSingleFile=true',
                '-p:IncludeNativeLibrariesForSelfExtract=true',
                '-p:EnableCompressionInSingleFile=true'
            )
        }
        dotnet publish "HeliumTodo.Desktop\HeliumTodo.Desktop.csproj" -c $Configuration -r $Runtime `
            --self-contained true -p:UseSharedCompilation=false -p:DebugType=none @singleFileArgs -o $staging
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败：$LASTEXITCODE" }
    }

    # 原生库的 .pdb（libSkiaSharp / libHarfBuzzSharp）会跟着发布目录走，
    # 加起来 100 MB 以上，只在调试时有意义，打包前删掉。
    Get-ChildItem $staging -Filter *.pdb -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

    Copy-Item (Join-Path $root 'LICENSE') $staging -Force
    Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $staging -Force
    Copy-Item (Join-Path $root 'CONTRIBUTING.md') $staging -Force
    Copy-Item (Join-Path $root 'LICENSES') $staging -Recurse -Force

    # SkiaSharp / HarfBuzzSharp 的 Windows 原生库依赖 MSVC 运行库；自包含发布不带它。
    # 目标机没装 VC++ Redistributable 时，加载原生库会以 0xc0000142（DLL 初始化失败）退出。
    # 这些 DLL 是微软允许随应用分发的运行库文件，从本机 System32 取同版本副本。
    $vcRuntime = @('vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll')
    foreach ($dll in $vcRuntime) {
        $source = Join-Path $env:SystemRoot "System32\$dll"
        if (Test-Path $source) {
            Copy-Item $source $staging -Force
        } else {
            Write-Warning "未找到 $source，目标机需要自行安装 VC++ Redistributable"
        }
    }

    $commit = (git rev-parse HEAD).Trim()
    @"
Helium Todo $version 源码对应关系
=====================================

仓库地址：本仓库（发布时填写公开仓库地址，见 README）。
提交：$commit
构建日期：$(Get-Date -Format 'yyyy-MM-dd')
运行时：$Runtime（self-contained）
构建方式：
  dotnet publish HeliumTodo.Desktop\HeliumTodo.Desktop.csproj -c $Configuration -r $Runtime --self-contained true

许可证：GPL-3.0-only，全文见 LICENSE；第三方组件声明见 THIRD-PARTY-NOTICES.md，
许可全文在 LICENSES/ 目录。

随包附带 vcruntime140.dll / vcruntime140_1.dll / msvcp140.dll（Microsoft Visual C++
Redistributable，微软允许随应用分发），供 SkiaSharp/HarfBuzzSharp 原生库使用；
目标机安装过 VC++ Redistributable 时可忽略这几个文件。
"@ | Set-Content (Join-Path $staging 'SOURCE.txt') -Encoding utf8

    if (Test-Path $zip) { Remove-Item $zip -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)

    $size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    $sha = (Get-FileHash $zip -Algorithm SHA256).Hash
    Write-Host "发布包：$zip"
    Write-Host "大小：$size MB"
    Write-Host "SHA256：$sha"
}
finally {
    Pop-Location
}