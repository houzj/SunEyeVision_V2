[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath,[string]$FrameworkOriginPath,[string]$SourceCommit)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Release.Common.ps1"
$root=Split-Path -Parent $PSScriptRoot
[xml]$solution=Get-Content (Join-Path $root 'SunEyeVision.slnx') -Raw
$tools=@($solution.SelectNodes('//Project')|ForEach-Object{Get-Item (Join-Path $root $_.Path)}|
    Where-Object{(Get-Content $_.FullName -Raw)-match '<IsToolPlugin>true</IsToolPlugin>'})
$head=$SourceCommit
if([string]::IsNullOrWhiteSpace($head)) {
    $head=(& git -C $root rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot identify the source commit.'}
}
$version=Get-Content (Join-Path $root 'version.json') -Raw|ConvertFrom-Json
$identity=Get-ReleaseIdentity $version
$payload=New-ReleasePayload -AppPath $AppPath -BuildId ('development-'+[guid]::NewGuid().ToString('N')) -SourceCommit $head `
    -ArtifactVersion $identity.ArtifactVersion -ExpectedPluginAssemblies $tools.BaseName -FrameworkOriginPath $FrameworkOriginPath
$sha=Write-ReleasePayload -AppPath $AppPath -Payload $payload
Write-Output "Build payload sealed: $sha"
