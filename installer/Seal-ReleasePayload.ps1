[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath,[string]$FrameworkOriginPath,[string]$SourceCommit)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Release.Common.ps1"
$root=Split-Path -Parent $PSScriptRoot
$appRoot=[IO.Path]::TrimEndingDirectorySeparator((Resolve-Path -LiteralPath $AppPath).Path)
$components=@(Get-ProductToolComponents)
Remove-RetiredProductToolOutputs -AppPath $appRoot
$head=$SourceCommit
if([string]::IsNullOrWhiteSpace($head)) {
    $head=(& git -C $root rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot identify the source commit.'}
}
$version=Get-Content (Join-Path $root 'version.json') -Raw|ConvertFrom-Json
$identity=Get-ReleaseIdentity $version
$payload=New-ReleasePayload -AppPath $AppPath -BuildId ('development-'+[guid]::NewGuid().ToString('N')) -SourceCommit $head `
    -ArtifactVersion $identity.ArtifactVersion -ProductComponents $components -FrameworkOriginPath $FrameworkOriginPath
$sha=Write-ReleasePayload -AppPath $AppPath -Payload $payload
Write-Output "Build payload sealed: $sha"
