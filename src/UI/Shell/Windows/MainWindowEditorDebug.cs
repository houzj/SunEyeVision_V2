using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.UI.Services.Editing;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private ProductionEditorContext? _editorDebugContext;
 private RuntimeNodeConfigurationControl? _editorDebugControl;
 private bool _refreshingEditorDebug, _editorDebugRefreshPending;
 private ProductionEditorContext? _workspaceEditorContext;
 private ToolConfiguration? _workspaceEditorConfiguration;
 private DeclaredOutputSnapshot? _workspaceEditorSnapshot;
 private string[] _workspaceEditorImages=[];
 private ProductionEditorContext? SelectedEditorContext=>_editorDebugContext is {IsValid:true} context
  &&ReferenceEquals(DocumentManager.CurrentSession,context.Session)&&ReferenceEquals(SelectedNode,context.FormalNode)?context:null;
 private DeclaredOutputSnapshot? CurrentEditorDebugSnapshot=>SelectedEditorContext is not null?_editorDebugControl?.CurrentDebugSnapshot:null;
 private void RefreshEditorDebugResult()
 {
  if(_documentHostReleased)return;
  if(_refreshingEditorDebug){_editorDebugRefreshPending=true;return;}
  _refreshingEditorDebug=true;
  try
  {
   do
   {
   _editorDebugRefreshPending=false;
   var context=SelectedEditorContext;
   var draft=context?.PreparedDraft;
   var configuration=draft?.Configuration;
   var snapshot=CurrentEditorDebugSnapshot;
   var contextChanged=!ReferenceEquals(context,_workspaceEditorContext);
   var snapshotChanged=!ReferenceEquals(snapshot,_workspaceEditorSnapshot);
   var imagesChanged=false;
   if(contextChanged||!ReferenceEquals(configuration,_workspaceEditorConfiguration))
   {
    var images=draft is null?Array.Empty<string>():draft.InputSchema.Inputs
      .Where(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image))
      .Select(port=>GetWorkspaceImageSourceKey(configuration!.Fields.Read(port.Id),draft.WorkflowId)??"").ToArray();
    imagesChanged=!images.SequenceEqual(_workspaceEditorImages,StringComparer.Ordinal);
    _workspaceEditorImages=images;
    _workspaceEditorConfiguration=configuration;
   }
   _workspaceEditorContext=context;
   _workspaceEditorSnapshot=snapshot;
   if(contextChanged||snapshotChanged)
   {
    // Opening the parameter window creates a draft context before its first
    // private execution. Keep the selected node's formal publication and
    // image/overlay lease visible during that interval. A new debug snapshot
    // replaces it when execution completes, and closing the context restores
    // the formal publication through the normal refresh path.
    var preserveFormalPublication=contextChanged&&context is not null&&snapshot is null
     &&_resultPublication is not null&&_resultPublicationSnapshot is not null
     &&SelectedNode?.Id==_resultPublicationSnapshot.Identity.NodeId;
    if(!preserveFormalPublication)RefreshFormalPublishedResult();
    NotifyResultDisplayChanged();
   }
   if(contextChanged||snapshotChanged||imagesChanged)ResultWorkspaceChanged?.Invoke();
   }while(_editorDebugRefreshPending&&!_documentHostReleased);
  }
  finally{_refreshingEditorDebug=false;_editorDebugRefreshPending=false;}
 }
 private bool IsCurrentPresentedSnapshot(DocumentSession session,string workflowId,NodeDefinition node,DeclaredOutputSnapshot snapshot)
 {
  // 调试快照优先；编辑器打开不得否决正式发布的当前性——原件 ModuleResultView 在编辑态
  // 同样显示该模块的正式结果（形状+文本）。否则 publication.IsCurrent() 恒 false，
  // 结果区只留图像、叠加被清空。
  if(ReferenceEquals(CurrentEditorDebugSnapshot,snapshot))return true;
  if(snapshot.Identity.DocumentSessionId!=session.ResultOwnerId)return false;
  return session.IsPublishedResultCurrent(workflowId,node.Id,snapshot);
 }
}
