param(
    [string]$ReportDirectory,
    [string]$NativeDirectory,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $ReportDirectory) { $ReportDirectory = Join-Path $repoRoot 'artifacts/validation/plugin-foundation-01' }
if (-not $NativeDirectory) { $NativeDirectory = Join-Path $repoRoot 'libs/halcon/runtime/x64-win64' }
New-Item -ItemType Directory -Force -Path $ReportDirectory | Out-Null
if (-not $SkipBuild) {
    & dotnet restore (Join-Path $PSScriptRoot 'PluginFoundation.slnx') --ignore-failed-sources -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Foundation restore failed' }
    & dotnet build (Join-Path $PSScriptRoot 'PluginFoundation.slnx') -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Foundation build failed' }
}
$assemblyPath = Join-Path $repoRoot 'artifacts/validation/plugin-foundation-01/bin/SunEyeVision.Plugin.Foundation.Verification/Release/SunEyeVision.Plugin.Foundation.Verification.dll'
$previousPath = $env:PATH
try {
    $env:PATH = $NativeDirectory + [IO.Path]::PathSeparator + $env:PATH
    & dotnet $assemblyPath (Join-Path $ReportDirectory 'local-report.json')
    $runExit = $LASTEXITCODE
} finally { $env:PATH = $previousPath }
exit $runExit
