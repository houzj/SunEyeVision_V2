using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Web.Script.Serialization;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
class VmBench {
 static Dictionary<string,object> R=new Dictionary<string,object>();
 static double Time(Action a){var s=Stopwatch.StartNew();a();return s.Elapsed.TotalMilliseconds;}
 [STAThread] static int Main(string[] args) {
  var root=Path.GetFullPath(args[0]);var src=File.ReadAllText(args[1]);var outPath=Path.GetFullPath(args[2]);
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {var f=Path.Combine(root,"vm-binaries",new AssemblyName(e.Name).Name+".dll");return File.Exists(f)?Assembly.LoadFrom(f):null;};
  try {
   var app=new System.Windows.Application();
   R["scope"]="isolated-original-language-component; not VM application UI";
   var paths=new[]{typeof(object).Assembly.Location,typeof(Uri).Assembly.Location,typeof(Enumerable).Assembly.Location,Path.Combine(root,"BenchReference.dll")};
   R["references"]=paths;var assemblies=paths.Select(Assembly.LoadFrom).ToArray();
   var dll=Assembly.LoadFrom(Path.Combine(root,"vm-binaries","VM.CodeCompletion.dll"));var type=dll.GetType("VM.CodeCompletion.CSharpCompletion",true);
   object completion=null;R["serviceMs"]=Time(()=>completion=Activator.CreateInstance(type,new object[]{assemblies}));
   var field=type.GetField("projectContent",BindingFlags.Instance|BindingFlags.NonPublic);
   var readyWatch=Stopwatch.StartNew();int refCount=0;
   while(readyWatch.ElapsedMilliseconds<15000){var content=field.GetValue(completion);var refs=(IEnumerable)content.GetType().GetProperty("AssemblyReferences").GetValue(content,null);refCount=refs.Cast<object>().Count();if(refCount>=paths.Length)break;System.Threading.Thread.Sleep(1);}
   R["referenceReadyWaitMs"]=readyWatch.Elapsed.TotalMilliseconds;R["referenceReadyCount"]=refCount;
   if(refCount<paths.Length)throw new Exception("VM reference preparation timeout");
   R["parseMs"]=Time(()=>type.GetMethod("ProcessInput").Invoke(completion,new object[]{src,"Probe.cs"}));
   var nr=Assembly.LoadFrom(Path.Combine(root,"vm-binaries","ICSharpCode.NRefactory.dll"));
   var doc=Activator.CreateInstance(nr.GetType("ICSharpCode.NRefactory.Editor.StringBuilderDocument",true),new object[]{src});
   doc=Activator.CreateInstance(nr.GetType("ICSharpCode.NRefactory.Editor.ReadOnlyDocument",true),new object[]{doc,"Probe.cs"});
   var method=type.GetMethods().First(x=>x.Name=="GetCompletions"&&x.GetParameters().Length==3);
   var offset=src.IndexOf("text.Append",StringComparison.Ordinal)+5;
   if(args.Length>3){var checks=new List<object>();foreach(int delta in new[]{-1,0,1})foreach(bool ctrl in new[]{false,true}){var result=method.Invoke(completion,new object[]{doc,offset+delta,ctrl});var values=(IEnumerable)result.GetType().GetField("CompletionData").GetValue(result);var names=new List<string>();foreach(var item in values)names.Add((string)item.GetType().GetProperty("Text").GetValue(item,null));checks.Add(new{delta=delta,ctrl=ctrl,names=names});}R["debug"]=checks;}
   R["queryMode"]="explicit completion (Ctrl+Space), same source and caret";
   var queries=new List<object>();for(int i=0;i<21;i++){object result=null;double ms=Time(()=>result=method.Invoke(completion,new object[]{doc,offset,true}));var list=(IEnumerable)result.GetType().GetField("CompletionData").GetValue(result);var names=new List<string>();foreach(var item in list)names.Add((string)item.GetType().GetProperty("Text").GetValue(item,null));queries.Add(new{iteration=i,ms=ms,count=names.Count,hasAppend=names.Contains("Append")});}R["completion"]=queries;
   var compiles=new List<object>();using(var provider=new CSharpCodeProvider())for(int i=0;i<3;i++){
    var cp=new CompilerParameters(paths){GenerateExecutable=false,GenerateInMemory=false,IncludeDebugInformation=false,CompilerOptions="/optimize+",OutputAssembly=Path.Combine(Path.GetDirectoryName(outPath),"vm-compiled-"+i+".dll")};
    CompilerResults result=null;double ms=Time(()=>result=provider.CompileAssemblyFromSource(cp,src));compiles.Add(new{iteration=i,ms=ms,success=!result.Errors.HasErrors,errors=result.Errors.Cast<CompilerError>().Where(x=>!x.IsWarning).Select(x=>x.ToString()).ToArray()});
   }R["compiler"]=compiles;
   R["loaded"]=AppDomain.CurrentDomain.GetAssemblies().Where(x=>!x.IsDynamic).Select(x=>x.Location).ToArray();R["success"]=true;
  }catch(Exception e){R["success"]=false;R["error"]=e.ToString();}
  File.WriteAllText(outPath,new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(R));Console.WriteLine(outPath);return (bool)R["success"]?0:1;
 }
}
