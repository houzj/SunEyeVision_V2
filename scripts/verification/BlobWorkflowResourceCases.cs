using System.IO;
using System.Text.Json;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.BlobAnalysis;
using SunEyeVision.Workflow;

internal static class WorkflowCases
{
 internal static async Task<int> Run()
 {
  try {
   var pixels=new byte[32*32];for(int y=4;y<8;y++)for(int x=3;x<7;x++)pixels[y*32+x]=255;
   using var input=HalImage.CreateFromBytes(pixels,32,32,1);
   ToolRegistry.RegisterTool(typeof(BlobAnalysisTool));
   var logger=new FixtureLogger();var engine=new WorkflowEngine(logger);var flow=engine.CreateWorkflow("s1-fixed-native-chain","Blob actual native chain");
   var parameters=new BlobAnalysisParameters{IsAreaFilterEnabled=false,IsCircularityFilterEnabled=false,IsRectangularityFilterEnabled=false,IsBlobCountFilterEnabled=false,Threshold=128,UpperThreshold=255,OutputBlobImage=true,OutputBinaryImage=true,OutputContours=true};
   flow.AddNode(new WorkflowNodeBase("blob","blob","blob","BlobAnalysis"){Parameters=parameters});
   var runner=new WorkflowExecutionEngine(engine,new UnusedPluginManager(),logger);
   using var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,inputImage:input)).WaitAsync(TimeSpan.FromSeconds(30));
   if(!run.Success)throw new Exception("Actual Workflow failed: "+string.Join(";",run.Errors));
   var output=run.DeclaredOutputs.Single();
   var first=run.AcquireDeclaredOutput(output.Identity);var second=run.AcquireDeclaredOutput(output.Identity);
   var records=second.Values!.Read(BlobOutputContract.BlobsPortId);
   if(records.Count!=1||records.GetElement(0).GetField("area").GetScalar<double>()!=16)throw new Exception("Native Blob record differs from 4x4 fixture");
   var port=BindingClrAdapter.DescribeOutput(BlobOutputContract.Describe().Single(o=>o.Name=="GrayImage"));
   var image=second.Values.Get<IReadOnlyImage>(port.Id);
   // S3 captures the actual input snapshot before the kernel runs. Gray/Binary/Region retain that
   // physical source; publication has its separate output.Identity and must not relabel the frame.
   var blobRegion=second.Values.Get<IReadOnlyRegion>(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobSchema.RegionPort);
   if(image.Frame.ProducerRun!=output.Identity||image.Frame.SourcePortId!=SunEyeVision.Tool.BlobAnalysis.Runtime.BlobDefinition.ImageInput
      ||image.EffectiveDomain.SourceFrame!=image.Frame||blobRegion.SourceFrame!=image.Frame)
       throw new Exception("Actual input capture / output publication / Region provenance is inconsistent");
   var frame=image.Frame;var pixelLease=image.AcquirePixels();var region=pixelLease.AcquireDomainReader();
   if(region.SourceFrame!=frame||pixelLease.Frame!=frame||region.RunCount!=32)throw new Exception("Reader/domain provenance or actual full test Domain differs");
   for(int y=0;y<32;y++)if(!pixelLease.GetRow(y).SequenceEqual(pixels.AsSpan(y*32,32)))throw new Exception("Actual Workflow gray pixels differ");
   run.Dispose();first.Dispose();
   if(image.CopyPixels().Length!=1024)throw new Exception("Second declared reader did not retain output");
   second.Dispose();
   if(pixelLease.GetRow(4)[3]!=255||region.ReadRuns(0,1).Span[0]!=new RegionRun(0,0,31))throw new Exception("Independent pixel/domain reader lost output after result release");
   pixelLease.Dispose();
   if(region.ReadRuns(31,1).Span[0]!=new RegionRun(31,0,31))throw new Exception("Domain reader did not retain independently");
   region.Dispose();
   var disposedRejected=false;try{pixelLease.GetRow(0);}catch(ObjectDisposedException){disposedRejected=true;}
   if(!disposedRejected)throw new Exception("Disposed pixel reader remained accessible");
   if(!input.GetPixelData().SequenceEqual(pixels))throw new Exception("Workflow modified caller input");
   Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",scope="actual Blob Runtime input capture -> canonical publish -> declared/pixel/Domain independent readers; discovery verified in component track",blobCount=records.Count,area=16,frame,checks=9}));
   return 0;
  }catch(Exception fault){Console.Error.WriteLine(fault);return 1;}
 }
 private sealed class FixtureLogger:ILogger {public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(source+": "+message+" "+exception);}}
 private sealed class UnusedPluginManager:IPluginManager
 {
  public void LoadPlugins()=>throw new InvalidOperationException("Discovery is outside this fixture");
  public void LoadPlugins(string path)=>throw new InvalidOperationException("Discovery is outside this fixture");
  public void UnloadPlugins()=>throw new InvalidOperationException();
  public List<T> GetPlugins<T>() where T:class=>throw new InvalidOperationException("Fixture unexpectedly called plugin catalog");
  public void RegisterPlugin(object value)=>throw new InvalidOperationException();
  public void UnregisterPlugin(object value)=>throw new InvalidOperationException();
  public bool IsPluginLoaded<T>() where T:class=>throw new InvalidOperationException();
 }
}
