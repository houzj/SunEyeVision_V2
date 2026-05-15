# deploy_runtime.ps1
# 将 runtime/ 目录中的运行时 DLL 复制到输出目录
# 在首次构建前执行一次即可，后续构建会自动复制

param(
    [string]$OutputDir = "dev\Debug"
)

$RuntimeDir = Join-Path $PSScriptRoot "runtime"

Write-Host "=== 部署运行时依赖 ===" -ForegroundColor Cyan
Write-Host "源: $RuntimeDir"
Write-Host "目标: $OutputDir"

if (-not (Test-Path $RuntimeDir)) {
    Write-Host "[!] runtime/ 目录不存在，请先放置运行时文件" -ForegroundColor Red
    exit 1
}

# 确保输出目录
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$copied = 0

# 复制所有 DLL
foreach ($dll in Get-ChildItem "$RuntimeDir\*.dll") {
    Copy-Item $dll.FullName (Join-Path $OutputDir $dll.Name) -Force
    Write-Host "  [+] $($dll.Name)"
    $copied++
}

# 复制 License
$licOut = Join-Path $OutputDir "license"
if (-not (Test-Path $licOut)) { New-Item -ItemType Directory -Path $licOut -Force | Out-Null }
Copy-Item "$RuntimeDir\license\license.txt" "$licOut\license.txt" -Force
Write-Host "  [+] license/license.txt"
$copied++

Write-Host "=== 完成: 复制 $copied 个文件 ===" -ForegroundColor Cyan
