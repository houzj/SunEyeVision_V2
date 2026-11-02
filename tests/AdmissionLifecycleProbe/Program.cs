using System.IO;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

var root=AppContext.BaseDirectory;
var manager=new PluginManager();
Console.WriteLine("probe: initial admission");
await manager.LoadPluginsAsync();
if(manager.LastAdmissionError is not null)throw new Exception("Initial admission failed: "+manager.LastAdmissionError);
var previous=manager.RuntimeDirectory??throw new Exception("Missing admitted directory.");
var oldDirectory=ToolRegistry.DirectoryCatalog??throw new Exception("Missing desktop directory.");
var nonOwner=new PluginManager();
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
// A genuine owner unload during preparation invalidates the stale candidate.
var stale=new PluginManager(new NullLogger(),new CallbackCatalog(()=>
{
    if(!Task.Run(staleOwner.UnloadPluginsAsync).Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Owner unload blocked during preparation.");
}));
await stale.LoadPluginsAsync();
if(stale.LastAdmissionError is null || ToolRegistry.DirectoryCatalog is not null)
    throw new Exception("Stale prepared admission was published after owner unload.");
Console.WriteLine("Desktop admission owner, failed-reload, recovery, reentrancy and stale-preparation checks passed; no active-run drain claimed.");

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
