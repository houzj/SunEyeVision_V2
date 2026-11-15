using System;
using System.Windows;
using SunEyeVision.UI.Shell.Windows;
namespace SunEyeVision.UI.Services.Workflow;
/// <summary>耗时详情是单个只读拥有者窗口；退出不释放由执行管理器持有的快照。</summary>
internal sealed class WorkflowExecutionSnapshotPresenter
{
 internal WorkflowExecutionSnapshotWindow? CurrentWindow {get;private set;}
 private MainWindow? _mainWindowAnchor;
  private EventHandler? _ownerStateChanged;
 private ShutdownMode _previousShutdownMode;
 public bool Show(WorkflowExecutionSnapshot? snapshot,Window? owner)
 {
  if(!Close())return false;
  if(snapshot==null)return false;
  // The timing viewer is an independent read-only tool window. It must not be
  // an owned child of MainWindow: closing the viewer must never participate in
  // the solution/main-window shutdown lifecycle.
  var window=new WorkflowExecutionSnapshotWindow(snapshot)
  {
   Owner=_mainWindowAnchor,
   WindowStartupLocation=WindowStartupLocation.CenterOwner,
   ShowInTaskbar=false
  };
  // WPF may promote the first independently shown window when MainWindow was
  // not explicitly restored. Keep the real workspace as the application's
  // shutdown anchor so closing this viewer can never trigger application exit.
  _mainWindowAnchor = owner as MainWindow;
  if (_mainWindowAnchor is not null && !ReferenceEquals(System.Windows.Application.Current?.MainWindow, _mainWindowAnchor))
   System.Windows.Application.Current.MainWindow = _mainWindowAnchor;
  if (_mainWindowAnchor is not null)
  {
   _ownerStateChanged = (_, _) =>
   {
    if (CurrentWindow is { IsLoaded:true } viewer)
     viewer.WindowState = _mainWindowAnchor.WindowState;
   };
   _mainWindowAnchor.StateChanged += _ownerStateChanged;
   window.WindowState = _mainWindowAnchor.WindowState;
  }
  if (System.Windows.Application.Current is { } application)
  {
   _previousShutdownMode = application.ShutdownMode;
   application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
  }
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
  if (_mainWindowAnchor is { IsLoaded:true } mainWindow && !ReferenceEquals(System.Windows.Application.Current?.MainWindow, mainWindow))
   System.Windows.Application.Current.MainWindow = mainWindow;
  if (_mainWindowAnchor is { } anchor && _ownerStateChanged is { } stateChanged)
   anchor.StateChanged -= stateChanged;
  _ownerStateChanged=null;
  if (System.Windows.Application.Current is { } application)
   application.ShutdownMode = _previousShutdownMode;
  _mainWindowAnchor=null;
  if(ReferenceEquals(CurrentWindow,window))CurrentWindow=null;
 }
}
