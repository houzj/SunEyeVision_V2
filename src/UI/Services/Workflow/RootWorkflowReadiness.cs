using SunEyeVision.Workflow;


using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Services.Workflow;

/// <summary>只读设计期检查；不执行工具、不读取上一轮成败，不代替正式运行准入。</summary>
internal static class RootWorkflowReadiness
{
 internal static IReadOnlyList<string> Evaluate(SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot directory,Flow root, Func<string,Flow?>? resolve)
 {
  var errors=new List<string>();
  if(root.IsSubroutine)return new[]{"此状态仅用于根流程。"};
  if(!directory.IsActive)errors.Add("工具运行目录已退役。");
  try{errors.AddRange(WorkflowDefinitionValidator.Validate(root,resolve));}
  catch(Exception error){errors.Add("流程定义校验失败："+error.Message);}
  foreach(var node in root.Nodes)
  {
   var label=string.IsNullOrWhiteSpace(node.DispName)?node.Name:node.DispName;
   if(!directory.Contains(node.ToolType)){errors.Add(label+"：工具未加载（"+node.ToolType+"）。");continue;}
   var configuration=node.RuntimeConfiguration;
   if(configuration is null){errors.Add(label+"：Runtime 参数尚未配置。");continue;}
   if(!ReferenceEquals(configuration.Directory,directory)){errors.Add(label+"：运行目录代次不一致。");continue;}
   try
   {
    errors.AddRange(configuration.Validate().Select(issue=>label+"："+issue.Message));
    if(ImageSourceResolver.IsImageSource(node.ToolType))
     errors.AddRange(EvaluateImageSource(node.Id,ImageSourcePolicy.Read(node)).Select(message=>label+"："+message));
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
 internal static IReadOnlyList<string> EvaluateImageSource(string nodeId,ImageSourcePolicy? source)
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
