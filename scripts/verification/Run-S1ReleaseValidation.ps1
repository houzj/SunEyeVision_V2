[CmdletBinding()]
param([ValidateSet('Contracts','Owner','Fields','Native','Workflow','Snapshot','Dialog')][string[]]$Tracks=@('Contracts','Owner','Fields','Native','Workflow','Snapshot','Dialog'),[string]$SnapshotFilter='',[string]$CrossVolumeDataRoot='',[ValidatePattern('^[A-Za-z0-9_-]+$')][string]$ValidationName='S1-20261025')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$product=Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$validation=Join-Path $root ('Release/_validation/' + $ValidationName)
$sources=@{
 Contracts='artifacts/validation/blob-s1-contracts-next/Runner/Program.cs'
 Owner='artifacts/validation/blob-s1-owner/Host/Program.cs'
 Fields='artifacts/validation/blob-s1-field-schema/Host/Program.cs'
 Native='artifacts/validation/blob-s1-contracts-next/NativeCheck/Program.cs'
 Snapshot='artifacts/validation/blob-s1-snapshot/Host/Program.cs'
}
$bootstrap=@'
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
internal static class ReleaseBootstrap {
 internal static void Initialize() {
  var root=Environment.GetEnvironmentVariable("S1_PRODUCT_ROOT") ?? throw new Exception("Missing official product root");
  Environment.SetEnvironmentVariable("PATH",root+";"+Path.Combine(root,"runtimes","win-x64","native")+";"+Environment.GetEnvironmentVariable("PATH"));
  AssemblyLoadContext.Default.Resolving+=(_,name)=>{
   var candidates=new[]{Path.Combine(root,name.Name+".dll"),Path.Combine(root,"plugins",name.Name!,name.Name+".dll"),Path.Combine(root,"plugins","SunEyeVision.Tool.BlobAnalysis",name.Name+".dll")};
   foreach(var path in candidates)if(File.Exists(path))return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
   if(!name.Name!.StartsWith("SunEyeVision.") && Environment.GetEnvironmentVariable("S1_TEST_HELPERS") is string helper){var p=Path.Combine(helper,name.Name+".dll");if(File.Exists(p))return AssemblyLoadContext.Default.LoadFromAssemblyPath(p);}
   return null;
  };
  AppDomain.CurrentDomain.ProcessExit+=(_,__)=>{
   try {
   var loaded=AppDomain.CurrentDomain.GetAssemblies().Where(a=>!a.IsDynamic && a.GetName().Name!.StartsWith("SunEyeVision.") && !a.GetName().Name!.EndsWith(".Tests")).Select(a=>new{assembly=a.GetName().Name,path=a.Location,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray();
   File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"loaded-product.tsv"),loaded.Select(a=>a.assembly+"\t"+a.path+"\t"+a.sha256));
   if(loaded.Any(a=>!Path.GetFullPath(a.path).StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))Environment.ExitCode=2;
   } catch(Exception error) { Console.Error.WriteLine(error); Environment.ExitCode=2; }
  };
 }
}
'@
$safeEntry=@'
using System.Reflection;
internal static class SafeEntry {
 [STAThread] public static int Main(string[] args) {
  try {
   ReleaseBootstrap.Initialize();
   var assembly=typeof(SafeEntry).Assembly;
   var type=assembly.GetType("Cases")??assembly.GetType("Program")!;
   var method=type.GetMethod(type.Name=="Cases"?"Run":"Main",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!;
   var value=method.Invoke(null,method.GetParameters().Length==0?null:new object[]{args});
   return value is Task<int> task?task.GetAwaiter().GetResult():(int)value!;
  } catch(Exception error) {Console.Error.WriteLine(error.InnerException??error);return 1;}
 }
}
'@
$dialog=@'
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Environment.GetEnvironmentVariable("S1_TEST_HELPERS")!,"SunEyeVision.UI.Tests.dll"));
var type=assembly.GetType("SunEyeVision.UI.Tests.SolutionDialogOwnerTests",true)!;
var instance=Activator.CreateInstance(type)!;
var results=new List<object>();
foreach(var entry in new[]{("YesButton","Yes"),("NoButton","No"),("CancelButton","Cancel"),("error","error")}){
 try{
  if(entry.Item1=="error")type.GetMethod("Actual_transition_error_keeps_host_owner_and_reentrant_request_is_rejected")!.Invoke(instance,null);
  else {var method=type.GetMethod("Deferred_closing_keeps_real_owner_and_original_three_button_results")!;method.Invoke(instance,new[]{(object)entry.Item1,Enum.Parse(method.GetParameters()[1].ParameterType,entry.Item2)});}
  results.Add(new{test=entry.Item1,passed=true});
 }catch(Exception ex){results.Add(new{test=entry.Item1,passed=false,error=(ex.InnerException??ex).ToString()});}
}
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"report.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(results));
return results.All(x=>JsonSerializer.SerializeToElement(x).GetProperty("passed").GetBoolean())?0:1;
'@
$references=@(Get-ChildItem -LiteralPath $product -File -Filter 'SunEyeVision*.dll')
$references+=Get-Item (Join-Path $product 'halcondotnetxl.dll')
$references+=Get-Item (Join-Path $product 'System.Text.Json.dll')
$references+=Get-Item (Join-Path $product 'System.Text.Encodings.Web.dll')
$references+=Get-Item (Join-Path $product 'plugins/SunEyeVision.Tool.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.dll')
$refs=($references | ForEach-Object {'<Reference Include="'+[Security.SecurityElement]::Escape($_.BaseName)+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'}) -join "`n"
$receipts=[Collections.Generic.List[object]]::new()
$receiptPath=Join-Path $validation 'validation-receipt.json'
if(Test-Path -LiteralPath $receiptPath){foreach($prior in @(Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json)){$receipts.Add($prior)}}
foreach($track in $Tracks){
 $dir=Join-Path $validation $track;New-Item -ItemType Directory -Path $dir -Force | Out-Null
 $attempt=1;while(Test-Path -LiteralPath (Join-Path $dir ('attempt-{0:d3}' -f $attempt))){$attempt++}
 $logDir=Join-Path $dir ('attempt-{0:d3}' -f $attempt);New-Item -ItemType Directory -Path $logDir | Out-Null
 $program=if($track -eq 'Dialog'){$dialog}elseif($track -eq 'Workflow'){'return await WorkflowCases.Run();'}else{[IO.File]::ReadAllText((Join-Path $root $sources[$track]))}
 if($track -eq 'Snapshot' -and [string]::IsNullOrWhiteSpace($CrossVolumeDataRoot)){
  # Keep the historical cross-volume case explicit; do not silently run it on one drive.
  $program=$program.Replace('if (Filter.Length > 0 && !name.Contains(Filter, StringComparison.Ordinal)) return;','if (Filter.Length > 0 && !name.Contains(Filter, StringComparison.Ordinal)) return; if(name == "cross-volume-success-and-central-lock") { Console.WriteLine("NOT_RERUN cross-volume: all current test data stays in Release; historical evidence retained"); return; }')
 }
 if($track -notin @('Owner','Snapshot')){
  $usings=([regex]::Matches($program,'(?m)^using [\w.]+;\r?$') | ForEach-Object {$_.Value}) -join "`n"
  $body=[regex]::Replace($program,'(?m)^using [\w.]+;\r?$','')
  $program=$usings+"`ninternal static class Cases { public static async Task<int> Run(string[] args) {`n"+$body+"`n} }"
 }
 # dotnet --depsfile uses the production base directory; fixture IO must stay in validation.
 $program=$program.Replace('AppContext.BaseDirectory','Environment.GetEnvironmentVariable("S1_CASE_ROOT")!')
 [IO.File]::WriteAllText((Join-Path $dir 'Program.cs'),$program)
 [IO.File]::WriteAllText((Join-Path $dir 'ReleaseBootstrap.cs'),$bootstrap.Replace('AppContext.BaseDirectory','Environment.GetEnvironmentVariable("S1_CASE_ROOT")!'))
 [IO.File]::WriteAllText((Join-Path $dir 'SafeEntry.cs'),$safeEntry)
 if($track -eq 'Workflow'){Copy-Item -LiteralPath (Join-Path $root 'artifacts/validation/blob-s1-regression/Host/WorkflowCases.cs') -Destination (Join-Path $dir 'WorkflowCases.cs')}
 $project='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0-windows</TargetFramework><UseWPF>true</UseWPF><OutputType>Exe</OutputType><StartupObject>SafeEntry</StartupObject><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
 [IO.File]::WriteAllText((Join-Path $dir 'Check.csproj'),$project)
 [IO.File]::WriteAllText((Join-Path $dir 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
 dotnet build (Join-Path $dir 'Check.csproj') -c Release *> (Join-Path $logDir 'build.log')
 $buildExit=$LASTEXITCODE
 if($buildExit -ne 0){Get-Content (Join-Path $logDir 'build.log') -Tail 10;throw "$track 检查宿主编译失败，不计作产品用例结果"}
 $env:S1_PRODUCT_ROOT=$product;$env:S1_TEST_HELPERS=Join-Path $root 'tests/bin/Release';$env:S1_CASE_ROOT=Join-Path $dir 'out'
 $argsForHost=@()
 if($track -eq 'Snapshot'){
  $fixture=Join-Path $validation 'Owner/out/fixtures/current.solution'
  $doc=Get-Content -LiteralPath $fixture -Raw | ConvertFrom-Json
  foreach($node in $doc.Workflows[0].Nodes){if($node.Id -eq 'consumer'){$node.Id='filter-consumer'}}
  $fixture=Join-Path $dir 'baseline.solution';$doc | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $fixture -Encoding utf8
  $recovery=if($CrossVolumeDataRoot){[IO.Path]::GetFullPath($CrossVolumeDataRoot)}else{Join-Path $dir 'cross-volume-not-used'}
  $argsForHost=@($fixture,$SnapshotFilter,$recovery)
 }
 $launch=@{FilePath='dotnet';WorkingDirectory=$product;WindowStyle='Hidden';PassThru=$true;RedirectStandardOutput=(Join-Path $logDir 'run.log');RedirectStandardError=(Join-Path $logDir 'run-error.log')}
 $launch.ArgumentList=@('exec','--depsfile',(Join-Path $product 'SunEyeVision.UI.deps.json'),'--runtimeconfig',(Join-Path $product 'SunEyeVision.UI.runtimeconfig.json'),(Join-Path $dir 'out/Check.dll'))+@($argsForHost) | ForEach-Object {'"'+$_+'"'}
 $process=Start-Process @launch
 if(-not $process.WaitForExit(90000)){$process.Kill();throw "$track 自有检查进程超时，已停止该自有进程"}
 foreach($name in @('loaded-product.tsv','report.json')){if(Test-Path -LiteralPath (Join-Path $dir "out/$name")){Copy-Item -LiteralPath (Join-Path $dir "out/$name") -Destination (Join-Path $logDir $name)}}
 $receipts.Add([ordered]@{track=$track;attempt=$attempt;pid=$process.Id;buildExit=$buildExit;exit=$process.ExitCode;productRoot=$product;log=(Join-Path $logDir 'run.log');assemblyIdentity=(Join-Path $logDir 'loaded-product.tsv');scope='针对正式Release的自动检查，由当前实施者执行，不冒称独立测试'})
 $receipts | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $validation 'validation-receipt.json') -Encoding utf8
 "${track}：exit=$($process.ExitCode)";Get-Content (Join-Path $logDir 'run.log') -Tail 3
 if($process.ExitCode -ne 0){Get-Content (Join-Path $logDir 'run-error.log') -Tail 15;throw "$track 检查失败，保留原日志"}
}
