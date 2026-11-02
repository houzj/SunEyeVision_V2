using System.IO;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Workflow;

var root=AppContext.BaseDirectory;
var manager=new PluginManager();
Console.WriteLine("probe: initial admission");
await manager.LoadPluginsAsync();
if(manager.LastAdmissionError is not null)throw new Exception("Initial admission failed: "+manager.LastAdmissionError);
var previous=manager.RuntimeDirectory??throw new Exception("Missing admitted directory.");
var oldDirectory=ToolRegistry.DirectoryCatalog??throw new Exception("Missing desktop directory.");
var nonOwner=new PluginManager();
using(var hostPin=manager.PinDesktopHost())
{
    await nonOwner.LoadPluginsAsync();
    if(nonOwner.LastAdmissionError is null || !ReferenceEquals(previous,manager.RuntimeDirectory))
        throw new Exception("A pinned desktop workspace allowed another manager to replace its generation.");
    try{await manager.UnloadPluginsAsync();throw new Exception("A pinned desktop workspace allowed its owner to unload.");}
    catch(InvalidOperationException){}
}
Console.WriteLine("probe: non-owner unload and failed reload");
await nonOwner.UnloadPluginsAsync();
if(!ReferenceEquals(previous,manager.RuntimeDirectory))throw new Exception("A manager without ownership cleared the active generation.");
var missing=Path.Combine(Path.GetTempPath(),"suneye-missing-payload-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(missing);
try
{
    AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",missing+Path.DirectorySeparatorChar);
    await manager.LoadPluginsAsync();
    if(manager.LastAdmissionError is null)throw new Exception("Missing payload was accepted.");
    if(!ReferenceEquals(previous,manager.RuntimeDirectory) || !ReferenceEquals(oldDirectory,ToolRegistry.DirectoryCatalog))
        throw new Exception("Failed reload replaced or cleared the prior complete generation.");
}
finally
{
    AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",root);
    Directory.Delete(missing);
}
await manager.LoadPluginsAsync();
Console.WriteLine("probe: recovered admission");
if(manager.LastAdmissionError is not null || manager.RuntimeDirectory is null || ReferenceEquals(previous,manager.RuntimeDirectory))
    throw new Exception("Re-admission did not publish a new generation: "+manager.LastAdmissionError);
var oldOwnerGeneration=manager.RuntimeDirectory;
await nonOwner.LoadPluginsAsync();
var newer=nonOwner.RuntimeDirectory;
if(nonOwner.LastAdmissionError is not null || newer is null || ReferenceEquals(oldOwnerGeneration,newer))throw new Exception("Second manager failed to publish its generation.");
await manager.UnloadPluginsAsync();
if(!ReferenceEquals(newer,nonOwner.RuntimeDirectory))throw new Exception("Old owner cleared a newer generation.");
await Task.WhenAll(manager.LoadPluginsAsync(),manager.UnloadPluginsAsync());
Console.WriteLine("probe: concurrent load and unload completed");
await manager.LoadPluginsAsync();
if(manager.LastAdmissionError is not null || manager.RuntimeDirectory is null || ToolRegistry.DirectoryCatalog is null)
    throw new Exception("Final admission after competing operations failed: "+manager.LastAdmissionError);
await manager.UnloadPluginsAsync();
if(manager.RuntimeDirectory is not null || ToolRegistry.DirectoryCatalog is not null)throw new Exception("Unload left published state.");
// A plugin/catalog preparation callback waits for another manager to unload. This
// must complete without holding the publication gate across untrusted preparation.
var helper=new PluginManager();
Console.WriteLine("probe: reentrant preparation");
var reentrant=new PluginManager(new NullLogger(),new CallbackCatalog(()=>
{
    if(!Task.Run(helper.UnloadPluginsAsync).Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Preparation blocked on a reentrant unload.");
}));
await reentrant.LoadPluginsAsync();
if(reentrant.LastAdmissionError is not null || reentrant.RuntimeDirectory is null)
    throw new Exception("Reentrant preparation failed: "+reentrant.LastAdmissionError);
reentrant.AssertCompleteReleaseToolSet(new ReentrantIds(()=>
{
    if(!Task.Run(helper.UnloadPluginsAsync).Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Enumeration held the admission gate.");
}));
var metadataFailures=new System.Collections.Concurrent.ConcurrentQueue<string>();
Parallel.Invoke(
    ()=> { for(var i=0;i<1000;i++) { var ids=ToolRegistry.GetAllToolMetadata().Select(x=>x.Id).OrderBy(x=>x).ToArray(); if(ids.Length!=0 && !ids.SequenceEqual(new[]{"BlobAnalysis","ImageLoad"}))metadataFailures.Enqueue(string.Join(",",ids)); } },
    ()=> { reentrant.UnloadPluginsAsync().GetAwaiter().GetResult(); helper.LoadPluginsAsync().GetAwaiter().GetResult(); });
if(!metadataFailures.IsEmpty)throw new Exception("Mixed metadata generation: "+string.Join(";",metadataFailures));
// Establish an ordinary owner for the stale-preparation case.
var staleOwner=new PluginManager();
await staleOwner.LoadPluginsAsync();
if(staleOwner.LastAdmissionError is not null)throw new Exception("Could not restore test owner.");
var retained=staleOwner.RuntimeDirectory??throw new Exception("Missing generation for drain check.");
using(var lease=retained.AcquireInvocation())
{
    var unload=staleOwner.UnloadPluginsAsync();
    await Task.Delay(100);
    if(unload.IsCompleted)throw new Exception("Unload completed before the active lease was released.");
    if(!lease.RetirementToken.IsCancellationRequested)throw new Exception("Retirement did not signal an active invocation.");
    try{using var rejected=retained.AcquireInvocation();throw new Exception("Retired generation accepted a new invocation.");}
    catch(InvalidOperationException){}
    lease.Dispose();
    await unload.WaitAsync(TimeSpan.FromSeconds(10));
}
await staleOwner.LoadPluginsAsync();
if(staleOwner.LastAdmissionError is not null)throw new Exception("Could not restore owner after drain check.");
var readerDirectory=staleOwner.RuntimeDirectory??throw new Exception("Missing generation for reader check.");
var execution=new ToolExecutionOwner().Begin("scope/source",Guid.NewGuid(),"5.0");
var output=DeclaredOutputSnapshot.Capture(new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>()),0,
    execution,"source",new ToolOutcome(true,new EmptyOutput(),BusinessJudgment.Pass,Array.Empty<Diagnostic>()),
    readerDirectory.AcquireInvocation(),0);
var reader=output.AcquireReader();
output.Dispose();
var readerUnload=staleOwner.UnloadPluginsAsync();
await Task.Delay(100);
if(readerUnload.IsCompleted)throw new Exception("Unload completed while a result reader retained its generation.");
reader.Dispose();
await readerUnload.WaitAsync(TimeSpan.FromSeconds(10));
await staleOwner.LoadPluginsAsync();
if(staleOwner.LastAdmissionError is not null)throw new Exception("Could not restore owner after reader check.");
var callbackGeneration=staleOwner.RuntimeDirectory??throw new Exception("Missing generation for callback failure check.");
var callbackLease=callbackGeneration.AcquireInvocation();
using var badCallback=callbackLease.RetirementToken.Register(()=>throw new InvalidOperationException("injected callback failure"));
callbackLease.Dispose();
await staleOwner.LoadPluginsAsync();
if(staleOwner.LastAdmissionError is not null || staleOwner.RuntimeDirectory is null || ReferenceEquals(callbackGeneration,staleOwner.RuntimeDirectory))
    throw new Exception("Published generation was reported rejected after a retirement callback failed.");
// A genuine owner unload during preparation invalidates the stale candidate.
var stale=new PluginManager(new NullLogger(),new CallbackCatalog(()=>
{
    if(!Task.Run(staleOwner.UnloadPluginsAsync).Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Owner unload blocked during preparation.");
}));
await stale.LoadPluginsAsync();
if(stale.LastAdmissionError is null || ToolRegistry.DirectoryCatalog is not null)
    throw new Exception("Stale prepared admission was published after owner unload.");
Console.WriteLine("probe: production workflow result reader");
var production=new PluginManager();
await production.LoadPluginsAsync();
if(production.LastAdmissionError is not null)throw new Exception("Production admission failed: "+production.LastAdmissionError);
var productionDirectory=production.RuntimeDirectory??throw new Exception("Production directory missing.");
// Tool admission guard: no unadmitted SDK, paused control flow or foreign generation can run.
void MustReject(Action action,string scenario)
{
    try { action(); } catch (InvalidOperationException) { return; }
    throw new Exception("Guard did not reject "+scenario);
}
var declaredImage = RuntimeToolConfiguration.Create(productionDirectory,"ImageLoad");
var imageNode = new WorkflowNodeBase("good","good","good","ImageLoad") { RuntimeConfiguration=declaredImage };
RuntimeNodeAdmission.Require(productionDirectory,imageNode);
MustReject(()=>RuntimeNodeAdmission.Require(productionDirectory,new WorkflowNodeBase("missing","missing","missing","UnknownTool")),"unknown tool");
MustReject(()=>RuntimeNodeAdmission.Require(productionDirectory,new WorkflowNodeBase("empty","empty","empty","ImageLoad")),"missing runtime configuration");
MustReject(()=>RuntimeNodeAdmission.Require(productionDirectory,new WorkflowNodeBase("bad-id","bad-id","bad-id","BlobAnalysis") { RuntimeConfiguration=declaredImage }),"tool identity mismatch");
foreach(var id in new[]{"Subroutine","Condition"})
    MustReject(()=>RuntimeNodeAdmission.Require(productionDirectory,new WorkflowNodeBase(id,id,id,id)),"paused control flow "+id);
MustReject(()=>RuntimeNodeAdmission.Require(productionDirectory,new WorkflowNodeBase("linked","linked","linked","ImageLoad") { RuntimeConfiguration=declaredImage, SubroutineId="child" }),"subroutine identity");
MustReject(()=>new WorkflowNodeBase("legacy","legacy","legacy","UnknownTool").CreateInstance(),"legacy SDK instance");
MustReject(()=>WorkflowNodeFactory.CreateAlgorithmNode(productionDirectory,"UnknownTool","legacy","legacy","legacy"),"legacy SDK node factory");
using(var eventBus=new SunEyeVision.Core.Events.EventBus(new NullLogger()))
{
    var oldEventPath=new EventEnabledWorkflow("old","old",eventBus);
    if(oldEventPath.ExecuteNode(new WorkflowNodeBase("old","old","old","UnknownTool"),null).Success)
        throw new Exception("Legacy event-based node execution reported success.");
}
var rejectedFlow=new SunEyeVision.Workflow.Workflow("rejected","rejected");
rejectedFlow.Nodes.Add(new WorkflowNodeBase("bad","bad","bad","UnknownTool") { Parameters=new GenericToolParameters() });
var rejectedEngine=new WorkflowEngine(new NullLogger(),productionDirectory);rejectedEngine.RegisterWorkflow(rejectedFlow);
var rejectedRunner=new WorkflowExecutionEngine(rejectedEngine,production,new NullLogger());
bool guardRejected=false;
try { using var invalidRun=await rejectedRunner.ExecuteWorkflowAsync(new WorkflowRunRequest(rejectedFlow.Id)); guardRejected=!invalidRun.Success; }
catch(InvalidOperationException) { guardRejected=true; }
if(!guardRejected)throw new Exception("Unadmitted workflow unexpectedly executed.");
MustReject(()=>RuntimeDebugInvocation.Capture(rejectedFlow,productionDirectory,rejectedFlow.Nodes[0],new ToolExecutionOwner(),0,_=>null,CancellationToken.None),"unadmitted debug");
Console.WriteLine("probe: tool admission, paused control flow, legacy factory, workflow and debug rejection passed");
Exception? uiError=null;
var uiThread=new Thread(()=>
{
    try
    {
        var chain=new SunEyeVision.UI.Services.Editing.NodeOpenDecisionChain();
        bool created=false;
        foreach(var rejectedNode in new[]{new WorkflowNodeBase("missing","missing","missing","UnknownTool"),new WorkflowNodeBase("paused","paused","paused","Subroutine") { SubroutineId="child" },new WorkflowNodeBase("mismatch","mismatch","mismatch","BlobAnalysis") { RuntimeConfiguration=declaredImage }})
        {
            var context=new SunEyeVision.UI.Services.Editing.NodeOpenContext { RuntimeDirectory=productionDirectory };
            if(chain.Execute(rejectedNode,context,()=>{created=true;return new System.Windows.Controls.UserControl();}) || created || context.CreatedWindow is not null)
                throw new Exception("Unadmitted node opened or constructed an editor.");
        }
        imageNode.DebugWindowStyle=DebugWindowStyle.None;
        if(!chain.Execute(imageNode,new SunEyeVision.UI.Services.Editing.NodeOpenContext { RuntimeDirectory=productionDirectory }))
            throw new Exception("Admitted Runtime node was rejected by open decision chain.");
    }
    catch(Exception error){uiError=error;}
});
uiThread.SetApartmentState(ApartmentState.STA);uiThread.Start();uiThread.Join();
if(uiError is not null)throw new Exception("Node open admission test failed.",uiError);
Console.WriteLine("probe: admitted open and unadmitted/paused open rejection passed");
var guardDir=Path.Combine(AppContext.BaseDirectory,"tool-guard-case-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(guardDir);
try
{
    var solutionManager=new SolutionManager(guardDir,productionDirectory);
    var document=solutionManager.CreateUntitledSolution();
    document.Workflows[0].Nodes.Add(new WorkflowNodeBase("source","source","source","ImageLoad") { RuntimeConfiguration=declaredImage });
    var repo=new SolutionRepository(Path.Combine(guardDir,"recovery"),productionDirectory);
    var validPath=Path.Combine(guardDir,"valid.solution");
    if(!repo.Save(document,validPath))throw new Exception("Admitted solution save failed: "+repo.LastError);
    if(repo.Load(validPath) is null)throw new Exception("Admitted solution reopen failed: "+repo.LastError);
    var original=File.ReadAllBytes(validPath); var before=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original));
    document.Workflows[0].Nodes[0].ToolType="UnknownTool";
    if(repo.Save(document,validPath))throw new Exception("Unadmitted solution was saved over the source.");
    if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(validPath)))!=before)throw new Exception("Rejected save changed source bytes.");
    foreach(var unavailable in new[]{"UnknownTool","Subroutine","Condition"})
    {
        var raw=System.Text.Json.Nodes.JsonNode.Parse(original)!;
        raw["Workflows"]![0]!["Nodes"]![0]!["ToolType"]=unavailable;
        var candidatePath=Path.Combine(guardDir,unavailable+".solution");File.WriteAllText(candidatePath,raw.ToJsonString());
        var checksum=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(candidatePath)));
        var candidate=repo.LoadCandidate(candidatePath);
        if(candidate.Candidate is not null)throw new Exception("Unadmitted solution opened: "+unavailable);
        if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(candidatePath)))!=checksum)throw new Exception("Rejected open changed source: "+unavailable);
    }
    Console.WriteLine("probe: admitted save/reopen and missing/paused rejected open/save preserve bytes passed");
}
finally { var resolved=Path.GetFullPath(guardDir); var allowed=Path.GetFullPath(AppContext.BaseDirectory); if(!resolved.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("测试目录不在候选 Release 中。"); Directory.Delete(resolved,true); }
var imageFile=Path.Combine(Path.GetTempPath(),"suneye-workflow-probe-"+Guid.NewGuid().ToString("N")+".bmp");
try
{
    using(var writer=new BinaryWriter(File.Create(imageFile)))
    {
        writer.Write((ushort)0x4d42);writer.Write(54+8*8*3);writer.Write(0);writer.Write(54);writer.Write(40);writer.Write(8);writer.Write(8);writer.Write((ushort)1);writer.Write((ushort)24);writer.Write(0);writer.Write(8*8*3);for(int i=0;i<4;i++)writer.Write(0);
        for(int y=0;y<8;y++)for(int x=0;x<8;x++){byte pixel=(byte)(x>=2&&x<6&&y>=2&&y<6?255:0);writer.Write(pixel);writer.Write(pixel);writer.Write(pixel);}
    }
    var flow=new SunEyeVision.Workflow.Workflow("admission-probe","admission-probe");
    flow.Nodes.Add(new WorkflowNodeBase("image","image","image","ImageLoad") {RuntimeConfiguration=RuntimeToolConfiguration.Create(productionDirectory,"ImageLoad")});
    var engine=new WorkflowEngine(new NullLogger(),productionDirectory);engine.RegisterWorkflow(flow);
    var runner=new WorkflowExecutionEngine(engine,production,new NullLogger());
    var request=new WorkflowRunRequest(flow.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{"image",new(imageFile,1){LocalReadAdmission=_=>imageFile}}});
    var run=await runner.ExecuteWorkflowAsync(request).WaitAsync(TimeSpan.FromSeconds(30));
    if(!run.Success)throw new Exception("Production workflow failed: "+string.Join(";",run.Errors));
    var result=run.DeclaredOutputs.Single(o=>o.Identity.NodeId=="image");
    var productionReader=run.AcquireDeclaredOutput(result.Identity);
    using var displaySources=new ResultDisplaySourceSet(new[]{result});
    var displayLease=displaySources.Acquire();
    var textLease=new ResultDisplayTextLease(result,new ResultTextAppearance(true,"probe",new ResultDisplayColor(255,255,255),new ResultDisplayColor(255,0,0),12,1,0,0));
    if(textLease.Evaluate(0).Content!="probe")throw new Exception("Display text lease did not evaluate its frozen result.");
    displaySources.Dispose();
    run.Dispose();
    var productionUnload=production.UnloadPluginsAsync();
    await Task.Delay(100);
    if(productionUnload.IsCompleted)throw new Exception("Production result reader did not hold generation open.");
    productionReader.Dispose();
    await Task.Delay(100);
    if(productionUnload.IsCompleted)throw new Exception("Display source reader did not hold generation open.");
    displayLease.Dispose();
    await Task.Delay(100);
    if(productionUnload.IsCompleted)throw new Exception("Display text reader did not hold generation open.");
    textLease.Dispose();
    await productionUnload.WaitAsync(TimeSpan.FromSeconds(10));
    await production.LoadPluginsAsync();
    if(production.LastAdmissionError is not null)throw new Exception("Production re-admission failed.");
    var activeDirectory=production.RuntimeDirectory??throw new Exception("Active generation missing.");
    MustReject(()=>RuntimeNodeAdmission.Require(activeDirectory,imageNode),"foreign/retired directory generation");
    MustReject(()=>RuntimeDebugInvocation.Capture(flow,activeDirectory,flow.Nodes[0],new ToolExecutionOwner(),0,_=>null,CancellationToken.None),"foreign generation debug");
    var foreignEngine=new WorkflowEngine(new NullLogger(),activeDirectory); foreignEngine.RegisterWorkflow(flow);
    var foreignRunner=new WorkflowExecutionEngine(foreignEngine,production,new NullLogger());
    bool rejectedForeign=false;
    try { using var foreign=await foreignRunner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id)); rejectedForeign=!foreign.Success; }
    catch(InvalidOperationException) { rejectedForeign=true; }
    if(!rejectedForeign)throw new Exception("Workflow accepted an old generation configuration.");
    Console.WriteLine("probe: old runtime generation rejected for execution and debug admission");
    var activeFlow=new SunEyeVision.Workflow.Workflow("active-probe","active-probe");
    activeFlow.Nodes.Add(new WorkflowNodeBase("image","image","image","ImageLoad") {RuntimeConfiguration=RuntimeToolConfiguration.Create(activeDirectory,"ImageLoad")});
    var activeEngine=new WorkflowEngine(new NullLogger(),activeDirectory);activeEngine.RegisterWorkflow(activeFlow);
    var activeRunner=new WorkflowExecutionEngine(activeEngine,production,new NullLogger());
    using var entered=new ManualResetEventSlim();
    using var release=new ManualResetEventSlim();
    var activeRequest=new WorkflowRunRequest(activeFlow.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>
    {{"image",new(imageFile,1){LocalReadAdmission=_=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("Probe did not release active image read.");return imageFile;}}}});
    var activeRun=Task.Run(()=>activeRunner.ExecuteWorkflowAsync(activeRequest));
    if(!entered.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("Production workflow did not enter image read.");
    var activeUnload=production.UnloadPluginsAsync();
    await Task.Delay(100);
    if(activeUnload.IsCompleted)throw new Exception("Unload completed while production workflow was executing.");
    release.Set();
    using(var completed=await activeRun.WaitAsync(TimeSpan.FromSeconds(10)))
        if(completed.Success)throw new Exception("A workflow retired during execution reported success.");
    await activeUnload.WaitAsync(TimeSpan.FromSeconds(10));
    await production.LoadPluginsAsync();
    if(production.LastAdmissionError is not null)throw new Exception("Display generation admission failed.");
    var displayDirectory=production.RuntimeDirectory??throw new Exception("Display generation missing.");
    var imageConfig=RuntimeToolConfiguration.Create(displayDirectory,"ImageLoad");
    var sourcePort=imageConfig.Schema.Outputs.Single(port=>port.Id=="output.image");
    var imageRef=new OutputReference("image",sourcePort.Id,sourcePort.Type,new[]{"data"},scopeId:"display-probe");
    var widthRef=new OutputReference("image",sourcePort.Id,sourcePort.Type,new[]{"width"},scopeId:"display-probe");
    var blobConfig=RuntimeToolConfiguration.Create(displayDirectory,"BlobAnalysis").WithFieldSettings(new Dictionary<string,ParameterSetting>
    {
        ["input.image"]=new ReferenceSetting(imageRef),
        ["display.text.content"]=new DisplayExpressionSetting("width={w}",new[]{new DisplayExpressionSlot("w",widthRef)})
    });
    var displayFlow=new SunEyeVision.Workflow.Workflow("display-probe","display-probe");
    displayFlow.Nodes.Add(new WorkflowNodeBase("image","image","image","ImageLoad"){RuntimeConfiguration=imageConfig});
    displayFlow.Nodes.Add(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){RuntimeConfiguration=blobConfig});
    displayFlow.Connections.Add(new WorkflowConnection{Id="edge",SourceNodeId="image",TargetNodeId="blob"});
    var displayEngine=new WorkflowEngine(new NullLogger(),displayDirectory);displayEngine.RegisterWorkflow(displayFlow);
    var displayRunner=new WorkflowExecutionEngine(displayEngine,production,new NullLogger());
    var displayRequest=new WorkflowRunRequest(displayFlow.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{"image",new(imageFile,1){LocalReadAdmission=_=>imageFile}}});
    var displayRun=await displayRunner.ExecuteWorkflowAsync(displayRequest).WaitAsync(TimeSpan.FromSeconds(30));
    if(!displayRun.Success)throw new Exception("Automatic display workflow failed: "+string.Join(";",displayRun.Errors));
    var blobResult=displayRun.DeclaredOutputs.Single(o=>o.Identity.NodeId=="blob");
    using(var blobReader=blobResult.AcquireReader())
        if(blobReader.DisplaySources?.CaptureResults().Any(item=>item.Node.NodeId=="image")!=true)throw new Exception("Workflow did not capture the automatic display source.");
    var automaticText=new ResultDisplayTextLease(blobResult,blobConfig.CaptureResultDisplay()!.Text);
    if(automaticText.Evaluate(0).Content!="width=8")throw new Exception("Cross-node display expression resolved an unexpected value.");
    Console.WriteLine("probe: automatic display source and upstream width=8 passed");
    DeclaredOutputSnapshot? Published(string id)=>displayRun.DeclaredOutputs.SingleOrDefault(output=>output.Identity.NodeId==id);
    using(var debug=RuntimeDebugInvocation.Capture(displayFlow,displayDirectory,displayFlow.Nodes.Single(node=>node.Id=="blob"),new ToolExecutionOwner(),0,
        Published,CancellationToken.None,publishedOwner:displayFlow.ExecutionOwner,readDisplayCandidate:Published))
    using(var debugResult=await debug.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30)))
    {
        if(!debugResult.IsSuccess)throw new Exception("Debug execution failed: "+debugResult.ErrorMessage);
        using var debugText=new ResultDisplayTextLease(debugResult,blobConfig.CaptureResultDisplay()!.Text);
        if(debugText.Evaluate(0).Content!="width=8")throw new Exception("Debug display expression resolved an unexpected value.");
        Console.WriteLine("probe: runtime debug execution and upstream width=8 passed");
    }
    displayRun.Dispose();
    var displayUnload=production.UnloadPluginsAsync();
    await Task.Delay(100);
    if(displayUnload.IsCompleted)throw new Exception("Cross-node text view did not retain its generation.");
    Console.WriteLine("probe: cross-node text reader retained retiring generation");
    automaticText.Dispose();
    await displayUnload.WaitAsync(TimeSpan.FromSeconds(10));
}
finally{File.Delete(imageFile);}
Console.WriteLine("Desktop admission and production workflow reader drain checks passed; desktop window shutdown remains unverified.");

