[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'installer/Release.Common.ps1')
Initialize-ReleaseToolMetadataReader
$identity=[SunEyeVision.ReleaseToolMetadataReader]::ReadProductIdentity((Join-Path $AppPath 'SunEyeVision.Foundation.dll'))
$artifact=$identity['Product'];if($identity['Channel'] -cne 'stable'){$artifact+='_'+$identity['Channel']}
$component=[pscustomobject]@{componentId='SunEyeVision.OutcomeOwnership.Probe';toolId='OutcomeOwnershipProbe';role='Runtime';entryType='SunEyeVision.OutcomeOwnership.Probe.Definition';companionToolId='';path='SunEyeVision.OutcomeOwnership.Probe.dll'}
$payload=New-ReleasePayload -AppPath $AppPath -BuildId ('test-'+[guid]::NewGuid().ToString('N')) -SourceCommit $identity['Commit'] -ArtifactVersion $artifact -ProductComponents @($component) -RuntimeConfigName 'OutcomeAdmissionProbe.runtimeconfig.json'
$sha=Write-ReleasePayload -AppPath $AppPath -Payload $payload
Write-Output "Test payload sealed: $sha"
