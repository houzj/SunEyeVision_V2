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
        if(_documentHostReleased)return;_documentHostReleased=true;
        WorkflowTabViewModel.UnbindDocumentManager();
        if(_documentEventOwner is {} manager)manager.SessionChanged-=FormalDocumentSessionChanged;
        _documentEventOwner=null;UnbindFormalSession();
        ReleaseProductionEditors();ClearFormalImageSourceSelection();ReleaseActiveResultView();RetireResultPublication();
    }
    private void UnbindFormalSession()
    {
        if(_observedDocumentSession is {} session){session.Changed-=FormalDocumentChanged;session.RuntimeResultAvailable-=FormalResultAvailable;}
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
        if(session is not null){session.Changed+=FormalDocumentChanged;session.RuntimeResultAvailable+=FormalResultAvailable;}
    }
    private void FormalDocumentChanged(object? sender,DocumentChangeSet change)
    {
        if(_documentHostReleased||!ReferenceEquals(sender,_observedDocumentSession)||!ReferenceEquals(sender,DocumentManager.CurrentSession))return;
        foreach(var editor in _productionEditors.ToArray())if(!editor.IsValid)editor.Discard();
        if(SelectedNode is {} selected&&!OwnsSelectedDocumentNode(selected))SelectedNode=null;
        else RefreshFormalPublishedResult();
        RefreshFormalImageSourceSelection();NotifyResultDisplayChanged();RefreshRootTabReadiness();UpdateCurrentSolutionInfo();UpdateUndoRedoCommands();
        OnPropertyChanged(nameof(CanUndoRootWorkspace));OnPropertyChanged(nameof(CanRedoRootWorkspace));OnPropertyChanged(nameof(EditHistoryLocations));
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
