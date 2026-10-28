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
    if(ImageSourceResolver.IsImageSource(node.ToolType))
     errors.AddRange(EvaluateImageSource(node.Id,node.ImageSourceConfig).Select(message=>label+"："+message));
    else
    {
     var result=node.Parameters.Validate();
     errors.AddRange(result.Errors.Select(message=>label+"："+message));
     if(!result.IsValid&&result.Errors.Count==0)errors.Add(label+"：参数校验未通过。");
    }
    errors.AddRange(node.Parameters.ValidateParameterContract().Select(issue=>label+"："+issue.Message));
    errors.AddRange(node.Parameters.RebindingRequired.Select(pair=>label+"."+pair.Key+"："+pair.Value));
    errors.AddRange(node.Parameters.BindingValidationPending.Select(pair=>label+"."+pair.Key+"：待确认，"+pair.Value));
   }
   catch(Exception error){errors.Add(label+"：参数校验失败，"+error.Message);}
  }
  foreach(var proxy in root.RootConfiguration.RunParameters)
  {
   try{var node=root.Nodes.SingleOrDefault(n=>n.Id==proxy.NodeId)??throw new InvalidOperationException("来源节点已移除。");_=SunEyeVision.UI.Services.Editing.RootParameterCatalog.Resolve(node,proxy);}
   catch(Exception error){errors.Add(proxy.DisplayName+"：根参数代理失效，"+error.Message);}
  }
  // 与恢复件一致：空图可以没有配置错误，是否可运行仍由正式运行入口判断。
  return errors.Distinct(StringComparer.Ordinal).ToArray();
 }
 internal static IReadOnlyList<string> EvaluateImageSource(string nodeId,ImageSourceConfig? source)
 {
  if(source is null)return new[]{"图像源尚未配置。"};
  var errors=new List<string>();
  if(source.NodeId!=nodeId||source.FilePaths is null||source.ForRunPaths is null)
   return new[]{"图像源身份或列表无效。"};
  if(!Enum.IsDefined(source.SourceKind)||!Enum.IsDefined(source.OutputFormat))errors.Add("图像来源或输出格式无效。");
  if(source.CameraTimeoutMs<=0)errors.Add("取帧超时必须大于零。");
  if(source.SNInitialValue<0)errors.Add("SN不能为负数。");
  if(source.RunMode is not (0 or 1))errors.Add("图像源运行模式无效。");
  if(source.CurrentIndex < -1 || source.CurrentIndex>=source.FilePaths.Count)errors.Add("图像源当前选择无效。");
  if(source.SourceKind==SunEyeVision.Plugin.SDK.Vision.Contracts.ImageSourceKind.Camera)
  {
   if(string.IsNullOrWhiteSpace(source.CameraDeviceId))errors.Add("请选择方案中的相机。");
  }
  else
  {
   var entries=source.RunMode==0?source.FilePaths:source.FilePaths.Where(path=>source.ForRunPaths.Contains(path,StringComparer.Ordinal)).ToList();
   if(entries.Count==0)errors.Add("图像来源没有可运行的图片。");
   if(source.CurrentIndex>=0&&source.CurrentIndex<source.FilePaths.Count&&
      !entries.Contains(source.FilePaths[source.CurrentIndex],StringComparer.Ordinal))errors.Add("已选图片不在运行序列中。");
  }
  return errors;
 }

}
