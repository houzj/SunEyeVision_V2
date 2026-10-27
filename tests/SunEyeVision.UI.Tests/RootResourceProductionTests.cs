using System.IO;
using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Xunit;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
/// <summary>单独进程运行：正式Release准入、实际ImageLoad和Blob，无模拟资源或算法。</summary>
[Collection("VM canvas UI")]
public sealed class RootResourceProductionTests
{
 [Fact]
 public async Task Actual_image_and_blob_region_subscriptions_survive_formal_reopen_and_execute_without_mutating_design_values()
 {
  var host=typeof(RootResourceProductionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="HostDebugRoot").Value!;
  var prior=AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
  try
  {
   AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.TrimEndingDirectorySeparator(Path.GetFullPath(host))+Path.DirectorySeparatorChar);
   Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(host)),Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
   var plugins=new PluginManager(VisionLogger.Instance);plugins.LoadPlugins(Path.Combine(host,"plugins"));Assert.Null(plugins.LastAdmissionError);
   var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-real-resources");Directory.CreateDirectory(directory);
   var imagePath=Path.Combine(directory,"native-input.bmp");WriteImage(imagePath);
   var imageParameters=ToolRegistry.CreateParameters("ImageLoad");imageParameters.GetType().GetProperty("FilePath")!.SetValue(imageParameters,imagePath);
   var first=ToolRegistry.CreateParameters("BlobAnalysis");var second=ToolRegistry.CreateParameters("BlobAnalysis");
   var mode=second.GetType().GetProperty("InspectionMode")!;mode.SetValue(second,Enum.Parse(mode.PropertyType,"InheritedRegion"));
   var root=new Flow("root-real","实际资源流程");root.Nodes.Add(new("image","图像源","图像源","ImageLoad"){Parameters=imageParameters});
   root.Nodes.Add(new("blob-a","区域源","区域源","BlobAnalysis"){Parameters=first});root.Nodes.Add(new("blob-b","区域消费者","区域消费者","BlobAnalysis"){Parameters=second});
   var document=new Solution{Workflows=new(){root}};
   var session=(SolutionSession)Activator.CreateInstance(typeof(SolutionSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{document},null)!;
   var reader=new OutputBindingChangeService();
   var query=new DataSourceQueryService{WorkflowId=root.Id,FormalOutputContractReader=id=>root.Nodes.FirstOrDefault(n=>n.Id==id) is {} node?reader.ReadContract(node):null,BindingCandidateNodeIds=id=>root.Nodes.TakeWhile(n=>n.Id!=id).Select(n=>n.Id).ToArray()};
   using(var context=new RootWorkflowConfigurationContext(session,root,query,()=>true,()=>true,_=>{}))
   {
    var imagePort=BindingClrAdapter.DescribeOutput(query.FormalOutputContractReader("image")!.Single(o=>o.Name=="OutputImage"));
    foreach(var target in new[]{"blob-a","blob-b"})
    {
     var proxy=context.Add(context.Candidates().Single(c=>c.Node.Id==target&&c.StoragePath=="ImageSource"));
     context.Write(proxy,new ReferenceSetting(new OutputReference("image",imagePort.Id,imagePort.Type,scopeId:root.Id)));
    }
    var region=context.Add(context.Candidates().Single(c=>c.Node.Id=="blob-b"&&c.StoragePath=="InspectionRegionInput"));
    var regionPort=BindingClrAdapter.DescribeOutput(query.FormalOutputContractReader("blob-a")!.Single(o=>o.Name=="Region"));
    context.Write(region,new ReferenceSetting(new OutputReference("blob-a",regionPort.Id,regionPort.Type,scopeId:root.Id)));
    Assert.True(context.TryApply(out var error),error);
   }
   Assert.Null(((MeasureGuideParameters)root.Nodes[1].Parameters).ImageSource);
   Assert.Null(second.GetType().GetProperty("InspectionRegionInput")!.GetValue(root.Nodes[2].Parameters));
   var snapshot=SunEyeVision.Workflow.Persistence.SolutionSnapshot.Prepare(document,Path.Combine(directory,"实际资源流程.json"));
   var restored=System.Text.Json.JsonSerializer.Deserialize<Solution>(snapshot.Json,WorkflowSerializationOptions.Default)!;
   var restoredRoot=Assert.Single(restored.Workflows);Assert.Equal(3,restoredRoot.RootConfiguration.RunParameters.Length);
   var engine=new WorkflowEngine(VisionLogger.Instance);engine.RegisterWorkflow(restoredRoot);
   var runner=new WorkflowExecutionEngine(engine,plugins,VisionLogger.Instance);
   using var run=await runner.ExecuteWorkflowAsync(restoredRoot.Id,null).WaitAsync(TimeSpan.FromSeconds(30));
   Assert.True(run.Success,string.Join(";",run.Errors.Concat(run.NodeResults.Values.SelectMany(n=>n.ErrorMessages))));Assert.Equal(3,run.NodeResults.Count);
   var consumer=run.NodeResults["blob-b"].Result!;
   Assert.Equal(1,Convert.ToInt32(consumer.GetType().GetProperty("FilteredBlobCount")!.GetValue(consumer)));
   Assert.Equal(16d,Convert.ToDouble(consumer.GetType().GetProperty("FilteredTotalArea")!.GetValue(consumer)));
   Assert.Null(((MeasureGuideParameters)restoredRoot.Nodes[2].Parameters).ImageSource);
   Assert.Null(second.GetType().GetProperty("InspectionRegionInput")!.GetValue(restoredRoot.Nodes[2].Parameters));
   File.WriteAllText(Path.Combine(directory,"production-receipt.json"),System.Text.Json.JsonSerializer.Serialize(new{source="ImageLoad",producer="BlobAnalysis.Region",consumer="BlobAnalysis.InspectionRegionInput",nodes=run.NodeResults.Count,blobs=1,area=16,formalRuntimeValuesRemainUnset=true}));
  }
  finally{AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",prior);}
 }
 private static void WriteImage(string path)
 {
  using var writer=new BinaryWriter(File.Create(path));writer.Write((ushort)0x4d42);writer.Write(54+8*8*3);writer.Write(0);writer.Write(54);writer.Write(40);writer.Write(8);writer.Write(8);writer.Write((ushort)1);writer.Write((ushort)24);writer.Write(0);writer.Write(8*8*3);for(int i=0;i<4;i++)writer.Write(0);
  for(int y=0;y<8;y++)for(int x=0;x<8;x++){byte value=(byte)(x>=2&&x<6&&y>=2&&y<6?255:0);writer.Write(value);writer.Write(value);writer.Write(value);}
 }
}