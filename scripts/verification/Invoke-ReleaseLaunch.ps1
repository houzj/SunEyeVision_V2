[CmdletBinding()]
param(
    [string]$Project = '',
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$app = Join-Path $repo 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$exe = Join-Path $app 'SunEyeVision.UI.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Release 程序不存在：$exe" }

Get-Process SunEyeVision.UI -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

if (-not [string]::IsNullOrWhiteSpace($Project)) {
    dotnet build (Join-Path $repo $Project) -c Release --no-restore -p:SunEyeVisionSkipPayloadSeal=true
    if ($LASTEXITCODE -ne 0) { throw "构建失败：$LASTEXITCODE" }
}

function Test-ReleasePayload {
    $manifestPath = Join-Path $app 'release-payload.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { return $false }
    try { $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json }
    catch { return $false }
    foreach ($entry in @($manifest.entries)) {
        $file = Join-Path $app ([string]$entry.path)
        if (-not (Test-Path -LiteralPath $file)) { return $false }
        $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        if ($actual -ne [string]$entry.sha256) { return $false }
    }
    return $true
}

$pwsh = $null
$localProps = Join-Path $repo 'installer/Release.Local.props'
if (Test-Path -LiteralPath $localProps) {
    [xml]$props = Get-Content -Raw -LiteralPath $localProps
    $pwsh = $props.Project.PropertyGroup.ReleasePowerShellPath
}
if ([string]::IsNullOrWhiteSpace($pwsh) -or -not (Test-Path -LiteralPath $pwsh)) {
    $fallback = Join-Path $env:USERPROFILE '.local/pwsh7/pwsh.exe'
    if (Test-Path -LiteralPath $fallback) { $pwsh = $fallback }
}
if ([string]::IsNullOrWhiteSpace($pwsh) -or -not (Test-Path -LiteralPath $pwsh)) {
    throw '找不到 PowerShell 7，无法封存载荷。'
}

if (-not (Test-ReleasePayload)) {
    Write-Host '检测到 Release 载荷哈希不同步，重新封存...'
    & $pwsh -NoProfile -File (Join-Path $repo 'installer/Seal-ReleasePayload.ps1') -AppPath $app
    if ($LASTEXITCODE -ne 0) { throw "封存失败：$LASTEXITCODE" }
}
else {
    Write-Host 'Release 载荷哈希一致，无需重新封存。'
}

if (-not $NoLaunch) {
    Start-Process -FilePath $exe
    Write-Host '已启动 Release。'
}
