using SunEyeVision.Plugin.SDK.Execution.Parameters;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Services.Editing;
internal static class RootWorkflowReferencePicker
{
 internal static ReferenceFieldOptions Options(RootWorkflowConfigurationContext context,RootParameterProxy proxy) => new(_ => Candidates(context,proxy),
  (_,proposal) => proposal is ReferenceSetting reference ? context.ValidateReference(proxy,reference.Reference) : null);
 internal static IReadOnlyList<DeclaredReferenceCandidate> Candidates(RootWorkflowConfigurationContext context,RootParameterProxy proxy)
 {
  var resource=context.Resolve(proxy).Resource;
  var query=context.Query;var candidates=new List<DeclaredReferenceCandidate>();var target=proxy.NodeId;
  var names=query.GetAvailableDataSources(target,null,true).Where(s=>!s.SourceNodeId.StartsWith("__",StringComparison.Ordinal)).GroupBy(s=>s.SourceNodeId).ToDictionary(g=>g.Key,g=>g.First().SourceNodeName);
  foreach(var id in query.BindingCandidateNodeIds?.Invoke(target)??names.Keys.ToArray())
   foreach(var output in query.FormalOutputContractReader?.Invoke(id)??Array.Empty<OutputVariable>())
   {
    if(output.Kind!=OutputVariableKind.Data)continue;Port port;try{port=BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
    if(resource is not null&&port.Type!=resource.Port.Type)continue;
    var label=(names.GetValueOrDefault(id)??id)+" · "+(output.DisplayName??output.Name);
    candidates.Add(new(label,id,query.WorkflowId,port,Array.Empty<string>()));if(resource is null)Members(port.Type,Array.Empty<string>(),label,id,port);
   }
  void Members(TypeRef type,IReadOnlyList<string> path,string label,string nodeId,Port root)
  {
   while(type.Shape==ValueShape.Sequence)type=type.Element!;if(type.Shape!=ValueShape.Record)return;
   foreach(var field in type.Fields){var next=path.Append(field.Id).ToArray();var caption=label+" / "+field.Id;candidates.Add(new(caption,nodeId,query.WorkflowId,root,next));Members(field.Type,next,caption,nodeId,root);}
  }
  return candidates;
 }
}
