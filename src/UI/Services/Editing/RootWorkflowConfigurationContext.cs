using System.Collections.Immutable;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;
using Flow=SunEyeVision.Workflow.Workflow;
using Setting=SunEyeVision.Plugin.Contracts.ParameterSetting;
namespace SunEyeVision.UI.Services.Editing;
/// <summary>根配置单一候选范围；清单与相关节点参数一起发布，窗口关闭只丢弃候选。</summary>
internal sealed class RootWorkflowConfigurationContext:IDocumentEditParticipant,IDisposable
{
 private readonly DocumentEditSession _edits;private readonly NodeConfigurationEditHost _host;private readonly Func<bool> _isCurrent,_canEdit;
 private readonly Action<BindingConfigurationPublication> _published;
 private RootWorkflowConfigurationEditDraft _root;
 private readonly Dictionary<string,NodeConfigurationEditDraft> _nodes=new(StringComparer.Ordinal);
 private readonly Dictionary<string,WorkflowNodeBase> _openingNodes=new(StringComparer.Ordinal);
 private bool _active=true;private int _undoCount,_redoCount;private long _historyVersion;
 private readonly Dictionary<string,WorkflowNodeBase> _formalNodes=new(StringComparer.Ordinal);
 internal Flow Root {get;}
 internal DataSourceQueryService Query {get;}
 internal Func<string?>? PrepareView {get;set;}
 internal Func<bool>? HasPendingView {get;set;}
 internal event Action? Changed;
 public Guid ScopeId {get;}=Guid.NewGuid();public string ScopeKey=>"root-configuration:"+Root.Id;
 public bool IsValid=>_active&&_isCurrent()&&_host.OwnsRoot(_root)&&_nodes.Values.All(d=>Root.Nodes.Any(n=>n.Id==d.Identity.NodeId&&_formalNodes.TryGetValue(n.Id,out var formal)&&ReferenceEquals(formal,n)&&_host.OwnsNode(Root.Id,n)));
 public bool HasPendingChanges=>_active&&(_host.HasPendingChanges(_root)||_nodes.Values.Any(_host.HasPendingChanges)||HasPendingView?.Invoke()==true);
 internal bool CanEdit=>IsValid&&_canEdit();
 internal RootWorkflowConfiguration Configuration=>_root.Configuration;
 internal RootWorkflowConfigurationContext(SolutionSession session,Flow root,DataSourceQueryService query,Func<bool> isCurrent,Func<bool> canEdit,Action<BindingConfigurationPublication> published)
 {
  if(root.IsSubroutine)throw new InvalidOperationException("Group不属于根配置编辑范围。");
  _edits=session.Editing;_host=_edits.Host;Root=root;Query=query;_isCurrent=isCurrent;_canEdit=canEdit;_published=published;
  _root=_host.BeginRootEdit(root.Id);_historyVersion=_host.Capture().Version;_edits.Register(this);
 }
 private void Ensure(bool edit=false){if(!IsValid||edit&&!_canEdit())throw new InvalidOperationException("根配置所属方案、流程或编辑状态已经变化。");}
 private WorkflowNodeBase Candidate(string id)
 {
  Ensure();if(_nodes.TryGetValue(id,out var existing))return existing.Node;
  var formal=Root.Nodes.SingleOrDefault(n=>n.Id==id)??throw new InvalidOperationException("代理来源节点已删除。");
  if(formal.ToolType=="Subroutine")throw new InvalidOperationException("Group参数迁移保持冻结。");
  var draft=_host.BeginEdit(Root.Id,id);_nodes.Add(id,draft);_formalNodes.Add(id,formal);_openingNodes.Add(id,_host.Freeze(draft).Node);return draft.Node;
 }
 internal IReadOnlyList<RootParameterCandidate> Candidates()=>RootParameterCatalog.Describe(Root).ToArray();
 internal RootParameterCandidate Resolve(RootParameterProxy proxy)=>RootParameterCatalog.Resolve(Candidate(proxy.NodeId),proxy);
 internal RootParameterProxy Add(RootParameterCandidate candidate)
 {
  Ensure(true);var formal=Root.Nodes.Single(n=>ReferenceEquals(n,candidate.Node));
  if(candidate.Unavailable!=null||candidate.Field==null)throw new InvalidOperationException(candidate.Unavailable??"字段不可用。");
  var proxy=new RootParameterProxy(Guid.NewGuid(),formal.Id,formal.ToolType,candidate.FieldId,candidate.StoragePath,candidate.DisplayName);
  _=Resolve(proxy);_root.Configuration=_root.Configuration with{RunParameters=_root.Configuration.RunParameters.Add(proxy)};_root.Configuration.Validate();Changed?.Invoke();return proxy;
 }
 internal void Remove(RootParameterProxy proxy)
 {
  Ensure(true);
  if(_nodes.TryGetValue(proxy.NodeId,out var draft)&&_openingNodes.TryGetValue(proxy.NodeId,out var original))
  {
   // 失效代理仍可明确移除；无法解析的字段不猜写值。
   var field=RootParameterCatalog.Describe(draft.Node).SingleOrDefault(f=>f.FieldId==proxy.FieldId&&f.StoragePath==proxy.StoragePath)?.Field;
   var opening=RootParameterCatalog.Describe(original).SingleOrDefault(f=>f.FieldId==proxy.FieldId&&f.StoragePath==proxy.StoragePath)?.Field;
   var resource=RootParameterCatalog.Describe(draft.Node).SingleOrDefault(f=>f.FieldId==proxy.FieldId&&f.StoragePath==proxy.StoragePath)?.Resource;
   if(resource!=null)resource.RestoreOpening(draft.Node,original);
   else if(field!=null&&opening!=null){field.Write(opening.Read());var prior=original.ParamSettings.GetSetting(proxy.StoragePath)?.Clone();if(prior==null)draft.Node.ParamSettings.RemoveSetting(proxy.StoragePath);else draft.Node.ParamSettings.SetSetting(prior);}
  }
  _root.Configuration=_root.Configuration with{RunParameters=_root.Configuration.RunParameters.Remove(proxy)};Changed?.Invoke();
 }
 internal Setting Read(RootParameterProxy proxy)
 {
  var target=Resolve(proxy);if(target.Resource is {} resource)return resource.Read(target.Node,Query,Root.Id);var field=target.Field!;var stored=target.Node.ParamSettings.GetSetting(field.StoragePath);
  if(stored?.ReferenceExpression is {} expression)return new ReferenceSetting(BindingReferenceSerializer.Deserialize(expression));
  if(stored?.BindingType==BindingType.Binding)
  {
   var output=Query.FormalOutputContractReader?.Invoke(stored.SourceNodeId!).SingleOrDefault(o=>stored.SourceFieldId.HasValue?o.FieldId==stored.SourceFieldId:o.Name==stored.SourceProperty)??throw new InvalidOperationException("现有订阅来源已失效，请解除或重新选择。");
   var port=BindingClrAdapter.DescribeOutput(output);return new ReferenceSetting(new OutputReference(stored.SourceNodeId!,port.Id,port.Type,scopeId:stored.SourceWorkflowId??Root.Id,storage:port.Storage));
  }
  return new ConstantSetting(field.Read());
 }
 internal void Write(RootParameterProxy proxy,Setting setting)
 {
  Ensure(true);var target=Resolve(proxy);if(target.Resource is {} resource){resource.Write(target.Node,setting,Query,Root.Id);return;}var field=target.Field!;
  if(setting is ConstantSetting constant){field.Write(constant.Value);target.Node.ParamSettings.RemoveSetting(field.StoragePath);}
  else if(setting is ReferenceSetting reference)target.Node.ParamSettings.SetSetting(ToSetting(field,reference.Reference));
  else throw new InvalidOperationException("此参数不支持空设置。");
  target.Node.Parameters.RebindingRequired.Remove(field.StoragePath);target.Node.Parameters.BindingValidationPending.Remove(field.StoragePath);
 }
 private ParamSetting ToSetting(ConfigurationFieldAccessor field,OutputReference reference)
 {
  var source=Query.FormalOutputContractReader?.Invoke(reference.NodeId)?.SingleOrDefault(output=>BindingClrAdapter.OutputPortId(output)==reference.PortId);
  return new(){ParameterName=field.StoragePath,BindingType=BindingType.Binding,ReferenceExpression=BindingReferenceSerializer.Serialize(reference),SourceNodeId=reference.NodeId,SourceWorkflowId=reference.ScopeId,SourceFieldId=source?.FieldId,SourceProperty=source?.Name??reference.PortId};
 }
 internal string? ValidateReference(RootParameterProxy proxy,OutputReference reference)
 {
  Ensure(true);var target=Resolve(proxy);
  if(target.Resource is {} resource)
  {
   var restore=resource.CaptureRestore(target.Node);
   try{resource.Write(target.Node,new ReferenceSetting(reference),Query,Root.Id);_host.ValidateBatch(_nodes.Values.ToArray(),null,new[]{_root});return null;}
   catch(Exception error){return error.Message;}
   finally{restore();}
  }
  var prior=target.Node.ParamSettings.GetSetting(proxy.StoragePath)?.Clone();
  try{target.Node.ParamSettings.SetSetting(ToSetting(target.Field!,reference));_host.ValidateBatch(_nodes.Values.ToArray(),null,new[]{_root});return null;}
  catch(Exception error){return error.Message;}
  finally{if(prior==null)target.Node.ParamSettings.RemoveSetting(proxy.StoragePath);else target.Node.ParamSettings.SetSetting(prior);}
 }
 public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
 {
  Ensure();if(!ReferenceEquals(host,_host))throw new InvalidOperationException("根配置编辑宿主不一致。");
  var viewError=PrepareView?.Invoke();if(!string.IsNullOrEmpty(viewError))throw new InvalidOperationException(viewError);
  _root.Configuration.Validate();foreach(var proxy in Configuration.RunParameters)_=Resolve(proxy);
  return DocumentEditProposal.ForRootConfiguration(host,_root,_nodes.Values.ToArray(),HasPendingChanges);
 }
 public void Acknowledge(DocumentEditProposal prepared,BindingConfigurationPublication publication)
 {
  Ensure();if(publication.Snapshot.Version!=_historyVersion){if(prepared.Token is true){_undoCount++;_redoCount=0;}else{_undoCount=_redoCount=0;}_historyVersion=publication.Snapshot.Version;}
  var newerRoot=!prepared.Matches(_root);var newerNodes=!prepared.Matches(_nodes.Values.ToArray());
  var newerConfiguration=_root.Configuration;_root=_host.BeginRootEdit(Root.Id);if(newerRoot)_root.Configuration=newerConfiguration;
  if(newerNodes){foreach(var id in _nodes.Keys.ToArray())_nodes[id]=_host.RebaseAcceptedDraft(_nodes[id]);}
  else{_nodes.Clear();_openingNodes.Clear();_formalNodes.Clear();}
  _published(publication);Changed?.Invoke();
 }
 internal bool TryApply(out string error)
 {try{Ensure(true);_edits.Apply(ScopeId);error="";return true;}catch(Exception exception){error=exception.Message;return false;}}
 internal void Undo(){Ensure(true);if(HasPendingChanges||!CanUndo)throw new InvalidOperationException("请先应用编辑，或先撤销后续配置修改。");var snapshot=_host.Undo();_undoCount--;_redoCount++;_historyVersion=snapshot.Version;HistoryPublished(snapshot);Reload();}
 internal void Redo(){Ensure(true);if(HasPendingChanges||!CanRedo)throw new InvalidOperationException("重做范围已失效或尚有待提交编辑。");var snapshot=_host.Redo();_redoCount--;_undoCount++;_historyVersion=snapshot.Version;HistoryPublished(snapshot);Reload();}
 private void HistoryPublished(BindingConfigurationSnapshot snapshot)=>_published(new(snapshot,Array.Empty<OutputBindingChange>(),Root.Nodes.Select(n=>new ScopedNodeIdentity(Root.Id,n.Id)).ToArray()));
 internal bool CanUndo=>_undoCount>0&&_historyVersion==_host.Capture().Version&&_host.CanUndo;
 internal bool CanRedo=>_redoCount>0&&_historyVersion==_host.Capture().Version&&_host.CanRedo;
 private void Reload(){_root=_host.BeginRootEdit(Root.Id);_nodes.Clear();_openingNodes.Clear();_formalNodes.Clear();Changed?.Invoke();}
 public void Cancel()=>Dispose();
 public void Dispose(){if(!_active)return;_active=false;_edits.Unregister(ScopeId);_nodes.Clear();_openingNodes.Clear();_formalNodes.Clear();Changed=null;}
}
