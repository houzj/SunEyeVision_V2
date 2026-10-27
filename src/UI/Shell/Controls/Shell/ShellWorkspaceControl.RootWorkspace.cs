using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Shell.Windows;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
 private void RootReadinessClick(object sender,RoutedEventArgs e)
 {if(GetWorkflowFromContextMenu(sender) is {} tab){tab.RefreshRootReadiness();MessageBox.Show(Window.GetWindow(this),tab.RootConfigurationSummary,"流程配置检查",MessageBoxButton.OK,MessageBoxImage.Information);}}
 private void RootCopyClick(object sender,RoutedEventArgs e)
 {if(GetWorkflowFromContextMenu(sender) is {} tab)_viewModel?.CopyRootWorkspace(tab);}
 private void RootUndoClick(object sender,RoutedEventArgs e)=>_viewModel?.UndoRootWorkspace();
 private void RootRedoClick(object sender,RoutedEventArgs e)=>_viewModel?.RedoRootWorkspace();
 private void RootMenuOpened(object sender,RoutedEventArgs e)
 {
  if(sender is not ContextMenu menu)return;
  foreach(var item in menu.Items.OfType<MenuItem>())
  {
   if(item.Tag as string=="RootEdit")item.IsEnabled=_viewModel?.CanEditRootWorkspace==true&&menu.DataContext is SunEyeVision.UI.Features.WorkflowUI.WorkflowTabViewModel {IsInSubroutine:false};
   if(item.Tag as string=="RootUndo")item.IsEnabled=_viewModel?.CanUndoRootWorkspace==true;
   if(item.Tag as string=="RootRedo")item.IsEnabled=_viewModel?.CanRedoRootWorkspace==true;
  }
 }
 private void RootRunSettingsClick(object sender,RoutedEventArgs e)
 {
  var tab=GetWorkflowFromContextMenu(sender);
  if(tab==null||_viewModel?.PrepareRootSettings(tab) is not {} admission)return;
  var window=new RootWorkflowRunSettingsWindow(admission.Opening){Owner=Window.GetWindow(this)};
  bool accepted;using(_viewModel.TrackParameterWindow(window)){accepted=window.ShowDialog()==true;}
  if(accepted&&!_viewModel.ChangeRootSettings(tab,admission,window.Settings))_viewModel.StatusText="流程状态已变化，运行设置未提交。";
 }
 private void RootStopOnNgClick(object sender,RoutedEventArgs e)
 {
  var tab=GetWorkflowFromContextMenu(sender);
  if(tab==null||_viewModel?.ResolveEditableRoot(tab) is not {} root)return;
  _viewModel.ChangeRootSettings(tab,root,root.WorkspaceSettings,root.WorkspaceSettings with{StopOnNg=!root.WorkspaceSettings.StopOnNg});
 }
}
