[CmdletBinding()]
param([ValidateSet('DocumentEditing','Related','RuntimeKernel','Components','Consumption','InputBoundaries','FieldEditing','BindingEditing')][string]$Track='DocumentEditing',
      [ValidateSet('S2','S3','S4')][string]$Stage='S2')
$ErrorActionPreference='Stop'
if($Track -in @('RuntimeKernel','Components','Consumption','InputBoundaries') -and $Stage -ne 'S3'){throw '本检查轨道仅用于S3正式产物验证'}
if($Track -in @('FieldEditing','BindingEditing') -and $Stage -ne 'S4'){throw '字段/绑定编辑检查仅用于S4正式产物验证'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$product=Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$dir=Join-Path $root ('Release/_validation/'+$Stage+'-20261025/' + $Track)
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$attempt=1;while(Test-Path -LiteralPath (Join-Path $dir ('attempt-{0:d3}' -f $attempt))){$attempt++}
$logDir=Join-Path $dir ('attempt-{0:d3}' -f $attempt)
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$references=@(Get-ChildItem -LiteralPath $product -File -Filter 'SunEyeVision*.dll')
$references+=Get-Item (Join-Path $product 'plugins/SunEyeVision.Tool.BlobAnalysis/SunEyeVision.Tool.BlobAnalysis.dll')
$references+=Get-Item (Join-Path $product 'halcondotnetxl.dll')
$references+=Get-Item (Join-Path $product 'System.Text.Json.dll')
$references+=Get-Item (Join-Path $product 'System.Text.Encodings.Web.dll')
$refs=($references | ForEach-Object {'<Reference Include="'+[Security.SecurityElement]::Escape($_.BaseName)+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'}) -join "`n"
$project='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0-windows</TargetFramework><UseWPF>true</UseWPF><OutputType>Exe</OutputType><StartupObject>SafeEntry</StartupObject><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
[IO.File]::WriteAllText((Join-Path $dir 'Check.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
$source=if($Track -eq 'Related'){'S2RelatedCases.cs'}else{'S2DocumentEditingCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'DocumentEditing'){$source='S3BlobRuntimeCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'Related'){$source='S3NeutralBridgeCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeKernel'){$source='S3RuntimeKernelCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'Components'){$source='S3ComponentCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'Consumption'){$source='S3ConsumptionCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'InputBoundaries'){$source='S3InputBoundaryCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'FieldEditing'){$source='S4FieldEditingCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'BindingEditing'){$source='S4BindingEditingCases.cs'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot $source) -Destination (Join-Path $dir 'Cases.cs')
Copy-Item -LiteralPath (Join-Path $dir 'Cases.cs') -Destination (Join-Path $logDir 'cases.cs.txt')
$caseSourceSha=(Get-FileHash -LiteralPath (Join-Path $logDir 'cases.cs.txt') -Algorithm SHA256).Hash
$entry=@'
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
internal static class SafeEntry {
 [STAThread] public static int Main() {
  try {
   Console.OutputEncoding=new System.Text.UTF8Encoding(false);
   Console.Error.Write("");
   var root=Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!;
   Environment.SetEnvironmentVariable("PATH",root+";"+Path.Combine(root,"runtimes","win-x64","native")+";"+Environment.GetEnvironmentVariable("PATH"));
   AssemblyLoadContext.Default.Resolving+=(_,n)=> {
    foreach(var p in new[]{Path.Combine(root,n.Name+".dll"),Path.Combine(root,"plugins",n.Name!,n.Name+".dll")})
     if(File.Exists(p))return AssemblyLoadContext.Default.LoadFromAssemblyPath(p);
    var helpers=Environment.GetEnvironmentVariable("S2_TEST_HELPERS")!;
    if(!n.Name!.StartsWith("SunEyeVision")||n.Name.EndsWith(".Tests")) {
     var helper=Path.Combine(helpers,n.Name+".dll");
     if(File.Exists(helper))return AssemblyLoadContext.Default.LoadFromAssemblyPath(helper);
    }
    return null;
   };
   AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{try {
    var log=Environment.GetEnvironmentVariable("S2_CASE_ROOT")!;
    var rows=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name!.StartsWith("SunEyeVision")&&!a.IsDynamic).Select(a=>{
     var p=a.Location;return a.GetName().Name+"\t"+p+"\t"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    });File.WriteAllLines(Path.Combine(log,"loaded-product.tsv"),rows);
   }catch(Exception e){Console.Error.WriteLine(e);}};
   return (int)typeof(SafeEntry).Assembly.GetType("S2DocumentEditingCases")!.GetMethod("Main",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
  }catch(Exception e){Console.Error.WriteLine(e.InnerException??e);return 1;}
 }
}
'@
[IO.File]::WriteAllText((Join-Path $dir 'SafeEntry.cs'),$entry)
dotnet build (Join-Path $dir 'Check.csproj') -c Release *> (Join-Path $logDir 'build.log')
if($LASTEXITCODE -ne 0){Get-Content (Join-Path $logDir 'build.log') -Tail 12;throw 'S2检查宿主编译失败，不计作产品失败'}
$env:S2_PRODUCT_ROOT=$product;$env:S2_CASE_ROOT=$logDir
$env:S2_TEST_HELPERS=Join-Path $root 'tests/bin/Release'
$env:TEMP=Join-Path $logDir 'data';$env:TMP=$env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
$launch=@{FilePath='dotnet';WorkingDirectory=$product;WindowStyle='Hidden';PassThru=$true;RedirectStandardOutput=(Join-Path $logDir 'run.log');RedirectStandardError=(Join-Path $logDir 'run-error.log')}
$launch.ArgumentList=@('exec','--depsfile',(Join-Path $product 'SunEyeVision.UI.deps.json'),'--runtimeconfig',(Join-Path $product 'SunEyeVision.UI.runtimeconfig.json'),(Join-Path $dir 'out/Check.dll')) | ForEach-Object {'"'+$_+'"'}
$process=Start-Process @launch
if(-not $process.WaitForExit(90000)){$process.Kill();throw 'S2自有检查进程超时，已停止该自有进程'}
[ordered]@{stage=$Stage;attempt=$attempt;pid=$process.Id;exit=$process.ExitCode;productRoot=$product;caseSource=$source;caseSourceSha256=$caseSourceSha;scope='当前实施者针对正式Release的定向检查，不是完整UI、阶段整体或独立验收';logDir=$logDir} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logDir 'receipt.json') -Encoding utf8
Get-Content (Join-Path $logDir 'run.log') -Encoding utf8
if($process.ExitCode -ne 0){Get-Content (Join-Path $logDir 'run-error.log');throw 'S2检查失败，保留原日志'}