sealed class EmptyOutput:IToolOutput
{
    public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>();
}

sealed class CallbackCatalog(Action callback):IToolCatalog
{
    private readonly ToolRegistryCatalog _inner=new();
    public int Count=>_inner.Count;
    public void Register(Type type)=>_inner.Register(type);
    public IPreparedToolRegistration PrepareRegistration(IReadOnlyList<Type> types)=>_inner.PrepareRegistration(types);
    public IPreparedToolRegistration PrepareRegistration(IReadOnlyList<Type> types,IReadOnlyList<ToolComponentRegistration> components)
    {callback();return _inner.PrepareRegistration(types,components);}
    public void Clear()=>_inner.Clear();
    public bool Contains(string id)=>_inner.Contains(id);
    public IToolPlugin? CreateTool(string id)=>_inner.CreateTool(id);
    public ToolParameters CreateParameters(string id)=>_inner.CreateParameters(id);
    public ToolMetadata? GetMetadata(string id)=>_inner.GetMetadata(id);
    public IReadOnlyList<ToolMetadata> GetAllMetadata()=>_inner.GetAllMetadata();
}

sealed class ReentrantIds(Action callback):IEnumerable<string>
{
    public IEnumerator<string> GetEnumerator()
    {
        callback();
        yield return "BlobAnalysis";
        yield return "ImageLoad";
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
}
