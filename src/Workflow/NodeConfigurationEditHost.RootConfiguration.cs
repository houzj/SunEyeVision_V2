using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace SunEyeVision.Workflow;
public sealed class RootWorkflowConfigurationEditDraft
{
 internal Guid Owner {get;} internal string Signature {get;} internal Workflow Formal {get;} internal bool Consumed {get;set;}
 public string WorkflowId=>Formal.Id;
 public RootWorkflowConfiguration Configuration {get;set;}
 internal RootWorkflowConfigurationEditDraft(Guid owner,Workflow formal,RootWorkflowConfiguration configuration,string signature)
 {Owner=owner;Formal=formal;Configuration=configuration;Signature=signature;}
}
public sealed partial class NodeConfigurationEditHost
{
 private readonly HashSet<Workflow> _rootNotifications=new();
 private static string RootSignature(RootWorkflowConfiguration value)=>JsonSerializer.Serialize(value,WorkflowSerializationOptions.Compact);
 public RootWorkflowConfigurationEditDraft BeginRootEdit(string workflowId)
 {
  try{lock(_gate){EnsureBaseline();var root=_design.Workflows.Single(w=>w.Id==workflowId&&!w.IsSubroutine);return new(_owner,root,root.RootConfiguration,RootSignature(root.RootConfiguration));}}
  finally{FlushStructureNotifications();}
 }
 public bool HasPendingChanges(RootWorkflowConfigurationEditDraft draft)=>RootSignature(draft.Configuration)!=draft.Signature;
 public bool OwnsRoot(RootWorkflowConfigurationEditDraft draft)=>!_closed&&_design.Workflows.Any(w=>ReferenceEquals(w,draft.Formal)&&!w.IsSubroutine);
 public RootWorkflowConfigurationEditDraft Freeze(RootWorkflowConfigurationEditDraft draft)
 {lock(_gate){EnsureOpen();if(draft.Owner!=_owner||draft.Consumed)throw new InvalidOperationException("根配置草稿已提交或不属于当前方案。");draft.Configuration.Validate();return new(_owner,draft.Formal,draft.Configuration,draft.Signature);}}
 public static bool SamePendingEdits(RootWorkflowConfigurationEditDraft left,RootWorkflowConfigurationEditDraft right)
 =>left.WorkflowId==right.WorkflowId&&RootSignature(left.Configuration)==RootSignature(right.Configuration);
 private void ComposeRoots(Solution candidate,IReadOnlyList<RootWorkflowConfigurationEditDraft>? roots)
 {
  var seen=new HashSet<string>(StringComparer.Ordinal);
  foreach(var draft in roots??Array.Empty<RootWorkflowConfigurationEditDraft>())
  {
   if(draft.Owner!=_owner||draft.Consumed||!OwnsRoot(draft)||!seen.Add(draft.WorkflowId))throw new InvalidOperationException("根配置范围已失效或重复。");
   var root=candidate.Workflows.Single(w=>w.Id==draft.WorkflowId&&!w.IsSubroutine);
   if(RootSignature(root.RootConfiguration)!=draft.Signature)throw new InvalidOperationException("根配置已被修改，草稿保留，请重新确认。");
   draft.Configuration.Validate();root.RootConfiguration=draft.Configuration;
  }
 }
}
