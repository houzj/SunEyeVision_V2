using SunEyeVision.Plugin.Runtime;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Xunit;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class RootResourceProxyTests
{
 private static readonly Guid ImageField=Guid.Parse("9634b14b-a1a8-4f5f-9ca2-ff6493cc6a3e"),RegionField=Guid.Parse("6899fa9d-019b-4934-a857-07ca5ea45103");
 public sealed class Parameters:MeasureGuideParameters,IInputSlotContract,IRuntimeParameterContract,IConfigurationFieldProvider,IOutputContractProvider
 {
  public int Gain {get;set;}=3;
  [JsonIgnore] public IReadOnlyRegion? Region {get;set;}
  public IReadOnlyList<InputSlotDescriptor> GetInputSlots()=>new[]{new InputSlotDescriptor(nameof(Region),"像素区域",InputSlotKind.Roi,ValueType:typeof(IReadOnlyRegion),Required:false)};
  public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters()=>new[]{new RuntimeParameterDescriptor(nameof(Gain),"增益",ValueType:typeof(int))};
  [JsonIgnore] public IReadOnlyDictionary<string,string> RegionInputStorage=>new Dictionary<string,string>{{"inspection",nameof(Region)}};
  public IReadOnlyList<ConfigurationFieldAccessor> GetConfigurationFields()=>new[]{new ConfigurationFieldAccessor(new Port("gain",TypeRef.ScalarOf(ScalarKind.Int64),Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),new("gain","增益","参数",0,FieldEditorKind.Scalar),nameof(Gain),()=> (long)Gain,value=>Gain=checked((int)(long)value!))};
  public IReadOnlyList<OutputVariable> GetOutputContract()=>Outputs();
 }
 public sealed class Results:ToolResults {public override List<PropertyMapEntry> GetPropertyMap()=>new();}
 [Tool("RootResourceProxyProbe","根资源订阅探针")]
 public sealed class Probe:IToolPlugin<Parameters,Results>{public Results Run(Parameters parameters)=>new(){Status=ExecutionStatus.Success};}
 private static IReadOnlyList<OutputVariable> Outputs()=>new[]{new OutputVariable{Name="Image",DisplayName="图像",FieldId=ImageField,DeclaredType=typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage),ValueDeclaration=TypeRef.ResourceOf(ResourceKind.Image)},new OutputVariable{Name="Region",DisplayName="区域",FieldId=RegionField,DeclaredType=typeof(IReadOnlyRegion),ValueDeclaration=TypeRef.ResourceOf(ResourceKind.Region)}};
 private static (SolutionSession Session,Flow Root,RootWorkflowConfigurationContext Context,DataSourceQueryService Query) Model()
 {
  ToolRegistry.RegisterTool(typeof(Probe));SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();
  var root=new Flow("root","根资源流程");root.Nodes.Add(new("source","来源","来源","RootResourceProxyProbe"){Parameters=new Parameters()});root.Nodes.Add(new("target","目标","目标","RootResourceProxyProbe"){Parameters=new Parameters()});
  root.Connections.Add(new(){Id="execution",SourceNodeId="source",TargetNodeId="target"});var solution=new Solution{Workflows=new(){root}};
  var session=(SolutionSession)Activator.CreateInstance(typeof(SolutionSession),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{solution},null)!;
  var query=new DataSourceQueryService{WorkflowId=root.Id,FormalOutputContractReader=id=>root.Nodes.Any(n=>n.Id==id)?Outputs():null,BindingCandidateNodeIds=id=>id=="target"?new[]{"source"}:Array.Empty<string>()};
  return(session,root,new(session,root,query,()=>true,()=>true,_=>{}),query);
 }
 private static OutputReference Reference(ResourceKind kind,string scope="root")=>new("source","field:"+(kind==ResourceKind.Image?ImageField:RegionField).ToString("N"),TypeRef.ResourceOf(kind),scopeId:scope);
 [Fact]
 public void Provider_scalar_fields_merge_with_image_and_declared_pixel_region_reference_slots()
 {
  var node=new WorkflowNodeBase("target","目标","目标","RootResourceProxyProbe"){Parameters=new Parameters()};var candidates=RootParameterCatalog.Describe(node);
  Assert.Contains(candidates,c=>c.FieldId=="gain"&&c.Resource is null&&c.Unavailable is null);
  Assert.Contains(candidates,c=>c.StoragePath=="ImageSource"&&c.Resource?.Port.Type==TypeRef.ResourceOf(ResourceKind.Image));
  Assert.Contains(candidates,c=>c.StoragePath=="Region"&&c.Resource?.Port.Type==TypeRef.ResourceOf(ResourceKind.Region));
  Assert.Equal(candidates.Count,candidates.Select(c=>c.StoragePath).Distinct().Count());
 }
 [Fact]
 public void Image_and_region_candidates_apply_atomically_undo_redo_clear_and_preserve_old_run_generation()
 {
  var (session,root,context,_)=Model();using(context)
  {
   var image=context.Add(context.Candidates().Single(c=>c.Node.Id=="target"&&c.StoragePath=="ImageSource"));
   var region=context.Add(context.Candidates().Single(c=>c.Node.Id=="target"&&c.StoragePath=="Region"));
   Assert.IsType<UnsetSetting>(context.Read(image));Assert.IsType<UnsetSetting>(context.Read(region));
   Assert.Throws<InvalidOperationException>(()=>context.Write(image,new ConstantSetting(null)));
   Assert.Throws<InvalidOperationException>(()=>context.Write(region,new ConstantSetting(null)));
   Assert.Null(context.ValidateReference(image,Reference(ResourceKind.Image)));
   Assert.IsType<UnsetSetting>(context.Read(image));
   context.Write(image,new ReferenceSetting(Reference(ResourceKind.Image)));context.Write(region,new ReferenceSetting(Reference(ResourceKind.Region)));
   Assert.Null(((Parameters)root.Nodes[1].Parameters).ImageSourceRef);Assert.Null(root.Nodes[1].ParamSettings.GetSetting("Region"));
   var frozen=WorkflowRunConfiguration.Capture(session.Document.Workflows);
   Assert.True(context.TryApply(out var error),error);
   Assert.Equal(ImageField,((Parameters)root.Nodes[1].Parameters).ImageSourceRef!.SourceFieldId);Assert.Null(((Parameters)root.Nodes[1].Parameters).ImageSource);
   Assert.NotNull(root.Nodes[1].ParamSettings.GetSetting("Region")!.ReferenceExpression);
   Assert.Null(((Parameters)frozen.CopyWorkflow(root.Id)!.Nodes[1].Parameters).ImageSourceRef);
   context.Undo();Assert.Null(((Parameters)root.Nodes[1].Parameters).ImageSourceRef);Assert.Empty(root.RootConfiguration.RunParameters);
   context.Redo();Assert.Equal(2,root.RootConfiguration.RunParameters.Length);
   context.Write(image,new UnsetSetting());context.Write(region,new UnsetSetting());Assert.True(context.TryApply(out error),error);
   Assert.Null(((Parameters)root.Nodes[1].Parameters).ImageSourceRef);Assert.Null(root.Nodes[1].ParamSettings.GetSetting("Region"));
   Assert.DoesNotContain("ImageSource",root.Nodes[1].Parameters.BindingSourceIdentities.Keys);Assert.DoesNotContain("Region",root.Nodes[1].Parameters.BindingSourceIdentities.Keys);
  }
 }
 [Fact]
 public void Removing_proxy_restores_opening_binding_and_cancel_never_mutates_formal_nodes()
 {
  var (_,root,context,_)=Model();using(context)
  {
   var image=context.Add(context.Candidates().Single(c=>c.Node.Id=="target"&&c.StoragePath=="ImageSource"));context.Write(image,new ReferenceSetting(Reference(ResourceKind.Image)));
   context.Remove(image);Assert.True(context.TryApply(out var error),error);Assert.Null(((Parameters)root.Nodes[1].Parameters).ImageSourceRef);
   image=context.Add(context.Candidates().Single(c=>c.Node.Id=="target"&&c.StoragePath=="ImageSource"));context.Write(image,new ReferenceSetting(Reference(ResourceKind.Image)));context.Cancel();
   Assert.Null(((Parameters)root.Nodes[1].Parameters).ImageSourceRef);Assert.Empty(root.RootConfiguration.RunParameters);
  }
 }
 [Fact]
 public void Formal_file_and_fragment_copy_remap_resource_identity_while_bad_or_missing_sources_are_rejected()
 {
  var (session,root,context,query)=Model();using(context)
  {
   var image=context.Add(context.Candidates().Single(c=>c.Node.Id=="target"&&c.StoragePath=="ImageSource"));var region=context.Add(context.Candidates().Single(c=>c.Node.Id=="target"&&c.StoragePath=="Region"));
   Assert.NotNull(context.ValidateReference(image,Reference(ResourceKind.Region)));Assert.NotNull(context.ValidateReference(image,Reference(ResourceKind.Image,"elsewhere")));
   context.Write(image,new ReferenceSetting(Reference(ResourceKind.Image)));context.Write(region,new ReferenceSetting(Reference(ResourceKind.Region)));Assert.True(context.TryApply(out var error),error);
   var snapshot=SunEyeVision.Workflow.Persistence.SolutionSnapshot.Prepare(session.Document,System.IO.Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"根资源代理正式保存.json"));
   var reopened=JsonSerializer.Deserialize<Solution>(snapshot.Json,WorkflowSerializationOptions.Default)!;
   Assert.Equal(ImageField,((Parameters)reopened.Workflows[0].Nodes[1].Parameters).ImageSourceRef!.SourceFieldId);
   var fragment=WorkflowFragmentCopy.Prepare(root,root.Nodes.Select(n=>n.Id),"copied",id=>id==root.Id?root:null,(node,_)=>new WorkflowNodeBase("copy-"+node.Id,node.Name,node.DispName,node.ToolType));
   var target=fragment.Nodes.Single(n=>n.Id=="copy-target");Assert.Equal("copy-source",((Parameters)target.Parameters).ImageSourceRef!.SourceNodeId);Assert.Equal("copied",((Parameters)target.Parameters).ImageSourceRef!.SourceWorkflowId);
   var reference=BindingReferenceSerializer.Deserialize(target.ParamSettings.GetSetting("Region")!.ReferenceExpression!);Assert.Equal("copy-source",reference.NodeId);Assert.Equal("copied",reference.ScopeId);
   query.FormalOutputContractReader=_=>null;Assert.NotNull(context.ValidateReference(image,Reference(ResourceKind.Image)));
   context.Write(image,new UnsetSetting());Assert.IsType<UnsetSetting>(context.Read(image));
  }
 }
}
