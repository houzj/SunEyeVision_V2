using System;
using System.Windows;
using SunEyeVision.UI.Shell.Windows;
namespace SunEyeVision.UI.Services.Workflow;
/// <summary>耗时详情是单个只读拥有者窗口；退出不释放由执行管理器持有的快照。</summary>
internal sealed class WorkflowExecutionSnapshotPresenter
{
 internal WorkflowExecutionSnapshotWindow? CurrentWindow {get;private set;}
 public bool Show(WorkflowExecutionSnapshot? snapshot,Window? owner)
 {
  if(!Close())return false;
  if(snapshot==null)return false;
  var window=new WorkflowExecutionSnapshotWindow(snapshot){Owner=owner};
  CurrentWindow=window;window.Closed+=WindowClosed;
  try{window.Show();return true;}
  catch{CurrentWindow=null;window.Closed-=WindowClosed;window.DataContext=null;window.Close();throw;}
 }
 public bool Close()
 {
  CurrentWindow?.Close();
  return CurrentWindow==null;
 }
 private void WindowClosed(object? sender,EventArgs e)
 {
  if(sender is not WorkflowExecutionSnapshotWindow window)return;
  window.Closed-=WindowClosed;window.DataContext=null;
  if(ReferenceEquals(CurrentWindow,window))CurrentWindow=null;
 }
}
