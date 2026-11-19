param(
    [string]$RepoRoot = 'D:\MyWork\SunEyeVision_V2',
    [string]$EvidenceRoot = 'D:\MyWork\SunEyeVision_V2\docs\evidence\original-release-units\BlobAnalysis'
)
$ErrorActionPreference = 'Stop'
$algorithmTab = Join-Path $EvidenceRoot 'IMVSBlobFindModuAlgorithmTab.xml'
$groups = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'docs') -Filter 'Group.xml' -File -Recurse | Select-Object -First 1).FullName
$selfParams = Join-Path $RepoRoot 'tools\SunEyeVision.Tool.BlobAnalysis\Resources\BlobParameters.xml'
$fieldsView = Join-Path $RepoRoot 'src\Plugin.Wpf\Fields\DeclaredFieldsView.cs'
foreach ($path in @($algorithmTab,$groups,$selfParams,$fieldsView)) { if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) { throw "missing evidence file: $path" } }
$utf8 = [System.Text.Encoding]::UTF8
$tabText = [System.IO.File]::ReadAllText($algorithmTab, $utf8)
$groupText = [System.IO.File]::ReadAllText($groups, $utf8)
$selfText = [System.IO.File]::ReadAllText($selfParams, $utf8)
$fieldsText = [System.IO.File]::ReadAllText($fieldsView, $utf8)
$checks = [ordered]@{}
$checks['VM basic first group is ImageSourceGroup'] = [bool]($tabText -match '(?s)Tab_Basic Params.*?<GroupLinkItem Name="ImageSourceGroup"')
$roiOrder = @('OutputMaskImageGroup','InputMaskImageGroup','RoiSelectGroup','ROISelecter Name="RoiType"','InheritWayGroup','ExternROITypeGroup','AssistGroup')
$positions = foreach ($item in $roiOrder) { [regex]::Match($tabText, [regex]::Escape($item)).Index }
$checks['VM ROI group order is preserved'] = (($positions | Where-Object { $_ -lt 0 }).Count -eq 0 -and (($positions[0] -lt $positions[1]) -and ($positions[1] -lt $positions[2]) -and ($positions[2] -lt $positions[3]) -and ($positions[3] -lt $positions[4]) -and ($positions[4] -lt $positions[5]) -and ($positions[5] -lt $positions[6])))
$checks['VM InputMask declares ROIAreaType'] = [bool]($groupText -match '(?s)InputMaskImageGroup.*?ROIAreaType')
$checks['VM InputMask declares MaskImage'] = [bool]($groupText -match '(?s)InputMaskImageGroup.*?MaskImage')
$checks['self input.image maps to basic/image section'] = [bool]($selfText -match 'id="input\.image"[^>]*page="[^"]+"[^>]*section="[^"]+"')
$checks['self input.pose maps to basic/ROI section'] = [bool]($selfText -match 'id="input\.pose"[^>]*page="[^"]+"[^>]*section="[^"]+"')
$checks['external field registration replays pending anchors'] = [bool]($fieldsText -match '_pendingPageActions\.Remove\(fieldId')
$checks['explicit missing-group index is honored'] = [bool]($fieldsText -match 'Math\.Clamp\(index\.Value,0,page\.Rows\.Count\)')
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
$result = [ordered]@{ generatedAt = (Get-Date).ToString('o'); authority = 'VisionMaster 4.4.0 Build20250516 static XML/decompiled evidence'; checks = $checks; pass = ($failed.Count -eq 0); failed = @($failed | ForEach-Object Key) }
$outDir = Join-Path $RepoRoot '.testdata'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outPath = Join-Path $outDir 'blob-basic-tab-static-gate-20261119.json'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPath -Encoding UTF8
$result | ConvertTo-Json -Depth 8
if ($failed.Count -gt 0) { exit 1 }
