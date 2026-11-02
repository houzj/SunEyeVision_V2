[CmdletBinding()]
param([Parameter(Mandatory)][string]$OtherProcessDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$other = (Resolve-Path -LiteralPath $OtherProcessDirectory).Path
$product = (Resolve-Path -LiteralPath (Join-Path $repo 'Release/SunEyeVision_v1.0.0/SunEyeVision')).Path
$otherManifest = Join-Path $other 'release-payload.json'
$before = if (Test-Path -LiteralPath $otherManifest) { (Get-FileHash -LiteralPath $otherManifest -Algorithm SHA256).Hash } else { $null }
$oldProcessDirectory = [Environment]::CurrentDirectory
Push-Location -LiteralPath $repo
try {
    [Environment]::CurrentDirectory = $other
    $commit = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
    & (Join-Path $repo 'installer/Seal-ReleasePayload.ps1') -AppPath './Release/SunEyeVision_v1.0.0/SunEyeVision' -SourceCommit $commit
    $manifest = Get-Content -LiteralPath (Join-Path $product 'release-payload.json') -Raw | ConvertFrom-Json
    $entry = @($manifest.entries | Where-Object path -eq 'SunEyeVision.Plugin.Runtime.dll')
    if ($entry.Count -ne 1) { throw 'Expected one Runtime entry.' }
    $actual = (Get-FileHash -LiteralPath (Join-Path $product $entry[0].path) -Algorithm SHA256).Hash
    if ($actual -ine $entry[0].sha256) { throw 'Manifest references the wrong Runtime payload.' }
    $after = if (Test-Path -LiteralPath $otherManifest) { (Get-FileHash -LiteralPath $otherManifest -Algorithm SHA256).Hash } else { $null }
    if ($before -cne $after) { throw 'The unrelated process directory manifest changed.' }
    Write-Output 'PASS: relative app path seals the intended worktree and leaves the process directory untouched.'
}
finally {
    [Environment]::CurrentDirectory = $oldProcessDirectory
    Pop-Location
}
