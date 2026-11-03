using System.Collections.Specialized;
using System.ComponentModel;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI;
public partial class WorkflowTabViewModel
{
 private Func<string,SunEyeVision.Workflow.Workflow?>? _readinessResolver;
 private IReadOnlyList<string> _rootConfigurationErrors=Array.Empty<string>();
 private readonly HashSet<WorkflowNodeBase> _readinessNodes=new();
 public IReadOnlyList<string> RootConfigurationErrors=>_rootConfigurationErrors;
 public bool IsRootConfigurationReady=>false;
 public string RootConfigurationSummary=>IsRootConfigurationReady?"配置检查通过；运行时仍由正式执行入口校验。":string.Join("\n",_rootConfigurationErrors);
 internal void RefreshRootReadiness()
 {
  if(_rootDefinition==null)return;
  var errors=new[]{"正式会话的无副作用运行准备检查尚未接入。"};
  if(_rootConfigurationErrors.SequenceEqual(errors))return;
  _rootConfigurationErrors=errors;
  OnPropertyChanged(nameof(RootConfigurationErrors));OnPropertyChanged(nameof(IsRootConfigurationReady));OnPropertyChanged(nameof(RootConfigurationSummary));
 }
 private void BindRootReadiness()
 {
  if(_rootDefinition==null)return;
  _rootDefinition.Nodes.CollectionChanged+=RootReadinessCollectionChanged;
  _rootDefinition.Connections.CollectionChanged+=RootReadinessCollectionChanged;
  RebindReadinessNodes();RefreshRootReadiness();
 }
 private void UnbindRootReadiness()
 {
  if(_rootDefinition!=null){_rootDefinition.Nodes.CollectionChanged-=RootReadinessCollectionChanged;_rootDefinition.Connections.CollectionChanged-=RootReadinessCollectionChanged;}
  foreach(var node in _readinessNodes)node.PropertyChanged-=RootReadinessNodeChanged;
  _readinessNodes.Clear();
 }
 private void RebindReadinessNodes()
 {
  foreach(var node in _readinessNodes)node.PropertyChanged-=RootReadinessNodeChanged;
  _readinessNodes.Clear();
  if(_rootDefinition==null)return;
  foreach(var node in _rootDefinition.Nodes)if(_readinessNodes.Add(node))node.PropertyChanged+=RootReadinessNodeChanged;
 }
 private void RootReadinessCollectionChanged(object? sender,NotifyCollectionChangedEventArgs e)
 {RebindReadinessNodes();RefreshRootReadiness();}
 private void RootReadinessNodeChanged(object? sender,PropertyChangedEventArgs e)
 {
  if(e.PropertyName is nameof(WorkflowNodeBase.RuntimeConfiguration) or nameof(WorkflowNodeBase.ToolType) or nameof(WorkflowNodeBase.SubroutineId) or nameof(WorkflowNodeBase.DispName))RefreshRootReadiness();
 }
}
