using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.Application;
using SunEyeVision.UI.Core;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private DocumentSession? _observedDocumentSession;
    private DocumentSessionManager? _documentEventOwner;
    private bool _documentHostReleased;
    public void ReleaseDocumentHost()
    {
        if(_documentHostReleased)return;_documentHostReleased=true;ClearCanvasClipboard();
        WorkflowTabViewModel.UnbindDocumentManager();
        if(_documentEventOwner is {} manager)manager.SessionChanged-=FormalDocumentSessionChanged;
        _documentEventOwner=null;UnbindFormalSession();
        ReleaseProductionEditors();ClearFormalImageSourceSelection();ReleaseActiveResultView();RetireResultPublication();
        // 方案会话仍持有正式结果/执行代次时，插件目录不能退役。关闭主窗口时
        // 由同一会话所有者先丢弃未保存编辑并释放会话资源，再进入目录排空。
        // 这只用于宿主退出（ReleaseDocumentHost），不改变方案切换语义。
        DocumentManager.CloseCurrent(discardPendingEdits:true);
    }
    private void UnbindFormalSession()
    {
        if(_observedDocumentSession is {} session){session.Changed-=FormalDocumentChanged;session.RuntimeResultAvailable-=FormalResultAvailable;session.NodeDisplayNavigationChanged-=FormalDisplayNavigationChanged;session.WorkspaceNavigationChanged-=FormalWorkspaceNavigationChanged;}
        // 会话失效即投影失效：全部常驻图像源缓存逐出，避免跨会话复用旧会话的投影。
        ReleaseImageSourceBindings();
        _observedDocumentSession=null;
    }
    private void FormalDocumentSessionChanged(object? sender,DocumentSessionReplacement change)
    {
        if(_documentHostReleased)return;
        UnbindFormalSession();ReleaseProductionEditors();ClearFormalImageSourceSelection();ReleaseFormalExecution();SelectedNode=null;
        BindDocumentHistory();RefreshFormalImageSourceSelection();UpdateCurrentSolutionInfo();
    }
    private void BindDocumentHistory()
    {
        if(_documentHostReleased)return;
        if(!ReferenceEquals(_documentEventOwner,DocumentManager))
        {
            if(_documentEventOwner is {} previous)previous.SessionChanged-=FormalDocumentSessionChanged;
            _documentEventOwner=DocumentManager;DocumentManager.SessionChanged+=FormalDocumentSessionChanged;
        }
        var session=DocumentManager.CurrentSession;
        if(ReferenceEquals(session,_observedDocumentSession))return;
        UnbindFormalSession();_observedDocumentSession=session;
        if(session is not null){session.Changed+=FormalDocumentChanged;session.RuntimeResultAvailable+=FormalResultAvailable;session.NodeDisplayNavigationChanged+=FormalDisplayNavigationChanged;session.WorkspaceNavigationChanged+=FormalWorkspaceNavigationChanged;}
        QueueFolderTriggerTracking();
    }
    private void FormalDocumentChanged(object? sender,DocumentChangeSet change)
    {
        if(_documentHostReleased||!ReferenceEquals(sender,_observedDocumentSession)||!ReferenceEquals(sender,DocumentManager.CurrentSession))return;
        foreach (var item in change.ChangedNodes)
            if ((item.Aspects & NodeChangedAspect.Configuration) != 0)
                _formalImageInputs?.RetireFolderCursor(change.EditSessionId, item.Identity.WorkflowId, item.Identity.NodeId);
        if (!change.ChangedNodes.IsDefaultOrEmpty || !change.ChangedRoots.IsDefaultOrEmpty)
            QueueFolderTriggerTracking();
        if(!change.RemovedNodes.IsDefaultOrEmpty)CanvasClipboardNodesDeleted(change.RemovedNodes.Select(item=>item.Node).ToArray());
        if(!change.ChangedRoots.IsDefaultOrEmpty)
        {
            // Retire selection-bound participants before their acknowledgement. A removed
            // root cannot accept a rebase; Undo must not reactivate its old windows.
            foreach(var editor in _productionEditors.ToArray())if(!editor.IsValid)editor.Discard();
            if(_rootConfigurationWindow is {} rootWindow&&change.ChangedRoots.Any(item=>
                item.Kind==RootCollectionChangeKind.Removed&&ReferenceEquals(item.Workflow,rootWindow.Context.Root)))
            {rootWindow.Context.Dispose();rootWindow.Close();}
            var session=_observedDocumentSession!;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>
            {
                if(_documentHostReleased||!ReferenceEquals(session,DocumentManager.CurrentSession))return;
                WorkflowTabViewModel.ReconcileDocumentTabs(session);
                RefreshChangedDocumentViews(change);
            }));
            return;
        }
        RefreshChangedDocumentViews(change);
    }
    private void RefreshChangedDocumentViews(DocumentChangeSet change)
    {
        foreach(var editor in _productionEditors.ToArray())if(!editor.IsValid)editor.Discard();
        // 提交会重建节点实例：无条件按稳定 Id 把选择重绑定到当前实例。
        // 下游（ImageSourceResolver 等）仍使用引用判定，不重绑定就会被误判失效并清空图像源条。
        if(SelectedNode is {} selected)
        {
            var rebound=FindCurrentNode(selected.Id);
            if(rebound is null)SelectedNode=null;
            else if(!ReferenceEquals(rebound,selected))SelectedNode=rebound;
            else RefreshFormalPublishedResult();
        }
        RefreshFormalImageSourceSelection();NotifyResultDisplayChanged();foreach(var tab in WorkflowTabViewModel.Tabs)tab.QueueRootReadiness();UpdateCurrentSolutionInfo();UpdateUndoRedoCommands();
        OnPropertyChanged(nameof(CanUndoRootWorkspace));OnPropertyChanged(nameof(CanRedoRootWorkspace));OnPropertyChanged(nameof(EditHistoryLocations));
    }
    internal void SetFormalDisplaySelection(SunEyeVision.Domain.Workflows.NodeDefinition node,string? reference)
        =>SetFormalDisplayNavigation(node,reference,null,replaceOverlays:false);
    internal void SetFormalDisplayOverlays(SunEyeVision.Domain.Workflows.NodeDefinition node,string? reference,
        SunEyeVision.Domain.Workflows.ImageOverlaySelection? overlays)
        =>SetFormalDisplayNavigation(node,reference,overlays,replaceOverlays:true);
    private void SetFormalDisplayNavigation(SunEyeVision.Domain.Workflows.NodeDefinition node,string? reference,
        SunEyeVision.Domain.Workflows.ImageOverlaySelection? overlays,bool replaceOverlays)
    {
        var session=DocumentManager.CurrentSession??throw new InvalidOperationException("没有当前方案。");
        var workflowId=WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId??throw new InvalidOperationException("没有当前流程。");
        if(!ReferenceEquals(SelectedNode,node)||!OwnsSelectedDocumentNode(node))throw new InvalidOperationException("图像显示选择已失效。");
        if(replaceOverlays)session.SetNodeDisplayOverlays(workflowId,node,reference,overlays);
        else session.SetNodeDisplayReference(workflowId,node,reference);
    }
    private void FormalDisplayNavigationChanged(object? sender,NodeDisplayNavigationChange change)
    {
        if(_documentHostReleased||!ReferenceEquals(sender,DocumentManager.CurrentSession))return;
        UpdateCurrentSolutionInfo();
        if(ReferenceEquals(SelectedNode,change.Node)){RefreshFormalImageSourceSelection();ResultWorkspaceChanged?.Invoke();NotifyResultDisplayChanged();}
    }
    private void FormalWorkspaceNavigationChanged(object? sender,WorkflowNavigationChange change)
    {
        if(_documentHostReleased||!ReferenceEquals(sender,DocumentManager.CurrentSession))return;
        UpdateCurrentSolutionInfo();
    }
    private void FormalResultAvailable(object? sender,PublishedNodeResult result)
    {
        if(_documentHostReleased||!ReferenceEquals(sender,DocumentManager.CurrentSession))return;
        if(_formalImageInputs is not null&&sender is DocumentSession session)RefreshFormalImageReadReceipts(session);
        if(SelectedNode?.Id==result.Node.NodeId&&WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId==result.Node.WorkflowId)
            RefreshFormalPublishedResult();
        ResultWorkspaceChanged?.Invoke();
    }
    // Full history navigation has no public session contract yet; Undo/Redo use the actual session.
    private ICommand? _navigateEditHistoryCommand;
    public ICommand NavigateEditHistoryCommand=>_navigateEditHistoryCommand??=new RelayCommand(()=>{StatusText="历史定位尚未接入正式会话，暂不可用。";},()=>false);
    public IReadOnlyList<EditHistoryLocation> EditHistoryLocations=>DocumentManager.CurrentSession is {} session
        ?session.HistoryEntries.Select(entry=>new EditHistoryLocation(session.EditSessionId,entry.Id,entry.Description,false)).ToArray():Array.Empty<EditHistoryLocation>();
}
