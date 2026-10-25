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
    } elseif($process.ExitCode -ne 0 -or @($data.toolboxTools).Count -ne 22){throw ('正式工具准入失败：'+$name)}
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
    Invoke-Probe 'baseline-present' $false
    $env:S3_USE_GLOBAL_LOADER='1';$env:S3_EXPECT_REJECTION='0'
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track Components
    if($LASTEXITCODE -ne 0){throw '全局装载入口的正式工作流失败'}
    $results.Add([ordered]@{name='global-loader-real-workflow';passed=$true})
    $env:S3_USE_GLOBAL_LOADER='0'
    $editor=@($baseline.entries|Where-Object{$_.ContainsKey('components') -and @($_.components|Where-Object role -eq 'Editor').Count -gt 0})
    if($editor.Count -ne 1){throw '必须有一个明确的编辑器组件'}
    $editorId=$editor[0].components[0].componentId
    $optional=@($baseline.entries|Where-Object{$_.ContainsKey('optionalComponentId') -and $_.optionalComponentId -eq $editorId})
    if($optional.Count -eq 0){throw '没有可选编辑器文件组'}
    foreach($entry in $optional){Move-IntoHold $entry.path}
    Invoke-Probe 'without-editor-toolbox' $false
    $env:S3_EXPECT_EDITOR='0'
    & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track Components
    if($LASTEXITCODE -ne 0){throw '缺编辑器的正式节点执行失败'}
    $results.Add([ordered]@{name='without-editor-real-workflow';passed=$true;scope='正式发现/注册/参数创建/节点/WorkflowExecutionEngine/HALCON/声明输出'})
    Restore-Held

    $runtime=@($baseline.entries|Where-Object{$_.ContainsKey('components') -and @($_.components|Where-Object role -eq 'Runtime').Count -gt 0})
    if($runtime.Count -ne 1){throw '必须有一个明确的Runtime组件'}
    Move-IntoHold $runtime[0].path
    try{
        Invoke-Probe 'missing-runtime' $true 'BlobAnalysis.Runtime|runtime|Runtime'
        $env:S3_USE_GLOBAL_LOADER='1';$env:S3_EXPECT_REJECTION='1'
        & (Join-Path $PSScriptRoot 'Run-S2ReleaseValidation.ps1') -Stage S3 -Track Components
        if($LASTEXITCODE -ne 0){throw '全局装载器没有拒绝缺少Runtime'}
        $results.Add([ordered]@{name='global-loader-missing-runtime-rejected';passed=$true})
    }finally{Restore-Held;$env:S3_USE_GLOBAL_LOADER='0';$env:S3_EXPECT_REJECTION='0'}
    Reject-Metadata 'duplicate-runtime' {
        param($p)
        $entry=$p.entries|Where-Object{$_.ContainsKey('components') -and @($_.components|Where-Object role -eq 'Runtime').Count -gt 0}|Select-Object -First 1
        $copy=$entry.components[0].Clone();$copy.componentId+='-duplicate';$entry.components+=@($copy)
    } 'Duplicate tool component role'
    Reject-Metadata 'wrong-contract-major' {
        param($p)
        $entry=$p.entries|Where-Object{$_.ContainsKey('components') -and @($_.components|Where-Object role -eq 'Runtime').Count -gt 0}|Select-Object -First 1
        $entry.components[0].contractMajor=999
    } 'contract version'
    Reject-Metadata 'orphan-editor' {
        param($p)
        $entry=$p.entries|Where-Object{$_.ContainsKey('components') -and @($_.components|Where-Object role -eq 'Runtime').Count -gt 0}|Select-Object -First 1
        $entry.components=@()
    } 'companion runtime'
    Reject-Metadata 'wrong-entry-type' {
        param($p)
        $entry=$p.entries|Where-Object{$_.ContainsKey('components') -and @($_.components|Where-Object role -eq 'Editor').Count -gt 0}|Select-Object -First 1
        $entry.components[0].entryType='System.String'
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
