using SunEyeVision.Workflow;
using SunEyeVision.UI.Core;
using System.Windows.Input;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private DocumentHistoryCoordinator? _observedDocumentHistory;
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
        var session = DocumentManager.Session;
        var history = session?.History;
        if (!ReferenceEquals(history, _observedDocumentHistory))
        {
            if (_observedDocumentHistory != null) _observedDocumentHistory.Changed -= DocumentHistoryChanged;
            _observedDocumentHistory = history;
            if (history != null) history.Changed += DocumentHistoryChanged;
        }
        if (session != null)
            foreach (var tab in WorkflowTabViewModel.Tabs)
                tab.BindRootDocumentHistory(session.History, () => session.Editing.Host.AcceptDocumentGraphHistory(),
                    () => NodeConfigurationEditHost.DocumentHistorySignature(session.Document));
    }
    private void DocumentHistoryChanged(object? sender, EventArgs args)
    {
        RefreshRootTabReadiness(); UpdateCurrentSolutionInfo(); UpdateUndoRedoCommands();
        OnPropertyChanged(nameof(CanUndoRootWorkspace)); OnPropertyChanged(nameof(CanRedoRootWorkspace));
    }
}
