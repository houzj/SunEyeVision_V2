using System.IO;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;



internal static class Program
{
[STAThread]
private static int Main(string[] args)
{
    ProbeErrorMode.DisableSystemDialogs();
    try
    {
        var payload=Environment.GetEnvironmentVariable("SUNEYE_INFRA_PRODUCT_ROOT");
        if(string.IsNullOrWhiteSpace(payload)||!Path.IsPathFullyQualified(payload)||!Directory.Exists(payload))throw new InvalidOperationException("必须提供本工作区正式封包目录 SUNEYE_INFRA_PRODUCT_ROOT。");
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.TrimEndingDirectorySeparator(Path.GetFullPath(payload))+Path.DirectorySeparatorChar);
        if (args.Contains("--hidden-node-window")) return NodeWindowProbe.Run();
        return RunAsync().GetAwaiter().GetResult();
    }
    catch(Exception error) { Console.Error.WriteLine(error); return 1; }
}

private static async Task<int> RunAsync()
{
try
{
var root=AppContext.BaseDirectory;
var manager=new PluginManager();
Console.WriteLine("probe: initial admission");
await manager.LoadPluginsAsync();
if(manager.LastAdmissionError is not null)throw new Exception("Initial admission failed: "+manager.LastAdmissionError);
var previous=manager.RuntimeDirectory??throw new Exception("Missing admitted directory.");
var oldAdmission=manager.DesktopAdmission??throw new Exception("Missing desktop directory."); var oldDirectory=oldAdmission.Directory;
var admittedIds=previous.ToolIds.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
if(!admittedIds.SequenceEqual(new[]{"BlobAnalysis","ImageLoad"}))throw new Exception("Unexpected admitted Runtime identities.");
var metadata=oldDirectory.Tools.ToArray();
if(metadata.Select(m=>m.Id).Distinct(StringComparer.Ordinal).Count()!=metadata.Length || !metadata.Select(m=>m.Id).OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(admittedIds))
    throw new Exception("Registry metadata does not match the admitted generation.");
foreach(var id in admittedIds)
{
    var definition=previous.CreateDefinition(id);var schema=definition.Describe();var catalog=previous.GetCatalog(id)??throw new Exception("Missing admitted catalog: "+id);
    if(definition.ToolId!=id||schema.Inputs.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=schema.Inputs.Count||schema.Outputs.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=schema.Outputs.Count||string.IsNullOrWhiteSpace(catalog.DisplayName))
        throw new Exception("Invalid admitted definition/schema/catalog: "+id);
}
Console.WriteLine("probe: admitted runtime metadata and schema identities are unique");
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
    if(!ReferenceEquals(previous,manager.RuntimeDirectory) || !ReferenceEquals(oldAdmission,manager.DesktopAdmission))
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
if(manager.LastAdmissionError is not null || manager.RuntimeDirectory is null || manager.DesktopAdmission is null)
    throw new Exception("Final admission after competing operations failed: "+manager.LastAdmissionError);
await manager.UnloadPluginsAsync();
if(manager.RuntimeDirectory is not null || manager.DesktopAdmission is not null)throw new Exception("Unload left published state.");
// A real generation retirement callback reenters another manager, outside the publication lock.
var callbackOwner=new PluginManager();
await callbackOwner.LoadPluginsAsync();
var callbackDirectory=callbackOwner.RuntimeDirectory??throw new Exception("Missing callback generation.");
using(var callbackPin=callbackDirectory.AcquireInvocation())
{
    var callbackHelper=new PluginManager();
    using var callback=callbackPin.RetirementToken.Register(()=>
    {
        if(!Task.Run(callbackHelper.UnloadPluginsAsync).Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Retirement callback held the publication gate.");
    });
    var pending=callbackOwner.UnloadPluginsAsync();
    await Task.Delay(100);
    callbackPin.Dispose();
    await pending.WaitAsync(TimeSpan.FromSeconds(10));
}
Console.WriteLine("probe: retirement callback reentered another manager");
var metadataOwner=new PluginManager();
await metadataOwner.LoadPluginsAsync();
var metadataFailures=new System.Collections.Concurrent.ConcurrentQueue<string>();
Parallel.Invoke(
    () => { for (var i=0;i<1000;i++) { var admission=metadataOwner.DesktopAdmission; if (admission is null) continue;
        var ids=admission.Directory.Tools.Select(item=>item.Id).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        if (!ids.SequenceEqual(new[]{"BlobAnalysis","ImageLoad"})) metadataFailures.Enqueue(string.Join(",",ids)); } },
    () => { metadataOwner.UnloadPluginsAsync().GetAwaiter().GetResult(); new PluginManager().LoadPluginsAsync().GetAwaiter().GetResult(); });
if (!metadataFailures.IsEmpty) throw new Exception("Mixed metadata generation: "+string.Join(";",metadataFailures));
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
await staleOwner.UnloadPluginsAsync();
Console.WriteLine("probe: production workflow result reader");
var production=new PluginManager();
await production.LoadPluginsAsync();
if(production.LastAdmissionError is not null)throw new Exception("Production admission failed: "+production.LastAdmissionError);
var productionDirectory=production.RuntimeDirectory??throw new Exception("Production directory missing.");
SessionLifecycleProbe.Run(production);
Console.WriteLine("Desktop admission and production workflow reader drain checks passed; hidden window mode is verified by a separate bounded child.");
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}
return 0;
}
}

static class ProbeErrorMode
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    public static void DisableSystemDialogs() => SetErrorMode(0x0001 | 0x0002 | 0x8000);
}

sealed class EmptyOutput:IToolOutput
{
    public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>();
}
