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
  (_,proposal) => proposal is ReferenceSetting reference ? context.ValidateReference(proxy,reference) : null);
 internal static ReferenceCandidates Candidates(RootWorkflowConfigurationContext context,RootParameterProxy proxy)
 {
  var resource=context.Resolve(proxy).Resource;
  var query=context.Query;var candidates=new List<DeclaredReferenceCandidate>();var target=proxy.NodeId;
  var names=query.GetBindingCandidateNodeNames(target);
  foreach(var id in names.Keys)
   foreach(var output in query.FormalOutputContractReader?.Invoke(id)??Array.Empty<OutputVariable>())
   {
    if(output.Kind!=OutputVariableKind.Data)continue;
    try{candidates.AddRange(DeclaredReferenceCandidateBuilder.Build(output,id,query.WorkflowId,names.GetValueOrDefault(id,"可绑定节点")));}
    catch(ArgumentException){continue;}
   }
  _=context.Resolve(proxy);
  return new(candidates.Where(candidate=>resource is null||resource.Runtime||candidate.SelectedType==resource.Port.Type),
   candidates.Where(candidate=>candidate.Output.Type.Shape!=ValueShape.Sequence&&candidate.SelectedType==DataType.Integer));
 }
}
