[CmdletBinding()]
param(
    [string]$Manifest = '',
    [string]$ToolId
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../../')).Path
if ([string]::IsNullOrWhiteSpace($Manifest)) {
    $Manifest = Join-Path $repo 'docs/evidence/tool-batch-manifest-20261120.json'
}
if (-not (Test-Path -LiteralPath $Manifest)) { throw "Manifest not found: $Manifest" }

$parsed = Get-Content -Raw -LiteralPath $Manifest | ConvertFrom-Json
$rows = @($parsed | ForEach-Object { $_ })
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$seen = @{}
$validStatus = @('unconfirmed', 'equivalent', 'diverged')

function Resolve-RepoPath([string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative)) { return $null }
    if ([System.IO.Path]::IsPathRooted($relative)) { return $relative }
    return (Join-Path $repo $relative)
}

function Add-Failure([string]$message) { $script:failures.Add($message) }
function Add-Warning([string]$message) { $script:warnings.Add($message) }

foreach ($row in $rows) {
    $id = [string]$row.toolId
    if ([string]::IsNullOrWhiteSpace($id)) {
        Add-Failure 'entry without toolId'
        continue
    }
    if ($seen.ContainsKey($id)) { Add-Failure "duplicate toolId: $id" }
    $seen[$id] = $true

    $projects = @()
    foreach ($field in @('project', 'runtimeProject', 'wpfProject')) {
        $value = [string]$row.$field
        if (-not [string]::IsNullOrWhiteSpace($value)) { $projects += $value }
    }
    if ($projects.Count -eq 0) { Add-Failure "$id`: no project path" }
    foreach ($relative in ($projects | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath (Resolve-RepoPath $relative))) {
            Add-Failure "$id`: project path missing: $relative"
        }
    }

    $pages = @($row.pages)
    if ($pages.Count -eq 0) { Add-Failure "$id`: pages empty" }

    $evidence = @($row.evidence)
    if ($evidence.Count -eq 0) { Add-Warning "$id`: no evidence file registered" }
    foreach ($relative in $evidence) {
        if ([string]::IsNullOrWhiteSpace($relative)) { continue }
        if (-not (Test-Path -LiteralPath (Resolve-RepoPath $relative))) {
            Add-Failure "$id`: evidence path missing: $relative"
        }
    }

    foreach ($control in @($row.specialControls)) {
        if ($null -eq $control) { continue }
        if ([string]::IsNullOrWhiteSpace([string]$control.type)) { Add-Failure "$id`: specialControl without type" }
        if ([string]::IsNullOrWhiteSpace([string]$control.evidence)) { Add-Warning "$id`: specialControl $($control.type) has no evidence id" }
    }

    if ($null -eq $row.algorithm) {
        Add-Warning "$id`: no algorithm block, equivalence stays AlgorithmUnconfirmed"
    }
    else {
        $status = [string]$row.algorithm.equivalenceStatus
        $golden = @($row.algorithm.goldenCases | Where-Object { $_ })
        if ($validStatus -notcontains $status) {
            Add-Failure "$id`: invalid equivalenceStatus '$status'"
        }
        if ($status -eq 'equivalent' -and $golden.Count -eq 0) {
            Add-Failure "$id`: declares equivalent without goldenCases"
        }
        if ($status -eq 'unconfirmed' -and $golden.Count -gt 0) {
            Add-Warning "$id`: golden cases exist but status is still unconfirmed"
        }
    }

    if ($null -eq $row.unconfirmed) {
        Add-Warning "$id`: unconfirmed array missing (use empty array instead)"
    }
}

if (-not [string]::IsNullOrWhiteSpace($ToolId) -and -not $seen.ContainsKey($ToolId)) {
    Add-Failure "tool not found in manifest: $ToolId"
}

$result = [ordered]@{
    Manifest = $Manifest
    ToolCount = $rows.Count
    ToolId = if ([string]::IsNullOrWhiteSpace($ToolId)) { $null } else { $ToolId }
    Status = if ($failures.Count -eq 0) { 'passed' } else { 'failed' }
    Failures = @($failures)
    Warnings = @($warnings)
}
$result | ConvertTo-Json -Depth 6

if ($failures.Count -gt 0) { exit 1 }
exit 0
