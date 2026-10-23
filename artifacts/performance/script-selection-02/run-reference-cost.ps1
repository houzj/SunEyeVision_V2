$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$old=Join-Path $root 'artifacts/independent-testing/SCRIPT-ENGINE-COMPARISON-01'
$snapshot=(Get-Content (Join-Path $old 'latest-inputs.txt') -Raw).Trim()
$run=Join-Path $PSScriptRoot 'reference-cost';New-Item -ItemType Directory -Path $run -Force|Out-Null
$runtime='C:/Program Files/dotnet/shared/Microsoft.NETCore.App/9.0.20'
$release=Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$frozen=Join-Path $run 'frozen';New-Item -ItemType Directory -Path $frozen -Force|Out-Null
foreach($name in @('SunEyeVision.Plugin.SDK.dll','SunEyeVision.Tool.Kernels.dll','halcondotnetxl.dll','OpenCvSharp.dll','OpenCvSharp.Extensions.dll')){Copy-Item -LiteralPath (Join-Path $release $name) -Destination (Join-Path $frozen $name)}
$provider=Get-Content (Join-Path $root 'tools/SunEyeVision.Tool.Script/ScriptEngine/MetadataReferenceProvider.cs') -Raw
$block=[regex]::Match($provider,'CoreBclAssemblyNames\s*=\s*\[([\s\S]*?)\];').Groups[1].Value
$names=@([regex]::Matches(($block -replace '//[^\r\n]*',''),'"([^"]+)"')|ForEach-Object{$_.Groups[1].Value+'.dll'})
$default=@($names|ForEach-Object{Join-Path $runtime $_}|Where-Object{Test-Path $_})+@((Join-Path $frozen 'SunEyeVision.Plugin.SDK.dll'))
$minimal=@('System.Private.CoreLib.dll','System.Runtime.dll','mscorlib.dll','System.Collections.dll','System.Text.Encoding.dll')|ForEach-Object{Join-Path $runtime $_}
$profiles=@{minimal=@($minimal);default=@($default);business=@($default)+@('SunEyeVision.Tool.Kernels.dll','halcondotnetxl.dll','OpenCvSharp.dll','OpenCvSharp.Extensions.dll'|ForEach-Object{Join-Path $frozen $_})}
$source=Join-Path $run 'Probe.cs';$text='using System; using System.Text; public static class Probe { public static void Run(){ var text=new StringBuilder(); text.Append("x"); } }';[IO.File]::WriteAllText($source,$text);$offset=$text.IndexOf('text.Append')+5
$records=@()
foreach($trial in 0..2){foreach($profile in $(if($trial%2){@('business','default','minimal')}else{@('minimal','default','business')})){
 $refs=@($profiles[$profile]|Select-Object -Unique);$refFile=Join-Path $run ($profile+'.json');ConvertTo-Json -InputObject $refs|Set-Content $refFile
 $job=Join-Path $run ($profile+'-'+$trial);New-Item -ItemType Directory -Path $job -Force|Out-Null;$output=Join-Path $job 'result.json'
 & dotnet (Join-Path $PSScriptRoot 'reference-worker/bin/Release/net9.0/WorkerA.dll') $snapshot $source $offset invoke 5 $output compile $refFile | Out-Null
 $d=Get-Content $output -Raw|ConvertFrom-Json;$records+=@{profile=$profile;trial=$trial;count=$refs.Count;bytes=($refs|ForEach-Object{(Get-Item $_).Length}|Measure-Object -Sum).Sum;success=$d.success;validCandidate=('Append' -cin $d.queries[0].names);compile=$d.syntaxBoundaryCompile;ready=$d.readyToQueryMs;first=$d.queries[0].ms;repeat=@($d.queries|Select-Object -Skip 1|ForEach-Object{$_.ms});raw=$output}
 $records|ConvertTo-Json -Depth 7|Set-Content (Join-Path $run 'summary.json');Write-Output ($profile+' '+$trial+' success='+$d.success)
}}
