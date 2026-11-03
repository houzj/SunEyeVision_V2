using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

ProbeErrorMode.DisableSystemDialogs();
try
{
if(typeof(RuntimeDirectorySnapshot).GetConstructors().Length!=0 ||
    typeof(RuntimeDirectorySnapshot).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Any(m=>m.Name=="Prepare"))
    throw new Exception("A caller can construct a directory without release admission.");
var authority=new ReleasePayloadAuthority(args.Length==0?AppContext.BaseDirectory:Path.GetFullPath(args[0]));
try
{
    ((IDictionary<string,string>)authority.ExpectedTools).Clear();
    throw new Exception("Expected tool set accepted caller modification.");
}
catch(NotSupportedException) { }
var directory=authority.PrepareRuntimeDirectory();
if(!directory.ToolIds.OrderBy(x=>x).SequenceEqual(new[]{"BlobAnalysis","ImageLoad"}))throw new Exception("Unexpected runtime tool set.");
var source=directory.CreateDefinition("ImageLoad");
var blob=directory.CreateDefinition("BlobAnalysis");
var path=Path.Combine(Path.GetTempPath(),"suneye-neutral-"+Guid.NewGuid().ToString("N")+".bmp");
try
{
    // 2x2, 24-bit BMP: two padded rows, self-contained fixture.
    byte[] image={0x42,0x4d,70,0,0,0,0,0,0,0,54,0,0,0,40,0,0,0,2,0,0,0,2,0,0,0,1,0,24,0,0,0,0,0,16,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,255,255,255,0,0,0,0,255,255,255,255,255,255,0,0};
    File.WriteAllBytes(path,image);
    using var resources=new FrozenOutputResources();
    var context=new Context(path,resources);
    var sourceInput=new InputValues(source.Describe().Inputs,new Dictionary<string,object?>{{"input.local-address",path},{"input.sequence",1L}});
    var outcome=await ToolRuntimeExecutor.ExecuteAsync(source,source.CreateSettings(),sourceInput,context);
    if(!outcome.Success || outcome.Output is null)throw new Exception("Image source failed: "+string.Join(";",outcome.Diagnostics.Select(x=>x.Message)));
    using var sourceOutput=(IDisposable)outcome.Output;
    var imageRecord=(RecordValue)outcome.Output.ToValues()["output.image"]!;
    var imageResource=imageRecord.Get<IReadOnlyImage>("data");
    var blobInput=new InputValues(blob.Describe().Inputs,new Dictionary<string,object?>{{"input.image",imageResource}});
    var blobOutcome=await ToolRuntimeExecutor.ExecuteAsync(blob,blob.CreateSettings(),blobInput,context);
    if(!blobOutcome.Success || blobOutcome.Output is null)throw new Exception("Blob failed: "+string.Join(";",blobOutcome.Diagnostics.Select(x=>x.Message)));
    (blobOutcome.Output as IDisposable)?.Dispose();
    SessionChain.Verify(directory, path);
    var forbidden=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).Where(n=>n is "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Xaml" or "SunEyeVision.Plugin.Wpf" or "SunEyeVision.UI" or "SunEyeVision.Plugin.SDK" or "SunEyeVision.Tool.ImageSource.Wpf" or "SunEyeVision.Tool.BlobAnalysis.Wpf").ToArray();
    if(forbidden.Length!=0)throw new Exception("Desktop assemblies loaded: "+string.Join(",",forbidden));
    Console.WriteLine("ImageLoad and BlobAnalysis executed; no WPF/UI/SDK loaded.");
}
finally { File.Delete(path); }
}
catch(Exception error) { Console.Error.WriteLine(error); return 1; }
return 0;

static class ProbeErrorMode
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
    internal static void DisableSystemDialogs()=>SetErrorMode(0x0001|0x0002|0x8000);
}

sealed class Context(string path,IOutputResources resources):IToolExecutionContext,ILocalImageReadContext
{
    public RunIdentity Identity {get;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"probe",Guid.NewGuid());
    public CancellationToken Cancellation=>CancellationToken.None;
    public IOutputResources Resources=>resources;
    public string AcceptLocalImage()=>path;
    public void Log(string message) { }
}
