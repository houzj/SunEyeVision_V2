[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($root -ine 'D:\MyWork\SunEyeVision_V2'){throw '只验证主仓唯一正式Release，不创建产品副本'}
$product=Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$base=Join-Path $root 'Release/_validation/ProductIsolation-20261028'
$attempt=1;while(Test-Path -LiteralPath (Join-Path $base ('attempt-{0:d3}' -f $attempt))){$attempt++}
$run=Join-Path $base ('attempt-{0:d3}' -f $attempt)
$held=Join-Path $run 'held'
New-Item -ItemType Directory -Path $held -Force|Out-Null
. (Join-Path $root 'installer/Product.Components.ps1')
$components=@(Get-ProductToolComponents)
$manifest=Join-Path $product 'release-payload.json'
$original=[IO.File]::ReadAllBytes($manifest)
$originalSha=(Get-FileHash -LiteralPath $manifest).Hash
$baseline=[Text.Encoding]::UTF8.GetString($original)|ConvertFrom-Json -AsHashtable -Depth 100
if($baseline.payloadSchemaVersion -ne 2){throw '需要组件产品载荷schema2'}
if(Test-Path -LiteralPath (Join-Path $product 'plugins')){throw '旧SDK插件目录仍在产品中'}
$results=[Collections.Generic.List[object]]::new()
$moves=[Collections.Generic.List[object]]::new()

function Invoke-AdmissionProbe([string]$name,[bool]$reject,[string]$reason='', [int]$editorCount=2) {
    $output=Join-Path $run ($name+'.json')
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=Join-Path $product 'SunEyeVision.UI.exe';$start.WorkingDirectory=$product
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.ArgumentList.Add('--toolbox-probe');$start.ArgumentList.Add($output)
    $process=[Diagnostics.Process]::Start($start)
    try {
        if(-not $process.WaitForExit(30000)){$process.Kill();$process.WaitForExit();throw '自有准入探针超时'}
        $data=Get-Content -LiteralPath $output -Raw|ConvertFrom-Json -AsHashtable -Depth 100
        if($reject) {
            if($process.ExitCode -eq 0 -or $data.status -ne 'Rejected' -or $data.error -notmatch $reason){throw "未明确拒绝$name"}
        } else {
            if($process.ExitCode -ne 0){throw "组件准入失败：$name"}
            $expected=@($components|Where-Object role -eq 'Runtime'|ForEach-Object toolId|Sort-Object)
            $actual=@($data.toolboxTools|ForEach-Object id|Sort-Object)
            if(($expected -join '|') -cne ($actual -join '|') -or @($data.toolboxTools|Where-Object {-not $_.available}).Count){throw '实际工具箱与产品清单不一致'}
            if(@($data.toolComponents|Where-Object Role -eq 1).Count -ne $editorCount){throw '可选编辑器装载数不符'}
        }
        $results.Add([ordered]@{name=$name;pid=$process.Id;exit=$process.ExitCode;expectedRejection=$reject;passed=$true})
    } finally {$process.Dispose()}
}
function Hold-Component([string]$relative) {
    if([IO.Path]::GetFileName($relative) -cne $relative){throw '只允许移动已声明的宿主根组件文件'}
    $source=[IO.Path]::GetFullPath((Join-Path $product $relative))
    $target=[IO.Path]::GetFullPath((Join-Path $held $relative))
    if(-not $source.StartsWith($product+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not $target.StartsWith($held+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '验证移动路径越界'}
    if(Test-Path -LiteralPath $target){throw '保留区同名文件已存在'}
    $hash=(Get-FileHash -LiteralPath $source).Hash
    Move-Item -LiteralPath $source -Destination $target
    $moves.Add([ordered]@{source=$source;held=$target;sha256=$hash})
}
function Restore-Components {
    foreach($move in $moves) {
        if(Test-Path -LiteralPath $move.held) {
            if(Test-Path -LiteralPath $move.source){throw '正式目录有并发写入，拒绝覆盖'}
            Move-Item -LiteralPath $move.held -Destination $move.source
        }
        if((Get-FileHash -LiteralPath $move.source).Hash -cne $move.sha256){throw '组件还原身份不符'}
    }
}
function Reject-Payload([string]$name,[scriptblock]$change,[string]$reason) {
    $candidate=[Text.Encoding]::UTF8.GetString($original)|ConvertFrom-Json -AsHashtable -Depth 100
    & $change $candidate
    [IO.File]::WriteAllText($manifest,($candidate|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
    try {Invoke-AdmissionProbe $name $true $reason}
    finally {[IO.File]::WriteAllBytes($manifest,$original)}
}
try {
    Invoke-AdmissionProbe 'baseline-two-tools-four-components' $false
    foreach($editor in @($components|Where-Object role -eq 'Editor')){Hold-Component $editor.path}
    try {Invoke-AdmissionProbe 'both-editors-absent' $false '' 0} finally {Restore-Components}
    foreach($runtime in @($components|Where-Object role -eq 'Runtime')) {
        Hold-Component $runtime.path
        try {Invoke-AdmissionProbe ('required-runtime-absent-'+$runtime.toolId) $true ([regex]::Escape($runtime.path))}
        finally {Restore-Components}
    }
    Reject-Payload 'schema1-rejected' {param($p) $p.payloadSchemaVersion=1} 'Unsupported release payload schema'
    Reject-Payload 'unapproved-component-entry-rejected' {param($p) $p.productComponents[0].entryType='System.String'} 'Product component differs'
    Reject-Payload 'old-sdk-producer-field-rejected' {param($p) $p.entries[0].tools=@()} 'Retired SDK tool deployments'
    Invoke-AdmissionProbe 'restored-two-tools-four-components' $false
} finally {
    [IO.File]::WriteAllBytes($manifest,$original)
    Restore-Components
    $results|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'results.json') -Encoding utf8
    $moves|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'file-moves.json') -Encoding utf8
    if((Get-FileHash -LiteralPath $manifest).Hash -cne $originalSha){throw '原载荷清单未还原'}
    foreach($entry in $baseline.entries) {
        if((Get-FileHash -LiteralPath (Join-Path $product $entry.path)).Hash -ine $entry.sha256){throw "正式文件SHA已变化：$($entry.path)"}
    }
}
[ordered]@{passed=$results.Count;payloadSha256=$originalSha;productRoot=$product;evidence=$run;scope='仅产品组件准入和缺席/拒绝边界；不代替算法、编辑、图像树表、VS设计器和用户验收'}|ConvertTo-Json -Depth 4
