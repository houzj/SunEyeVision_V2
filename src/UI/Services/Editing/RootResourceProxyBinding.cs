using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Workflow;
using Setting=SunEyeVision.Plugin.Contracts.ParameterSetting;
namespace SunEyeVision.UI.Services.Editing;
/// <summary>Reference identity adapter on a node draft. Never reads, stores or owns an image/region value.</summary>
internal sealed record RootResourceProxyBinding(string StoragePath,Port Port,bool ImageSource)
{
 internal ConfigurationFieldAccessor Field(string display)=>new(Port,new(Port.Id,display,"输入订阅",0,FieldEditorKind.Scalar),StoragePath,
  ()=>null,_=>throw new InvalidOperationException("运行资源只能订阅来源，不能写常量。"));
 internal Setting Read(WorkflowNodeBase node,DataSourceQueryService query,string rootId)
 {
  if(ImageSource)
  {
   var value=((MeasureGuideParameters)node.Parameters).ImageSourceRef;
   if(value is null)return new UnsetSetting();
   var source=Find(query,value.SourceNodeId,value.SourceFieldId,null,value.PropertyName);
   var port=BindingClrAdapter.DescribeOutput(source);
   if(port.Type!=Port.Type)throw new InvalidOperationException("图像来源正式类型已变化，请解除或重新选择。");
   return new ReferenceSetting(new OutputReference(value.SourceNodeId,port.Id,port.Type,scopeId:value.SourceWorkflowId??rootId,storage:port.Storage));
  }
  var stored=node.ParamSettings.GetSetting(StoragePath);if(stored is null)return new UnsetSetting();
  if(stored.ReferenceExpression is {} expression)
  {
   var reference=BindingReferenceSerializer.Deserialize(expression);
   var source=BindingClrAdapter.DescribeOutput(Find(query,reference.NodeId,null,reference.PortId,null));
   if(reference.ScopeId is not null&&reference.ScopeId!=rootId||reference.Type!=source.Type||source.Type!=Port.Type)
    throw new InvalidOperationException("区域来源正式类型或作用域已变化，请解除或重新选择。");
   return new ReferenceSetting(reference);
  }
  if(stored.BindingType!=BindingType.Binding)throw new InvalidOperationException("资源槽不接受常量，请解除旧设置。");
  var output=Find(query,stored.SourceNodeId!,stored.SourceFieldId,null,stored.SourceProperty);
  var declaration=BindingClrAdapter.DescribeOutput(output);
  return new ReferenceSetting(new OutputReference(stored.SourceNodeId!,declaration.Id,declaration.Type,scopeId:stored.SourceWorkflowId??rootId,storage:declaration.Storage));
 }
 internal void Write(WorkflowNodeBase node,Setting setting,DataSourceQueryService query,string rootId)
 {
  if(setting is not ReferenceSetting && setting is not UnsetSetting)throw new InvalidOperationException("运行资源仅支持订阅或解除，不接受常量。");
  OutputVariable? output=null;OutputReference? reference=(setting as ReferenceSetting)?.Reference;
  if(reference is not null)
  {
   if(reference.ScopeId is not null&&reference.ScopeId!=rootId)throw new InvalidOperationException("来源跨出当前根流程。");
   if(reference.MemberFieldPath.Count!=0||reference.Selection is not BindingSelection.AllItems)throw new InvalidOperationException("此资源槽要求完整声明的资源输出。");
   output=Find(query,reference.NodeId,null,reference.PortId,null);var sourcePort=BindingClrAdapter.DescribeOutput(output);
   if(sourcePort.Type!=Port.Type||reference.Type!=sourcePort.Type)throw new InvalidOperationException("来源资源类型与输入槽不匹配。");
  }
  if(ImageSource)
  {
   var parameters=(MeasureGuideParameters)node.Parameters;
   // The runtime descriptor is not a canonical parameter and must not retain an old resource source.
   parameters.ImageSource=null;
   parameters.ImageSourceRef=reference is null?null:new(reference.NodeId,output!.Name,reference.NodeId,output.DisplayName??output.Name)
    {SourceWorkflowId=rootId,SourceFieldId=output.FieldId};
   node.ParamSettings.RemoveSetting(StoragePath);
  }
  else if(reference is null)node.ParamSettings.RemoveSetting(StoragePath);
  else node.ParamSettings.SetSetting(new(){ParameterName=StoragePath,BindingType=BindingType.Binding,
   ReferenceExpression=BindingReferenceSerializer.Serialize(reference),SourceNodeId=reference.NodeId,SourceWorkflowId=rootId,
   SourceFieldId=output!.FieldId,SourceProperty=output.Name});
  if(reference is null)node.Parameters.BindingSourceIdentities.Remove(StoragePath);
  else node.Parameters.BindingSourceIdentities[StoragePath]=new(reference.NodeId+"."+output!.Name,rootId,reference.NodeId,output.FieldId);
  node.Parameters.RebindingRequired.Remove(StoragePath);node.Parameters.BindingValidationPending.Remove(StoragePath);
 }
 internal Action CaptureRestore(WorkflowNodeBase node)
 {
  var setting=node.ParamSettings.GetSetting(StoragePath)?.Clone();
  var image=ImageSource?((MeasureGuideParameters)node.Parameters).ImageSource:null;
  var imageRef=ImageSource?((MeasureGuideParameters)node.Parameters).ImageSourceRef:null;
  var identity=node.Parameters.BindingSourceIdentities.GetValueOrDefault(StoragePath);
  var pending=node.Parameters.BindingValidationPending.GetValueOrDefault(StoragePath);var rebinding=node.Parameters.RebindingRequired.GetValueOrDefault(StoragePath);
  return ()=>
  {
   if(ImageSource){var parameters=(MeasureGuideParameters)node.Parameters;parameters.ImageSource=image;parameters.ImageSourceRef=imageRef;}
   if(setting is null)node.ParamSettings.RemoveSetting(StoragePath);else node.ParamSettings.SetSetting(setting);
   if(identity is null)node.Parameters.BindingSourceIdentities.Remove(StoragePath);else node.Parameters.BindingSourceIdentities[StoragePath]=identity;
   if(pending is null)node.Parameters.BindingValidationPending.Remove(StoragePath);else node.Parameters.BindingValidationPending[StoragePath]=pending;
   if(rebinding is null)node.Parameters.RebindingRequired.Remove(StoragePath);else node.Parameters.RebindingRequired[StoragePath]=rebinding;
  };
 }
 internal void RestoreOpening(WorkflowNodeBase node,WorkflowNodeBase opening)
 {
  if(ImageSource){var current=(MeasureGuideParameters)node.Parameters;current.ImageSource=null;current.ImageSourceRef=((MeasureGuideParameters)opening.Parameters).ImageSourceRef;}
  var original=opening.ParamSettings.GetSetting(StoragePath)?.Clone();if(original is null)node.ParamSettings.RemoveSetting(StoragePath);else node.ParamSettings.SetSetting(original);
  if(opening.Parameters.BindingSourceIdentities.TryGetValue(StoragePath,out var identity))node.Parameters.BindingSourceIdentities[StoragePath]=identity;else node.Parameters.BindingSourceIdentities.Remove(StoragePath);
  if(opening.Parameters.BindingValidationPending.TryGetValue(StoragePath,out var pending))node.Parameters.BindingValidationPending[StoragePath]=pending;else node.Parameters.BindingValidationPending.Remove(StoragePath);
  if(opening.Parameters.RebindingRequired.TryGetValue(StoragePath,out var rebinding))node.Parameters.RebindingRequired[StoragePath]=rebinding;else node.Parameters.RebindingRequired.Remove(StoragePath);
 }
 private static OutputVariable Find(DataSourceQueryService query,string nodeId,Guid? fieldId,string? portId,string? name)
  =>query.FormalOutputContractReader?.Invoke(nodeId)?.SingleOrDefault(o=>o.Kind==OutputVariableKind.Data&&(fieldId.HasValue?o.FieldId==fieldId:portId is not null?BindingClrAdapter.OutputPortId(o)==portId:o.Name==name))
    ??throw new InvalidOperationException("正式资源来源已失效，请解除或重新选择。");
}
