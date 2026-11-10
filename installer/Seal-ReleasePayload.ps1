[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath,[string]$FrameworkOriginPath,[string]$SourceCommit,
    [string]$RuntimeConfigName = 'SunEyeVision.UI.runtimeconfig.json',[switch]$NeutralHost)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Release.Common.ps1"
$root=Split-Path -Parent $PSScriptRoot
$appRoot=[IO.Path]::TrimEndingDirectorySeparator((Resolve-Path -LiteralPath $AppPath).Path)
$components=@(Get-ProductToolComponents)
if($NeutralHost){$components=@($components|Where-Object role -eq 'Runtime')}
if(!$NeutralHost){Remove-RetiredProductToolOutputs -AppPath $appRoot -EntryAssemblyName ($RuntimeConfigName -replace '\.runtimeconfig\.json$','')}
$head=$SourceCommit
if([string]::IsNullOrWhiteSpace($head)) {
    $head=(& git -C $root rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot identify the source commit.'}
}
$version=Get-Content (Join-Path $root 'version.json') -Raw|ConvertFrom-Json
$identity=Get-ReleaseIdentity $version
$payload=New-ReleasePayload -AppPath $appRoot -BuildId ('development-'+[guid]::NewGuid().ToString('N')) -SourceCommit $head `
    -ArtifactVersion $identity.ArtifactVersion -ProductComponents $components -FrameworkOriginPath $FrameworkOriginPath -RuntimeConfigName $RuntimeConfigName -NeutralHost:$NeutralHost
$sha=Write-ReleasePayload -AppPath $appRoot -Payload $payload
Write-Output "Build payload sealed: $sha"
