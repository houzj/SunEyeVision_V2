param(
  [string]$RepoRoot = 'D:\MyWork\SunEyeVision_V2'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$unit = Join-Path $RepoRoot 'tools\SunEyeVision.Tool.ImageSource\Resources\OriginalReleaseUnit'
$tabPath = Join-Path $unit 'ImageSourceModuleAlgorithmTab.xml'
$algorithmPath = Join-Path $unit 'ImageSourceModuleAlgorithm.xml'
$mappingPath = Join-Path $unit 'ImageSourceFieldMapping.json'
$selfFieldsPath = Join-Path $RepoRoot 'tools\SunEyeVision.Tool.ImageSource\ImageSourceFields.cs'
$outPath = Join-Path $RepoRoot '.testdata\image-source-mapping-static-gate-20261119.json'

$tabXmlRaw = [System.IO.File]::ReadAllText($tabPath, [System.Text.Encoding]::UTF8)
$tabXmlRepairApplied = $false
if ($tabXmlRaw -match '<SelecetedTips>') {
  # The preserved VM XML contains one mismatched closing tag. Keep the source byte-identical
  # and repair only the in-memory parse view so the mapping gate can still inspect all items.
  $tabXmlRaw = $tabXmlRaw.Replace('<SelecetedTips>', '<BooleanTips>').Replace('</SelecetedTips>', '</BooleanTips>')
  $tabXmlRepairApplied = $true
}
[xml]$tab = $tabXmlRaw
[xml]$algorithm = [System.IO.File]::ReadAllText($algorithmPath, [System.Text.Encoding]::UTF8)
$mapping = [System.IO.File]::ReadAllText($mappingPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json

$items = @($tab.AlgorithmTabRoot.Tabs.Tab.Categorys.Category | ForEach-Object {
  $category = $_
  @($category.Items.ChildNodes | Where-Object { $_.NodeType -eq 'Element' } | ForEach-Object {
    [pscustomobject]@{ vm = $_.Name; category = $category.Name }
  })
})
$mapByVm = @{}
foreach ($entry in @($mapping.items)) { $mapByVm[$entry.vm] = $entry }
$selfText = [System.IO.File]::ReadAllText($selfFieldsPath, [System.Text.Encoding]::UTF8)
$expectedCategories = [ordered]@{
  'Tab_Basic Params' = @('ImageSourceType','CurrentImagePath','PixelFormat','SolSaveImageData','ShowImageName','CameraID','ExposureTimeInput','GainInput','InitialSN','PathCache','OutMono8','StitchEnable','StitchStartHeight','StitchHeight','ClearTrigger','ClearTriggerCondition')
  'Tab_Trigger' = @('AutoPlay','AutoStop','TriggerStringEnable','TriggerString','TriggerFilter')
  'Tab_Subscribe_File' = @('IsSubscribeFolderMode','SubscribeFolderPath','RefreshImage','TriggerProcessRun')
}
$checks = [ordered]@{}
$checks['tab item count is 25'] = ($items.Count -eq 25)
$checks['mapping item count is 25'] = (@($mapping.items).Count -eq 25)
$checks['tab order equals mapping order'] = ((@($items.vm) -join '|') -eq (@($mapping.items.vm) -join '|'))
$checks['category order equals original category contract'] = ((@($items.category | Select-Object -Unique) -join '|') -eq 'Tab_Basic Params|Tab_Trigger|Tab_Subscribe_File')
$checks['self field declarations contain every mapped self id'] = (@($mapping.items | Where-Object { $_.self -and $selfText.IndexOf(('"' + $_.self + '"'), [StringComparison]::Ordinal) -lt 0 }).Count -eq 0)
$categoryChecks = [ordered]@{}
foreach ($category in $expectedCategories.Keys) {
  $actual = @($items | Where-Object category -eq $category | Select-Object -ExpandProperty vm)
  $categoryChecks[$category] = [ordered]@{
    expected = $expectedCategories[$category]
    actual = $actual
    exact = (($actual -join '|') -eq ($expectedCategories[$category] -join '|'))
  }
}
$algorithmNames = @($algorithm.AlgorithmParamList.ParamItem | ForEach-Object Name)
$algorithmOnly = @($algorithmNames | Where-Object { $_ -and (@($items.vm) -notcontains $_) })
$result = [ordered]@{
  generatedAt = (Get-Date).ToString('o')
  authority = 'VisionMaster 4.4.0 Build20250516'
  checks = $checks
  categories = $categoryChecks
  algorithmOnlyParameters = $algorithmOnly
  mappingStatus = @($mapping.items | Group-Object status | ForEach-Object { [ordered]@{ status = $_.Name; count = $_.Count } })
  sourceXmlRepairApplied = $tabXmlRepairApplied
  pass = (($checks.Values -notcontains $false) -and (@($categoryChecks.Values | Where-Object { -not $_.exact }).Count -eq 0))
}
New-Item -ItemType Directory -Force (Split-Path $outPath) | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 $outPath
$result | ConvertTo-Json -Depth 8

