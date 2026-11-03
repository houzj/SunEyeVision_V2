[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'installer/Release.Common.ps1')
Initialize-ReleaseToolMetadataReader
$identity=[SunEyeVision.ReleaseToolMetadataReader]::ReadProductIdentity((Join-Path $AppPath 'SunEyeVision.Foundation.dll'))
$artifact=$identity['Product'];if($identity['Channel'] -cne 'stable'){$artifact+='_'+$identity['Channel']}
$component=[pscustomobject]@{componentId='SunEyeVision.AdmissionPreparation.Probe';toolId='AdmissionPreparationProbe';role='Runtime';entryType='SunEyeVision.AdmissionPreparation.Probe.Definition';companionToolId='';path='SunEyeVision.AdmissionPreparation.Probe.dll'}
$payload=New-ReleasePayload -AppPath $AppPath -BuildId ('test-'+[guid]::NewGuid().ToString('N')) -SourceCommit $identity['Commit'] -ArtifactVersion $artifact -ProductComponents @($component) -RuntimeConfigName 'AdmissionPreparationProbe.runtimeconfig.json'
$sha=Write-ReleasePayload -AppPath $AppPath -Payload $payload
Write-Output "Test payload sealed: $sha"
