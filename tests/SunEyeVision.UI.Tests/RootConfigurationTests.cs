using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using Xunit;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class RootConfigurationTests
{
 public sealed class Parameters:ToolParameters,IRuntimeParameterContract
 {
  public int Value {get;set;}=3;
  public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters()=>new[]{new RuntimeParameterDescriptor(nameof(Value),"数值",ValueType:typeof(int))};
 }
 public sealed class Results:ToolResults {public int Echo {get;set;} public override List<PropertyMapEntry> GetPropertyMap()=>new(){new("Echo","回显")};}
 [Tool("RootConfigurationProbe","根配置回归探针")]
 public sealed class Probe:IToolPlugin<Parameters,Results>
 {public Results Run(Parameters parameters)=>new(){Status=ExecutionStatus.Success,Echo=parameters.Value};}
 private static (Solution Document,Flow Root,NodeConfigurationEditHost Host) Model()
 {
  ToolRegistry.RegisterTool(typeof(Probe));SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();var root=new Flow("root","根流程");
  root.Nodes.Add(new("a","a","a","RootConfigurationProbe"){Parameters=new Parameters()});
  root.Nodes.Add(new("b","b","b","RootConfigurationProbe"){Parameters=new Parameters()});
  var document=new Solution{Workflows=new(){root}};return(document,root,new(document));
 }
 private static RootWorkflowConfiguration Configuration()=>new(){RunParameters=ImmutableArray.Create(new RootParameterProxy(Guid.NewGuid(),"a","RootConfigurationProbe","Value","Value","数值"))};
 [Fact]
 public void Root_directory_and_two_node_candidates_publish_atomically_and_history_preserves_new_canvas_settings()
 {
  var (document,root,host)=Model();var frozen=WorkflowRunConfiguration.Capture(document.Workflows);
  var rootDraft=host.BeginRootEdit(root.Id);rootDraft.Configuration=Configuration();
  var a=host.BeginEdit(root.Id,"a");var b=host.BeginEdit(root.Id,"b");((Parameters)a.Node.Parameters).Value=11;((Parameters)b.Node.Parameters).Value=23;
  Assert.Empty(root.RootConfiguration.RunParameters);Assert.Equal(3,((Parameters)root.Nodes[0].Parameters).Value);
  host.CommitBatch(new[]{a,b},null,new[]{rootDraft});Assert.Single(root.RootConfiguration.RunParameters);Assert.Equal(23,((Parameters)root.Nodes[1].Parameters).Value);
  Assert.Empty(frozen.CopyWorkflow(root.Id)!.RootConfiguration.RunParameters);Assert.Equal(3,((Parameters)frozen.CopyWorkflow(root.Id)!.Nodes[0].Parameters).Value);
  root.WorkspaceSettings=new(){RunIntervalMs=51};host.SynchronizeStructure();host.Undo();
  Assert.Empty(root.RootConfiguration.RunParameters);Assert.All(root.Nodes,n=>Assert.Equal(3,((Parameters)n.Parameters).Value));Assert.Equal(51,root.WorkspaceSettings.RunIntervalMs);
  host.Redo();Assert.Single(root.RootConfiguration.RunParameters);Assert.Equal(11,((Parameters)root.Nodes[0].Parameters).Value);Assert.Equal(51,root.WorkspaceSettings.RunIntervalMs);
 }
 [Theory][InlineData(1)][InlineData(2)]
 public void Acceptance_failure_restores_root_directory_nodes_and_history(int failAfter)
 {
  var (_,root,host)=Model();var before=host.Capture();var draft=host.BeginRootEdit(root.Id);draft.Configuration=Configuration();var node=host.BeginEdit(root.Id,"a");((Parameters)node.Node.Parameters).Value=19;
  typeof(NodeConfigurationEditHost).GetField("_acceptanceFailureAfterRawMutation",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(host,failAfter);
  Assert.Throws<InvalidOperationException>(()=>host.CommitBatch(new[]{node},null,new[]{draft}));
  Assert.Same(before,host.Capture());Assert.Empty(root.RootConfiguration.RunParameters);Assert.Equal(3,((Parameters)root.Nodes[0].Parameters).Value);Assert.False(host.CanUndo);
 }
 [Fact]
 public void Stale_root_candidate_cannot_overwrite_an_accepted_directory_and_draft_is_preserved()
 {
  var (_,root,host)=Model();var first=host.BeginRootEdit(root.Id);var old=host.BeginRootEdit(root.Id);first.Configuration=Configuration();old.Configuration=Configuration();
  host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),null,new[]{first});
  Assert.Throws<InvalidOperationException>(()=>host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),null,new[]{old}));
  Assert.Equal(first.Configuration,root.RootConfiguration);Assert.NotEqual(first.Configuration,old.Configuration);
 }
 [Fact]
 public void Formal_file_boundary_preserves_directory_and_copy_remaps_identity_without_losing_dangling_entries()
 {
  var (document,root,host)=Model();var draft=host.BeginRootEdit(root.Id);draft.Configuration=Configuration();host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(),null,new[]{draft});
  var path=System.IO.Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"根运行参数正式保存.json");
  var prepared=SunEyeVision.Workflow.Persistence.SolutionSnapshot.Prepare(document,path);
  var restored=JsonSerializer.Deserialize<Solution>(prepared.Json,WorkflowSerializationOptions.Default)!;
  Assert.Equal(root.RootConfiguration.RunParameters.ToArray(),restored.Workflows[0].RootConfiguration.RunParameters.ToArray());
  var remapped=root.RootConfiguration.Remap(new Dictionary<string,string>{{"a","copied-a"}});Assert.Equal("copied-a",remapped.RunParameters[0].NodeId);Assert.NotEqual(root.RootConfiguration.RunParameters[0].Id,remapped.RunParameters[0].Id);
  Assert.Equal("a",root.RootConfiguration.Remap(new Dictionary<string,string>()).RunParameters[0].NodeId);
  Assert.Throws<ArgumentException>(()=>(root.RootConfiguration with{RunParameters=root.RootConfiguration.RunParameters.Add(root.RootConfiguration.RunParameters[0] with{Id=Guid.NewGuid()})}).Validate());
 }
}