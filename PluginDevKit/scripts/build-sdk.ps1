# 从主项目构建 SDK 并复制到 PluginDevKit
# 使用方法: .\build-sdk.ps1 [-Configuration Release]

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# 路径配置
$RootDir = Split-Path -Parent $PSScriptRoot
$MainProjectDir = Split-Path -Parent $RootDir
$SdkOutputDir = Join-Path $RootDir "sdk\net9.0"

Write-Host "=== 构建 SunEyeVision Plugin SDK ===" -ForegroundColor Cyan
Write-Host "配置: $Configuration"
Write-Host "主项目目录: $MainProjectDir"
Write-Host "SDK 输出目录: $SdkOutputDir"

# 创建 SDK 输出目录
if (-not (Test-Path $SdkOutputDir)) {
    New-Item -ItemType Directory -Path $SdkOutputDir -Force | Out-Null
    Write-Host "创建 SDK 目录: $SdkOutputDir" -ForegroundColor Green
}
Get-ChildItem -LiteralPath $SdkOutputDir -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force

$projects = @(
    "src\Plugin.Contracts\SunEyeVision.Plugin.Contracts.csproj",
    "src\Plugin.Runtime\SunEyeVision.Plugin.Runtime.csproj",
    "src\Plugin.Wpf\SunEyeVision.Plugin.Wpf.csproj"
)
foreach ($relative in $projects) {
    $project = Join-Path $MainProjectDir $relative
    dotnet build $project -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "发布 SDK 工程构建失败：$relative" }
}

$ReleaseDir = Join-Path $MainProjectDir "Release\SunEyeVision_v1.0.0\SunEyeVision"
$SdkFiles = @(
    "SunEyeVision.Plugin.Contracts.dll",
    "SunEyeVision.Plugin.Runtime.dll",
    "SunEyeVision.Plugin.Wpf.dll",
    "SunEyeVision.Foundation.dll"
)
foreach ($file in $SdkFiles) {
    $srcPath = Join-Path $ReleaseDir $file
    if (-not (Test-Path $srcPath)) { throw "发布 SDK 文件不存在：$srcPath" }
    Copy-Item $srcPath $SdkOutputDir -Force
}
$contractsXml = Join-Path $ReleaseDir 'SunEyeVision.Plugin.Contracts.xml'
if (Test-Path $contractsXml) { Copy-Item $contractsXml $SdkOutputDir -Force }

Write-Host "`n=== SDK 构建完成 ===" -ForegroundColor Cyan
Write-Host "SDK 文件位于: $SdkOutputDir"

# 列出 SDK 文件
Get-ChildItem $SdkOutputDir | Format-Table Name, Length, LastWriteTime
