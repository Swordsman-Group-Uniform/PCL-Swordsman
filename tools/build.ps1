# PCLCE For SGU —— Windows 构建脚本（与 build.sh 等价）
#
# 用途：Windows 上没有 bash / python3 时，用 PowerShell 走同一套流程：
#   读取 metadata.json 拼出版本号 -> dotnet publish -> 重命名为
#   "PCLCE For SGU [下游版本号(上游版本号)].exe" -> 同步到 dist/ 发布包目录
#
# 用法（在仓库根目录执行）：
#   powershell -ExecutionPolicy Bypass -File tools\build.ps1
#   powershell -ExecutionPolicy Bypass -File tools\build.ps1 -Platform ARM64
#   powershell -ExecutionPolicy Bypass -File tools\build.ps1 -Configuration Debug
#   powershell -ExecutionPolicy Bypass -File tools\build.ps1 -RestoreSources D:\nuget-offline
#
# 前提：.NET SDK 10；产物为框架依赖单文件，运行需要 .NET 10 桌面运行时。
# 注意：不要改程序集名（AssemblyName），否则 pack:// 资源 URI 会失效；
#       文件重命名只在构建完成后做（本脚本负责）。
#
# 与 build.sh 的对应关系（务必保持一致，否则两套入口会产出不同内容）：
#   1) 非正式版（metadata.json 的 suffix 非空）编译时传入 -p:IncludeBundledPlugins=true，
#      把 plugins\dist\*.pclpkg 作为 EmbeddedResource 嵌进 exe；正式版传 false。
#   2) 产物除了留在 artifact-<平台>，还复制一份到 dist\（发布包目录）。
#   3) 插件包始终复制到 dist\ 与 dist\plugins\；非正式版另复制到 exe 同级 plugins\，
#      正式版则删除 exe 同级 plugins\。
#   4) 构建前先跑 plugins\package.ps1（/package.sh）打包插件，并校验本地化 XAML
#      没有重复的 x:Key（重复键会让界面所有文字变空）。
#
# 本文件必须保存为「UTF-8 with BOM」：Windows PowerShell 5.1 对无 BOM 的 UTF-8
# 会按系统代码页解码，中文注释可能把换行吞掉，导致 param 块解析失败。
param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",

    [ValidateSet("Release", "Debug", "Beta", "CI")]
    [string]$Configuration = "Release",

    # 可选：本地 / 私有 NuGet 源目录或 URL。网络受限（如只能离线还原）时指定。
    [string]$RestoreSources = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Proj = Join-Path $Root "Plain Craft Launcher 2\Plain Craft Launcher 2.csproj"
$Meta = Join-Path $Root "Plain Craft Launcher 2\metadata.json"
$PluginDist = Join-Path $Root "plugins\dist"

if (-not (Test-Path $Meta)) { throw "找不到 metadata.json：$Meta" }

$v = (Get-Content $Meta -Raw | ConvertFrom-Json).version
$suffix = if ($v.suffix) { "-" + $v.suffix } else { "" }
$version = "$($v.base)$suffix($($v.upstream))"

# 非正式版嵌入插件包，正式版不嵌入（与 build.sh 的 BUNDLE_FLAG 一致）
$bundleFlag = if ($v.suffix) { "true" } else { "false" }

