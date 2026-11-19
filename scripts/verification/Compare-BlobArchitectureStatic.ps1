param([string]$RepoRoot='D:\MyWork\SunEyeVision_V2')
$vmPath=Join-Path $RepoRoot 'docs\evidence\original-release-units\BlobAnalysis\IMVSBlobFindModuAlgorithmTab.xml'
$selfPath=Join-Path $RepoRoot 'tools\SunEyeVision.Tool.BlobAnalysis\Resources\BlobParameters.xml'
$mappingPath=Join-Path $RepoRoot 'docs\evidence\original-release-units\BlobAnalysis\BlobGroupLinkMapping.json'
[xml]$vm=[System.IO.File]::ReadAllText($vmPath,[System.Text.Encoding]::UTF8)
[xml]$self=[System.IO.File]::ReadAllText($selfPath,[System.Text.Encoding]::UTF8)
$vmCategories=@()
foreach($tabNode in @($vm.AlgorithmTabRoot.Tabs.Tab)){ foreach($category in @($tabNode.Categorys.Category)){ $links=@(); foreach($link in @($category.Items.GroupLinkItem)){ if($link.Name){$links += [ordered]@{name=$link.Name; link=$link.LinkName} } }; $count=@($category.Items.ChildNodes | Where-Object NodeType -eq 'Element').Count; $vmCategories += [ordered]@{name=$category.Name; groupLinks=$links; itemCount=$count} } }
$selfGroups=@()
foreach($group in @($self.BlobParameters.Group)){ $ids=@($group.Parameter | ForEach-Object id); $selfGroups += [ordered]@{page=$group.page; section=$group.section; vmCategory=$group.vmCategory; parameterCount=$ids.Count; ids=$ids} }
$checks=[ordered]@{'self groups preserve original category identity'=(@($selfGroups|Where-Object{-not $_.vmCategory}).Count -eq 0);'self has one mapped group for each non-display VM category'=(@('Tab_ROI Area','Tab_Run Params','Tab_Check Result'|Where-Object{ @($selfGroups.vmCategory) -notcontains $_}).Count -eq 0)}
$mapping=if(Test-Path $mappingPath){[System.IO.File]::ReadAllText($mappingPath,[System.Text.Encoding]::UTF8)|ConvertFrom-Json}else{$null}
$mappedNames=@($mapping.links|ForEach-Object {$_.vm})
$vmLinkNames=@($vmCategories|ForEach-Object {$_.groupLinks}|ForEach-Object {$_.name}) + 'TextDisplayGroup' + 'BottomCommandGroup'
$unmapped=@($vmLinkNames|Where-Object {$mappedNames -notcontains $_})
$mappingConsumerPass=($null -ne $mapping -and @($mapping.links|Where-Object {[string]::IsNullOrWhiteSpace($_.consumer) -or [string]::IsNullOrWhiteSpace($_.self)}).Count -eq 0)
$out=[ordered]@{generatedAt=(Get-Date).ToString('o');authority='VisionMaster 4.4.0 Build20250516';checks=$checks;vmCategories=$vmCategories;selfGroups=$selfGroups;mappingConsumerPass=$mappingConsumerPass;unmappedVmGroupLinks=$unmapped;categoryPass=($checks.Values -notcontains $false);externalGroupLinksClosed=($mappingConsumerPass -and $unmapped.Count -eq 0);runtimeBehaviorUnconfirmed=@($mapping.links|Where-Object {$_.status -match 'unconfirmed'}|ForEach-Object {$_.vm});pass=(($checks.Values -notcontains $false) -and $mappingConsumerPass -and $unmapped.Count -eq 0)}
$path=Join-Path $RepoRoot '.testdata\blob-architecture-static-diff-20261119.json'
$out|ConvertTo-Json -Depth 10|Set-Content $path -Encoding UTF8
$out|ConvertTo-Json -Depth 10

