using SunEyeVision.Workflow;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using Xunit;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class RootTabStateTests
{
 [Fact]
 public void Configuration_check_reads_formal_graph_tool_parameters_and_pending_bindings_without_running()
 {
  PluginLoader.MarkAsLoaded();ToolRegistry.RegisterTool(typeof(RootWorkspaceSettingsTests.RootPolicyProbe));
  var flow=new Flow("root","根");var tab=new WorkflowTabViewModel{Id=flow.Id};tab.BindRootDefinition(flow);
  Assert.True(tab.IsRootConfigurationReady);Assert.Empty(flow.Nodes); // 空图就绪不代表可运行。
  var invalid=new InvalidParameters();var node=new WorkflowNodeBase("node","工具","工具","RootPolicyProbe"){Parameters=invalid};
  flow.Nodes.Add(node);Assert.False(tab.IsRootConfigurationReady);Assert.Contains("缺少长度",tab.RootConfigurationSummary);
  node.Parameters=new RootWorkspaceSettingsTests.ProbeParameters();Assert.True(tab.IsRootConfigurationReady);
  node.Parameters.BindingValidationPending["InputImage"]="来源未确认";tab.RefreshRootReadiness();Assert.False(tab.IsRootConfigurationReady);Assert.Contains("来源未确认",tab.RootConfigurationSummary);
  node.Parameters.BindingValidationPending.Clear();tab.RefreshRootReadiness();Assert.True(tab.IsRootConfigurationReady);
  flow.Connections.Add(new(){Id="bad",SourceNodeId="missing",TargetNodeId=node.Id});Assert.False(tab.IsRootConfigurationReady);
  flow.Connections.Clear();Assert.True(tab.IsRootConfigurationReady);
  node.ToolType="NotDeployed";Assert.False(tab.IsRootConfigurationReady);Assert.Contains("工具未加载",tab.RootConfigurationSummary);
  flow.Nodes.Clear();Assert.True(tab.IsRootConfigurationReady);
  var replacement=new Flow("other","替换");tab.BindRootDefinition(replacement);flow.Nodes.Add(node);Assert.True(tab.IsRootConfigurationReady);Assert.Empty(replacement.Nodes);
 }
 [Theory]
 [InlineData(100,100,80,400,1000,false,100)]
 [InlineData(100,10,80,400,1000,false,70)]
 [InlineData(100,310,80,400,1000,false,130)]
 [InlineData(100,-60,80,400,1000,false,10)]
 [InlineData(100,400,80,400,1000,false,210)]
 [InlineData(100,400,80,400,180,false,180)]
 [InlineData(100,100,80,400,1000,true,0)]
 public void Selection_scroll_keeps_native_edge_allowance_instead_of_centering(double offset,double left,double width,double viewport,double maximum,bool first,double expected)
 =>Assert.Equal(expected,WorkflowTabViewport.SelectedOffset(offset,left,width,viewport,maximum,first));
 public sealed class InvalidParameters:ToolParameters{public override ValidationResult Validate()=>ValidationResult.Failure("缺少长度");}
}
