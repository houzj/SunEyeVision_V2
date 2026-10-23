param([string]$RunName='formal-r1')
$ErrorActionPreference='Stop'
$runRoot=Join-Path $PSScriptRoot $RunName
$commands=Get-Content (Join-Path $runRoot 'commands.json') -Raw | ConvertFrom-Json
function Get-Stats($values) {
    $sorted=@($values | Sort-Object)
    if(-not $sorted.Count){return $null}
    [ordered]@{n=$sorted.Count;p50=$sorted[[Math]::Ceiling($sorted.Count*0.5)-1];p95=$sorted[[Math]::Ceiling($sorted.Count*0.95)-1];max=$sorted[-1]}
}
$groups=@()
foreach($variant in @('baseline','candidate')) {
foreach($lines in @(101,1001,5001)) {
    $files=@(Get-ChildItem -LiteralPath $runRoot -Filter "$variant-$lines-*.json")
    $data=@($files | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json})
    $valid=@($data | Where-Object {$_.success -eq $true -and @($_.samples).Count -eq 3})
    foreach($mode in @('first','reopen')) {
        $samples=@($valid | ForEach-Object {$_.samples} | Where-Object {if($mode -eq 'first'){$_.round -eq 0}else{$_.round -gt 0}})
        $input=Get-Stats @($samples | ForEach-Object {$_.inputMs})
        $hot=Get-Stats @($samples | ForEach-Object {$_.fiveExplicitPopupMs})
        $groups += [ordered]@{variant=$variant;lines=$lines;mode=$mode;validProcesses=$valid.Count;failedProcesses=@($commands | Where-Object {$_.variant -eq $variant -and $_.lines -eq $lines -and -not $_.success}).Count;
            visibleMs=(Get-Stats @($samples | ForEach-Object {$_.visibleMs}));inputMs=$input;
            inputBudget500Pass=($null -ne $input -and $input.p95 -le 500);
            dotPopupMs=(Get-Stats @($samples | ForEach-Object {$_.dotPopupMs}));hotPopupMs=$hot;
            hotBudget150Pass=($null -ne $hot -and $hot.p95 -le 150);
            semanticMs=(Get-Stats @($samples | ForEach-Object {$_.semanticMs}));
            heartbeatMaxGapMs=(Get-Stats @($samples | ForEach-Object {$_.heartbeatMaxGapMs}));
            compileDurationMs=(Get-Stats @($samples | ForEach-Object {$_.compilationSamples[0].durationMs}));
            compileHeartbeatMaxGapMs=(Get-Stats @($samples | ForEach-Object {$_.compilationSamples[0].heartbeatMaxGapMs}));
            savedInputCount=@($samples | Where-Object {$_.inputPreservedOnClose}).Count;
            validLengthCandidateCount=@($samples | Where-Object {$_.dotCandidates -contains 'Length'}).Count}
    }
}}
[ordered]@{run=$RunName;progress=(Get-Content (Join-Path $runRoot 'progress.json') -Raw | ConvertFrom-Json);
    quantile='nearest rank; no outlier exclusion';groups=$groups;
    limitations=@('WPF routed TextInput, not OS physical keyboard','baseline has no CtrlSpace binding; explicit query compatibility hook used and ctrlSpaceBound false',
    'baseline semantic readiness occurs before Show; candidate first50 readiness follows edits; semantic moments differ',
    'Standalone actual controls and DefaultDebugWindow; no product App/device/services/workflow',
    'Developer performance evidence; independent acceptance and VS designer/manual visual pending')
} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $runRoot 'summary.json') -Encoding utf8
