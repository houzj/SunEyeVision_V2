using System.Runtime.InteropServices;
using HalconDotNet;using SunEyeVision.Plugin.Contracts;using SunEyeVision.HalAdapter;
[assembly:PluginContract(3,0)]
namespace SunEyeVision.Plugin.Foundation.HalconSample;
public sealed record ThresholdSettings(int Threshold):IToolSettings<ThresholdSettings>{public ThresholdSettings Freeze()=>this;}
public sealed record ImageOutput(IReadOnlyImage Image):IToolOutput{public IReadOnlyDictionary<string,object?> ToValues()=>new Dictionary<string,object?>{{"image",Image}};}
/// <summary>Real Gray8 full-domain binary slice, reusing production HalconThresholdOperations; not all threshold modes/ROI.</summary>
public sealed class ThresholdDefinition:IToolDefinition<ThresholdSettings,ImageOutput>
{
 public string ToolId=>"ImageThreshold.FoundationSample";
 public ToolSchema Describe(ThresholdSettings s)=>new(new[]{new Port("image",DataType.Image)},new[]{new Port("image",DataType.Image)});
 public IReadOnlyList<Diagnostic> Validate(ThresholdSettings s)=>s.Threshold is >=0 and <=255?Array.Empty<Diagnostic>():new[]{new Diagnostic("threshold","Threshold out of range")};
 public ToolCapabilities Capabilities(ThresholdSettings s)=>new(new[]{"halcon:foundation-sample"});
 public IToolSession<ThresholdSettings,ImageOutput> CreateSession()=>new Session();
 private sealed class Session:IToolSession<ThresholdSettings,ImageOutput>
 {
  public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
  public ValueTask<ToolOutcome<ImageOutput>> ExecuteAsync(ToolInvocation<ThresholdSettings> call)
  {
   call.Context.Cancellation.ThrowIfCancellationRequested();var input=call.Inputs.Get<IReadOnlyImage>("image");var bytes=input.CopyPixels();var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);
   try
   {
    using var image=new HImage("byte",input.Width,input.Height,pin.AddrOfPinnedObject());
    using var region=HalconThresholdOperations.SelectBasic(image,0,call.Settings.Threshold);
    using var binary=HalconThresholdOperations.Render(image,region,0,call.Settings.Threshold,255,input.Width,input.Height);
    var ptr=binary.GetImagePointer1(out string type,out int width,out int height);var result=new byte[checked(width*height)];Marshal.Copy(ptr,result,0,result.Length);
    call.Context.Cancellation.ThrowIfCancellationRequested();
    return ValueTask.FromResult(ToolOutcome<ImageOutput>.Completed(new(call.Context.Resources.CreateGray8(width,height,result))));
   }finally{pin.Free();}
  }
 }
}
