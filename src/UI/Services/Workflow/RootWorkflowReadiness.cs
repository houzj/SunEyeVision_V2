using SunEyeVision.Workflow;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Services.Workflow;

/// <summary>只读设计期检查；不执行工具、不读取上一轮成败，不代替正式运行准入。</summary>
internal static class RootWorkflowReadiness
{
 internal static IReadOnlyList<string> Evaluate(Flow root, Func<string,Flow?>? resolve)
 {
  var errors=new List<string>();
  if(root.IsSubroutine)return new[]{"此状态仅用于根流程。"};
  if(!PluginLoader.IsLoaded)errors.Add("工具元数据尚未加载完成。");
  try{errors.AddRange(WorkflowDefinitionValidator.Validate(root,resolve));}
  catch(Exception error){errors.Add("流程定义校验失败："+error.Message);}
  foreach(var node in root.Nodes)
  {
   var label=string.IsNullOrWhiteSpace(node.DispName)?node.Name:node.DispName;
   if(!ToolRegistry.ToolExists(node.ToolType))errors.Add(label+"：工具未加载（"+node.ToolType+"）。");
   if(node.Parameters==null){errors.Add(label+"：参数尚未配置。");continue;}
   try
   {
    var result=node.Parameters.Validate();
    errors.AddRange(result.Errors.Select(message=>label+"："+message));
    if(!result.IsValid&&result.Errors.Count==0)errors.Add(label+"：参数校验未通过。");
    errors.AddRange(node.Parameters.ValidateParameterContract().Select(issue=>label+"："+issue.Message));
    errors.AddRange(node.Parameters.RebindingRequired.Select(pair=>label+"."+pair.Key+"："+pair.Value));
    errors.AddRange(node.Parameters.BindingValidationPending.Select(pair=>label+"."+pair.Key+"：待确认，"+pair.Value));
   }
   catch(Exception error){errors.Add(label+"：参数校验失败，"+error.Message);}
  }
  // 与恢复件一致：空图可以没有配置错误，是否可运行仍由正式运行入口判断。
  return errors.Distinct(StringComparer.Ordinal).ToArray();
 }
}
