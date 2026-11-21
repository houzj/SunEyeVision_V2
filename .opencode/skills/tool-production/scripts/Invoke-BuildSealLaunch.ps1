[CmdletBinding()]
param(
    [string]$Project = 'src/UI/SunEyeVision.UI.csproj',
    [switch]$SkipBuild,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$app = Join-Path $repo 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$exe = Join-Path $app 'SunEyeVision.UI.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Release 程序不存在：$exe" }

# 构建前检查工作区、HALCON 部署、Release 清单和文件锁。
& (Join-Path $PSScriptRoot 'Check-ReleasePreflight.ps1') -Repo $repo -StopUi
if ($LASTEXITCODE -ne 0) { throw "Release 构建前置检查失败：$LASTEXITCODE" }

if (-not $SkipBuild) {
    dotnet build (Join-Path $repo $Project) -c Release --no-restore -p:SunEyeVisionSkipPayloadSeal=true
    if ($LASTEXITCODE -ne 0) { throw "构建失败：$LASTEXITCODE" }
}

# 封存必须用 PowerShell 7；优先读取本机配置，否则用固定便携路径。
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

& $pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'installer/Seal-ReleasePayload.ps1') -AppPath $app
if ($LASTEXITCODE -ne 0) { throw "封存失败：$LASTEXITCODE" }

if (-not $NoLaunch) {
    Start-Process -FilePath $exe
    Write-Host '已启动 Release。启动后先确认窗口标题，再操作。'
}
