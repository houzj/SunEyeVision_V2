[CmdletBinding()]
param([ValidateSet('DocumentEditing','Related','RuntimeKernel','RuntimeGlobals','RuntimeAlgorithm','RuntimeWorkflow','RuntimeDebug','RuntimeImage','RuntimeUnits','RuntimeCatalog','RuntimeEditor','RuntimeEditorUiStage','VariableDialog','VariableFields','DesignTime','Components','FieldEditing','NumericContract','RangeRoundtrip','ImageInputSelection','ParameterValues','BlobParameterProduction','BlobRangeLayout','ManualReferenceQualification','NumericIndexProduction','FullChain','WindowPresentation','Algorithm','Resources','RoiComposition','RoiBindings','WorkflowBaseline','WorkflowHost','ImageSources','GlobalBinding','ThemeStartup','SdkBaseline','InfrastructureBaseline','PolygonPrimitives','ResultChain','ImageSourceRecovery')][string]$Track='DocumentEditing',
      [ValidateSet('S2','S3','S4')][string]$Stage='S2',
      [ValidateSet('Formal','Candidate')][string]$ValidationScope='Formal')
$ErrorActionPreference='Stop'
if($Track -in @('RuntimeKernel','RuntimeGlobals','RuntimeAlgorithm','RuntimeWorkflow','RuntimeDebug','RuntimeImage','RuntimeUnits','RuntimeCatalog','Components','Algorithm','Resources','RoiComposition','RoiBindings') -and $Stage -ne 'S3'){throw '本检查轨道仅用于S3正式产物验证'}
if($Track -in @('RuntimeEditor','RuntimeEditorUiStage','VariableDialog','VariableFields','DesignTime','FieldEditing','NumericContract','RangeRoundtrip','ImageInputSelection','ParameterValues','BlobParameterProduction','BlobRangeLayout','ManualReferenceQualification','NumericIndexProduction','FullChain','WindowPresentation','PolygonPrimitives','ResultChain','ImageSourceRecovery') -and $Stage -ne 'S4'){throw '字段/绑定/区域/整链/窗口检查仅用于S4正式产物验证'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
# Formal validation must run from the primary checkout, before creating any output.
$gitDir = git -C $root rev-parse --absolute-git-dir
if ($LASTEXITCODE -ne 0) { throw '无法识别正式验证仓库。' }
$commonDir = git -C $root rev-parse --git-common-dir
if ($LASTEXITCODE -ne 0) { throw '无法识别正式验证仓库的公共 Git 目录。' }
$gitDir = [IO.Path]::GetFullPath($gitDir, $root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$commonDir = [IO.Path]::GetFullPath($commonDir, $root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
if ($ValidationScope -eq 'Formal' -and -not [string]::Equals($gitDir, $commonDir, [StringComparison]::OrdinalIgnoreCase)) {
    throw '正式验证必须从主仓工作目录执行，不能使用独立 worktree 的 Release。'
}
if ($ValidationScope -eq 'Candidate' -and [string]::Equals($gitDir, $commonDir, [StringComparison]::OrdinalIgnoreCase)) {
    throw '候选验证必须在独立 worktree 执行，不能把主仓正式产物标作候选。'
}
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

if($Track -eq 'RuntimeGlobals')
{
    $probeDir=Join-Path $dir 'runtime-probe'
    New-Item -ItemType Directory -Path $probeDir -Force|Out-Null
    $probeSource=Join-Path $root 'tests/_shared/GlobalProductionRuntimeProbe.cs'
    $contractPath=Join-Path $product 'SunEyeVision.Plugin.Contracts.dll'
    $probeProject='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AssemblyName>SunEyeVision.GlobalProduction.Runtime.Tests</AssemblyName><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="'+[Security.SecurityElement]::Escape($probeSource)+'"/><Reference Include="SunEyeVision.Plugin.Contracts"><HintPath>'+[Security.SecurityElement]::Escape($contractPath)+'</HintPath><Private>false</Private></Reference></ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
    [IO.File]::WriteAllText((Join-Path $probeDir 'Probe.csproj'),$probeProject)
    [IO.File]::WriteAllText((Join-Path $probeDir 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
    dotnet build (Join-Path $probeDir 'Probe.csproj') -c Release *> (Join-Path $logDir 'probe-build.log')
    if($LASTEXITCODE -ne 0){Get-Content (Join-Path $logDir 'probe-build.log') -Tail 12;throw '变量生产中立测试声明编译失败'}
    $env:S2_RUNTIME_PROBE=Join-Path $probeDir 'out/SunEyeVision.GlobalProduction.Runtime.Tests.dll'
    $references+=Get-Item $env:S2_RUNTIME_PROBE
    foreach($name in @('xunit.assert.dll','xunit.core.dll','xunit.abstractions.dll','NSubstitute.dll','Castle.Core.dll')){$references+=Get-Item (Join-Path $root ('tests/bin/Release/'+$name))}
    [ordered]@{path='tests/_shared/GlobalProductionRuntimeProbe.cs';sha256=(Get-FileHash -LiteralPath $probeSource -Algorithm SHA256).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $logDir 'probe-source.json') -Encoding utf8
}
if($Track -in @('ManualReferenceQualification','NumericIndexProduction'))
{
    $probeDir=Join-Path $dir 'runtime-probe'
    New-Item -ItemType Directory -Path $probeDir -Force|Out-Null
    $probeSource=Join-Path $root $(if($Track -eq 'NumericIndexProduction'){'tests/_shared/NumericIndexRuntimeProbe.cs'}else{'tests/_shared/ManualReferenceRuntimeProbe.cs'})
    $contractPath=Join-Path $product 'SunEyeVision.Plugin.Contracts.dll'
    $probeProject='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>SunEyeVision.ManualReference.Runtime.Tests</AssemblyName><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="'+[Security.SecurityElement]::Escape($probeSource)+'"/><Reference Include="SunEyeVision.Plugin.Contracts"><HintPath>'+[Security.SecurityElement]::Escape($contractPath)+'</HintPath><Private>false</Private></Reference></ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
    [IO.File]::WriteAllText((Join-Path $probeDir 'Probe.csproj'),$probeProject)
    [IO.File]::WriteAllText((Join-Path $probeDir 'NuGet.Config'),'<configuration><packageSources><clear/></packageSources></configuration>')
    Copy-Item -LiteralPath $probeSource -Destination (Join-Path $logDir 'probe-source.cs.txt')
    dotnet build (Join-Path $probeDir 'Probe.csproj') -c Release *> (Join-Path $logDir 'probe-build.log')
    if($LASTEXITCODE -ne 0){Get-Content (Join-Path $logDir 'probe-build.log') -Tail 12;throw '普通来源资格专用测试声明编译失败'}
    $env:S2_RUNTIME_PROBE=Join-Path $probeDir 'out/SunEyeVision.ManualReference.Runtime.Tests.dll'
    $references+=Get-Item $env:S2_RUNTIME_PROBE
    $manualProbeEvidence=[ordered]@{
        source=if($Track -eq 'NumericIndexProduction'){'tests/_shared/NumericIndexRuntimeProbe.cs'}else{'tests/_shared/ManualReferenceRuntimeProbe.cs'}
        sourceSha256=(Get-FileHash -LiteralPath (Join-Path $logDir 'probe-source.cs.txt') -Algorithm SHA256).Hash
        assembly=$env:S2_RUNTIME_PROBE
        assemblySha256=(Get-FileHash -LiteralPath $env:S2_RUNTIME_PROBE -Algorithm SHA256).Hash
        contracts=$contractPath
        contractsSha256=(Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash
    }
    $manualProbeEvidence|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $logDir 'probe-source.json') -Encoding utf8
}
$refs=($references | ForEach-Object {'<Reference Include="'+[Security.SecurityElement]::Escape($_.BaseName)+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'}) -join "`n"
$project='<Project><PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets></PropertyGroup><Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk"/><PropertyGroup><TargetFramework>net9.0-windows</TargetFramework><UseWPF>true</UseWPF><OutputType>Exe</OutputType><StartupObject>SafeEntry</StartupObject><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><OutputPath>out/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup>'+$refs+'</ItemGroup><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>'
if($Track -eq 'ImageSourceRecovery')
{
    $testSources=@('tests/_shared/WpfTestDispatcher.cs','tests/_shared/ProductRuntimeTestHost.cs','tests/SunEyeVision.ImageSource.Runtime.Tests/ImageSourceRuntimeTests.cs','tests/SunEyeVision.ImageSource.Runtime.Tests/ImageSourceDeclarationAndFormatTests.cs','tests/SunEyeVision.UI.Tests/LocalResultImagePreviewTests.cs','tests/SunEyeVision.UI.Tests/ResultWorkspaceProductionTests.cs','tests/SunEyeVision.UI.Tests/ResultPresentationModelTests.cs','tests/SunEyeVision.UI.Tests/ResultPresentationPresenterTests.cs','tests/SunEyeVision.UI.Tests/ResultImagePaneTests.cs','tests/SunEyeVision.UI.Tests/ResultViewportFullscreenScopeTests.cs','tests/SunEyeVision.UI.Tests/NativeResultCompositionTests.cs','tests/SunEyeVision.UI.Tests/RootResultImagePreviewTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceDesignCompositionTests.cs','tests/SunEyeVision.UI.Tests/SolutionManagerUiTests.cs','tests/SunEyeVision.UI.Tests/ImagePreviewInteractionTests.cs','tests/SunEyeVision.UI.Tests/LocalImageExecutionTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceAcceptanceTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceDebugInvocationTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceConfigurationHistoryTests.cs','tests/SunEyeVision.UI.Tests/DocumentHistoryCoordinatorTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceSavePersistenceTests.cs','tests/SunEyeVision.UI.Tests/LocalImageConfigurationSnapshotTests.cs','tests/SunEyeVision.UI.Tests/LocalImageSequenceTests.cs','tests/SunEyeVision.UI.Tests/ImageSourceResolverTests.cs','tests/SunEyeVision.UI.Tests/CameraStreamOwnershipTests.cs','tests/SunEyeVision.UI.Tests/CameraWorkflowFrameFeedTests.cs','tests/SunEyeVision.UI.Tests/SolutionTransitionTests.cs')
    $compile=($testSources|ForEach-Object {'<Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $root $_))+'"/>'}) -join "`n"
    $project=$project.Replace('<ItemGroup>','<PropertyGroup><AssemblyName>SunEyeVision.UI.Tests</AssemblyName></PropertyGroup><ItemGroup><Compile Remove="runtime-probe/**/*.cs"/>'+$compile)
    $sourceEvidence=@(($testSources+@('tests/_shared/ResultWorkspaceRuntimeProbe.cs','docs/collaboration/evidence/D07_FULL_IMAGESOURCE_TRACK_MIGRATION_AUDIT_20261029.json'))|ForEach-Object {[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash}})
    $sourceEvidence|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $logDir 'test-sources.json') -Encoding utf8
    [IO.File]::WriteAllText((Join-Path $dir 'HostIdentity.cs'),'[assembly:System.Reflection.AssemblyMetadata("HostDebugRoot",@"'+$product+'")]')
}
if($Track -eq 'RuntimeGlobals')
{
    $testSources=@('tests/SunEyeVision.UI.Tests/WorkflowGlobalProductionTests.cs')
    $compile=($testSources|ForEach-Object {'<Compile Include="'+[Security.SecurityElement]::Escape((Join-Path $root $_))+'"/>'}) -join "`n"
    $project=$project.Replace('<ItemGroup>','<ItemGroup><Compile Remove="runtime-probe/**/*.cs"/>'+$compile)
    $testSources|ForEach-Object {[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash}}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $logDir 'test-sources.json') -Encoding utf8
}
if($Track -in @('ManualReferenceQualification','NumericIndexProduction'))
{
    # The fixture is compiled once against Contracts and then loaded by SafeEntry.
    # Do not pick its nested generated sources up again in the WPF test host.
    $project=$project.Replace('<ItemGroup>','<ItemGroup><Compile Remove="runtime-probe/**/*.cs"/>')
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
if($Stage -eq 'S4' -and $Track -eq 'NumericContract'){$source='S4NumericContractCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'RangeRoundtrip'){$source='S4RangeRoundtripCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'ImageInputSelection'){$source='S4ImageInputSelectionCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'FullChain'){$source='S4FullChainCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'WindowPresentation'){$source='O06WindowPresentationCases.cs'}
if($Stage -eq 'S4' -and $Track -in @('RuntimeEditor','RuntimeEditorUiStage')){$source='S4RuntimeEditorCases.cs'}
if($Stage -eq 'S4' -and $Track -eq 'DesignTime'){$source='S4DesignTimeCases.cs'}
if($Track -eq 'ResultChain'){$source='I2ResultChainCases.cs'}
if($Track -eq 'ImageSourceRecovery'){$source='I2ImageSourceRecoveryCases.cs'}
if($Track -eq 'PolygonPrimitives'){$source='O06PolygonPrimitiveCases.cs'}
if($Track -in @('WorkflowBaseline','WorkflowHost','ThemeStartup','SdkBaseline','InfrastructureBaseline')){$source=$Track+'Cases.cs'}
if($Track -eq 'RuntimeGlobals'){$source='S3RuntimeGlobalCases.cs'}
if($Track -eq 'VariableDialog'){$source='S4VariableDialogCases.cs'}
if($Track -eq 'VariableFields'){$source='S4VariableReferenceCases.cs'}
if($Track -eq 'ParameterValues'){$source='S4ParameterCurrentValueCases.cs'}
if($Track -eq 'BlobParameterProduction'){$source='S4BlobParameterProductionCases.cs'}
if($Track -eq 'BlobRangeLayout'){$source='S4BlobRangeLayoutCases.cs'}
if($Track -eq 'ManualReferenceQualification'){$source='S4ManualReferenceQualificationCases.cs'}
if($Track -eq 'NumericIndexProduction'){$source='S4NumericIndexProductionCases.cs'}
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
$launch.Environment['S4_EDITOR_TRACK']=$Track
$launch.UseShellExecute=$false;$launch.CreateNoWindow=$true
$launch.RedirectStandardOutput=$true;$launch.RedirectStandardError=$true
$launch.Arguments=(@('exec','--depsfile',(Join-Path $product 'SunEyeVision.UI.deps.json'),'--runtimeconfig',(Join-Path $product 'SunEyeVision.UI.runtimeconfig.json'),(Join-Path $dir $(if($Track -eq 'ImageSourceRecovery'){'out/SunEyeVision.UI.Tests.dll'}else{'out/Check.dll'}))) | ForEach-Object {'"'+$_+'"'}) -join ' '
# Own the process handle through exit; Windows PowerShell Start-Process can otherwise return a null ExitCode.
$process=New-Object System.Diagnostics.Process
$process.StartInfo=$launch
if(-not $process.Start()){throw 'S2自有检查进程启动失败'}
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
# Recovery contains hundreds of real WPF cases. Its runner enforces a separate
# 90-second watchdog per case; the aggregate budget must not terminate a healthy
# case merely because earlier completed cases consumed the former global budget.
$processTimeoutMs=if($Track -eq 'FieldEditing' -and $env:B08_REFERENCE_CASE_SCOPE -eq 'WindowSession'){240000}elseif($Track -eq 'ImageSourceRecovery' -and -not $env:S2_DIAGNOSTIC_CASE){300000}elseif($Track -eq 'FieldEditing' -and $env:B08_REFERENCE_CASE_SCOPE -eq 'CrossDocument'){360000}elseif($Track -eq 'RuntimeEditorUiStage'){180000}elseif($Track -eq 'FieldEditing' -and -not $env:B08_REFERENCE_CASE_SCOPE){180000}else{90000}
$finished=$process.WaitForExit($processTimeoutMs)
if(-not $finished){$process.Kill($true);$process.WaitForExit()}
[IO.File]::WriteAllText((Join-Path $logDir 'run.log'),$stdout.GetAwaiter().GetResult(),[Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $logDir 'run-error.log'),$stderr.GetAwaiter().GetResult(),[Text.Encoding]::UTF8)
if(-not $finished){throw 'S2自有检查进程超时，已停止该自有进程'}
$receipt=[ordered]@{stage=$Stage;validationScope=$ValidationScope;attempt=$attempt;pid=$process.Id;exit=$process.ExitCode;productRoot=$product;caseSource=$source;caseSourceSha256=$caseSourceSha;scope=$(if($ValidationScope -eq 'Formal'){'当前实施者针对主仓正式Release的定向检查，不是完整UI、阶段整体或独立验收'}else{'独立worktree自行构建产物的候选检查，尚未集成或正式验收'});logDir=$logDir}
if($Track -in @('ManualReferenceQualification','NumericIndexProduction'))
{
    $receipt.track=$Track
    $receipt.probe=$manualProbeEvidence
    $receipt.caseReport=Join-Path $logDir $(if($Track -eq 'NumericIndexProduction'){'numeric-index-report.json'}else{'manual-reference-report.json'})
}
if($Track -eq 'ImageInputSelection'){$receipt.track=$Track;$receipt.caseReport=Join-Path $logDir 'image-input-selection-report.json';$receipt.loaded=($env:S4_IMAGE_INPUT_LOADED -eq '1')}
if($Track -eq 'RangeRoundtrip'){$receipt.track=$Track;$receipt.caseReport=Join-Path $logDir 'range-roundtrip-report.json';$receipt.loaded=($env:S4_RANGE_LOADED -eq '1')}
if($Track -eq 'BlobRangeLayout'){$receipt.track=$Track;$receipt.caseReport=Join-Path $logDir 'blob-range-layout-report.json'}
if($Track -eq 'BlobParameterProduction'){$receipt.track=$Track;$receipt.caseReport=Join-Path $logDir 'blob-parameter-production-report.json'}
if($Track -in @('RuntimeEditor','RuntimeEditorUiStage')){$receipt.track=$Track;$receipt.caseReport=Join-Path $logDir 'ui-stage-report.json'}
$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $logDir 'receipt.json') -Encoding utf8
Get-Content (Join-Path $logDir 'run.log') -Encoding utf8
if($process.ExitCode -ne 0){Get-Content (Join-Path $logDir 'run-error.log');throw 'S2检查失败，保留原日志'}

