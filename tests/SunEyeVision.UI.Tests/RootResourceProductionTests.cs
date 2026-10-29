using SunEyeVision.Plugin.Runtime;
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
/// <summary>正式Release同版源与Blob Runtime：根代理草稿、历史、重开及真实资源执行，无模拟资源或算法。</summary>
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
   var plugins=new PluginManager(VisionLogger.Instance);
   Assert.True(PluginLoader.LoadPlugins(Path.Combine(host,"plugins")));
   var directory=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"root-real-resources");Directory.CreateDirectory(directory);
   var imagePath=Path.Combine(directory,"native-input.bmp");WriteImage(imagePath);
   var image=new WorkflowNodeBase("image","图像源","图像源","ImageLoad"){RuntimeConfiguration=RuntimeToolConfiguration.Create("ImageLoad")};
   var policy=ImageSourcePolicy.Read(image)!;policy.FilePaths=new(){imagePath};policy.CurrentIndex=0;policy.ApplyTo(image);
   var first=RuntimeToolConfiguration.Create("BlobAnalysis").WithFieldValues(new Dictionary<string,object?>{[AreaEnabled]=false});
   var root=new Flow("root-real","实际资源流程");root.Nodes.Add(image);
   root.Nodes.Add(new("blob-a","区域源","区域源","BlobAnalysis"){RuntimeConfiguration=first});root.Nodes.Add(new("blob-b","区域消费者","区域消费者","BlobAnalysis"){RuntimeConfiguration=first.Copy()});
   var document=new Solution{Workflows=new(){root}};
   var session=(SolutionSession)Activator.CreateInstance(typeof(SolutionSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{document},null)!;
   var reader=new OutputBindingChangeService();
   var query=new DataSourceQueryService{WorkflowId=root.Id,FormalOutputContractReader=id=>root.Nodes.FirstOrDefault(n=>n.Id==id) is {} node?reader.ReadContract(node):null,BindingCandidateNodeIds=id=>root.Nodes.TakeWhile(n=>n.Id!=id).Select(n=>n.Id).ToArray()};
   using(var context=new RootWorkflowConfigurationContext(session,root,query,()=>true,()=>true,_=>{}))
   {
    var imagePort=BindingClrAdapter.DescribeOutput(query.FormalOutputContractReader("image")!.Single(o=>BindingClrAdapter.OutputPortId(o)=="output.image"));
    foreach(var target in new[]{"blob-a","blob-b"})
    {
     var proxy=context.Add(context.Candidates().Single(c=>c.Node.Id==target&&c.StoragePath=="input.image"));
     context.Write(proxy,new ReferenceSetting(new OutputReference("image",imagePort.Id,imagePort.Type,scopeId:root.Id)));
    }
    var region=context.Add(context.Candidates().Single(c=>c.Node.Id=="blob-b"&&c.StoragePath=="input.inspection.region"));
    var regionPort=BindingClrAdapter.DescribeOutput(query.FormalOutputContractReader("blob-a")!.Single(o=>BindingClrAdapter.OutputPortId(o)==BlobField(101)));
    context.Write(region,new ReferenceSetting(new OutputReference("blob-a",regionPort.Id,regionPort.Type,scopeId:root.Id)));
    Assert.Empty(root.Nodes[1].ParamSettings.Settings);Assert.Empty(root.RootConfiguration.RunParameters);
    var priorReference=BindingReferenceSerializer.Serialize(Assert.IsType<ReferenceSetting>(context.Read(region)).Reference);
    Assert.NotNull(context.ValidateReference(region,new OutputReference("blob-a",regionPort.Id,regionPort.Type,scopeId:"other-root")));
    Assert.Equal(priorReference,BindingReferenceSerializer.Serialize(Assert.IsType<ReferenceSetting>(context.Read(region)).Reference));
    Assert.NotNull(context.ValidateReference(region,new OutputReference("image",imagePort.Id,imagePort.Type,scopeId:root.Id)));
    Assert.Equal(priorReference,BindingReferenceSerializer.Serialize(Assert.IsType<ReferenceSetting>(context.Read(region)).Reference));
    Assert.DoesNotContain(context.Candidates(),candidate=>candidate.StoragePath=="input.local-address");
    var sequence=context.Add(context.Candidates().Single(candidate=>candidate.Node.Id=="image"&&candidate.StoragePath=="sequence.initial"));
    context.Write(sequence,new ConstantSetting(99L));Assert.Equal(99L,Assert.IsType<ConstantSetting>(context.Read(sequence)).Value);
    Assert.Equal(1L,ImageSourcePolicy.Read(root.Nodes[0])!.SNInitialValue);
    Assert.ThrowsAny<Exception>(()=>context.Write(sequence,new ConstantSetting("invalid")));
    Assert.Equal(99L,Assert.IsType<ConstantSetting>(context.Read(sequence)).Value);
    context.Remove(sequence); // 删除曝光代理还原该字段，不丢其他节点订阅。
    Assert.True(context.TryApply(out var error),error);
    Assert.True(context.CanUndo);context.Undo();Assert.Empty(root.RootConfiguration.RunParameters);Assert.Empty(root.Nodes[1].ParamSettings.Settings);
    Assert.True(context.CanRedo);context.Redo();Assert.Equal(3,root.RootConfiguration.RunParameters.Length);
   }
   Assert.Equal(1L,ImageSourcePolicy.Read(root.Nodes[0])!.SNInitialValue);
   Assert.False(root.Nodes[1].RuntimeConfiguration!.ReadFieldValues().Get<bool>(AreaEnabled));
   var committed=root.Nodes[2].ParamSettings.Settings.Select(setting=>setting.ReferenceExpression).ToArray();
   using(var cancelled=new RootWorkflowConfigurationContext(session,root,query,()=>true,()=>true,_=>{}))
   {
    var proxy=cancelled.Configuration.RunParameters.Single(p=>p.NodeId=="blob-b"&&p.StoragePath=="input.inspection.region");
    cancelled.Write(proxy,new UnsetSetting());Assert.IsType<UnsetSetting>(cancelled.Read(proxy));cancelled.Cancel();
   }
   Assert.Equal(committed,root.Nodes[2].ParamSettings.Settings.Select(setting=>setting.ReferenceExpression).ToArray());
   var snapshot=SunEyeVision.Workflow.Persistence.SolutionSnapshot.Prepare(document,Path.Combine(directory,"实际资源流程.json"));
   var restored=System.Text.Json.JsonSerializer.Deserialize<Solution>(snapshot.Json,WorkflowSerializationOptions.Default)!;
   var restoredRoot=Assert.Single(restored.Workflows);Assert.Equal(3,restoredRoot.RootConfiguration.RunParameters.Length);
   var reopenedSession=(SolutionSession)Activator.CreateInstance(typeof(SolutionSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{restored},null)!;
   var reopenedQuery=new DataSourceQueryService{WorkflowId=restoredRoot.Id,FormalOutputContractReader=id=>restoredRoot.Nodes.FirstOrDefault(node=>node.Id==id) is {} node?reader.ReadContract(node):null,
    BindingCandidateNodeIds=id=>restoredRoot.Nodes.TakeWhile(node=>node.Id!=id).Select(node=>node.Id).ToArray()};
   using(var reopened=new RootWorkflowConfigurationContext(reopenedSession,restoredRoot,reopenedQuery,()=>true,()=>true,_=>{}))
    foreach(var proxy in reopened.Configuration.RunParameters){Assert.Equal(proxy.FieldId,reopened.Resolve(proxy).FieldId);Assert.IsType<ReferenceSetting>(reopened.Read(proxy));}
   var engine=new WorkflowEngine(VisionLogger.Instance);engine.RegisterWorkflow(restoredRoot);
   var runner=new WorkflowExecutionEngine(engine,plugins,VisionLogger.Instance);
   using var run=await runner.ExecuteWorkflowAsync(new WorkflowRunRequest(restoredRoot.Id,sourceInputs:new Dictionary<string,ImageSourceRunInput>{["image"]=new(imagePath,1){LocalReadAdmission=_=>imagePath}})).WaitAsync(TimeSpan.FromSeconds(30));
   Assert.True(run.Success,string.Join(";",run.Errors.Concat(run.NodeResults.Values.SelectMany(n=>n.ErrorMessages))));Assert.Equal(3,run.NodeResults.Count);
   var publication=run.DeclaredOutputs.Single(output=>output.Identity.NodeId=="blob-b");
   using var consumer=run.AcquireDeclaredOutput(publication.Identity);
   Assert.Equal(1L,consumer.Values!.Get<long>(BlobField(11)));
   Assert.Equal(16d,consumer.Values.Get<double>(BlobField(12)));
   Assert.Equal(committed,restoredRoot.Nodes[2].ParamSettings.Settings.Select(setting=>setting.ReferenceExpression).ToArray());
   Assert.DoesNotContain("native-input.bmp",System.Text.Json.JsonSerializer.Serialize(restoredRoot.Nodes[2].RuntimeConfiguration));
   Assert.All(restoredRoot.Nodes.Skip(1),node=>Assert.All(node.ParamSettings.Settings,setting=>{Assert.Equal(BindingType.Binding,setting.BindingType);Assert.Null(setting.ConstantValue);}));
   File.WriteAllText(Path.Combine(directory,"production-receipt.json"),System.Text.Json.JsonSerializer.Serialize(new{source="ImageLoad.Runtime:output.image",producer="BlobAnalysis.Runtime:"+BlobField(101),consumer="input.inspection.region",nodes=run.NodeResults.Count,blobs=1,area=16,formalRuntimeValuesRemainUnset=true}));
  }
  finally{AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",prior);}
 }
 private const string AreaEnabled="field:e9845ab7a46e5ee9b42310bef5a11c19";
 private static string BlobField(int number)=>"field:"+Guid.Parse("8ca10000-0000-4000-8000-"+number.ToString("D12",System.Globalization.CultureInfo.InvariantCulture)).ToString("N");
 private static void WriteImage(string path)
 {
  using var writer=new BinaryWriter(File.Create(path));writer.Write((ushort)0x4d42);writer.Write(54+8*8*3);writer.Write(0);writer.Write(54);writer.Write(40);writer.Write(8);writer.Write(8);writer.Write((ushort)1);writer.Write((ushort)24);writer.Write(0);writer.Write(8*8*3);for(int i=0;i<4;i++)writer.Write(0);
  for(int y=0;y<8;y++)for(int x=0;x<8;x++){byte value=(byte)(x>=2&&x<6&&y>=2&&y<6?255:0);writer.Write(value);writer.Write(value);writer.Write(value);}
 }
}