param([Parameter(Mandatory=$true)][string]$CandidateRoot, [string]$RunName = 'formal-r1')
$ErrorActionPreference='Stop'
$repository=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$baselineRoot=(Resolve-Path (Join-Path $repository 'artifacts/validation/script-s1/delivery-a254710d/app')).Path
$candidate=(Resolve-Path -LiteralPath $CandidateRoot).Path
$probe=(Resolve-Path (Join-Path $PSScriptRoot 'bin/WindowProbe.dll')).Path
$runRoot=Join-Path $PSScriptRoot $RunName
if(Test-Path -LiteralPath $runRoot){throw 'Never overwrite a formal run.'}
New-Item -ItemType Directory -Path $runRoot | Out-Null
$commands=@(); $completed=0; $failures=0
foreach($lines in @(101,1001,5001)){
for($sampleIndex=0;$sampleIndex -lt 10;$sampleIndex++){
$variants=if($sampleIndex%2 -eq 0){@('baseline','candidate')}else{@('candidate','baseline')}
foreach($variant in $variants){
$application=if($variant -eq 'baseline'){$baselineRoot}else{$candidate}
$resultFile=Join-Path $runRoot "$variant-$lines-$sampleIndex.json"
$arguments=@(('"'+$probe+'"'),('"'+$application+'"'),('"'+$resultFile+'"'),$lines,'3')
$process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $arguments -WorkingDirectory $runRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$resultFile.stdout.log" -RedirectStandardError "$resultFile.stderr.log"
[ordered]@{pid=$process.Id;variant=$variant;lines=$lines;index=$sampleIndex;result=$resultFile} | ConvertTo-Json | Set-Content (Join-Path $runRoot 'live-process.json') -Encoding utf8
$finished=$process.WaitForExit(110000)
if(-not $finished){throw "Own probe PID $($process.Id) exceeded watchdog; recorded, no unrelated process touched."}
$process.Refresh()
$data=if(Test-Path -LiteralPath $resultFile){Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json}else{$null}
$ok=$process.ExitCode -eq 0 -and $data.success -eq $true -and @($data.samples).Count -eq 3
$completed++; if(-not $ok){$failures++}
$commands += [ordered]@{pid=$process.Id;variant=$variant;lines=$lines;index=$sampleIndex;arguments=$arguments;exitCode=$process.ExitCode;success=$ok;result=$resultFile}
$commands | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $runRoot 'commands.json') -Encoding utf8
[ordered]@{completedProcesses=$completed;plannedProcesses=60;failedProcesses=$failures;completeWindows=($completed-$failures)*3;lastResult=$resultFile;running=$true} | ConvertTo-Json | Set-Content (Join-Path $runRoot 'progress.json') -Encoding utf8
if(-not $ok){throw "Formal sample failed: $resultFile"}
}
}
}
[ordered]@{completedProcesses=$completed;plannedProcesses=60;failedProcesses=$failures;completeWindows=$completed*3;running=$false;probeSha256=(Get-FileHash -LiteralPath $probe).Hash} | ConvertTo-Json | Set-Content (Join-Path $runRoot 'progress.json') -Encoding utf8
Get-Content (Join-Path $runRoot 'progress.json')
