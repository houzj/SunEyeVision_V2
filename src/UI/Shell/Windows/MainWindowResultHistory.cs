using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private ICommand? _clearResultHistoryCommand;
    private ICommand? _saveResultHistoryCommand;

    public IReadOnlyList<ResultHistoryRow> ResultHistory
    {
        get
        {
            if (SelectedEditorContext is not null)
                return _editorDebugControl?.DebugHistory?.Read() ?? Array.Empty<ResultHistoryRow>();
            if (SelectedNode is { } node && OwnsSelectedDocumentNode(node) &&
                DocumentManager.CurrentSession is { } session &&
                WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId is { } workflow)
                return session.ReadResultHistory(workflow, node);
            return Array.Empty<ResultHistoryRow>();
        }
    }

    public ICommand ClearResultHistoryCommand => _clearResultHistoryCommand ??=
        new RelayCommand(ClearResultHistory, () => ResultHistory.Count != 0);
    public ICommand SaveResultHistoryCommand => _saveResultHistoryCommand ??=
        new RelayCommand(SaveResultHistory, () => ResultHistory.Count != 0);

    private void NotifyResultHistoryChanged()
    {
        OnPropertyChanged(nameof(ResultHistory));
        CommandManager.InvalidateRequerySuggested();
    }

    private void ClearResultHistory()
    {
        if (SelectedEditorContext is not null)
            _editorDebugControl?.DebugHistory?.Clear();
        else if (SelectedNode is { } node && OwnsSelectedDocumentNode(node) &&
                 DocumentManager.CurrentSession is { } session &&
                 WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId is { } workflow)
            session.ClearResultHistory(workflow, node);
        NotifyResultHistoryChanged();
    }

    private void SaveResultHistory()
    {
        var rows = ResultHistory;
        var node = SelectedNode;
        var session = DocumentManager.CurrentSession;
        var editor = SelectedEditorContext;
        var selection = _outputContractSelectionGeneration;
        var workflow = WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
        if (rows.Count == 0 || node is null) return;
        var name = string.Concat(node.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var dialog = new SaveFileDialog
        {
            Title = "保存历史结果", Filter = "文本文件 (*.txt)|*.txt", DefaultExt = ".txt",
            AddExtension = true, FileName = $"{name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
        };
        try
        {
            if (dialog.ShowDialog(ResolveSolutionDialogOwner(null)) != true ||
                _documentHostReleased || selection != _outputContractSelectionGeneration ||
                WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId != workflow ||
                !ReferenceEquals(session, DocumentManager.CurrentSession) ||
                !ReferenceEquals(node, SelectedNode) || !ReferenceEquals(editor, SelectedEditorContext))
                return;
            Services.Startup.StartupLaunchContext.Current.ValidatePath(dialog.FileName);
            File.WriteAllText(dialog.FileName, string.Concat(rows.Select(row => $"{row.RowIndex}  {row.AddTime}  {row.Content}\r\n")));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            StatusText = "保存历史结果失败：" + error.Message;
            AddLog(StatusText);
        }
    }
}
