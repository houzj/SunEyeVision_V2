[CmdletBinding()]
param([ValidateSet('DocumentEditing','Related','RuntimeKernel','RuntimeAlgorithm','RuntimeWorkflow','RuntimeDebug','RuntimeImage','RuntimeUnits','RuntimeCatalog','RuntimeEditor','DesignTime','Components','FieldEditing','FullChain','WindowPresentation','Algorithm','Resources','RoiComposition','RoiBindings','WorkflowBaseline','WorkflowHost','ImageSources','GlobalBinding','ThemeStartup','SdkBaseline','InfrastructureBaseline','PolygonPrimitives','ResultChain','ImageSourceRecovery')][string]$Track='DocumentEditing',
      [ValidateSet('S2','S3','S4')][string]$Stage='S2')
$ErrorActionPreference='Stop'
if($Track -in @('RuntimeKernel','RuntimeAlgorithm','RuntimeWorkflow','RuntimeDebug','RuntimeImage','RuntimeUnits','RuntimeCatalog','Components','Algorithm','Resources','RoiComposition','RoiBindings') -and $Stage -ne 'S3'){throw '本检查轨道仅用于S3正式产物验证'}
if($Track -in @('RuntimeEditor','DesignTime','FieldEditing','FullChain','WindowPresentation','PolygonPrimitives','ResultChain','ImageSourceRecovery') -and $Stage -ne 'S4'){throw '字段/绑定/区域/整链/窗口检查仅用于S4正式产物验证'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$product=Join-Path $root 'Release/SunEyeVision_v1.0.0/SunEyeVision'
$dir=Join-Path $root ('Release/_validation/'+$Stage+'-20261025/' + $Track)
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$attempt=1;while(Test-Path -LiteralPath (Join-Path $dir ('attempt-{0:d3}' -f $attempt))){$attempt++}
$logDir=Join-Path $dir ('attempt-{0:d3}' -f $attempt)
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$references=@(Get-ChildItem -LiteralPath $product -File -Filter 'SunEyeVision*.dll')
$references+=Get-Item (Join-Path $product 'halcondotnetxl.dll')
$references+=Get-Item (Join-Path $product 'System.Text.Json.dll')
$references+=Get-Item (Join-Path $product 'System.Text.Encodings.Web.dll')
if($Track -eq 'ImageSourceRecovery')
{
    $probeDir=Join-Path $dir 'runtime-probe'
    New-Item -ItemType Directory -Path $probeDir -Force|Out-Null
    $probeSource=Join-Path $root 'tests/_shared/ResultWorkspaceRuntimeProbe.cs'
    $contractPath=Join-Path $product 'SunEyeVision.Plugin.Contracts.dll'
    $probeProject='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AssemblyName>SunEyeVision.ResultWorkspace.Runtime.Tests</AssemblyName><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="'+[Security.SecurityElement]::Escape($probeSource)+'"/><Reference Include="SunEyeVision.Plugin.Contracts"><HintPath>'+[Security.SecurityElement]::Escape($contractPath)+'</HintPath><Private>false</Private></Reference></ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
    [IO.File]::WriteAllText((Join-Path $probeDir 'Probe.csproj'),$probeProject)
    [IO.File]::WriteAllText((Join-Path $probeDir 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
    dotnet build (Join-Path $probeDir 'Probe.csproj') -c Release *> (Join-Path $logDir 'probe-build.log')
    if($LASTEXITCODE -ne 0){Get-Content (Join-Path $logDir 'probe-build.log') -Tail 12;throw '中立测试声明编译失败'}
    $env:S2_RUNTIME_PROBE=Join-Path $probeDir 'out/SunEyeVision.ResultWorkspace.Runtime.Tests.dll'
    $references+=Get-Item $env:S2_RUNTIME_PROBE
    foreach($name in @('xunit.assert.dll','xunit.core.dll','xunit.abstractions.dll','NSubstitute.dll','Castle.Core.dll')){$references+=Get-Item (Join-Path $root ('tests/bin/Release/'+$name))}
}
else{$env:S2_RUNTIME_PROBE=$null}

$refs=($references | ForEach-Object {'<Reference Include="'+[Security.SecurityElement]::Escape($_.BaseName)+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'}) -join "`n"
$project='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0-windows</TargetFramework><UseWPF>true</UseWPF><OutputType>Exe</OutputType><StartupObject>SafeEntry</StartupObject><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
if($Track -eq 'ImageSourceRecovery')
{
    $testSources=@('tests/_shared/WpfTestDispatcher.cs','tests/SunEyeVision.UI.Tests/LocalResultImagePreviewTests.cs','tests/SunEyeVision.UI.Tests/ResultWorkspaceProductionTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceDesignCompositionTests.cs','tests/SunEyeVision.UI.Tests/SolutionManagerUiTests.cs','tests/SunEyeVision.UI.Tests/ImagePreviewInteractionTests.cs','tests/SunEyeVision.UI.Tests/LocalImageExecutionTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceAcceptanceTests.cs','tests/SunEyeVision.UI.Tests/CameraStreamOwnershipTests.cs','tests/SunEyeVision.UI.Tests/CameraWorkflowFrameFeedTests.cs')
    $compile=($testSources|ForEach-Object {'<Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $root $_))+'"/>'}) -join "`n"
    $project=$project.Replace('<ItemGroup>','<PropertyGroup><AssemblyName>SunEyeVision.UI.Tests</AssemblyName></PropertyGroup><ItemGroup><Compile Remove="runtime-probe/**/*.cs"/>'+$compile)
    $sourceEvidence=@(($testSources+@('tests/_shared/ResultWorkspaceRuntimeProbe.cs'))|ForEach-Object {[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash}})
    $sourceEvidence|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $logDir 'test-sources.json') -Encoding utf8
    [IO.File]::WriteAllText((Join-Path $dir 'HostIdentity.cs'),'[assembly:System.Reflection.AssemblyMetadata("HostDebugRoot",@"'+$product+'")]')
}
[IO.File]::WriteAllText((Join-Path $dir 'Check.csproj'),$project)
[IO.File]::WriteAllText((Join-Path $dir 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
$source=if($Track -eq 'Related'){'S2RelatedCases.cs'}else{'S2DocumentEditingCases.cs'}
if($Track -eq 'ImageSources'){$source='ImageSourceCases.cs'}
if($Track -eq 'GlobalBinding'){$source='GlobalBindingCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'DocumentEditing'){$source='S3BlobRuntimeCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'Related'){$source='S3NeutralBridgeCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'Resources'){$source='O06ResourceCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RoiComposition'){$source='O06RoiCompositionCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RoiBindings'){$source='O06RoiBindingCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'Algorithm'){$source='O06AlgorithmCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeKernel'){$source='S3RuntimeKernelCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeAlgorithm'){$source='S3BlobRuntimeCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeWorkflow'){$source='S3RuntimeWorkflowCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeDebug'){$source='S3RuntimeDebugCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeImage'){$source='S3RuntimeImageCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeUnits'){$source='S3RuntimeUnitCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'RuntimeCatalog'){$source='S3RuntimeCatalogCases.cs'}
if($Stage -eq 'S3' -and $Track -eq 'Components'){$source='S3ComponentCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'FieldEditing'){$source='S4FieldEditingCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'FullChain'){$source='S4FullChainCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'WindowPresentation'){$source='O06WindowPresentationCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'RuntimeEditor'){$source='S4RuntimeEditorCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'DesignTime'){$source='S4DesignTimeCases.cs'}
if($Track -eq 'ResultChain'){$source='I2ResultChainCases.cs'}
if($Track -eq 'ImageSourceRecovery'){$source='I2ImageSourceRecoveryCases.cs'}
if($Track -eq 'PolygonPrimitives'){$source='O06PolygonPrimitiveCases.cs'}
if($Track -in @('WorkflowBaseline','WorkflowHost','ThemeStartup','SdkBaseline','InfrastructureBaseline')){$source=$Track+'Cases.cs'}
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
   AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",root+Path.DirectorySeparatorChar);
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
    var rows=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name!.StartsWith("SunEyeVision")&&!a.GetName().Name!.EndsWith(".Tests")&&!a.IsDynamic).Select(a=>{
     var p=a.Location;return a.GetName().Name+"\t"+p+"\t"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    });File.WriteAllLines(Path.Combine(log,"loaded-product.tsv"),rows);
   }catch(Exception e){Console.Error.WriteLine(e);}};
   var probe=Environment.GetEnvironmentVariable("S2_RUNTIME_PROBE");
   if(!string.IsNullOrEmpty(probe))AssemblyLoadContext.Default.LoadFromAssemblyPath(probe);
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
$launch=New-Object System.Diagnostics.ProcessStartInfo
$launch.FileName='dotnet';$launch.WorkingDirectory=$product
$launch.UseShellExecute=$false;$launch.CreateNoWindow=$true
$launch.RedirectStandardOutput=$true;$launch.RedirectStandardError=$true
$launch.Arguments=(@('exec','--depsfile',(Join-Path $product 'SunEyeVision.UI.deps.json'),'--runtimeconfig',(Join-Path $product 'SunEyeVision.UI.runtimeconfig.json'),(Join-Path $dir $(if($Track -eq 'ImageSourceRecovery'){'out/SunEyeVision.UI.Tests.dll'}else{'out/Check.dll'}))) | ForEach-Object {'"'+$_+'"'}) -join ' '
# Own the process handle through exit; Windows PowerShell Start-Process can otherwise return a null ExitCode.
$process=New-Object System.Diagnostics.Process
$process.StartInfo=$launch
if(-not $process.Start()){throw 'S2自有检查进程启动失败'}
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
$finished=$process.WaitForExit(90000)
if(-not $finished){$process.Kill($true);$process.WaitForExit()}
[IO.File]::WriteAllText((Join-Path $logDir 'run.log'),$stdout.GetAwaiter().GetResult(),[Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $logDir 'run-error.log'),$stderr.GetAwaiter().GetResult(),[Text.Encoding]::UTF8)
if(-not $finished){throw 'S2自有检查进程超时，已停止该自有进程'}
[ordered]@{stage=$Stage;attempt=$attempt;pid=$process.Id;exit=$process.ExitCode;productRoot=$product;caseSource=$source;caseSourceSha256=$caseSourceSha;scope='当前实施者针对正式Release的定向检查，不是完整UI、阶段整体或独立验收';logDir=$logDir} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logDir 'receipt.json') -Encoding utf8
Get-Content (Join-Path $logDir 'run.log') -Encoding utf8
if($process.ExitCode -ne 0){Get-Content (Join-Path $logDir 'run-error.log');throw 'S2检查失败，保留原日志'}