# 构建前校验：本地化 XAML 里不允许出现重复的 x:Key。
# 重复键会让 ResourceDictionary 加载抛 ArgumentException，而 PCL 的本地化加载失败
# 只记一条 WARN「本地化 启动失败」，表现出来却是整个界面所有文字全部变空——
# 代价极高又很难第一时间定位，所以在这里直接拦下来。
$LangDir = Join-Path $Root "PCL.Core\App\Localization\Languages"
if (Test-Path $LangDir) {
    $langFiles = @(Get-ChildItem -LiteralPath $LangDir -Filter *.xaml -File)
    foreach ($f in $langFiles) {
        $keys = [regex]::Matches((Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8), 'x:Key="([^"]+)"') |
            ForEach-Object { $_.Groups[1].Value }
        $dups = @($keys | Group-Object | Where-Object { $_.Count -gt 1 })
        if ($dups.Count -gt 0) {
            $detail = ($dups | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ", "
            throw "本地化文件 $($f.Name) 含重复键：$detail"
        }
    }
    Write-Host "==> 本地化键校验通过（$($langFiles.Count) 个语言文件）"
}

$Out = Join-Path $Root ("artifact-" + $Platform.ToLower())
Write-Host "==> 构建 PCLCE For SGU $version ($Configuration/$Platform)"

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

# 插件包必须先打出来：非正式版会把 plugins\dist\*.pclpkg 嵌进 exe（IncludeBundledPlugins=true）
$PackagePs1 = Join-Path $Root "plugins\package.ps1"
if (Test-Path -LiteralPath $PackagePs1) {
    Write-Host "==> 构建插件包"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PackagePs1 -Configuration $Configuration -Platform $Platform
    if ($LASTEXITCODE -ne 0) { Write-Host "!! 插件打包失败，继续构建本体" }
}

$publishArgs = @(
    "publish", $Proj,
    "-p:Configuration=$Configuration",
    "-p:Platform=$Platform",
    "-p:DeleteExistingFiles=true",
    "-p:IncludeBundledPlugins=$bundleFlag",
    "-m:1",
    "-p:UseSharedCompilation=false",
    "-o", $Out,
    "--no-self-contained"
)
if ($RestoreSources) { $publishArgs += "-p:RestoreSources=$RestoreSources" }

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败，退出码 $LASTEXITCODE" }

$src = Join-Path $Out "Plain Craft Launcher 2.exe"
$dst = Join-Path $Out "PCLCE For SGU $version.exe"
if (-not (Test-Path $src)) { throw "未找到预期产物：$src" }

Move-Item -Force $src $dst
Write-Host "==> 构建完成：$dst"

# 同步一份到 dist/（发布包目录，与 samples 的 .pclpkg 放在一起）
$Dist = Join-Path $Root "dist"
if (-not (Test-Path $Dist)) { New-Item -ItemType Directory -Path $Dist | Out-Null }
Copy-Item -LiteralPath $dst -Destination $Dist -Force
Write-Host "==> 已同步到发布包目录：$Dist\$(Split-Path -Leaf $dst)"

# 插件包：始终给发布包目录留一份（正式版用户要手动装）
if (Test-Path $PluginDist) {
    $pkgs = @(Get-ChildItem -LiteralPath $PluginDist -Filter *.pclpkg -File -ErrorAction SilentlyContinue)
    if ($pkgs.Count -gt 0) {
        $DistPlugins = Join-Path $Dist "plugins"
        if (-not (Test-Path $DistPlugins)) { New-Item -ItemType Directory -Path $DistPlugins | Out-Null }
        foreach ($p in $pkgs) {
            Copy-Item -LiteralPath $p.FullName -Destination $Dist -Force
            Copy-Item -LiteralPath $p.FullName -Destination $DistPlugins -Force
        }
        Write-Host "==> 已同步 $($pkgs.Count) 个插件包到 $Dist 与 $Dist\plugins"

        # 非正式版：把插件包放到 exe 同级 plugins/ 目录，启动器首次运行会自动安装
        $OutPlugins = Join-Path $Out "plugins"
        if ($v.suffix) {
            if (-not (Test-Path $OutPlugins)) { New-Item -ItemType Directory -Path $OutPlugins | Out-Null }
            foreach ($p in $pkgs) { Copy-Item -LiteralPath $p.FullName -Destination $OutPlugins -Force }
            Write-Host "==> 非正式版构建：已附带插件包（首次启动自动安装）"
        }
        else {
            if (Test-Path $OutPlugins) { Remove-Item -LiteralPath $OutPlugins -Recurse -Force }
            Write-Host "==> 正式版构建：不随包附带插件（插件包已放入 dist\plugins\）"
        }
    }
}
