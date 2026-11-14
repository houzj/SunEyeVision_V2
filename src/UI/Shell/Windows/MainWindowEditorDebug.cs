using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.UI.Services.Editing;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private ProductionEditorContext? _editorDebugContext;
 private RuntimeNodeConfigurationControl? _editorDebugControl;
 private bool _refreshingEditorDebug, _editorDebugRefreshPending;
 /// <summary>编辑器相关输入的单图临时显示态（VM ModuleResultView.OnRelatedIOChanged）：
 /// 只改变结果工作区的视图选择，不写正式 SelectedDisplayReference。</summary>
 internal sealed record EditorRelatedImage(string Key,string PortId,Guid? EditorScopeId);
 private EditorRelatedImage? _editorRelatedImage;
 internal EditorRelatedImage? EditorRelatedImageState=>_editorRelatedImage;
 internal void SetEditorRelatedImage(EditorRelatedImage? value)=>_editorRelatedImage=value;
 /// <summary>ROI 交互借帧被宿主阻断的原因（空=未阻断）；由 presenter 在阻断时写入，
 /// 经参数视图传递给工具侧编辑器的不可绘反馈（原件 RoiDrawTips 语义：
 /// "无效的绘制操作！请检查绘制图像是否为模块的输入图像"，用户实机截图在案）。</summary>
 internal string? RoiDrawBlockReason{get;set;}
 private ProductionEditorContext? _workspaceEditorContext;
 private ToolConfiguration? _workspaceEditorConfiguration;
 private DeclaredOutputSnapshot? _workspaceEditorSnapshot;
 private DeclaredOutputSnapshot? _retainedEditorDebugSnapshot;
 private IDisposable? _retainedEditorDebugLease;
 // Image-source policy edits replace the draft configuration object before the
 // display refresh callback runs. Keep the presentation contract that belonged
 // to the completed result, so the result scene can be rebuilt without
 // restyling or invalidating its graphics/text.
 private ResultDisplayDescriptor? _retainedResultDisplayDescriptor;
 private ResultDisplayAppearance? _retainedResultDisplayAppearance;
 private ResultGeometrySnapshot? _retainedResultDisplayGeometry;
 private string[] _workspaceEditorImages=[];
 private ProductionEditorContext? SelectedEditorContext=>_editorDebugContext is {IsValid:true} context
  &&ReferenceEquals(DocumentManager.CurrentSession,context.Session)&&ReferenceEquals(SelectedNode,context.FormalNode)?context:null;
 private DeclaredOutputSnapshot? CurrentEditorDebugSnapshot=>SelectedEditorContext is not null?_editorDebugControl?.CurrentDebugSnapshot:null;
 // Debug and formal executions share the document owner. Retention is therefore
 // identified by the retained snapshot reference plus the active node/workflow,
 // not by a different DocumentSessionId.
 private bool IsRetainedEditorDebugSnapshot(DeclaredOutputSnapshot snapshot)
  =>ReferenceEquals(snapshot,_retainedEditorDebugSnapshot)
    &&DocumentManager.CurrentSession is {} session
    &&SelectedNode is {} node
    &&snapshot.Identity.NodeId==node.Id
    &&snapshot.SourceWorkflowId==WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
 internal void RetainEditorDebugSnapshot(DeclaredOutputSnapshot snapshot)
 {
  if(!snapshot.IsComplete)return;
  if(ReferenceEquals(snapshot,_retainedEditorDebugSnapshot))return;
  var lease=snapshot.Retain();
  _retainedEditorDebugLease?.Dispose();
  _retainedEditorDebugLease=lease;_retainedEditorDebugSnapshot=snapshot;
 }
 private void ClearRetainedEditorDebugSnapshot()
 {
  _retainedEditorDebugSnapshot=null;
  _retainedEditorDebugLease?.Dispose();_retainedEditorDebugLease=null;
  _retainedResultDisplayDescriptor=null;
  _retainedResultDisplayAppearance=null;
  _retainedResultDisplayGeometry=null;
 }
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
   var previousEditorImages=_workspaceEditorImages;
   var snapshot=CurrentEditorDebugSnapshot;
   var contextChanged=!ReferenceEquals(context,_workspaceEditorContext);
   var snapshotChanged=!ReferenceEquals(snapshot,_workspaceEditorSnapshot);
   var imagesChanged=false;
   if(configuration is not null&&draft is not null)
   {
    var images=draft.InputSchema.Inputs
      .Where(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image))
      .Select(port=>GetWorkspaceImageSourceKey(configuration.Fields.Read(port.Id),draft.WorkflowId)??"").ToArray();
    imagesChanged=!images.SequenceEqual(_workspaceEditorImages,StringComparer.Ordinal);
    _workspaceEditorImages=images;
    _workspaceEditorConfiguration=configuration;
   }
   _workspaceEditorContext=context;
   _workspaceEditorSnapshot=snapshot;
   if(imagesChanged&&previousEditorImages.Any(key=>!string.IsNullOrEmpty(key)))
   {
    // 只有真实发生变更的第二输入才切换单图显示；打开窗口本身不改变已呈现结果。
    var imageInput=draft?.InputSchema.Inputs.FirstOrDefault(port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image));
    var portId=imageInput is not null&&configuration is not null
     &&configuration.Fields.Read(imageInput.Id) is ReferenceSetting reference?reference.Reference.PortId:null;
    var key=_workspaceEditorImages.FirstOrDefault(value=>!string.IsNullOrEmpty(value));
    _editorRelatedImage=key is not null&&portId is not null?new EditorRelatedImage(key,portId,context?.ScopeId):null;
   }
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
  if(contextChanged&&context is null)_editorRelatedImage=null;
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
  if(ReferenceEquals(CurrentEditorDebugSnapshot,snapshot)||IsRetainedEditorDebugSnapshot(snapshot))return true;
  if(snapshot.Identity.DocumentSessionId!=session.ResultOwnerId)return false;
  return session.IsPublishedResultCurrent(workflowId,node.Id,snapshot);
 }
}
