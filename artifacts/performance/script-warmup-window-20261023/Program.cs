using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CommonControl;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

class WindowProbe {
 [STAThread] static int Main(string[] args) {
  var result=new Dictionary<string,object?>();var app=new Application();var output=args[1];var process=Stopwatch.StartNew();
  var release=Path.GetFullPath("Release/SunEyeVision_v1.0.0/SunEyeVision");
  System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(c,n)=>{var f=Path.Combine(release,n.Name+".dll");return File.Exists(f)?c.LoadFromAssemblyPath(f):null;};
  var watchdog=new DispatcherTimer{Interval=TimeSpan.FromSeconds(45)};watchdog.Tick+=(_,_)=>{result["timeout"]=true;Save();app.Shutdown(2);};watchdog.Start();
  app.Dispatcher.BeginInvoke(new Action(async()=>{try {
   var type=typeof(ScriptEditorCore);var flags=BindingFlags.Static|BindingFlags.NonPublic;
   result["scope"]="Release ScriptEditorCore in standalone WPF window; not full ScriptDebugControl/product shell";result["mode"]=args[0];
   if(args[0]=="startup-disabled-probe") {type.GetField("_warmupStarted",flags)!.SetValue(null,true);result["isolationNote"]="Probe-only static guard suppresses background startup preparation; on-demand initialization still runs; product unchanged";}
   if(args[0]=="prewarmed") {type.GetMethod("WarmupForStartup",flags)!.Invoke(null,null);var timer=Stopwatch.StartNew();while(!(bool)type.GetField("_completionReady",flags)!.GetValue(null)!){if(timer.ElapsedMilliseconds>30000)throw new TimeoutException("Startup warmup not ready");await Task.Delay(10);}result["startupPreparationMs"]=timer.Elapsed.TotalMilliseconds;}
   var refs=JsonSerializer.Deserialize<string[]>(File.ReadAllText("artifacts/performance/script-baseline-20261023/references.json"))!;
   var source="using System.Text; public class Probe { public void Run(){ var text=new StringBuilder(); text.Append(\"x\"); } }";
   for(int round=0;round<2;round++) {
    var sw=Stopwatch.StartNew();var core=new ScriptEditorCore();core.SetReferenceProvider(()=>refs.Select(x=>MetadataReference.CreateFromFile(x)));core.SetCodeSource(()=>"",()=>core.GetCode());core.LoadCode(source);
    var window=new Window{Title="SunEye script isolated timing probe",Width=900,Height=650,Content=core};window.Measure(new Size(900,650));window.Arrange(new Rect(0,0,900,650));window.UpdateLayout();
    var init=Stopwatch.StartNew();await core.InitializeAndWaitAsync();var initMs=init.Elapsed.TotalMilliseconds;
    var rendered=new TaskCompletionSource();window.ContentRendered+=(_,_)=>rendered.TrySetResult();window.Show();await rendered.Task;await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
    var visibleMs=sw.Elapsed.TotalMilliseconds;var doc=core.GetUserDocument()??throw new Exception("No document");var service=CompletionService.GetService(doc)!;var query=Stopwatch.StartNew();var items=await service.GetCompletionsAsync(doc,source.IndexOf("text.Append")+5,CompletionTrigger.Invoke);query.Stop();
    result["round"+round]=new{initializationMs=initMs,constructToContentRenderedMs=visibleMs,firstRequestedQueryMs=query.Elapsed.TotalMilliseconds,hasAppend=items?.ItemsList.Any(i=>i.DisplayText=="Append")==true,queryExcludesPopup=true};window.Close();
   }
   result["success"]=true;
  }catch(Exception e){result["success"]=false;result["error"]=e.ToString();}finally{Save();app.Shutdown();}}));
  void Save(){result["totalMs"]=process.Elapsed.TotalMilliseconds;File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));}
  app.ShutdownMode=ShutdownMode.OnExplicitShutdown;return app.Run();
 }
}
