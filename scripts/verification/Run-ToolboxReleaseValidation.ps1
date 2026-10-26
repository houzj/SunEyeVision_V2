[CmdletBinding()]
param([switch]$Build)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$mainRoot=[IO.Path]::GetFullPath('D:/MyWork/SunEyeVision_V2')
if(![string]::Equals($root,$mainRoot,[StringComparison]::OrdinalIgnoreCase)){
    throw '本入口只在合并后的主仓正式构建窗口运行，不在独立worktree生成第二套Release。'
}
$product=Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$base=Join-Path $root 'Release/_validation/Toolbox-20261026'
New-Item -ItemType Directory -Path $base -Force|Out-Null
$attempt=1;while(Test-Path (Join-Path $base ('attempt-{0:d3}' -f $attempt))){$attempt++}
$run=Join-Path $base ('attempt-{0:d3}' -f $attempt);New-Item $run -ItemType Directory|Out-Null
Push-Location $root
try {
    if($Build){
        dotnet build SunEyeVision.slnx -c Release --no-restore *> (Join-Path $run 'build-solution.log')
        if($LASTEXITCODE -ne 0){throw '全量Release构建失败，保留日志。'}
    }
    $expectedCommit=(& git rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0){throw '无法确定源码身份。'}
    $info=Join-Path $run 'toolbox-probe.json'
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=Join-Path $product 'SunEyeVision.UI.exe';$start.WorkingDirectory=$product
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.ArgumentList.Add('--toolbox-probe');$start.ArgumentList.Add($info)
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(30000)){$process.Kill();$process.WaitForExit();throw '自有目录探针超时，已停止探针进程。'}
    if($process.ExitCode -ne 0){throw "目录探针失败：exit $($process.ExitCode)，见toolbox-probe.json"}
    $probe=Get-Content $info -Raw|ConvertFrom-Json
    if($probe.commit -cne $expectedCommit){throw '正式产物源码提交与当前主仓不一致，不能计作本候选通过。'}
    [xml]$index=Get-Content (Join-Path $product 'ToolGroups.xml') -Raw
    $expected=@($index.ToolGroups.Tool|Where-Object {$_.IsVisible -ne 'false'}|ForEach-Object {$_.Id}|Sort-Object)
    $actual=@($probe.toolboxTools|Where-Object {$_.id -notlike 'vm:*'}|ForEach-Object {$_.id}|Sort-Object)
    if(($expected -join "`n") -cne ($actual -join "`n")){throw '探针工具身份与部署目录索引不一致。'}
    if(@($probe.toolboxCategories).Count -ne @($index.ToolGroups.Group).Count){throw '分类数量与随包目录定义不一致。'}
    if(@($probe.toolboxTools|Where-Object {$_.id -like 'vm:*' -and $_.available}).Count -ne 0){throw '未实现的VM目录条目错误地处于可用状态。'}
    $receipt=[ordered]@{sourceCommit=$expectedCommit;productRoot=$product;toolCount=$actual.Count;categoryCount=@($probe.toolboxCategories).Count;loadedToolAssemblies=@($probe.loadedDirectoryToolAssemblies);probePid=$process.Id;exit=$process.ExitCode;payloadSha256=(Get-FileHash (Join-Path $product 'release-payload.json')).Hash;scope='正式完整目录与组件加载探针；不代表拖入/保存重开/鼠标/设计器/人工验收通过'}
    $receipt|ConvertTo-Json -Depth 5|Set-Content (Join-Path $run 'receipt.json') -Encoding utf8
    $receipt|ConvertTo-Json -Depth 5
}finally{Pop-Location}