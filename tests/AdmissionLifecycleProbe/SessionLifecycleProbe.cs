using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;

internal static class SessionLifecycleProbe
{
 internal static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 internal static void Reject(Action action,string scenario){try{action();}catch(Exception e) when(e is InvalidOperationException or ArgumentException or KeyNotFoundException or NotSupportedException or JsonException){return;}throw new Exception("正式入口没有拒绝："+scenario);}
 internal static string CaseDirectory(string name){var root=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("SUNEYE_INFRA_PRODUCT_ROOT")!,"..","..","_validation","AdmissionLifecycle",name+"-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);return root;}
 private static readonly FrozenVariableTypeCatalog Types=new(Array.Empty<KeyValuePair<string,string>>());
 private static readonly JsonSerializerOptions Options=new();
 private static NodeDefinition Add(DocumentSession session,WorkflowDefinition flow,string tool){var graph=session.PrepareGraphEdit(flow.Id);var node=session.CreateNode(graph,tool,new(150,120));session.CommitGraphEdit(graph,"实际准入节点");return node;}
 private static void Edit(DocumentSession session,WorkflowDefinition flow,NodeDefinition node,Dictionary<string,ParameterSetting> values){var draft=session.Editing.Host.BeginEdit(flow.Id,node.Id);draft.ApplyFields(values);session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{draft}));}
 private static void ConfigureImage(DocumentSession session,WorkflowDefinition flow,NodeDefinition node,string file)=>Edit(session,flow,node,new(){["local.paths"]=new ConstantSetting(new[]{file}),["local.start"]=new ConstantSetting(0L)});
 private static void Wait(Task task)=>task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
 private static T Wait<T>(Task<T> task)=>task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
 private static DeclaredOutputReader Run(DocumentSession session,WorkflowDefinition flow,NodeDefinition resultNode,IHostInputFactory factory)
 {
  using var batch=session.CaptureRunBatch(new[]{flow.Id},hostFactory:factory);using var result=Wait(Task.Run(()=>batch.RunAsync(_=>{})));session.DrainRuntimeChanges();
  var reader=session.AcquirePublishedResult(flow.Id,resultNode.Id)??throw new Exception("实际Session没有发布结果");Check(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage??"正式运行失败");return reader;
 }
 internal static void Run(PluginManager production)
 {
  Check(!AppDomain.CurrentDomain.GetAssemblies().Any(assembly=>assembly.GetName().Name=="SunEyeVision.Workflow"),"正式准入仍加载已退役Workflow程序集");
  var directory=production.RuntimeDirectory!;var data=CaseDirectory("session");
  var imageFile=Path.Combine(data,"image.pgm");File.WriteAllBytes(imageFile,System.Text.Encoding.ASCII.GetBytes("P5\n8 8\n255\n").Concat(Enumerable.Range(0,64).Select(i=>(byte)(i%8>=2&&i%8<6&&i/8>=2&&i/8<6?255:0))).ToArray());
  using(var manager=new DocumentSessionManager(directory))
  {
   var session=manager.CreateNew("准入与文件");var flow=session.GetWorkflows().Single();var source=Add(session,flow,"ImageLoad");
   foreach(var id in new[]{"UnknownTool","Subroutine","Condition"})
   {
    var candidate=session.PrepareGraphEdit(flow.Id);Reject(()=>session.CreateNode(candidate,id,new(0,0)),"未知或暂停工具"+id);
    var injected=session.PrepareGraphEdit(flow.Id);injected.AddNode(new NodeDefinition(Guid.NewGuid().ToString("N"),id,id,new ToolConfiguration(id,new FieldSettings(Array.Empty<Port>(),new Dictionary<string,ParameterSetting>()))));
    Reject(()=>session.CommitGraphEdit(injected,"拒绝未准入节点"),"正式图提案未准入工具"+id);Check(flow.Nodes.Count==1,"失败节点漏入live");
   }
   Reject(()=>new NodeDefinition("mismatch","","BlobAnalysis",source.Configuration),"工具配置身份不一致");
   Reject(()=>new NodeDefinition("null","","ImageLoad",null!),"缺配置");
   var fake=new NodeDefinition(source.Id,source.Name,source.ToolType,source.Configuration);Reject(()=>session.OpenDebugSession(flow.Id,fake),"同ID伪actual调试");Reject(()=>session.GetNodeHostInputDeclaration(flow.Id,fake),"同ID伪actual宿主");
   Reject(()=>session.AdmitPresentation(flow.Id,"UnknownTool"),"未准入节点呈现");
   Console.WriteLine("probe: tool admission, paused control flow, workflow and debug rejection passed");
   Console.WriteLine("probe: unadmitted/paused open rejection passed; real editor windows use bounded hidden child");
   ConfigureImage(session,flow,source,imageFile);
   var valid=Path.Combine(data,"valid.solution");session.SaveAs(valid,Path.Combine(data,"recovery"),data,Types,Options,scope=>new SdkVariableValueExtensionCodec(scope));
   var original=File.ReadAllBytes(valid);var hash=System.Security.Cryptography.SHA256.HashData(original);
   session=manager.Open(valid,_=>Types,scope=>new SdkVariableValueExtensionCodec(scope),Options,discardPendingEdits:true);
   Check(session.GetNodes(session.GetWorkflows().Single().Id).Single().ToolType=="ImageLoad","实际保存重开失去节点");
   foreach(var id in new[]{"UnknownTool","Subroutine","Condition"})
   {
    var raw=JsonNode.Parse(original)!;raw["Workflows"]![0]!["Nodes"]![0]!["ToolType"]=id;raw["Workflows"]![0]!["Nodes"]![0]!["Configuration"]!["ToolId"]=id;
    var path=Path.Combine(data,id+".solution");File.WriteAllText(path,raw.ToJsonString());var candidateBytes=File.ReadAllBytes(path);
    Reject(()=>manager.Open(path,_=>Types,scope=>new SdkVariableValueExtensionCodec(scope),Options),"缺组件V4 Open "+id);
    Check(ReferenceEquals(session,manager.CurrentSession)&&candidateBytes.SequenceEqual(File.ReadAllBytes(path)),"失败Open改源/换会话");
   }
   foreach(var malformed in new[]{"missing-configuration","mismatch-configuration","legacy-sdk-model","paused-subroutine-link","paused-subroutine-space"})
   {
    var raw=JsonNode.Parse(original)!;var node=raw["Workflows"]![0]!["Nodes"]![0]!.AsObject();
    if(malformed=="missing-configuration")node.Remove("Configuration");
    else if(malformed=="mismatch-configuration")node["Configuration"]!["ToolId"]="BlobAnalysis";
    else if(malformed=="legacy-sdk-model")node["Parameters"]=new JsonObject();
    else node["SubroutineId"]=malformed=="paused-subroutine-space"?" ":"paused-child";
    var path=Path.Combine(data,malformed+".solution");File.WriteAllText(path,raw.ToJsonString());var bytes=File.ReadAllBytes(path);
    Reject(()=>manager.Open(path,_=>Types,scope=>new SdkVariableValueExtensionCodec(scope),Options),malformed);
    Check(ReferenceEquals(session,manager.CurrentSession)&&bytes.SequenceEqual(File.ReadAllBytes(path)),"坏配置Open更改源/会话："+malformed);
   }
   var emptyLink=JsonNode.Parse(original)!;emptyLink["Workflows"]![0]!["Nodes"]![0]!["SubroutineId"]="";
   var emptyPath=Path.Combine(data,"empty-subroutine-default.solution");File.WriteAllText(emptyPath,emptyLink.ToJsonString());
   using(var independent=new DocumentSessionManager(directory)){var allowed=independent.Open(emptyPath,_=>Types,scope=>new SdkVariableValueExtensionCodec(scope),Options);Check(allowed.GetNodes(allowed.GetWorkflows().Single().Id).Single().SubroutineId=="","空默认SubroutineId被误拒绝");}
   Reject(()=>session.Save(valid,Path.Combine(data,"recovery"),Types,Options,_=>throw new InvalidOperationException("受控codec失败")),"真实保存准入失败");
   Check(hash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(valid))),"失败Save改写源");
   Console.WriteLine("probe: admitted save/reopen and missing/paused rejected open/save preserve bytes passed");
  }
  RetiredSaveAndAdmission(production,data,imageFile);
  directory=production.RuntimeDirectory!;
  using(var manager=new DocumentSessionManager(directory))
  using(var factory=new ImageSourceHostInputFactory())
  {
   var session=manager.CreateNew("正式reader");var flow=session.GetWorkflows().Single();var source=Add(session,flow,"ImageLoad");ConfigureImage(session,flow,source,imageFile);
   var capability=session.AdmitPresentation(flow.Id,source.Id);var oldDraft=session.Editing.Host.BeginEdit(flow.Id,source.Id);
   var reader=Run(session,flow,source,factory);using var sources=new ResultDisplaySourceSet(new[]{reader.Snapshot});var displayLease=sources.Acquire();
   var text=new ResultDisplayTextLease(reader.Snapshot,new ResultTextAppearance(true,"probe",new ResultDisplayColor(255,255,255),new ResultDisplayColor(255,0,0),12,1,0,0));Check(text.Evaluate(0).Content=="probe","真实文本lease异常");sources.Dispose();
   manager.CloseCurrent(true);var unload=production.UnloadPluginsAsync();Thread.Sleep(100);Check(!unload.IsCompleted,"Session结果reader未保代");reader.Dispose();Thread.Sleep(100);Check(!unload.IsCompleted,"显示source reader未保代");displayLease.Dispose();Thread.Sleep(100);Check(!unload.IsCompleted,"显示text reader未保代");text.Dispose();Wait(unload);
   Check(!capability.IsCurrent,"旧呈现cap在retirement后有效");Reject(()=>oldDraft.ApplyFields(new Dictionary<string,ParameterSetting>()),"退休草稿");Reject(()=>session.CaptureRunBatch(new[]{flow.Id}),"退休Session执行");Reject(()=>session.OpenDebugSession(flow.Id,source),"退休Session调试");
  }
  Wait(production.LoadPluginsAsync());Check(production.LastAdmissionError is null,production.LastAdmissionError??"");Console.WriteLine("probe: old runtime generation rejected for execution and debug admission");
  ActiveRetirement(production,imageFile);
  Wait(production.LoadPluginsAsync());Check(production.LastAdmissionError is null,production.LastAdmissionError??"");
  DisplayAndDebug(production,imageFile);
 }
 private static void RetiredSaveAndAdmission(PluginManager production,string data,string imageFile)
 {
  using var manager=new DocumentSessionManager(production.RuntimeDirectory!);var session=manager.CreateNew("目录代次保存守卫");var flow=session.GetWorkflows().Single();var node=Add(session,flow,"ImageLoad");ConfigureImage(session,flow,node,imageFile);
  var path=Path.Combine(data,"retired-save.solution");session.SaveAs(path,Path.Combine(data,"retired-recovery"),data,Types,Options,scope=>new SdkVariableValueExtensionCodec(scope));var before=File.ReadAllBytes(path);
  var cap=session.AdmitPresentation(flow.Id,node.Id);var draft=session.Editing.Host.BeginEdit(flow.Id,node.Id);using var debug=session.OpenDebugSession(flow.Id,node);
  Wait(production.LoadPluginsAsync());Check(production.LastAdmissionError is null,production.LastAdmissionError??"");Check(!cap.IsCurrent,"新目录发布没有退休旧cap");
  Reject(()=>session.Save(path,Path.Combine(data,"retired-recovery"),Types,Options,scope=>new SdkVariableValueExtensionCodec(scope)),"缺旧准入代的真实Save");
  Check(before.SequenceEqual(File.ReadAllBytes(path)),"目录退休的Save更改源字节");
  Reject(()=>session.CaptureRunBatch(new[]{flow.Id}),"旧目录会话执行");Reject(()=>debug.Capture(draft,null,_=>{}),"旧目录私有调试");
  Console.WriteLine("probe: retired actual session save/run/debug rejected and source preserved");
 }
 private static void ActiveRetirement(PluginManager production,string imageFile)
 {
  using var manager=new DocumentSessionManager(production.RuntimeDirectory!);var session=manager.CreateNew("活跃输入退休");var flow=session.GetWorkflows().Single();var source=Add(session,flow,"ImageLoad");ConfigureImage(session,flow,source,imageFile);
  using var actual=new ImageSourceHostInputFactory();using var factory=new BlockingFactory(actual);using var batch=session.CaptureRunBatch(new[]{flow.Id},hostFactory:factory);
  var task=Task.Run(()=>batch.RunAsync(_=>{}));Check(factory.Entered.Wait(TimeSpan.FromSeconds(10)),"实际执行未进入输入准备");var unload=production.UnloadPluginsAsync();Thread.Sleep(100);Check(!unload.IsCompleted,"活跃执行未阻止卸载");factory.Release.Set();
  using(var result=Wait(task))Check(result.Workflows.Values.All(item=>item.Status!=ExecutionStatus.Success),"退休期间实际执行报成功");batch.Dispose();manager.CloseCurrent(true);Wait(unload);Console.WriteLine("probe: active production invocation cancelled and drained before unload");
 }
 private static void DisplayAndDebug(PluginManager production,string imageFile)
 {
  using var manager=new DocumentSessionManager(production.RuntimeDirectory!);using var factory=new ImageSourceHostInputFactory();var session=manager.CreateNew("显示与调试");var flow=session.GetWorkflows().Single();var source=Add(session,flow,"ImageLoad");var blob=Add(session,flow,"BlobAnalysis");ConfigureImage(session,flow,source,imageFile);
  var schema=session.GetNodeSchema(flow.Id,source.Id)!;var port=schema.Outputs.Single(p=>p.Id=="output.image");var image=new OutputReference(source.Id,port.Id,port.Type,new[]{"data"},scopeId:flow.Id);var width=new OutputReference(source.Id,port.Id,port.Type,new[]{"width"},scopeId:flow.Id);
  var graph=session.PrepareGraphEdit(flow.Id);graph.AddConnection(new ConnectionDefinition(Guid.NewGuid().ToString("N"),source.Id,blob.Id,"BottomPort","TopPort"));session.CommitGraphEdit(graph,"图像源到Blob");
  Edit(session,flow,blob,new(){["input.image"]=new ReferenceSetting(image),["display.text.content"]=new DisplayExpressionSetting("width={w}",new[]{new DisplayExpressionSlot("w",width)})});
  var reader=Run(session,flow,blob,factory);Check(reader.DisplaySources?.CaptureResults().Any(item=>item.Node.NodeId==source.Id)==true,"未捕获自动显示来源");var appearance=session.GetNodeResultPresentation(flow.Id,blob).Appearance!.Text;
  var text=new ResultDisplayTextLease(reader.Snapshot,appearance);Check(text.Evaluate(0).Content=="width=8","自动上游显示值异常");Console.WriteLine("probe: automatic display source and upstream width=8 passed");
  using(var scope=session.OpenDebugSession(flow.Id,blob))
  using(var invocation=scope.Capture(session.Editing.Host.BeginEdit(flow.Id,blob.Id),null,_=>{},hostFactory:factory))
  using(var result=Wait(Task.Run(invocation.ExecuteAsync)))
  {Check(result.IsSuccess,result.ErrorMessage??"私有调试失败");using var debugText=new ResultDisplayTextLease(result,appearance);Check(debugText.Evaluate(0).Content=="width=8","调试上游显示异常");invocation.AcceptParameters();Console.WriteLine("probe: runtime debug execution and upstream width=8 passed");}
  reader.Dispose();manager.CloseCurrent(true);var unload=production.UnloadPluginsAsync();Thread.Sleep(100);Check(!unload.IsCompleted,"跨节点text未持退休代");Console.WriteLine("probe: cross-node text reader retained retiring generation");text.Dispose();Wait(unload);
 }
 private sealed class BlockingFactory(IHostInputFactory actual):IHostInputFactory,IDisposable
 {
  internal readonly ManualResetEventSlim Entered=new(),Release=new();
  public IHostInputPreparer? Create(HostInputFactoryContext context)=>new Block(actual.Create(context)!,this);
  public void Dispose(){Release.Set();Entered.Dispose();Release.Dispose();}
  private sealed class Block(IHostInputPreparer inner,BlockingFactory owner):IHostInputPreparer
  {public string CapabilityId=>inner.CapabilityId;public int CapabilityVersion=>inner.CapabilityVersion;public IReadOnlyList<string> Services=>inner.Services;public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context){owner.Entered.Set();if(!owner.Release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("未释放实际宿主输入");return await inner.PrepareAsync(context);}public void Dispose()=>inner.Dispose();}
 }
}
