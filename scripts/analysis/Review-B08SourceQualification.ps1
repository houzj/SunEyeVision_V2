$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$owned=Join-Path $workspace 'Release/_validation/B08SourceQualification-20261031-attempt01'
$receipt=Get-Content (Join-Path $owned 'receipt.json') -Raw|ConvertFrom-Json
if(!$receipt.finished -or $receipt.exit -ne 0){throw '原厂进程未正常结束'}
$rows=Get-Content (Join-Path $owned 'b08-observations.json') -Raw|ConvertFrom-Json
$initialization=Get-Content (Join-Path $owned 'init-observations.json') -Raw|ConvertFrom-Json
foreach($phase in @('create-return','destroy-return')){
    $row=@($initialization|Where-Object phase -eq $phase)
    if($row.Count -ne 1 -or $row[0].data.code -ne 0){throw ('原厂生命周期未正常返回：'+$phase)}
}
if(@($rows|Where-Object phase -eq 'error').Count -or @($rows|Where-Object phase -eq 'dispose-error').Count){throw '存在未处理的取证/方案退出错误'}
if(@($rows|Where-Object phase -eq 'owned-solution-disposed').Count -ne 1){throw '缺方案清理证据'}
function Raw-Leaves($nodes,[string]$parent=''){
    foreach($node in $nodes){
        $path=if($parent){$parent+'/'+$node.name}else{$node.name}
        if($node.nDataType -eq 2){Raw-Leaves $node.children $path}else{[pscustomobject]@{path=$path;type=$node.type;extra=$node.extra}}
    }
}
function Managed-Leaves($nodes){foreach($node in $nodes){if($node.leaf){$node}else{Managed-Leaves $node.children}}}
function Tree-Leaves($nodes){foreach($node in $nodes){if($null -ne $node.rootId){$node}else{Tree-Leaves $node.children}}}
$phaseNames=@('before-image-subscription','after-image-subscription-before-run','after-image-source-run','after-control-connection','after-first-run')
$summaries=@();$snapshots=@{}
foreach($phase in $phaseNames){
    $found=@($rows|Where-Object phase -eq $phase)
    if($found.Count -ne 1){throw ('五状态缺失或重复：'+$phase)}
    $snapshot=$found[0];$snapshots[$phase]=$snapshot
    $blob=@($snapshot.modules|Where-Object id -eq 2);$source=@($snapshot.modules|Where-Object id -eq 0)
    if($blob.Count -ne 1 -or $source.Count -ne 1){throw '实际模块身份不符'}
    $connected=$phase -in @('after-control-connection','after-first-run')
    $connection=@($snapshot.connections|Where-Object id -eq 2)
    if($connection.Count -ne 1 -or (($connection[0].previous -contains 0) -ne $connected) -or (($blob[0].previous -contains 0) -ne $connected)){throw ('SDK连接/托管前驱不符：'+$phase)}
    $treeSummary=@()
    foreach($tree in $snapshot.ordinaryNumericSourceTrees){
        $leaves=@(Tree-Leaves $tree.roots|Where-Object rootId -eq 0)
        $expected=if(!$connected){0}elseif($tree.type -eq 'int'){12}else{1}
        if($leaves.Count -ne $expected){throw ('原普通来源树叶数不符：'+$phase+'/'+$tree.type)}
        if($connected -and $tree.type -eq 'int' -and 'ModuStatus' -notin $leaves.name){throw 'CustomVisible=false的ModuStatus未进入实际手动来源树'}
        $treeSummary+=@{type=$tree.type;imageSourceLeafCount=$leaves.Count;imageSourceNames=@($leaves.name);rootLabels=@($tree.roots.label)}
    }
    $summaries+=@{phase=$phase;connections=$snapshot.connections;blobPredecessors=@($blob[0].previous);sourceTrees=$treeSummary}
}
$last=$snapshots['after-first-run'];$inventory=Get-Content (Join-Path $workspace 'docs/collaboration/evidence/B08_OUTPUT_QUALIFICATION_INVENTORY_20261031.json') -Raw|ConvertFrom-Json
$catalogues=@()
foreach($pair in @(@(0,'ImageSource',17),@(2,'Blob',75))){
    $module=@($last.modules|Where-Object id -eq $pair[0])[0]
    $raw=@(Raw-Leaves $module.raw.items);$managed=@(Managed-Leaves $module.managed)
    $xml=@($inventory.originalTools|Where-Object tool -eq $pair[1])[0]
    if($raw.Count -ne $pair[2] -or $managed.Count -ne $raw.Count+4){throw '原SDK叶数或托管时间戳补入数量不符'}
    $differences=@(Compare-Object ($raw|ForEach-Object {$_.path+'|'+$_.type}|Sort-Object) ($xml.leaves|ForEach-Object {$_.path+'|'+$_.type}|Sort-Object))
    if($differences.Count){throw '本实例SDK路径/类型与原XML不符；不能直接采用预期结论'}
    $excluded=@($managed|Where-Object {!$_.canSubscribe -or !$_.visible -or $_.isPrivate})
    $expectedExcluded=if($pair[1] -eq 'Blob'){14}else{4}
    if($excluded.Count -ne $expectedExcluded){throw '实际叶资格不符'}
    $catalogues+=@{tool=$pair[1];rawLeafCount=$raw.Count;managedLeafCount=$managed.Count;pathTypeMatchesXml=$true;
        excluded=@($excluded|Select-Object path,type,canSubscribe,visible,isPrivate,customVisible,resultVisible)}
}
$preserved=Get-Content (Join-Path $workspace 'docs/collaboration/evidence/B08_OWNED_CACHE_PRESERVATION_20261031.json') -Raw|ConvertFrom-Json
foreach($file in $preserved.rows){if((Get-FileHash -LiteralPath $file.preserved -Algorithm SHA256).Hash -ne $file.sha256){throw '原缓存保全副本变化'}}
$history='D:/Program Files/VisionMaster4.4.0/Applications/ModuleProxy/ImageSourceTempPath.txt'
$historyLines=@([IO.File]::ReadAllLines($history)|Where-Object {-not [string]::IsNullOrWhiteSpace($_)})
$expectedHistory=[IO.Path]::GetFullPath((Join-Path $owned ('ImageTemp/VM'+$receipt.pid+'ImgSrcTemp')))
if($historyLines.Count -ne 1 -or [IO.Path]::GetFullPath($historyLines[0]) -ne $expectedHistory){throw '退出后的共享清单出现本批之外目标，需单独调查'}
$files=@('receipt.json','b08-observations.json','init-observations.json','plan.json','B08SourceQualificationProbe.exe','B08SourceQualificationCapture.cs','B08BindingRuntimeProbe.cs','InitializedEntry.cs')
$audit=[ordered]@{status='本实例五状态原厂取证及生命周期核对通过；非全部VM语义或产品验收';pid=$receipt.pid;exit=$receipt.exit;finished=$receipt.finished;
    phases=$summaries;catalogues=$catalogues;cache=@{originalCacheDirectoryExists=$null;preservedFilesMatch=$true;historyAfter=$historyLines;historyAfterSha256=(Get-FileHash $history -Algorithm SHA256).Hash};
    artifacts=@($files|ForEach-Object {@{path=(Join-Path $owned $_);sha256=(Get-FileHash (Join-Path $owned $_) -Algorithm SHA256).Hash}});
    limitations=@('仅本构建/本方案/本地图像模式/这两个模块及观测时点；不是原生全域调度结论','原厂SDK返回结构在本实例与XML路径/类型相符，不推广动态修改/重建/失败','Blob自源不在本普通候选树；隐藏资格由实际VmIO标志与已核原谓词共同支持','变量弹窗展开、Group父输入、IMAGE专用组合、字符串触发寿命与全部算法仍未验','未运行产品或替代VS/人工验收')}
$audit.cache.originalCacheDirectoryExists=Test-Path (Split-Path $preserved.rows[0].source -Parent)
$audit|ConvertTo-Json -Depth 15|Set-Content (Join-Path $workspace 'docs/collaboration/evidence/B08_SOURCE_QUALIFICATION_RUNTIME_20261031.json') -Encoding utf8
'通过：五状态、原SDK与XML叶路径/类型、实际候选、生命周期和保全核对；不冒称整批完成'
