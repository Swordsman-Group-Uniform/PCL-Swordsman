# 打包插件为 .pclpkg（zip：plugin.json + <name>.dll），产物到 plugins/dist/
#
# 与 plugins/package.sh 等价。Windows 上没有 bash / python3 时用这个。
#
# 用法：
#   powershell -NoProfile -ExecutionPolicy Bypass -File plugins\package.ps1
#   powershell ... -File plugins\package.ps1 -Configuration Release -Platform x64
#   powershell ... -File plugins\package.ps1 -BundleOnly        # 只打包，不重新编译
#
# 构建完记得同步到 dist\plugins\ 或 exe 同级 plugins\（tools\build.ps1 会自动做）。

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Platform = 'x64',
    [switch]$BundleOnly,
    [string]$DotNet = 'dotnet'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'plugins\dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# HelloPlugin 在 samples/ 下，其余在 plugins/ 下
# Out = 'dist' 的插件不提供给启动器内嵌（否则非正式版构建会默认装上），只放到 dist\ 供用户手动安装
$plugins = @(
    @{ Name = 'SguPack'; Base = (Join-Path $root 'plugins') },
    @{ Name = 'PCLSCore'; Base = (Join-Path $root 'plugins') },
    @{ Name = 'AdvancedMaterials'; Base = (Join-Path $root 'plugins') },
    @{ Name = 'Welcome'; Base = (Join-Path $root 'plugins') },
    @{ Name = 'FadedGray'; Base = (Join-Path $root 'plugins') },
    @{ Name = 'HelloPlugin'; Base = (Join-Path $root 'samples') }
)

foreach ($plugin in $plugins) {
    $name = $plugin.Name
    $base = $plugin.Base
    $dir = Join-Path $base $name
    $project = Join-Path $dir ($name + '.csproj')

    if (-not $BundleOnly) {
        Write-Host "==> 构建插件 $name（$Configuration/$Platform）"
        & $DotNet build $project -c $Configuration "-p:Platform=$Platform" `
            -p:RunAnalyzers=false -m:1 -p:UseSharedCompilation=false -v q
        if ($LASTEXITCODE -ne 0) { throw "构建失败：$name（exit $LASTEXITCODE）" }
    }

    $out = Join-Path $dir ("bin\" + $Platform + "\" + $Configuration + "\net10.0-windows")
    $files = @('plugin.json', ($name + '.dll'))
    foreach ($file in $files) {
        $path = Join-Path $out $file
        if (-not (Test-Path -LiteralPath $path)) { throw "未找到插件输出：$path" }
    }

    $outDir = if ($plugin.Out -eq 'dist') { Join-Path $root 'dist' } else { $dist }
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    $pkg = Join-Path $outDir ($name + '.pclpkg')
    if (Test-Path -LiteralPath $pkg) { Remove-Item -LiteralPath $pkg -Force }

    $zip = [IO.Compression.ZipFile]::Open($pkg, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $path = Join-Path $out $file
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $path, $file, [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $zip.Dispose()
    }

    Write-Host "==> 已打包：$pkg"
}

Write-Host "==> 插件包全部就绪：$dist"
