[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppPath,
    [switch]$Force,
    [string]$SourceCommit,
    [string]$FrameworkOriginPath,
    [string]$RuntimeConfigName = 'SunEyeVision.UI.runtimeconfig.json'
)
$ErrorActionPreference = 'Stop'
$AppPath = [IO.Path]::TrimEndingDirectorySeparator((Resolve-Path -LiteralPath $AppPath).Path)
$manifest = Join-Path $AppPath 'release-payload.json'

# 并行构建时多个工程会在 Build 结束后同时请求封存。用命名互斥锁串行化，
# 并在锁内按清单时间跳过紧随其后的重复请求；最后一个工程（UI 或被直接
# 构建的工具）负责写入最终载荷，避免并发写坏 release-payload.json。
$mutex = [Threading.Mutex]::new($false, 'Global\SunEyeVision.ReleaseSeal')
$acquired = $false
try {
    try { $acquired = $mutex.WaitOne([TimeSpan]::FromSeconds(120)) }
    catch [Threading.AbandonedMutexException] { $acquired = $true }
    if (-not $acquired) { throw '等待 Release 封存锁超时。' }

    if (-not $Force -and (Test-Path -LiteralPath $manifest)) {
        $age = [DateTime]::UtcNow - (Get-Item -LiteralPath $manifest).LastWriteTimeUtc
        if ($age.TotalSeconds -lt 3) {
            Write-Output 'Release payload seal skipped (already fresh).'
            exit 0
        }
    }

    $arguments = @{ AppPath = $AppPath; RuntimeConfigName = $RuntimeConfigName }
    if (-not [string]::IsNullOrWhiteSpace($SourceCommit)) { $arguments.SourceCommit = $SourceCommit }
    if (-not [string]::IsNullOrWhiteSpace($FrameworkOriginPath)) { $arguments.FrameworkOriginPath = $FrameworkOriginPath }
    & (Join-Path $PSScriptRoot 'Seal-ReleasePayload.ps1') @arguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
