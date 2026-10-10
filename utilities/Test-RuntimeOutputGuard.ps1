[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $root ('artifacts/validation/runtime-output-guard/' + [guid]::NewGuid().ToString('N'))
$passed = 0
function Assert([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:passed++
}
foreach ($scenario in 'release', 'debug', 'self-contained', 'packaging', 'design-time', 'custom-output', 'non-ui') {
    $scenarioRoot = Join-Path $fixtureRoot $scenario
    $configuration = if ($scenario -eq 'debug') { 'Debug' } else { 'Release' }
    $relative = if ($scenario -eq 'debug') { 'dev/Debug' } elseif ($scenario -eq 'custom-output') { 'custom' } else { 'Release/SunEyeVision_v1.0.0/SunEyeVision' }
    $output = Join-Path $scenarioRoot $relative
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $runtimeNames = @('hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll')
    foreach ($name in $runtimeNames) { [IO.File]::WriteAllText((Join-Path $output $name), "fixture-$scenario-$name") }
    [IO.File]::WriteAllText((Join-Path $output 'user-config.json'), '{"keep":true}')
    $hashes = @{}
    foreach ($name in $runtimeNames) { $hashes[$name] = (Get-FileHash (Join-Path $output $name)).Hash }
    $project = if ($scenario -eq 'non-ui') { 'NonUiRuntimeGuard.proj' } else { 'SunEyeVision.UI.proj' }
    $arguments = @('msbuild', (Join-Path $root "tests/build/$project"), '-t:QuarantineStaleRuntimeHost',
        "-p:RuntimeGuardFixtureRoot=$scenarioRoot/", "-p:Configuration=$configuration", '-v:quiet')
    switch ($scenario) {
        'self-contained' { $arguments += '-p:SelfContained=true' }
        'packaging' { $arguments += "-p:PackagingRoot=$scenarioRoot/package" }
        'design-time' { $arguments += '-p:DesignTimeBuild=true' }
        'custom-output' { $arguments += "-p:OutDir=$output/" }
    }
    & dotnet @arguments
    Assert ($LASTEXITCODE -eq 0) "$scenario target failed"
    Assert ((Get-Content (Join-Path $output 'user-config.json') -Raw) -eq '{"keep":true}') "$scenario user file changed"
    $backups = @(Get-ChildItem (Join-Path $scenarioRoot 'artifacts/runtime-host-backups') -Filter '*.dll' -Recurse -ErrorAction SilentlyContinue)
    $shouldMove = $scenario -in 'release', 'debug'
    Assert ($backups.Count -eq $(if ($shouldMove) { 3 } else { 0 })) "$scenario backup count incorrect"
    foreach ($name in $runtimeNames) {
        Assert ((Test-Path (Join-Path $output $name)) -eq !$shouldMove) "$scenario unexpected runtime mutation: $name"
        if ($shouldMove) {
            $backup = $backups | Where-Object Name -eq $name
            Assert ((Get-FileHash $backup.FullName).Hash -eq $hashes[$name]) "$scenario backup content changed: $name"
        }
    }
    if ($shouldMove) {
        & dotnet @arguments
        Assert ($LASTEXITCODE -eq 0) "$scenario repeated target failed"
        Assert (@(Get-ChildItem (Join-Path $scenarioRoot 'artifacts/runtime-host-backups') -Filter '*.dll' -Recurse).Count -eq 3) "$scenario repeated target created extra backups"
    }
}
Write-Host "$passed runtime output checks passed. Evidence: $fixtureRoot"
