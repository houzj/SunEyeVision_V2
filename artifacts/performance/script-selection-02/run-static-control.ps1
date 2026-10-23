$ErrorActionPreference='Stop'
$base=Split-Path $PSScriptRoot -Parent
$old=Join-Path (Split-Path $base -Parent) 'independent-testing/SCRIPT-ENGINE-COMPARISON-01'
$snapshot=(Get-Content (Join-Path $old 'latest-inputs.txt') -Raw).Trim()
$matrix=Get-Content (Join-Path $old 'matrix.json') -Raw|ConvertFrom-Json
$run=Join-Path $PSScriptRoot 'targeted-static'
New-Item -ItemType Directory -Path $run -Force|Out-Null
$cases=@()
$prefix="using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using BenchCommon;
public static class Probe { public static void Run() {
"
foreach($v in @(@{id='typeof';body='Type x = typeof(BenchCommon.|SharedMath);'},@{id='namespace-new';body='object x = new BenchCommon.|SharedMath();'},@{id='static-member';body='int x = BenchCommon.SharedMath.|Twice(2);'},@{id='unqualified-member';body='int x = SharedMath.|Twice(2);'})){
 $marked=$prefix+$v.body+' } }';$offset=$marked.IndexOf('|');$source=$marked.Replace('|','');$f=Join-Path $run ($v.id+'.cs');[IO.File]::WriteAllText($f,$source);$cases+=@{id=$v.id;source=$f;offset=$offset;compile=$false}
}
foreach($m in $matrix.cases|Where-Object {$_.group -eq 'modern-boundary'}){$cases+=@{id=$m.id;source=$m.source;offset=$m.caretUtf16;compile=$true}}
$cases[2].compile=$true
$cases[3].compile=$true
$records=@()
foreach($case in $cases){foreach($side in @('a','b')){
 $job=Join-Path $run ($case.id+'-'+$side);New-Item -ItemType Directory -Path $job -Force|Out-Null
 $psi=[Diagnostics.ProcessStartInfo]::new();$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardError=$true;$psi.RedirectStandardOutput=$true
 if($side -eq 'a'){$psi.FileName='dotnet';$psi.ArgumentList.Add((Join-Path $old 'worker-a/bin/Release/net9.0/WorkerA.dll'))}else{$psi.FileName=Join-Path $old 'worker-b/WorkerBExplicitRefs.exe'}
 $output=Join-Path $job 'result.json'
 foreach($arg in @($snapshot,$case.source,[string]$case.offset,'invoke','2',$output,$(if($case.compile){'compile'}else{'no-compile'}))){$psi.ArgumentList.Add($arg)}
 $p=[Diagnostics.Process]::Start($psi);$o=$p.StandardOutput.ReadToEndAsync();$e=$p.StandardError.ReadToEndAsync();if(!$p.WaitForExit(45000)){$p.Kill($true);throw 'Own test timeout'}
 [IO.File]::WriteAllText((Join-Path $job 'stdout.log'),$o.GetAwaiter().GetResult());[IO.File]::WriteAllText((Join-Path $job 'stderr.log'),$e.GetAwaiter().GetResult())
 $d=Get-Content $output -Raw|ConvertFrom-Json
 $records+=@{case=$case.id;side=$side;source=$case.source;sha256=(Get-FileHash $case.source).Hash;success=$d.success;count=$d.queries[0].count;hasSharedMath=('SharedMath' -cin $d.queries[0].names);hasTwice=('Twice' -cin $d.queries[0].names);compile=$d.syntaxBoundaryCompile;raw=$output}
 $records|ConvertTo-Json -Depth 8|Set-Content (Join-Path $run 'summary.json')
 Write-Output ($case.id+' '+$side+' success='+$d.success)
}}
