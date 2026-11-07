[CmdletBinding()]
param(
    [string]$AppPath = (Join-Path $PSScriptRoot '../../Release/SunEyeVision_v1.0.0/SunEyeVision'),
    [switch]$Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$app = (Resolve-Path $AppPath).Path

if ($Build) {
    dotnet restore (Join-Path $repo 'tests/NeutralReleaseProbe/NeutralReleaseProbe.csproj')
    dotnet build (Join-Path $repo 'tests/NeutralReleaseProbe/NeutralReleaseProbe.csproj') -c Release --no-restore
}

Get-ChildItem -LiteralPath $app -File -Filter 'SunEyeVision.Plugin.SDK.*' -ErrorAction SilentlyContinue | Remove-Item -Force
$commit = (& git -C $repo rev-parse HEAD).Trim()
& (Join-Path $repo 'installer/Seal-ReleasePayload.ps1') -AppPath $app -SourceCommit $commit

$probeRoot = Join-Path $repo 'Release/_validation/bin/NeutralReleaseProbe/Release'
$probeFiles = @('NeutralReleaseProbe.dll', 'NeutralReleaseProbe.deps.json', 'NeutralReleaseProbe.runtimeconfig.json')
$copied = [System.Collections.Generic.List[string]]::new()
try {
    foreach ($name in $probeFiles) {
        $source = Join-Path $probeRoot $name
        if (-not (Test-Path -LiteralPath $source)) { throw "探针输出不存在：$source" }
        $target = Join-Path $app $name
        Copy-Item -LiteralPath $source -Destination $target -Force
        $copied.Add($target)
    }
    dotnet exec (Join-Path $app 'NeutralReleaseProbe.dll') $app
}
finally {
    foreach ($path in $copied) {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
}
