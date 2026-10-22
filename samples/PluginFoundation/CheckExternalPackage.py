from pathlib import Path
import tempfile,json,subprocess,html
r=Path(__file__).resolve().parents[2];o=r/'artifacts/validation/plugin-foundation-01';t=Path(tempfile.mkdtemp(prefix='SunEye-Foundation-External-'));feed=o/'sdk'
(t/'Directory.Build.props').write_text('<Project />',encoding='utf-8');(t/'Directory.Build.targets').write_text('<Project />',encoding='utf-8')
(t/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="local" value="'+html.escape(str(feed))+'"/></packageSources></configuration>',encoding='utf-8')
(t/'External.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit><RestorePackagesPath>packages</RestorePackagesPath><RestoreFallbackFolders>C:/Users/houzhongjie/.nuget/packages</RestoreFallbackFolders></PropertyGroup><ItemGroup><PackageReference Include="SunEyeVision.Plugin.Contracts" Version="3.0.0-foundation.2" /></ItemGroup></Project>',encoding='utf-8')
(t/'Program.cs').write_text('''using SunEyeVision.Plugin.Contracts;
var schema=new ToolSchema(new[]{new Port("value",DataType.Number)},new[]{new Port("result",DataType.Number)});
var values=new InputValues(schema.Inputs,new Dictionary<string,object?>{{"value",2d}});
if(values.Get<double>("value")!=2)throw new Exception("input");
var refs=typeof(IToolDefinition<,>).Assembly.GetReferencedAssemblies();
if(refs.Any(a=>a.Name!.Contains("Presentation")||a.Name.Contains("WindowsBase")||a.Name.Contains("SunEyeVision.")))throw new Exception("Unexpected host dependency");
Console.WriteLine("External package-only console PASS");''',encoding='utf-8')
log=[];codes=[]
for args in [['dotnet','restore','--configfile','NuGet.Config'],['dotnet','run','-c','Release','--no-restore']]:
 q=subprocess.run(args,cwd=t,capture_output=True,text=True,encoding='utf-8',errors='replace');codes.append(q.returncode);log.append(q.stdout+q.stderr)
(o/'external-consumer.log').write_text('\n'.join(log),encoding='utf-8');(o/'external-consumer.json').write_text(json.dumps({'directory':str(t),'repositoryProjectReferences':False,'packageOnly':True,'exitCodes':codes},indent=2),encoding='utf-8');print(codes);print(log[-1]);raise SystemExit(max(codes))


