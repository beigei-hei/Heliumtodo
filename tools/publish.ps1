# SPDX-License-Identifier: GPL-3.0-only
# 发布脚本：自包含 win-x64 包 + 许可证 + SOURCE.txt + zip。
# 用法：pwsh tools\publish.ps1 [-SkipPublish]
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$SkipPublish
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
        dotnet publish "HeliumTodo.Desktop\HeliumTodo.Desktop.csproj" -c $Configuration -r $Runtime `
            --self-contained true -p:UseSharedCompilation=false -p:DebugType=none -o $staging
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败：$LASTEXITCODE" }
    }

    Copy-Item (Join-Path $root 'LICENSE') $staging -Force
    Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $staging -Force
    Copy-Item (Join-Path $root 'CONTRIBUTING.md') $staging -Force
    Copy-Item (Join-Path $root 'LICENSES') $staging -Recurse -Force

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