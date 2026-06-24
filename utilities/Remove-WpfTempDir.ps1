# 工具插件辅助脚本: 删除 WPF 临时构建目录
# 用法: powershell -NoProfile -File "Remove-WpfTempDir.ps1" -Path "C:\path\to\plugins" -Filter "ToolName_*_wpftmp"
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Path,
    [Parameter(Mandatory=$true)][string]$Filter
)

$ErrorActionPreference = 'SilentlyContinue'
if (-not (Test-Path -LiteralPath $Path)) {
    exit 0
}

# Get-ChildItem -Filter 不支持 .*_xxx 模式(包含 _* 通配符)
# 改用 Get-ChildItem + Where-Object 过滤
$regex = [Regex]::Escape($Filter).Replace('\*', '.*')
Get-ChildItem -LiteralPath $Path -Directory -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -match "^$regex$"
} | ForEach-Object {
    Write-Host "[Remove-WpfTempDir] Deleting: $($_.FullName)"
    Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
}
exit 0
