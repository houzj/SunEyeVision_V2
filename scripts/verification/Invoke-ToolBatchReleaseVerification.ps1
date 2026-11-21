[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$solution = Join-Path $repo 'SunEyeVision.slnx'
$releaseRoot = Join-Path $repo 'Release'
$probeRoot = Join-Path $releaseRoot '_validation/ToolBatchVerification'
$logRoot = Join-Path $probeRoot 'logs'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    $log = Join-Path $logRoot ($Name + '.log')
    Write-Host "== $Name =="
    & $Action 2>&1 | Tee-Object -FilePath $log
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE. See $log"
    }
}

if (-not $SkipBuild) {
    Invoke-Step 'release-build' {
        dotnet build $solution -c $Configuration --no-restore
    }
}

$productRoot = Join-Path $releaseRoot 'SunEyeVision_v1.0.0/SunEyeVision'
if (-not (Test-Path $productRoot)) {
    throw "Release payload was not found at $productRoot"
}

Invoke-Step 'control-naming-probe' {
    dotnet run --project (Join-Path $repo 'tests/ControlNamingProbe/ControlNamingProbe.csproj') -c $Configuration --no-build
}
Invoke-Step 'logic-data-tests' {
    dotnet test (Join-Path $repo 'tests/SunEyeVision.Tools.LogicData.Tests/SunEyeVision.Tools.LogicData.Tests.csproj') -c $Configuration --no-build --nologo
}
Invoke-Step 'image-processing-tests' {
    dotnet test (Join-Path $repo 'tests/SunEyeVision.Tools.ImageProcessing.Tests/SunEyeVision.Tools.ImageProcessing.Tests.csproj') -c $Configuration --no-build --nologo
}
Invoke-Step 'inspection-tests' {
    dotnet test (Join-Path $repo 'tests/SunEyeVision.Tools.Inspection.Tests/SunEyeVision.Tools.Inspection.Tests.csproj') -c $Configuration --no-build --nologo
}

$catalog = Join-Path $repo 'src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json'
$componentProps = Join-Path $repo 'product/ToolComponents.props'
$manifestPath = Join-Path $repo 'docs/evidence/tool-batch-manifest-20261120.json'
Invoke-Step 'product-directory-gate' {
    $required = 'ImageLoad','BlobAnalysis','PixelCount','FrameMean','IntensityMeasure','ColorExtract','DivideImage','ContourMatch','GrayMatch','MarkFind','BlobLabels','If'
    $catalogText = Get-Content -Raw $catalog
    $propsText = Get-Content -Raw $componentProps
    $manifest = @(Get-Content -Raw $manifestPath | ConvertFrom-Json)
    if ($manifest.Count -ne $required.Count) { throw "Batch manifest must contain $($required.Count) tools, found $($manifest.Count)" }
    foreach ($toolId in $required) {
        if ($catalogText -notmatch ('"ToolId"\s*:\s*"' + [regex]::Escape($toolId) + '"')) { throw "Toolbox mapping missing: $toolId" }
        if ($propsText -notmatch ('<ToolId>' + [regex]::Escape($toolId) + '</ToolId>')) { throw "Product component missing: $toolId" }
        $entry = $manifest | Where-Object ToolId -eq $toolId
        if ($null -eq $entry) { throw "Batch manifest missing: $toolId" }
        foreach ($relative in @($entry.runtimeProject, $entry.wpfProject, $entry.evidence)) {
            if (-not (Test-Path (Join-Path $repo $relative))) { throw "Manifest path missing for ${toolId}: $relative" }
        }
    }
    $componentCount = ([regex]::Matches($propsText, '<ProductToolComponent\s')).Count
    if ($componentCount -ne 24) { throw "Expected 24 product components, found $componentCount" }
    Write-Host "PASS: 12 baseline and batch tools, 24 product components"
}

$result = [ordered]@{
    Timestamp = [DateTimeOffset]::Now.ToString('o')
    Configuration = $Configuration
    ProductRoot = $productRoot
    Batches = @(
        [ordered]@{ Name = 'Baseline'; Tools = @('ImageLoad','BlobAnalysis') },
        [ordered]@{ Name = 'Batch1'; Tools = @('PixelCount','FrameMean','IntensityMeasure') },
        [ordered]@{ Name = 'Batch2'; Tools = @('ColorExtract','DivideImage') },
        [ordered]@{ Name = 'Batch3'; Tools = @('ContourMatch','GrayMatch','MarkFind') },
        [ordered]@{ Name = 'Batch4'; Tools = @('BlobLabels','If') }
    )
    Status = 'passed'
}
$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $probeRoot 'summary.json') -Encoding UTF8
Write-Host "PASS: batch verification summary written to $(Join-Path $probeRoot 'summary.json')"
