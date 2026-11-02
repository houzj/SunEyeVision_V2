using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Services.Editing;
internal sealed record RootParameterCandidate(WorkflowNodeBase Node,string FieldId,string StoragePath,string DisplayName,ConfigurationFieldAccessor? Field,string? Unavailable=null,RootResourceProxyBinding? Resource=null)
{public string Label=>Node.DispName+" · "+DisplayName+(Unavailable is null?"":"（"+Unavailable+"）");}
internal static class RootParameterCatalog
{
 internal static IReadOnlyList<RootParameterCandidate> Describe(WorkflowNodeBase node)
 {
  return node.RuntimeConfiguration is {} runtime ? DescribeRuntime(node,runtime) : Array.Empty<RootParameterCandidate>();
 }
 private static IReadOnlyList<RootParameterCandidate> DescribeRuntime(WorkflowNodeBase node,RuntimeToolConfiguration runtime)
 {
  return runtime.BindingTargets.Where(port=>!runtime.IsDisplayField(port.Id)&&port.EditModes!=FieldEditModes.None).Select(port=>
  {
   var schema=runtime.SettingsSchema?.Inputs.Any(field=>field.Id==port.Id)==true?runtime.SettingsSchema:runtime.Schema;
   var presentation=schema?.Presentation?.Fields.SingleOrDefault(field=>field.FieldId==port.Id)
    ??new FieldPresentation(port.Id,port.Id,"运行参数",0,FieldEditorKind.Scalar);
   var adapter=new RootResourceProxyBinding(port.Id,port,false,Runtime:true);
   // Structured constants need their dedicated composer; references still use the declared type.
   var unavailable=!Supports(port.Type)&&(port.EditModes&FieldEditModes.Reference)==0?"需要专用集合或资源编辑器":null;
   return new RootParameterCandidate(node,port.Id,port.Id,presentation.DisplayName,
    new ConfigurationFieldAccessor(port,presentation,port.Id,()=>adapter.Literal(node),value=>adapter.Write(node,new ConstantSetting(value),null!,"")),
    unavailable,adapter);
  }).ToArray();
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
