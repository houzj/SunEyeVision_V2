# ScriptReferences 初始化 — 复制 BCL 核心程序集 + 项目依赖
$targetDir = "d:\MyWork\SunEyeVision_V2-script\dev\Debug\data\ScriptRefs"
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }

# === 自动探测最新 .NET 运行时版本目录 ===
$dotnetRoot = "C:\Program Files\dotnet\shared\Microsoft.NETCore.App"
if (-not (Test-Path $dotnetRoot)) {
    Write-Host "ERROR: .NET runtime directory not found at $dotnetRoot" -ForegroundColor Red
    exit 1
}

$latestVersion = Get-ChildItem $dotnetRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1

if (-not $latestVersion) {
    Write-Host "ERROR: no .NET runtime version directory found under $dotnetRoot" -ForegroundColor Red
    exit 1
}

$netCoreDir = $latestVersion.FullName
Write-Host "使用 .NET 运行时: $($latestVersion.Name) ($netCoreDir)" -ForegroundColor Cyan

# === BCL 核心程序集（脚本模板 using：System, System.Collections.Generic, System.Linq, System.Text, System.IO, System.Diagnostics） ===
$bclAssemblies = @(
    "System.Private.CoreLib.dll",   # ★ 关键：所有 BCL 类型的实际定义
    "System.Runtime.dll",           # 类型转发到 System.Private.CoreLib
    "System.Collections.dll",       # System.Collections.Generic
    "System.Linq.dll",              # System.Linq
    "System.Console.dll",           # System.Console
    "System.IO.dll",                # System.IO
    # Note: System.Text 命名空间类型已包含在 System.Private.CoreLib.dll 中
    "System.Diagnostics.Debug.dll"  # System.Diagnostics
)

Write-Host "`n复制 BCL 程序集..." -ForegroundColor Yellow
$copiedCount = 0
foreach ($asm in $bclAssemblies) {
    $src = Join-Path $netCoreDir $asm
    $dst = Join-Path $targetDir $asm
    if (Test-Path $src -PathType Leaf) {
        Copy-Item -Path $src -Destination $dst -Force
        Write-Host "  OK  $asm" -ForegroundColor Green
        $copiedCount++
    } else {
        Write-Host " MISS  $asm (not found in runtime)" -ForegroundColor Red
    }
}

# === 项目依赖程序集 ===
Write-Host "`n复制项目依赖..." -ForegroundColor Yellow
$appAssemblies = @{
    "SunEyeVision.Tool.Script.dll"  = "d:\MyWork\SunEyeVision_V2-script\dev\Debug\plugins\Script\SunEyeVision.Tool.Script.dll"
    "SunEyeVision.Plugin.SDK.dll"   = "d:\MyWork\SunEyeVision_V2-script\dev\Debug\SunEyeVision.Plugin.SDK.dll"
}

foreach ($name in $appAssemblies.Keys) {
    $src = $appAssemblies[$name]
    $dst = Join-Path $targetDir $name
    if (Test-Path $src -PathType Leaf) {
        Copy-Item -Path $src -Destination $dst -Force
        Write-Host "  OK  $name" -ForegroundColor Green
        $copiedCount++
    } else {
        Write-Host " MISS  $name (not found)" -ForegroundColor Red
    }
}

# === 汇总 ===
Write-Host ""
Write-Host "ScriptRefs 初始化完成 (共 $copiedCount DLLs)" -ForegroundColor Cyan
Get-ChildItem $targetDir | Select-Object Name | Format-Table -AutoSize
