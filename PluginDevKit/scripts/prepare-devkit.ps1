# 一键准备 PluginDevKit（构建 SDK 和 Runtime）
# 使用方法: .\prepare-devkit.ps1 [-Configuration Release]

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  准备 PluginDevKit 开发环境" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 构建 SDK
Write-Host "`n[1/2] 构建 SDK..." -ForegroundColor Yellow
& (Join-Path $ScriptDir "build-sdk.ps1") -Configuration $Configuration

# 构建 Runtime
Write-Host "`n[2/2] 构建 Runtime..." -ForegroundColor Yellow
& (Join-Path $ScriptDir "build-runtime.ps1") -Configuration $Configuration

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  PluginDevKit 准备完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "接下来可以：" -ForegroundColor Yellow
Write-Host "  1. 复制 templates/RuntimePlugin.Template 或 templates/WpfTool.Template 开始新插件开发" -ForegroundColor White
Write-Host "  2. 运行 launch-debug.ps1 启动调试" -ForegroundColor White
Write-Host ""
