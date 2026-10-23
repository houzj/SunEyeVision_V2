using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
var root=Path.GetFullPath(args[0]);var source=File.ReadAllText(args[1]);var output=Path.GetFullPath(args[2]);
System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(context,name)=>{var f=Path.Combine(root,"self-binaries",name.Name+".dll");return File.Exists(f)?context.LoadFromAssemblyPath(f):null;};
var report=new Dictionary<string,object?>();
try {
 report["scope"]="isolated Roslyn binaries from self Release; not application editor UI";
 var fw=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Microsoft.NET","Framework64","v4.0.30319");
 var paths=JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(root,"references.json")))!;
 report["references"]=paths;var references=paths.Select(p=>MetadataReference.CreateFromFile(p)).ToArray();
 var sw=Stopwatch.StartNew();var host=MefHostServices.Create(MefHostServices.DefaultAssemblies);report["serviceMs"]=sw.Elapsed.TotalMilliseconds;
 sw.Restart();using var ws=new AdhocWorkspace(host);var project=ws.AddProject("Probe",LanguageNames.CSharp).WithMetadataReferences(references).WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
 ws.TryApplyChanges(project.Solution);var doc=ws.AddDocument(project.Id,"Probe.cs",SourceText.From(source));report["documentMs"]=sw.Elapsed.TotalMilliseconds;
 sw.Restart();await doc.GetSemanticModelAsync();report["semanticMs"]=sw.Elapsed.TotalMilliseconds;
 var offset=source.IndexOf("text.Append",StringComparison.Ordinal)+5;var completion=CompletionService.GetService(doc)??throw new Exception("CompletionService missing");
 report["queryMode"]="explicit completion (Ctrl+Space), same source and caret";
 var queries=new List<object>();for(int i=0;i<21;i++){sw.Restart();var result=await completion.GetCompletionsAsync(doc,offset,CompletionTrigger.Default);var elapsed=sw.Elapsed.TotalMilliseconds;queries.Add(new{iteration=i,ms=elapsed,count=result?.ItemsList.Count??0,hasAppend=result?.ItemsList.Any(x=>x.DisplayText=="Append")??false});}report["completion"]=queries;
 var compiles=new List<object>();for(int i=0;i<3;i++){sw.Restart();var parse=Stopwatch.StartNew();var tree=CSharpSyntaxTree.ParseText(SourceText.From(source));var parseMs=parse.Elapsed.TotalMilliseconds;var create=Stopwatch.StartNew();var c=CSharpCompilation.Create("Probe_"+i,new[]{tree},references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release));var createMs=create.Elapsed.TotalMilliseconds;using var ms=new MemoryStream();var emit=Stopwatch.StartNew();var result=c.Emit(ms);var emitMs=emit.Elapsed.TotalMilliseconds;compiles.Add(new{iteration=i,ms=sw.Elapsed.TotalMilliseconds,parseMs,createMs,emitMs,success=result.Success,errors=result.Diagnostics.Where(x=>x.Severity==DiagnosticSeverity.Error).Select(x=>x.ToString()).ToArray()});}report["compiler"]=compiles;
 report["loaded"]=AppDomain.CurrentDomain.GetAssemblies().Where(x=>!x.IsDynamic).Select(x=>x.Location).ToArray();report["success"]=true;
}catch(Exception e){report["success"]=false;report["error"]=e.ToString();}
File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(output);
return report["success"] is true?0:1;
