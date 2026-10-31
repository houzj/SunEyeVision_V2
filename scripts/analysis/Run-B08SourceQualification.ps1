[CmdletBinding()]
param([switch]$Execute,[switch]$ExclusiveWindowConfirmed)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$preparation=Join-Path $workspace 'Release/_validation/B08SourceQualification-20261031-preparation'
$owned=Join-Path $workspace 'Release/_validation/B08SourceQualification-20261031-attempt01'
$baseline=Join-Path $workspace 'Release/_validation/B08OriginalBinding-20261030-attempt03'
$history='D:/Program Files/VisionMaster4.4.0/Applications/ModuleProxy/ImageSourceTempPath.txt'
$historyHash='ABC3F631D0E717C273FBA53FCDB40A3705684FEF58B42F868A195497400F7823'
$oldCache=[IO.Path]::GetFullPath((Join-Path $baseline 'ImageTemp/VM127868ImgSrcTemp'))
function Assert-NoReparse([string]$path) {
    $item=Get-Item -LiteralPath $path
    while($null -ne $item){
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('拒绝重解析路径：'+$item.FullName)}
        $item=if($item -is [IO.FileInfo]){$item.Directory}else{$item.Parent}
    }
}
function Assert-OwnedHistory {
    $metadataPath=Join-Path $workspace 'docs/collaboration/evidence/B08_PROBE_STATIC_METADATA_20261031.json'
    if((Get-FileHash -LiteralPath $metadataPath -Algorithm SHA256).Hash -ne $plan.metadataAuditSha256){throw '原程序集元数据审计身份变化'}
    $metadata=Get-Content -LiteralPath $metadataPath -Raw|ConvertFrom-Json
    foreach($artifact in $metadata.artifacts){
        Assert-NoReparse $artifact.path
        if((Get-FileHash -LiteralPath $artifact.path -Algorithm SHA256).Hash -ne $artifact.sha256){throw '原程序集身份与静态接口检查不符'}
    }
    Assert-NoReparse $history
    $proxy='D:/Program Files/VisionMaster4.4.0/Applications/ModuleProxy/x64/ModuleProxy.dll'
    Assert-NoReparse $proxy
    if((Get-FileHash -LiteralPath $proxy -Algorithm SHA256).Hash -ne '76F09A40184B2B5F99B7173EDDE23A716D339E3C64955696D6927495EFDF68A9'){throw '原ModuleProxy身份变化'}
    if((Get-FileHash -LiteralPath $history -Algorithm SHA256).Hash -ne $historyHash){throw '共享历史清单已变化，拒绝启动'}
    $lines=@([IO.File]::ReadAllLines($history)|Where-Object {-not [string]::IsNullOrWhiteSpace($_)})
    if($lines.Count -ne 1 -or [IO.Path]::GetFullPath($lines[0]) -ne $oldCache){throw '共享历史出现本次范围外的目标'}
    Assert-NoReparse $oldCache
    $preservation=Get-Content (Join-Path $workspace 'docs/collaboration/evidence/B08_OWNED_CACHE_PRESERVATION_20261031.json') -Raw|ConvertFrom-Json
    $files=@(Get-ChildItem -LiteralPath $oldCache -Force)
    if($files.Count -ne $preservation.rows.Count){throw '自有缓存成员已变化'}
    foreach($file in $files){
        if($file.PSIsContainer){throw '缓存出现未核子目录'}
        Assert-NoReparse $file.FullName
        $record=@($preservation.rows|Where-Object {$_.source -eq $file.FullName})
        if($record.Count -ne 1 -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $record[0].sha256 -or
            (Get-FileHash -LiteralPath $record[0].preserved -Algorithm SHA256).Hash -ne $record[0].sha256){throw '缓存或保全身份不符'}
    }
    $processes=@(Get-CimInstance Win32_Process)
    if($processes.ProcessId -contains 127868){throw '旧缓存PID已复用或仍存活，拒绝启动'}
    foreach($process in $processes){
        if($process.Name -match '^(VisionMaster|VisionMasterServerApp|B08Probe|B08SourceQualificationProbe|OriginalImageSourceProbe)\.exe$'){
            throw ('存在其他原厂客户端或探针：'+$process.Name)
        }
    }
}
if(!(Test-Path -LiteralPath $owned)){
    [IO.Directory]::CreateDirectory($owned)|Out-Null
    foreach($name in @('InitializedEntry.cs','B08BindingRuntimeProbe.cs')){Copy-Item -LiteralPath (Join-Path $preparation $name) -Destination (Join-Path $owned $name)}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'B08SourceQualificationCapture.cs') -Destination (Join-Path $owned 'B08SourceQualificationCapture.cs')
    Copy-Item -LiteralPath (Join-Path $baseline 'fixture.sol') -Destination (Join-Path $owned 'fixture.sol')
    Copy-Item -LiteralPath (Join-Path $baseline 'image.bmp') -Destination (Join-Path $owned 'image.bmp')
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:InitializedEntry /r:System.Core.dll /r:System.Web.Extensions.dll "/out:$(Join-Path $owned 'B08SourceQualificationProbe.exe')" (Join-Path $owned 'InitializedEntry.cs') (Join-Path $owned 'B08BindingRuntimeProbe.cs') (Join-Path $owned 'B08SourceQualificationCapture.cs') *> (Join-Path $owned 'compile.log')
    if($LASTEXITCODE){throw '完整取证程序编译失败，保留日志'}
    Copy-Item -LiteralPath (Join-Path $baseline 'B08Probe.exe.config') -Destination (Join-Path $owned 'B08SourceQualificationProbe.exe.config')
    $artifacts=@(Get-ChildItem -LiteralPath $owned -File|Where-Object {$_.Name -ne 'compile.log'}|ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
    @{status='仅准备，未执行SDK';artifacts=$artifacts;historySha256=$historyHash;ownedCache=$oldCache;
        metadataAuditSha256=(Get-FileHash (Join-Path $workspace 'docs/collaboration/evidence/B08_PROBE_STATIC_METADATA_20261031.json') -Algorithm SHA256).Hash;
        concurrency='文件前检不是原子隔离；执行需要人工确认原版VM独占窗口，不改共享清单'}|ConvertTo-Json -Depth 7|Set-Content (Join-Path $owned 'plan.json') -Encoding utf8
}
$plan=Get-Content (Join-Path $owned 'plan.json') -Raw|ConvertFrom-Json
foreach($artifact in $plan.artifacts){if((Get-FileHash -LiteralPath (Join-Path $owned $artifact.name) -Algorithm SHA256).Hash -ne $artifact.sha256){throw '已准备程序或输入身份变化'}}
if(!$Execute){Get-Content (Join-Path $owned 'plan.json');return}
if(!$ExclusiveWindowConfirmed){throw '尚未确认原版VM独占测试窗口；不启动SDK'}
if(Test-Path -LiteralPath (Join-Path $owned 'receipt.json')){throw '已有运行回执，拒绝重复或覆盖'}
Assert-OwnedHistory
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName=Join-Path $owned 'B08SourceQualificationProbe.exe';$start.WorkingDirectory=$owned
$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$start.Environment['B08_REVIEWED_RUN_WINDOW']='owned-history-window-reviewed'
foreach($value in @('D:/Program Files/VisionMaster4.4.0/Applications',$owned,(Join-Path $owned 'fixture.sol'),'@create','Flow1.Image Source1','B08Var',(Join-Path $owned 'image.bmp'))){$start.ArgumentList.Add($value)}
$process=[Diagnostics.Process]::new();$process.StartInfo=$start
Assert-OwnedHistory
if(!$process.Start()){throw '自有取证进程未启动'}
Write-Output ('自有原厂取证进程：'+$process.Id)
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
$timer=[Diagnostics.Stopwatch]::StartNew();$finished=$false
while($timer.ElapsedMilliseconds -lt 180000){if($process.WaitForExit(1000)){$finished=$true;break}}
if(!$finished){$process.Kill();$process.WaitForExit()}
[IO.File]::WriteAllText((Join-Path $owned 'stdout.log'),$stdout.GetAwaiter().GetResult())
[IO.File]::WriteAllText((Join-Path $owned 'stderr.log'),$stderr.GetAwaiter().GetResult())
@{pid=$process.Id;exit=$process.ExitCode;finished=$finished;elapsedMs=$timer.ElapsedMilliseconds;scope='原SDK单次窗口取证，不是产品验收';atomicIsolation=$false}|ConvertTo-Json|Set-Content (Join-Path $owned 'receipt.json') -Encoding utf8
Get-Content (Join-Path $owned 'receipt.json')
exit $process.ExitCode
