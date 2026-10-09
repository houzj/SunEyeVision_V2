Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Release.Common.ps1"
$passed = 0
function Assert($Condition, $Message) { if (!$Condition) { throw $Message }; $script:passed++ }
function Assert-Rejected([scriptblock]$Action) {
    $rejected=$false
    try { & $Action | Out-Null } catch { $rejected=$true }
    Assert $rejected 'Expected validation failure.'
}
foreach ($channel in 'alpha','beta','rc','stable','local') {
    $source = [pscustomobject]@{version='2.0.0';channel=$channel;fileRevision=42;releaseDate='2026-10-08';schemaVersion=2}
    $result = Get-ReleaseIdentity $source
    Assert ($result.FileVersion -eq '2.0.0.42') 'Incorrect Windows file version.'
    $expected=if($channel -eq 'stable'){'2.0.0.42'}else{"2.0.0.42_$channel"}
    Assert ($result.ArtifactVersion -eq $expected) 'Incorrect installer version label.'
    Assert ($result.ReleaseVersion -eq $result.FileVersion) 'Product/file versions must agree.'
    $next=Get-ReleaseIdentity $source -Next
    Assert ($next.ReleaseVersion -eq '2.0.0.43') 'New release must advance fourth component.'
    Assert ($source.fileRevision -eq 42) 'Version reservation must not mutate counter before success.'
    Assert ((Get-ReleaseIdentity $source).ReleaseVersion -eq $result.ReleaseVersion) 'Repeated packaging changes version.'
}
foreach ($channel in 'alpha','beta','rc','stable') {
    $artifact=if($channel -eq 'stable'){'2.0.0.42'}else{"2.0.0.42_$channel"}
    $branding=Get-InstallerBranding $channel $artifact
    Assert ($branding.DisplayName -eq "SunEyeVision $artifact") 'Installed name must include complete release version.'
    Assert ($branding.DefaultDir -eq 'SunEyeVision') 'Normal release must use the canonical directory.'
    Assert ($branding.AppId -eq (Get-InstallerBranding 'alpha' '2.0.0.42_alpha').AppId) 'Normal channels must share upgrade identity.'
    Assert ($branding.AssociationId -eq 'SunEyeVision.SolutionFile') 'Normal association must use canonical identity.'
}
$localBranding=Get-InstallerBranding 'local' '2.0.0.42_local'
Assert ($localBranding.AppId -ne (Get-InstallerBranding 'alpha' '2.0.0.42_alpha').AppId) 'Local installer must remain isolated.'
Assert ($localBranding.DefaultDir -ne 'SunEyeVision') 'Local installer must not overwrite normal release.'
Assert-Rejected {Get-InstallerBranding 'alpha' '2.0.0.42_beta'}
Assert-Rejected {Get-InstallerBranding 'stable' '2.0.0.42_alpha'}
Assert ((Get-InstallerBranding 'alpha' '2.0.0.43_alpha').AppId -eq (Get-InstallerBranding 'alpha' '2.0.0.42_alpha').AppId) 'Revision must not change installation identity.'
$installerSource=Get-Content "$PSScriptRoot/SunEyeVision.iss" -Raw
Assert ($installerSource -match '(?m)^UsePreviousAppDir=no\r?$') 'Installer must not reuse old Preview directory.'
Assert ($installerSource -match '(?m)^UsePreviousGroup=no\r?$') 'Installer must not reuse old Preview group.'
Assert ($installerSource -match '(?m)^AppMutex=Global\\SunEyeVision\.Running\r?$') 'Installer must check runtime marker.'
Assert ($installerSource -match 'function InitializeUninstall: Boolean;') 'Uninstall must have an initialization guard.'
Assert ($installerSource -match 'Result := RequireApplicationStopped;') 'Uninstall must verify stopped processes before mutation.'
Assert ($installerSource -match 'CurUninstallStep = usAppMutexCheck') 'Uninstall must recheck before removal begins.'
Assert ($installerSource -match 'procedure DeinitializeUninstall;') 'Uninstall must release its maintenance gate.'
Assert ($installerSource -match 'MB_RETRYCANCEL, IDCANCEL') 'Suppressed/cancelled prompts must fail closed.'
Assert ($installerSource -match 'if UninstallSilent then') 'Silent uninstall must abort rather than wait or force close.'
foreach ($bad in @(
    @{version='02.0.0'}, @{channel='unknown'}, @{fileRevision=65535}, @{fileRevision=-1},
    @{schemaVersion='2'}, @{fileRevision='1'}, @{releaseDate='2026-02-30'})) {
    $source = [pscustomobject]@{version='2.0.0';channel='alpha';fileRevision=42;releaseDate='2026-10-08';schemaVersion=2}
    foreach($key in $bad.Keys){$source.$key=$bad[$key]}
    Assert-Rejected {Get-ReleaseIdentity $source}
}
Assert-Rejected {Get-ReleaseIdentity ([pscustomobject]@{version='2.0.0';channel='alpha';fileRevision=65534;releaseDate='2026-10-08';schemaVersion=2}) -Next}
Assert-Rejected {Invoke-ReleaseNative pwsh @('-NoProfile','-Command','exit 7')}
Assert-Rejected {Assert-ReleasePayload $PSScriptRoot '2.0.0.42' @('MissingPlugin')}
Assert-ReleaseFileVersionAdvance '2.0.0.43' '2.0.0.42'
Assert $true 'Increasing file revision rejected.'
Assert-Rejected {Assert-ReleaseFileVersionAdvance '2.0.0.42' '2.0.0.42'}
Assert-Rejected {Assert-ReleaseFileVersionAdvance '2.0.0.41' '2.0.0.42'}
$padded = [pscustomobject]@{FileVersion='2.0.0.42    ';FileMajorPart=2;FileMinorPart=0;FileBuildPart=0;FilePrivatePart=42}
Assert ((Get-ReleaseBinaryFileVersion $padded) -eq '2.0.0.42') 'Padded Inno version should use binary fields.'
$summary=Get-ReleaseTestSummary "$PSScriptRoot/tests/pass"
Assert ($summary.passed -eq 2 -and $summary.failed -eq 0) 'Incorrect test summary.'
Assert-Rejected {Get-ReleaseTestSummary "$PSScriptRoot/tests/fail"}
Assert-Rejected {Get-ReleaseTestSummary "$PSScriptRoot/tests/empty"}
$parseErrors=$null; $tokens=$null
foreach($file in Get-ChildItem $PSScriptRoot -Filter '*.ps1') {
    [Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$parseErrors) | Out-Null
    Assert ($parseErrors.Count -eq 0) "Parse errors: $($file.Name): $parseErrors"
}
Write-Host "$passed checks passed."
