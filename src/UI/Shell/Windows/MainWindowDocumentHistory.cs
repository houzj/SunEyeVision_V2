using SunEyeVision.Workflow;
using SunEyeVision.UI.Core;
using System.Windows.Input;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private DocumentHistoryCoordinator? _observedDocumentHistory;
    private SolutionManager? _documentEventOwner;
    private bool _documentHostReleased;
    public void ReleaseDocumentHost()
    {
        if(_documentHostReleased)return;
        _documentHostReleased=true;
        if(_documentEventOwner is {} manager)
        {
            manager.CurrentSolutionChanged-=OnCurrentSolutionChanged;
            manager.SolutionSaved-=OnDocumentHostSolutionSaved;
        }
        _documentEventOwner=null;
        var history=_observedDocumentHistory;_observedDocumentHistory=null;
        if(history is not null)
        {
            history.Changed-=DocumentHistoryChanged;
            history.ReleaseMutationLeaseProvider(_executionManager.BeginHistoryMutation);
        }
    }
    private void OnDocumentHostSolutionSaved(object? sender, Solution document)
    {
        if (_documentHostReleased) return;
        foreach (var cached in _workflowDsqsCache.Values)
            cached.ResourcePaths = SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(document);
        if (ReferenceEquals(DocumentManager.CurrentSolution, document))
        {
            RefreshAcceptedImageSourceViews();
            ForceRefreshImagePreview();
        }
        UpdateCurrentSolutionInfo();
    }
    private ICommand? _navigateEditHistoryCommand;
    public ICommand NavigateEditHistoryCommand => _navigateEditHistoryCommand ??= new RelayCommand(
        request => {
            if (request is not EditHistoryLocation location || !CanNavigateEditHistory(location)) return;
            try {
                DocumentManager.Session!.History.NavigateTo(location.SessionId, location.EntryId);
                var rootId = DocumentManager.Session.History.LastAppliedEntry?.Scopes.FirstOrDefault()?.RootId;
                var tab = WorkflowTabViewModel.Tabs.FirstOrDefault(t => t.RootWorkflowId == rootId);
                if (tab != null) SetSelectedTab(tab);
                SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.OutputBindings.ReconcileAll();
                SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.RefreshCommittedBindings();
            } catch (Exception error) { StatusText = "历史跳转已停止：" + error.Message; }
        }, request => request is EditHistoryLocation location && CanNavigateEditHistory(location));
    private bool CanNavigateEditHistory(EditHistoryLocation location) => CanEditRootWorkspace
        && WorkflowTabViewModel.SelectedTab?.IsInSubroutine != true
        && DocumentManager.Session?.History is {} history && history.SessionId == location.SessionId
        && !history.IsClosed && history.Fault == null;
    public IReadOnlyList<EditHistoryLocation> EditHistoryLocations
    {
        get {
            if (DocumentManager.Session?.History is not {} history) return Array.Empty<EditHistoryLocation>();
            return new[] { new EditHistoryLocation(history.SessionId, null, "保留历史起点", history.Position == 0) }
                .Concat(history.Entries.Select((entry,index) => new EditHistoryLocation(history.SessionId, entry.Id,
                    entry.Description, history.Position == index + 1))).ToArray();
        }
    }
    private void BindDocumentHistory()
    {
        if(_documentHostReleased)return;
        var session = DocumentManager.Session;
        var history = session?.History;
        if (!ReferenceEquals(history, _observedDocumentHistory))
        {
            if (_observedDocumentHistory != null)
            {
                _observedDocumentHistory.Changed -= DocumentHistoryChanged;
                _observedDocumentHistory.ReleaseMutationLeaseProvider(_executionManager.BeginHistoryMutation);
            }
            _observedDocumentHistory = null;
            if (history != null)
            {
                history.SetMutationLeaseProvider(_executionManager.BeginHistoryMutation);
                _observedDocumentHistory=history;
                history.Changed += DocumentHistoryChanged;
            }
        }
        if (session != null)
            foreach (var tab in WorkflowTabViewModel.Tabs)
                tab.BindRootDocumentHistory(session.History, () => session.Editing.Host.AcceptDocumentGraphHistory(),
                    () => NodeConfigurationEditHost.DocumentHistorySignature(session.Document));
    }
    private void DocumentHistoryChanged(object? sender, EventArgs args)
    {
        var failures = new List<Exception>();
        void Refresh(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        if (sender is DocumentHistoryCoordinator history && !history.IsClosed
            && DocumentManager.Session is {} session && ReferenceEquals(session.History, history)
            && !IsRunning && !_executionManager.IsAnyRunning() && history.LastInvalidatedNodes.Count != 0)
            Refresh(() => RefreshReplayedResults(session, history));
        Refresh(RefreshAcceptedImageSourceViews);
        Refresh(RefreshCurrentResultBindings);
        Refresh(RefreshRootTabReadiness); Refresh(UpdateCurrentSolutionInfo); Refresh(UpdateUndoRedoCommands);
        Refresh(() => OnPropertyChanged(nameof(CanUndoRootWorkspace)));
        Refresh(() => OnPropertyChanged(nameof(CanRedoRootWorkspace)));
        if (failures.Count != 0) throw new AggregateException("历史已更新，部分界面刷新失败。", failures);
    }

    private void RefreshReplayedResults(SolutionSession session, DocumentHistoryCoordinator history)
    {
        var revision = history.Revision;
        bool Current() => ReferenceEquals(DocumentManager.Session, session)
            && ReferenceEquals(session.History, history) && !history.IsClosed && history.Revision == revision
            && !IsRunning && !_executionManager.IsAnyRunning();
        InvalidatePublishedResults(session, history.LastInvalidatedNodes, Current);
    }

    private void InvalidatePublishedResults(SolutionSession session, IReadOnlyList<ScopedNodeIdentity> nodes, Func<bool> current)
    {
        var affected = nodes.ToArray();
        var failures = new List<Exception>();
        foreach (var node in affected)
        {
            if (!current()) break;
            var context = _workflowContextService.Get(node.WorkflowId);
            var oldResult = context?.GetNodeResult(node.NodeId);
            // Invalidate queued completions before dropping the cached reader. A retirement
            // observer may reenter and install a newer result; never remove that new result.
            try { session.Document.ExecutionOwner.RetireNode(node.WorkflowId + "/" + node.NodeId); }
            catch (Exception error) { failures.Add(error); }
            if (!current()) break;
            try { if (oldResult is not null && ReferenceEquals(context?.GetNodeResult(node.NodeId), oldResult)) context!.RemoveNodeResult(node.NodeId); }
            catch (Exception error) { failures.Add(error); }
        }
        if (current() && SelectedNode is {} selected && WorkflowTabViewModel?.SelectedTab?.CurrentWorkflowId is {} workflowId
            && affected.Any(node => node.WorkflowId == workflowId && node.NodeId == selected.Id)
            && session.Document.Workflows.Any(flow => flow.Id == workflowId && flow.Nodes.Any(node => ReferenceEquals(node, selected))))
        {
            // If retirement reentered and published a new generation, its presentation wins.
            var currentResult = _workflowContextService.Get(workflowId)?.GetNodeResult(selected.Id)?.Result;
            try { if (currentResult?.ExecutionIdentity?.IsCurrent != true) UpdateModuleResults(null, selected.Name, selected.ToolType, selected.Id, selected.Parameters); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count != 0) throw new AggregateException("配置已接纳，部分结果清理或刷新失败。", failures);
    }
}
