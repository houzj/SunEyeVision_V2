using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Services.Editing;
internal sealed record RootParameterCandidate(WorkflowNodeBase Node,string FieldId,string StoragePath,string DisplayName,ConfigurationFieldAccessor? Field,string? Unavailable=null)
{public string Label=>Node.DispName+" · "+DisplayName+(Unavailable is null?"":"（"+Unavailable+"）");}
internal static class RootParameterCatalog
{
 internal static IReadOnlyList<RootParameterCandidate> Describe(WorkflowNodeBase node)
 {
  if(node.ToolType=="Subroutine"||node.Parameters==null)return Array.Empty<RootParameterCandidate>();
  if(node.Parameters is IConfigurationFieldProvider provider)
   return provider.GetConfigurationFields().Select(field=>new RootParameterCandidate(node,field.Port.Id,field.StoragePath,field.Presentation.DisplayName,field,
    Supports(field.Port.Type)?null:"需要专用资源或集合编辑器")).ToArray();
  var fields=new List<RootParameterCandidate>();
  var declared=((node.Parameters as IRuntimeParameterContract)?.GetRuntimeParameters()??Array.Empty<RuntimeParameterDescriptor>()).Concat(((node.Parameters as IInputSlotContract)?.GetInputSlots()??Array.Empty<InputSlotDescriptor>()).Select(input=>new RuntimeParameterDescriptor(input.PropertyName,input.DisplayName,input.TypeVocabulary,input.ValueType,Bindable:input.Bindable))).GroupBy(field=>field.PropertyName,StringComparer.Ordinal).Select(group=>group.First());
  foreach(var descriptor in declared)
  {
   var property=node.Parameters.GetType().GetProperty(descriptor.PropertyName,BindingFlags.Public|BindingFlags.Instance);
   if(property is not {CanRead:true,CanWrite:true}||property.GetIndexParameters().Length!=0){fields.Add(new(node,descriptor.PropertyName,descriptor.PropertyName,descriptor.DisplayName,null,"正式属性不可读写"));continue;}
   var type=property.PropertyType;Port port;FieldPresentation presentation;
   if(type.IsEnum){port=new(descriptor.PropertyName,DataType.Text,EditModes:FieldEditModes.Constant,Choices:new TextChoiceConstraint(Enum.GetNames(type)));presentation=new(port.Id,descriptor.DisplayName,"运行参数",fields.Count,FieldEditorKind.Choice,Enum.GetNames(type));}
   else
   {
    if(!BindingClrAdapter.TryDescribe(type,out var declaration,out _)||declaration.Shape!=ValueShape.Scalar){fields.Add(new(node,descriptor.PropertyName,descriptor.PropertyName,descriptor.DisplayName,null,"需要专用类型编辑器"));continue;}
    port=BindingClrAdapter.DescribePort(descriptor.PropertyName,type) with{EditModes=descriptor.Bindable?FieldEditModes.Constant|FieldEditModes.Reference:FieldEditModes.Constant};
    presentation=new(port.Id,descriptor.DisplayName,"运行参数",fields.Count,FieldEditorKind.Scalar);
   }
   var accessor=new ConfigurationFieldAccessor(port,presentation,descriptor.PropertyName,
    ()=>type.IsEnum?property.GetValue(node.Parameters)?.ToString():BindingClrAdapter.NormalizeValue(port,property.GetValue(node.Parameters)),
    value=>property.SetValue(node.Parameters,type.IsEnum?Enum.Parse(type,(string)value!):BindingClrAdapter.Materialize(port,type,value)));
   fields.Add(new(node,port.Id,accessor.StoragePath,descriptor.DisplayName,accessor));
  }
  return fields;
 }
 private static bool Supports(TypeRef type)=>type.Shape==ValueShape.Scalar||type.Shape==ValueShape.Record&&type.Fields.All(field=>Supports(field.Type));
 internal static RootParameterCandidate Resolve(WorkflowNodeBase node,RootParameterProxy proxy)
 {
  if(node.Id!=proxy.NodeId||node.ToolType!=proxy.ToolType)throw new InvalidOperationException("代理来源节点或工具已变化。");
  var field=Describe(node).SingleOrDefault(field=>field.FieldId==proxy.FieldId&&field.StoragePath==proxy.StoragePath)??throw new InvalidOperationException("正式字段已移除或存储路径已变化。");
  if(field.Unavailable!=null||field.Field==null)throw new InvalidOperationException(field.Unavailable??"字段不可编辑。");return field;
 }
 internal static IEnumerable<RootParameterCandidate> Describe(Flow root)=>root.Nodes.SelectMany(Describe);
}
