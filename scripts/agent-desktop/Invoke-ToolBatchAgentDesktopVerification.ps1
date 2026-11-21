[CmdletBinding()]
param(
    [string]$App = 'D:\MyWork\SunEyeVision_V2\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe',
    [string]$SourceSolution = 'C:\Users\Administrator\AppData\Local\SunEyeVision\Solutions\11111.solution',
    [string]$OutputRoot = 'D:\MyWork\SunEyeVision_V2\Release\_validation\AgentDesktopToolReview\batch-all',
    [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
$toolIds = @(
    'ImageLoad', 'BlobAnalysis',
    'PixelCount', 'FrameMean', 'IntensityMeasure',
    'ColorExtract', 'DivideImage',
    'ContourMatch', 'GrayMatch', 'MarkFind',
    'BlobLabels', 'If'
)

if (-not (Test-Path -LiteralPath $App)) { throw "Release 程序不存在: $App" }
if (-not (Test-Path -LiteralPath $SourceSolution)) { throw "源方案不存在: $SourceSolution" }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$solutionPath = Join-Path $OutputRoot 'batch-all.solution'
$document = Get-Content -Raw -LiteralPath $SourceSolution | ConvertFrom-Json
$selectedNames = [System.Collections.Generic.List[string]]::new()
$selectedToolIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($workflow in @($document.Workflows)) {
    $nodes = @($workflow.Nodes | Where-Object { $_.ToolType -in $toolIds })
    $ids = @{}
    foreach ($node in $nodes) { $ids[$node.Id] = $true }
    $workflow.Nodes = $nodes
    $workflow.Connections = @($workflow.Connections | Where-Object {
        $ids.ContainsKey($_.SourceNodeId) -and $ids.ContainsKey($_.TargetNodeId)
    })
    foreach ($node in $nodes) {
        if ($node.ToolType -in $toolIds -and $selectedToolIds.Add([string]$node.ToolType)) {
            $selectedNames.Add([string]$node.Name)
        }
    }
}
if ($selectedNames.Count -ne $toolIds.Count) {
    throw "源方案只找到 $($selectedNames.Count)/$($toolIds.Count) 个目标 ToolId: $($selectedToolIds -join ', ')"
}
$document.Name = 'batch-all'
$document.Description = '隔离工具批量验证方案，源自源方案副本，原始方案不修改'
$document | ConvertTo-Json -Depth 100 | Set-Content -Encoding UTF8 -LiteralPath $solutionPath

$audit = Join-Path $PSScriptRoot 'Invoke-SingleToolAudit.ps1'
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($nodeName in $selectedNames) {
    try {
        $run = & $audit -App $App -SolutionText 'batch-all' -NodeText $nodeName -SolutionPath $solutionPath -OutputRoot $OutputRoot -KeepOpen:$KeepOpen 2>&1 |
            Select-Object -Last 1
        $rows.Add([ordered]@{ Node = $nodeName; Status = 'passed'; Run = [string]$run })
    }
    catch {
        $rows.Add([ordered]@{ Node = $nodeName; Status = 'failed'; Error = $_.Exception.Message })
    }
}

$summary = [ordered]@{
    CreatedUtc = [DateTimeOffset]::Now.ToUniversalTime().ToString('o')
    SourceSolution = $SourceSolution
    IsolatedSolution = $solutionPath
    ToolIds = $toolIds
    Results = $rows
    Passed = @($rows | Where-Object Status -eq 'passed').Count
    Failed = @($rows | Where-Object Status -eq 'failed').Count
}
$summary | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputRoot 'batch-summary.json')
$summary | ConvertTo-Json -Depth 20
if ($summary.Failed -gt 0) { exit 1 }
