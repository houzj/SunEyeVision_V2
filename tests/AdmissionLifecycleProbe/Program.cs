using System.IO;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

var root=AppContext.BaseDirectory;
var manager=new PluginManager();
manager.LoadPlugins();
if(manager.LastAdmissionError is not null)throw new Exception("Initial admission failed: "+manager.LastAdmissionError);
var previous=manager.RuntimeDirectory??throw new Exception("Missing admitted directory.");
var oldDirectory=ToolRegistry.DirectoryCatalog??throw new Exception("Missing desktop directory.");
var missing=Path.Combine(Path.GetTempPath(),"suneye-missing-payload-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(missing);
try
{
    AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",missing+Path.DirectorySeparatorChar);
    manager.LoadPlugins();
    if(manager.LastAdmissionError is null)throw new Exception("Missing payload was accepted.");
    if(!ReferenceEquals(previous,manager.RuntimeDirectory) || !ReferenceEquals(oldDirectory,ToolRegistry.DirectoryCatalog))
        throw new Exception("Failed reload replaced or cleared the prior complete generation.");
}
finally
{
    AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",root);
    Directory.Delete(missing);
}
manager.LoadPlugins();
if(manager.LastAdmissionError is not null || manager.RuntimeDirectory is null || ReferenceEquals(previous,manager.RuntimeDirectory))
    throw new Exception("Re-admission did not publish a new generation: "+manager.LastAdmissionError);
Parallel.Invoke(manager.LoadPlugins,manager.UnloadPlugins);
manager.LoadPlugins();
if(manager.LastAdmissionError is not null || manager.RuntimeDirectory is null || ToolRegistry.DirectoryCatalog is null)
    throw new Exception("Final admission after competing operations failed: "+manager.LastAdmissionError);
manager.UnloadPlugins();
if(manager.RuntimeDirectory is not null || ToolRegistry.DirectoryCatalog is not null)throw new Exception("Unload left published state.");
Console.WriteLine("Desktop admission failed-reload/recovery and load/unload serialization passed; no active-run drain claimed.");
