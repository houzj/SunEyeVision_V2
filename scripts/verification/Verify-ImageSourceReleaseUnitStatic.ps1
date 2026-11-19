param(
    [string]$RepoRoot = 'D:\MyWork\SunEyeVision_V2',
    [string]$VmRoot = 'C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Collection\ImageSourceModule'
)
$ErrorActionPreference = 'Stop'
$required = @(
    'ImageSourceModule.dll','ImageSourceModuleControl.dll','ImageSourceModuleCs.dll',
    'ImageSourceModule.xml','ImageSourceModuleAlgorithm.xml','ImageSourceModuleAlgorithmTab.xml',
    'ImageSourceModuleCs.xml','ImageSourceModuleDisplay.xml','ToolItemInfo.xml'
)
$checks = [ordered]@{}
foreach ($name in $required) { $checks["VM file $name"] = Test-Path -LiteralPath (Join-Path $VmRoot $name) }
$tab = [xml](Get-Content -LiteralPath (Join-Path $VmRoot 'ImageSourceModuleAlgorithmTab.xml') -Raw)
$items = @($tab.AlgorithmTabRoot.Tabs.Tab | Where-Object Name -eq 'Tab_Basic Params' | Select-Object -First 1 | ForEach-Object { $_.Categorys.Category.Items.ChildNodes | Where-Object LocalName -ne '#comment' | ForEach-Object Name })
$expected = @('ImageSourceType','CurrentImagePath','PixelFormat','SolSaveImageData','ShowImageName','CameraID','ExposureTimeInput','GainInput','InitialSN','PathCache','OutMono8','StitchEnable','StitchStartHeight','StitchHeight','ClearTrigger','ClearTriggerCondition','AutoPlay','AutoStop','TriggerStringEnable','TriggerString','TriggerFilter','IsSubscribeFolderMode','SubscribeFolderPath','RefreshImage','TriggerProcessRun')
$checks['VM basic tab has expected 25 items'] = ($items.Count -eq $expected.Count -and -not (Compare-Object $items $expected))
$display = [xml](Get-Content -LiteralPath (Join-Path $VmRoot 'ImageSourceModuleDisplay.xml') -Raw)
$displayNames = @($display.ParamRoot.Categorys.Category.Items.Object | ForEach-Object Name)
$checks['VM display declares output image objects'] = ($displayNames -contains 'OutputImage' -and $displayNames -contains 'OutputImageMono8')
$checks['VM display declares data records'] = (($displayNames | Where-Object { $_ -eq 'Data Record' }).Count -eq 2)
$selfRoot = Join-Path $RepoRoot 'tools\SunEyeVision.Tool.ImageSource'
$checks['self runtime project exists'] = Test-Path -LiteralPath (Join-Path $selfRoot 'SunEyeVision.Tool.ImageSource.csproj')
$checks['self WPF project exists'] = Test-Path -LiteralPath (Join-Path $RepoRoot 'tools\SunEyeVision.Tool.ImageSource.Wpf\SunEyeVision.Tool.ImageSource.Wpf.csproj')
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
$result = [ordered]@{ generatedAt=(Get-Date).ToString('o'); authority='VisionMaster 4.4.0 Build20250516'; checks=$checks; vmBasicItems=$items; selfStructure='Runtime + Wpf'; pass=($failed.Count -eq 0); failed=@($failed | ForEach-Object Key) }
$outDir = Join-Path $RepoRoot '.testdata'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outDir 'image-source-release-unit-static-gate-20261119.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 8
if ($failed.Count -gt 0) { exit 1 }
