[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$product=[IO.Path]::GetFullPath((Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'))
$base=Join-Path $root 'Release/_validation/S3-20261025/ComponentDeployment'
$attempt=1;while(Test-Path -LiteralPath (Join-Path $base ('attempt-{0:d3}' -f $attempt))){$attempt++}
$run=Join-Path $base ('attempt-{0:d3}' -f $attempt)
$held=Join-Path $run 'held'
New-Item -ItemType Directory -Path $held -Force | Out-Null
$manifest=Join-Path $product 'release-payload.json'
$original=[IO.File]::ReadAllBytes($manifest)
$originalSha=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash
[IO.File]::WriteAllBytes((Join-Path $run 'original-release-payload.json'),$original)
$moves=[Collections.Generic.List[object]]::new()
$results=[Collections.Generic.List[object]]::new()
$baseline=[Text.Encoding]::UTF8.GetString($original)|ConvertFrom-Json -AsHashtable -Depth 100
$oldExpectedEditor=$env:S3_EXPECT_EDITOR
$oldGlobalLoader=$env:S3_USE_GLOBAL_LOADER
$oldExpectedRejection=$env:S3_EXPECT_REJECTION

# 此轨验证 Blob 的可选编辑器及故障隔离，不限制全产品组件数量。
function Get-BlobComponent($payload,[string]$role) {
    $matches=@(foreach($entry in $payload.entries) {
        if(-not $entry.ContainsKey('components')){continue}
        foreach($component in $entry.components) {
            if($component.toolId -eq 'BlobAnalysis' -and $component.role -eq $role) {
                [pscustomobject]@{Entry=$entry;Component=$component}
            }
        }
    })
    if($matches.Count -ne 1){throw ('BlobAnalysis 必须有唯一的 '+$role+' 组件，实际：'+$matches.Count)}
    return $matches[0]
}
function Move-IntoHold([string]$relative) {
    $source=[IO.Path]::GetFullPath((Join-Path $product $relative))
    $target=[IO.Path]::GetFullPath((Join-Path $held ([IO.Path]::GetFileName($relative))))
    if(-not $source.StartsWith($product+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
       -not $target.StartsWith($held+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '测试移动路径越界'}
    if(Test-Path -LiteralPath $target){throw '保留区已存在同名文件，拒绝覆盖'}
    $sha=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    Move-Item -LiteralPath $source -Destination $target
    $moves.Add([pscustomobject]@{source=$source;held=$target;sha256=$sha})
}
function Restore-Held {
    foreach($move in $moves) {
        if(Test-Path -LiteralPath $move.held) {
            if(Test-Path -LiteralPath $move.source){throw ('正式目录出现并发写入，保留原件不覆盖：'+$move.source)}
            Move-Item -LiteralPath $move.held -Destination $move.source
        }
        if((Get-FileHash -LiteralPath $move.source -Algorithm SHA256).Hash -ne $move.sha256){throw '还原文件身份不符'}
    }
}
function Invoke-Probe([string]$name,[bool]$reject,[string]$reason='') {
    $output=Join-Path $run ($name+'.json')
    $process=Start-Process -FilePath (Join-Path $product 'SunEyeVision.UI.exe') -ArgumentList @('--toolbox-probe',('"'+$output+'"')) -WorkingDirectory $product -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(45000)){$process.Kill();$process.WaitForExit();throw '自有发布探针超时，已停止该自有进程'}
    $data=Get-Content -LiteralPath $output -Raw|ConvertFrom-Json -AsHashtable -Depth 100
    if($reject) {
        if($process.ExitCode -eq 0 -or $data.status -ne 'Rejected' -or $data.error -notmatch $reason){throw ('应明确拒绝 '+$name+'，实际：'+($data|ConvertTo-Json -Compress))}
    } elseif($process.ExitCode -ne 0 -or @($data.toolboxTools|Where-Object {$_.id -notlike 'vm:*'}).Count -ne 22){throw ('正式工具准入失败：'+$name)}
    $results.Add([ordered]@{name=$name;pid=$process.Id;exit=$process.ExitCode;expectedRejection=$reject;passed=$true})
}
function Reject-Metadata([string]$name,[scriptblock]$change,[string]$reason) {
    $candidate=[Text.Encoding]::UTF8.GetString($original)|ConvertFrom-Json -AsHashtable -Depth 100
    & $change $candidate
    $candidate|ConvertTo-Json -Depth 100|Set-Content -LiteralPath $manifest -Encoding utf8
    try{Invoke-Probe $name $true $reason}
    finally{[IO.File]::WriteAllBytes($manifest,$original)}
}

try {
    if(Test-Path -LiteralPath (Join-Path $product 'plugins/SunEyeVision.Tool.BlobAnalysis')){throw '退出的SDK Blob目录仍在正式发布中'}
    if($baseline.expectedPluginAssemblies -contains 'SunEyeVision.Tool.BlobAnalysis'){throw '清单仍要求退出的SDK Blob'}
    $results.Add([ordered]@{name='retired-sdk-absent-in-clean-release';passed=$true})
    Invoke-Probe 'baseline-present' $false
    $env:S3_USE_GLOBAL_LOADER='1';$env:S3_EXPECT_REJECTION='0'
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track Components
    if($LASTEXITCODE -ne 0){throw '全局装载入口的正式工作流失败'}
    $results.Add([ordered]@{name='global-loader-real-workflow';passed=$true})
    $env:S3_USE_GLOBAL_LOADER='0'
    $editor=Get-BlobComponent $baseline 'Editor'
    $editorId=$editor.Component.componentId
    $optional=@($baseline.entries|Where-Object{$_.ContainsKey('optionalComponentId') -and $_.optionalComponentId -eq $editorId})
    if($optional.Count -eq 0){throw '没有可选编辑器文件组'}
    foreach($entry in $optional){Move-IntoHold $entry.path}
    Invoke-Probe 'without-editor-toolbox' $false
    $env:S3_EXPECT_EDITOR='0'
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track Components
    if($LASTEXITCODE -ne 0){throw '缺编辑器的正式节点执行失败'}
    $results.Add([ordered]@{name='without-editor-real-workflow';passed=$true;scope='正式发现/注册/参数创建/节点/WorkflowExecutionEngine/HALCON/声明输出'})
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S4 -Track FullChain
    if($LASTEXITCODE -ne 0){throw '缺少Wpf时生产结果表/高亮/保存重开整链失败'}
    $results.Add([ordered]@{name='without-editor-result-host-chain';passed=$true;scope='真实MainWindow选择通用结果表、HALCON事件刷新/高亮、失败清空、确认取消、保存重开'})
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track RuntimeWorkflow
    if($LASTEXITCODE -ne 0){throw '缺少Wpf时中立Runtime调度/引用/保存重开失败'}
    $results.Add([ordered]@{name='without-editor-runtime-workflow';passed=$true;scope='中立配置→实际调度器→HALCON→输入引用与同帧ROI→保存重开→取消与资源租约'})
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track RuntimeDebug
    if($LASTEXITCODE -ne 0){throw '缺少Wpf时Runtime已发布输入调试快照失败'}
    $results.Add([ordered]@{name='without-editor-runtime-debug';passed=$true;scope='已发布输入快照→真实HALCON，不重跑上游/不改来源身份；冻结设置、取消与资源排空'})
    if(Test-Path -LiteralPath (Join-Path $product 'plugins/SunEyeVision.Tool.BlobAnalysis')){throw '退出的SDK Blob发布目录仍存在'}
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track RuntimeCatalog
    if($LASTEXITCODE -ne 0){throw '无Wpf时Runtime目录/默认创建失败'}
    $results.Add([ordered]@{name='runtime-catalog-without-editor';passed=$true})
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S4 -Track RuntimeEditor
    if($LASTEXITCODE -ne 0){throw '无Wpf时真实Runtime参数窗口失败'}
    $results.Add([ordered]@{name='runtime-window-without-editor';passed=$true})
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track RuntimeImage
    if($LASTEXITCODE -ne 0){throw '无Wpf时Runtime与现行工具图像消费失败'}
    $results.Add([ordered]@{name='runtime-image-without-editor';passed=$true})
    Restore-Held

    $runtime=Get-BlobComponent $baseline 'Runtime'
    Move-IntoHold $runtime.Entry.path
    try{
        Invoke-Probe 'missing-runtime' $true 'BlobAnalysis.Runtime|runtime|Runtime'
        $env:S3_USE_GLOBAL_LOADER='1';$env:S3_EXPECT_REJECTION='1'
        & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track Components
        if($LASTEXITCODE -ne 0){throw '全局装载器没有拒绝缺少Runtime'}
        $results.Add([ordered]@{name='global-loader-missing-runtime-rejected';passed=$true})
    }finally{Restore-Held;$env:S3_USE_GLOBAL_LOADER='0';$env:S3_EXPECT_REJECTION='0'}
    Reject-Metadata 'duplicate-runtime' {
        param($p)
        $target=Get-BlobComponent $p 'Runtime'
        $copy=$target.Component.Clone();$copy.componentId+='-duplicate';$target.Entry.components+=@($copy)
    } 'Duplicate tool component role'
    Reject-Metadata 'wrong-contract-major' {
        param($p)
        $target=Get-BlobComponent $p 'Runtime'
        $target.Component.contractMajor=999
    } 'contract version'
    Reject-Metadata 'orphan-editor' {
        param($p)
        $target=Get-BlobComponent $p 'Runtime'
        $target.Entry.components=@($target.Entry.components|Where-Object { $_.componentId -ne $target.Component.componentId })
    } 'companion runtime'
    Reject-Metadata 'wrong-entry-type' {
        param($p)
        $target=Get-BlobComponent $p 'Editor'
        $target.Component.entryType='System.String'
    } 'PE declarations differ'
    $env:S3_EXPECT_EDITOR='1'
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track Components
    if($LASTEXITCODE -ne 0){throw '还原后正式组件消费失败'}
    Invoke-Probe 'restored-present' $false
}
finally {
    [IO.File]::WriteAllBytes($manifest,$original)
    Restore-Held
    $env:S3_EXPECT_EDITOR=$oldExpectedEditor
    $env:S3_USE_GLOBAL_LOADER=$oldGlobalLoader
    $env:S3_EXPECT_REJECTION=$oldExpectedRejection
    $moves|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'file-moves.json') -Encoding utf8
    $results|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'results.json') -Encoding utf8
    if((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash -ne $originalSha){throw '发布清单未按原字节还原'}
}
Write-Output ('S3正式部署检查通过：'+$results.Count+'项；临时移动的'+$moves.Count+'个文件与发布清单均已原身份还原。证据：'+$run)
