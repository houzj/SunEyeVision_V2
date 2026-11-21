[CmdletBinding()]
param(
    [string]$Repo = '',
    [switch]$StopUi
)

$ErrorActionPreference = 'Stop'
$root = if ([string]::IsNullOrWhiteSpace($Repo)) {
    # scripts -> tool-production -> skills -> .opencode -> repository root
    (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
} else {
    (Resolve-Path $Repo).Path
}
$app = Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Require-Path([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path)) { $errors.Add("$label 不存在：$path") }
}

Require-Path $app 'Release 目录'
Require-Path (Join-Path $app 'SunEyeVision.UI.exe') 'Release 程序'
Require-Path (Join-Path $app 'release-payload.json') 'Release 清单'
Require-Path (Join-Path $root 'libs/halcon/net/halcondotnetxl.dll') 'HALCON 托管程序集'
Require-Path (Join-Path $root 'libs/halcon/runtime/x64-win64') 'HALCON native 目录'

$ui = @(Get-Process SunEyeVision.UI -ErrorAction SilentlyContinue)
if ($ui.Count -gt 0) {
    if ($StopUi) {
        $ui | Stop-Process -Force
        Start-Sleep -Milliseconds 500
        if (@(Get-Process SunEyeVision.UI -ErrorAction SilentlyContinue).Count -gt 0) {
            $errors.Add('SunEyeVision.UI 仍在运行，无法安全重写 Release 载荷。')
        }
    } else {
        $errors.Add('SunEyeVision.UI 正在运行，构建前必须先关闭。')
    }
}

$probeFiles = @(
    (Join-Path $app 'release-payload.json'),
    (Join-Path $app '.halcon-native-deployed')
)
foreach ($path in $probeFiles | Where-Object { Test-Path -LiteralPath $_ }) {
    try {
        $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $stream.Dispose()
    } catch {
        $errors.Add("关键 Release 文件不可写或被占用：$path。$($_.Exception.Message)")
    }
}

$payload = Join-Path $app 'release-payload.json'
if (Test-Path -LiteralPath $payload) {
    $payloadTime = (Get-Item -LiteralPath $payload).LastWriteTimeUtc
    $newerAssemblies = @(Get-ChildItem -LiteralPath $app -Filter '*.dll' -File |
        Where-Object { $_.LastWriteTimeUtc -gt $payloadTime.AddSeconds(1) })
    if ($newerAssemblies.Count -gt 0) {
        $warnings.Add("Release 清单早于程序集，构建后必须重新封存：$($newerAssemblies.Name -join ', ')")
    }
}

$result = [ordered]@{
    Repo = $root
    ReleaseApp = $app
    Status = if ($errors.Count -eq 0) { 'passed' } else { 'blocked' }
    Errors = @($errors)
    Warnings = @($warnings)
}
$result | ConvertTo-Json -Depth 5
if ($errors.Count -gt 0) { exit 1 }
exit 0
