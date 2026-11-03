using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.UI.Services.Editing;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private ProductionEditorContext? _editorDebugContext;
 private RuntimeNodeConfigurationControl? _editorDebugControl;
 private bool _refreshingEditorDebug;
 private ProductionEditorContext? SelectedEditorContext=>_editorDebugContext is {IsValid:true} context
  &&ReferenceEquals(DocumentManager.CurrentSession,context.Session)&&ReferenceEquals(SelectedNode,context.FormalNode)?context:null;
 private DeclaredOutputSnapshot? CurrentEditorDebugSnapshot=>SelectedEditorContext is not null?_editorDebugControl?.CurrentDebugSnapshot:null;
 private void RefreshEditorDebugResult()
 {
  if(_documentHostReleased||_refreshingEditorDebug)return;
  _refreshingEditorDebug=true;
  try{RefreshFormalPublishedResult();NotifyResultDisplayChanged();ResultWorkspaceChanged?.Invoke();}
  finally{_refreshingEditorDebug=false;}
 }
 private bool IsCurrentPresentedSnapshot(DocumentSession session,string workflowId,NodeDefinition node,DeclaredOutputSnapshot snapshot)
 {
  if(ReferenceEquals(CurrentEditorDebugSnapshot,snapshot))return true;
  if(snapshot.Identity.DocumentSessionId!=session.ResultOwnerId)return false;
  using var latest=session.AcquirePublishedResult(workflowId,node.Id);
  return latest is not null&&ReferenceEquals(latest.Snapshot,snapshot);
 }
}
