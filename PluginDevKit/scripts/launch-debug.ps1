# 启动主程序进行插件调试
# 使用方法: .\launch-debug.ps1

param(
    [string]$PluginName = "",
    [switch]$Detach,
    [string]$ReceiptPath = "",
    [string]$StartupRoot = ""
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$RuntimeDir = Join-Path $RootDir "runtime"

Write-Host "=== 启动调试 ===" -ForegroundColor Cyan

# 检查 Runtime 是否存在
if (-not (Test-Path $RuntimeDir)) {
    Write-Host "Runtime 目录不存在，请先运行 build-runtime.ps1" -ForegroundColor Red
    exit 1
}

# 查找主程序
$ExePath = Join-Path $RuntimeDir "SunEyeVision.exe"
if (-not (Test-Path $ExePath)) {
    # 尝试其他可能的名称
    $PossibleExes = @(
        "SunEyeVision.UI.exe",
        "SunEyeVision.App.exe"
    )
    
    foreach ($exe in $PossibleExes) {
        $fullPath = Join-Path $RuntimeDir $exe
        if (Test-Path $fullPath) {
            $ExePath = $fullPath
            break
        }
    }
}

if (-not (Test-Path $ExePath)) {
    Write-Host "找不到主程序可执行文件" -ForegroundColor Red
    Write-Host "Runtime 目录内容:" -ForegroundColor Yellow
    Get-ChildItem $RuntimeDir -Filter "*.exe" | Format-Table Name
    exit 1
}

Write-Host "启动主程序: $ExePath" -ForegroundColor Green

# 构建插件（如果指定）
if ($PluginName -ne "") {
    $PluginDir = Join-Path $RootDir "samples\$PluginName"
    if (Test-Path $PluginDir) {
        Write-Host "构建插件: $PluginName" -ForegroundColor Yellow
        $PluginProject = Get-ChildItem $PluginDir -Filter "*.csproj" | Select-Object -First 1
        if ($PluginProject) {
            dotnet build $PluginProject.FullName -c Debug
        }
    }
}

# 启动程序。外部 SDK 调试可指定隔离启动根，避免占用正式用户数据目录。
$arguments = @()
if (-not [string]::IsNullOrWhiteSpace($StartupRoot)) {
    $StartupRoot = [System.IO.Path]::GetFullPath($StartupRoot)
    New-Item -ItemType Directory -Path $StartupRoot -Force | Out-Null
    $marker = Join-Path $StartupRoot ".suneye-startup-test"
    if (-not (Test-Path $marker)) { New-Item -ItemType File -Path $marker -Force | Out-Null }
    $arguments += @("--startup-test-root", $StartupRoot, "--image-warmup-mode", "off")
}
$process = Start-Process -FilePath $ExePath -WorkingDirectory $RuntimeDir -ArgumentList $arguments -WindowStyle Normal -PassThru
$receipt = [ordered]@{
    pid = $process.Id
    executable = $ExePath
    runtimeRoot = $RuntimeDir
    startedAt = [DateTimeOffset]::Now.ToString("O")
    pluginName = $PluginName
    startupRoot = if ([string]::IsNullOrWhiteSpace($StartupRoot)) { $null } else { $StartupRoot }
    scope = "外部开发宿主启动与 VS 附加准备，不代表 VS 已附加"
}
$receiptFile = if ([string]::IsNullOrWhiteSpace($ReceiptPath)) {
    Join-Path $RuntimeDir "external-debug-session.json"
} else {
    $ReceiptPath
}
$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $receiptFile -Encoding utf8
Write-Host ("调试宿主 PID: {0}" -f $process.Id) -ForegroundColor Green
Write-Host ("附加根目录: {0}" -f $RuntimeDir) -ForegroundColor Green
Write-Host ("会话回执: {0}" -f $receiptFile) -ForegroundColor Green
if ($Detach) {
    return
}
try {
    $process.WaitForExit()
} finally {
    Remove-Item -LiteralPath $receiptFile -Force -ErrorAction SilentlyContinue
}
