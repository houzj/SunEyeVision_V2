using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.UI.Features.WorkflowUI;
public partial class WorkflowTabViewModel
{
 private IReadOnlyList<string> _rootConfigurationErrors=Array.Empty<string>();
 private bool _readinessQueued;
 internal void QueueRootReadiness()
 {
  if(_readinessQueued)return;_readinessQueued=true;
  System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>{_readinessQueued=false;if(!IsRunning)RefreshRootReadiness();}));
 }
 public IReadOnlyList<string> RootConfigurationErrors=>_rootConfigurationErrors;
 public bool IsRootConfigurationReady=>_canvasSession is not null&&RootWorkflowNodes.Count>0&&_rootConfigurationErrors.Count==0;
 public string RootConfigurationSummary=>IsRootConfigurationReady?"配置检查通过；运行时仍由正式执行入口校验。":string.Join("\n",_rootConfigurationErrors);
 internal void RefreshRootReadiness()
 {
  if(IsRunning)return;
  IReadOnlyList<string> errors;
  try{errors=_canvasSession is null?new[]{"没有实际方案会话。"}:RootWorkflowNodes.Count==0?new[]{"流程没有可执行节点。"}:_canvasSession.CheckWorkflowReadiness(Id).Select(issue=>issue.Node.NodeId+"："+issue.Message).ToArray();}
  catch(InvalidOperationException error){errors=new[]{error.Message};}
  _rootConfigurationErrors=errors;
  OnPropertyChanged(nameof(RootConfigurationErrors));OnPropertyChanged(nameof(IsRootConfigurationReady));OnPropertyChanged(nameof(RootConfigurationSummary));OnPropertyChanged(nameof(RootSettings));OnPropertyChanged(nameof(RootWorkflowNodes));
 }
}
