using System;
using System.Reflection;
using System.IO;
class VmInspect {
 [STAThread] static void Main(string[] args) {
  var root=Path.GetFullPath(args[0]);
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {var f=Path.Combine(root,new AssemblyName(e.Name).Name+".dll");return File.Exists(f)?Assembly.LoadFrom(f):null;};
  var a=Assembly.LoadFrom(Path.Combine(root,"VM.CodeCompletion.dll"));
  foreach(var t in a.GetTypes()) if(t.Name=="CSharpCompletion") {
   foreach(var c in t.GetConstructors()) { Console.WriteLine(c); foreach(var p in c.GetParameters())Console.WriteLine("  "+p.Name+":"+p.ParameterType); }
   foreach(var m in t.GetMethods())if(m.Name=="ProcessInput"||m.Name=="GetCompletions") {Console.WriteLine(m);foreach(var p in m.GetParameters())Console.WriteLine("  "+p.Name+":"+p.ParameterType);}
  }
  foreach(var name in new[]{"ICSharpCode.NRefactory.dll","VM.CodeCompletion.dll"}) {
   foreach(var t in Assembly.LoadFrom(Path.Combine(root,name)).GetTypes()) if(t.Name.Contains("Document")||t.Name=="CodeCompletionResult") {
    Console.WriteLine("TYPE "+t.FullName); foreach(var c in t.GetConstructors())Console.WriteLine(c);
    if(t.Name=="CodeCompletionResult")foreach(var m in t.GetMembers())Console.WriteLine(m);
   }
  }
 }
}
