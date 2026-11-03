using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private bool _rootCollectionBusy;
    public bool CanEditRootCollection => !_documentHostReleased && _directory.IsActive && DocumentManager.CurrentSession is not null &&
        !IsRunning && !_solutionTransitionPrepared && !_transitionPreparationInProgress && !_rootCollectionBusy &&
        _editorDebugControl?.IsDebugBusy != true;

    internal bool CanDeleteRootWorkspace(Features.WorkflowUI.WorkflowTabViewModel tab) =>
        ResolveCollectionRoot(tab) is not null && DocumentManager.CurrentSession!.GetWorkflows().Count(flow => !flow.IsSubroutine) > 1;

    private WorkflowDefinition? ResolveCollectionRoot(Features.WorkflowUI.WorkflowTabViewModel tab)
    {
        var session = DocumentManager.CurrentSession;
        return CanEditRootCollection && session is not null && WorkflowTabViewModel.Tabs.Contains(tab) &&
            tab.ActualRoot is { IsSubroutine: false } root && tab.OwnsRoot(session, root) &&
            session.GetWorkflows().Any(actual => ReferenceEquals(actual, root)) ? root : null;
    }

    private static string AvailableRootName(DocumentSession session, WorkflowDefinition? source = null)
    {
        var names = session.GetWorkflows().Where(flow => !flow.IsSubroutine).Select(flow => flow.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var number = 1; ; number = checked(number + 1))
        {
            var suffix = source is null ? "流程" + number : "_副本" + number;
            var prefix = source?.Name ?? string.Empty;
            if (prefix.Length + suffix.Length > 64) prefix = prefix[..(64 - suffix.Length)];
            if (prefix.Length > 0 && char.IsHighSurrogate(prefix[^1])) prefix = prefix[..^1];
            var name = prefix + suffix;
            if (!names.Contains(name)) return name;
        }
    }

    private bool ApplyRootCollection(string description,
        Func<DocumentSession, RootWorkflowCollectionEditDraft, WorkflowDefinition?> edit)
    {
        if (!CanEditRootCollection) { StatusText = "请先停止并排空执行，或结束当前方案过渡。"; return false; }
        var session = DocumentManager.CurrentSession!;
        _rootCollectionBusy = true;
        RootCollectionParticipant? participant = null;
        WorkflowDefinition? issuedSelection = null;
        Exception? failure = null;
        try
        {
            participant = new(session, () => ReferenceEquals(DocumentManager.CurrentSession, session) && !_documentHostReleased);
            issuedSelection = edit(session, participant.Draft);
            session.Editing.Register(participant);
            // The pending node/root/variable views and the collection join one frozen proposal.
            // In particular a copy reads the prepared source configuration in that candidate.
            session.Editing.Apply();
        }
        catch (Exception error) { failure = error; }
        finally
        {
            if (participant is not null) { session.Editing.Unregister(participant.ScopeId); participant.Dispose(); }
            _rootCollectionBusy = false;
        }

        if (participant?.Receipt is not { } receipt)
        {
            StatusText = description + "未提交：" + failure?.Message;
            NotifyRootCollectionAvailability();
            return false;
        }
        if (session.LastObserverError is {} observerError)
            failure = failure is null ? observerError : new AggregateException(failure, observerError);
        // A failed observer/acknowledgement cannot undo the already committed document.
        // Reconcile only after every participant has received its receipt.
        try
        {
            WorkflowDefinition? preferred = null;
            if (!session.Editing.HasPendingChanges && issuedSelection is not null &&
                receipt.AcceptedRoots.TryGetValue(issuedSelection, out var accepted)) preferred = accepted;
            WorkflowTabViewModel.ReconcileDocumentTabs(session, preferred);
            RefreshProductionPublication(session, receipt);
            UpdateCurrentSolutionInfo();
        }
        catch (Exception refreshError) { failure = failure is null ? refreshError : new AggregateException(failure, refreshError); }
        NotifyRootCollectionAvailability();
        StatusText = failure is null ? description + "已提交。" : description + "已提交，但部分编辑视图未刷新：" + failure.Message;
        return true;
    }

    private void NotifyRootCollectionAvailability()
    {
        OnPropertyChanged(nameof(CanEditRootCollection));
        OnPropertyChanged(nameof(CanEditRootWorkspace));
        OnPropertyChanged(nameof(WorkflowCount));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private sealed class RootCollectionParticipant : IDocumentEditParticipant, IDisposable
    {
        private readonly DocumentSession _session;
        private readonly Func<bool> _current;
        private bool _disposed;
        internal RootWorkflowCollectionEditDraft Draft { get; private set; }
        internal DocumentEditReceipt? Receipt { get; private set; }
        internal RootCollectionParticipant(DocumentSession session, Func<bool> current)
        { _session = session; _current = current; Draft = session.Editing.Host.BeginRootCollectionEdit(); }
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey => "root-collection";
        public bool IsValid => !_disposed && _current();
        public bool HasPendingChanges => IsValid;
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
        {
            if (!IsValid || !ReferenceEquals(host, _session.Editing.Host)) throw new InvalidOperationException("根流程集合编辑会话已改变。");
            return DocumentEditProposal.Prepare(host, collection: Draft);
        }
        public void Acknowledge(DocumentEditProposal prepared, DocumentEditReceipt receipt)
        {
            if (!ReferenceEquals(receipt.Session, _session)) throw new InvalidOperationException("根流程集合回执不属于当前方案。");
            Receipt = receipt;
            var next = _session.Editing.Host.RebaseAcceptedDraft(Draft, prepared, receipt);
            Draft.Dispose(); Draft = next;
        }
        public void Cancel() => Dispose();
        public void Dispose() { if (_disposed) return; _disposed = true; Draft.Dispose(); }
    }
}
