[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ToolId,
    [string]$Manifest = 'D:\MyWork\SunEyeVision_V2\docs\evidence\tool-batch-manifest-20261120.json',
    [string]$OutputRoot = 'D:\MyWork\SunEyeVision_V2\Release\_validation\ToolProduction',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifestRows = @(Get-Content -Raw -LiteralPath $Manifest | ConvertFrom-Json)
$entry = $manifestRows | Where-Object ToolId -eq $ToolId | Select-Object -First 1
if ($null -eq $entry) { throw "生产清单没有工具: $ToolId" }

$run = Join-Path $OutputRoot ("$ToolId-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $run | Out-Null
$stages = [System.Collections.Generic.List[object]]::new()

function Stage([string]$Name, [scriptblock]$Action) {
    $started = [DateTimeOffset]::Now
    try {
        & $Action
        $stages.Add([ordered]@{ Name = $Name; Status = 'passed'; Started = $started.ToString('o'); Finished = [DateTimeOffset]::Now.ToString('o') })
    }
    catch {
        $stages.Add([ordered]@{ Name = $Name; Status = 'failed'; Started = $started.ToString('o'); Finished = [DateTimeOffset]::Now.ToString('o'); Error = $_.Exception.Message })
        throw
    }
}

Stage 'evidence-and-project-gate' {
    foreach ($relative in @($entry.runtimeProject, $entry.wpfProject, $entry.evidence)) {
        if (-not (Test-Path -LiteralPath (Join-Path $repo $relative))) { throw "清单路径不存在: $relative" }
    }
}

if (-not $SkipBuild) {
    Stage 'release-build' {
        & dotnet build (Join-Path $repo 'SunEyeVision.slnx') -c Release --no-restore -v:minimal 2>&1 |
            Tee-Object -FilePath (Join-Path $run 'release-build.log')
        if ($LASTEXITCODE -ne 0) { throw "Release 构建失败: $LASTEXITCODE" }
    }
}

Stage 'batch-static-and-runtime-gate' {
    & (Join-Path $repo 'scripts/verification/Invoke-ToolBatchReleaseVerification.ps1') -SkipBuild 2>&1 |
        Tee-Object -FilePath (Join-Path $run 'batch-verification.log')
    if ($LASTEXITCODE -ne 0) { throw "统一验证失败: $LASTEXITCODE" }
}

$report = [ordered]@{
    ToolId = $ToolId
    CreatedUtc = [DateTimeOffset]::Now.ToUniversalTime().ToString('o')
    Manifest = $Manifest
    Entry = $entry
    Stages = $stages
    Status = if (@($stages | Where-Object Status -eq 'failed').Count -eq 0) { 'passed' } else { 'failed' }
}
$report | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $run 'iteration-report.json')
$report | ConvertTo-Json -Depth 20
