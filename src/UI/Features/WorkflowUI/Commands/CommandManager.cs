using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.WorkflowUI.Commands
{
    /// <summary>
    /// 命令管理- 用于撤销/重做功能
    /// </summary>
    public class CommandManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new Stack<IUndoableCommand>();
        private readonly Stack<IUndoableCommand> _redoStack = new Stack<IUndoableCommand>();
        private readonly ObservableCollection<WorkflowNodeBase> _nodes;
        private readonly ObservableCollection<WorkflowConnection> _connections;

        public CommandManager(ObservableCollection<WorkflowNodeBase> nodes, ObservableCollection<WorkflowConnection> connections)
        {
            _nodes = nodes;
            _connections = connections;
        }

        public bool CanUndo => _transaction == null && (_documentHistory?.CanUndo ?? (_undoStack.Count > 0));
        public bool CanRedo => _transaction == null && (_documentHistory?.CanRedo ?? (_redoStack.Count > 0));

        public string LastCommandDescription => _documentHistory?.LastAppliedEntry?.Description ?? (_undoStack.Count > 0 ? _undoStack.Peek().GetType().Name : "");
        private DocumentHistoryCoordinator? _documentHistory;
        private Func<IReadOnlyList<ScopedNodeIdentity>>? _synchronize;
        private Func<string>? _signature;
        private string _rootId = "";
        public void BindDocumentHistory(DocumentHistoryCoordinator history, string rootId, Func<IReadOnlyList<ScopedNodeIdentity>> synchronize, Func<string> signature)
        {
            if (ReferenceEquals(_documentHistory, history)) return;
            if (_documentHistory != null) _documentHistory.Changed -= HistoryChanged;
            _documentHistory = history; _rootId = rootId; _synchronize = synchronize; _signature = signature;
            _undoStack.Clear(); _redoStack.Clear();
            history.Changed += HistoryChanged;
        }
        private void HistoryChanged(object? sender, EventArgs args) => OnCommandStateChanged();
        private void Publish(IUndoableCommand command, bool accepted = false)
        {
            if (_documentHistory == null) { _undoStack.Push(command); _redoStack.Clear(); OnCommandStateChanged(); return; }
            var operation = new CanvasOperation(command, _rootId, _synchronize!, _signature!, accepted);
            try { _documentHistory.Execute(operation); }
            catch { if (accepted && !operation.Reverted) command.Undo(); throw; }
        }
        private sealed class CanvasOperation(IUndoableCommand command, string rootId, Func<IReadOnlyList<ScopedNodeIdentity>> synchronize, Func<string> signature, bool accepted)
            : IDocumentHistoryOperation, IDocumentHistoryReplayGuard, IDocumentHistoryReplayEffects
        {
            private string _expected = signature();
            private bool _initial = true;
            public bool Reverted { get; private set; }
            public IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes { get; private set; } = Array.Empty<ScopedNodeIdentity>();
            public string Description => command switch {
                AddNodeCommand => "添加节点", AddConnectionCommand => "连接节点",
                MoveNodeCommand or MoveNodesSnapshotCommand or BatchMoveNodesCommand => "移动节点",
                DeleteWorkflowItemsCommand or BatchDeleteNodesAndConnectionsCommand => "删除节点或连线",
                InsertNodeIntoConnectionCommand => "在线路中插入节点", EditConnectionPortsCommand => "调整连线端口",
                EditConnectionPathCommand => "调整连线路径", PasteWorkflowFragmentCommand => "粘贴节点",
                RenameCanvasNodeCommand => "重命名节点", SetNodesEnabledCommand => "修改节点启用状态",
                ChangeRootWorkspaceSettingsCommand => "修改流程运行设置", CompositeEditCommand => "添加并连接节点",
                _ => "编辑画布"
            };
            public IReadOnlyList<DocumentHistoryScope> Scopes => new[] { new DocumentHistoryScope(rootId, rootId) };
            public bool CanReplay(DocumentHistoryDirection direction) => signature() == _expected;
            public PreparedDocumentHistoryChange Prepare(DocumentHistoryDirection direction)
            {
                if (!CanReplay(direction)) throw new InvalidOperationException("后续Group或外部编辑尚未撤销。");
                var skip = _initial && accepted; var applied = skip; var previous = _expected;
                return new(() => {
                    if (!skip) { if (direction == DocumentHistoryDirection.Forward) command.Execute(null); else command.Undo(); applied = true; }
                    InvalidatedNodes = synchronize(); _expected = signature(); _initial = false;
                }, () => {
                    if (applied) { if (direction == DocumentHistoryDirection.Forward) command.Undo(); else command.Execute(null); synchronize(); Reverted = true; }
                    InvalidatedNodes = Array.Empty<ScopedNodeIdentity>();
                    _expected = previous;
                });
            }
            public void Dispose() { }
        }

        /// <summary>
        /// 命令状态变更事件
        /// </summary>
        public event EventHandler? CommandStateChanged;

        private void OnCommandStateChanged()
        {
            CommandStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Execute(IUndoableCommand command)
        {
            if (_transaction != null) { command.Execute(null); _transaction.Commands.Add(command); return; }
            if (_documentHistory != null) { Publish(command); return; }
            command.Execute(null); Publish(command);
        }

        public void Undo()
        {
            if (_documentHistory != null) { if (_transaction == null) _documentHistory.Undo(); return; }
            if (CanUndo)
            {
                var command = _undoStack.Peek();
                command.Undo(); _undoStack.Pop();
                _redoStack.Push(command);
                OnCommandStateChanged();
            }
        }

        public void Redo()
        {
            if (_documentHistory != null) { if (_transaction == null) _documentHistory.Redo(); return; }
            if (CanRedo)
            {
                var command = _redoStack.Peek();
                command.Execute(null); _redoStack.Pop();
                _undoStack.Push(command);
                OnCommandStateChanged();
            }
        }

        private Transaction? _transaction;
        public Transaction BeginTransaction()
        {
            if (_transaction != null) throw new InvalidOperationException("已有未结束的编辑事务。");
            var lease = _documentHistory?.BeginCompositeMutation();
            try { return _transaction = new Transaction(this, lease); }
            catch { lease?.Dispose(); throw; }
        }
        public sealed class Transaction : IDisposable
        {
            private readonly CommandManager _owner;
            internal readonly List<IUndoableCommand> Commands = new();
            private bool _complete, _disposed;
            private readonly IDisposable? _lease;
            internal Transaction(CommandManager owner, IDisposable? lease) { _owner = owner; _lease = lease; }
            public void Complete() => _complete = true;
            public void Dispose()
            {
                if (_disposed) return; _disposed = true; _owner._transaction = null;
                try
                {
                    if (!_complete)
                    {
                        for (var i = Commands.Count - 1; i >= 0; --i) Commands[i].Undo();
                        _owner.OnCommandStateChanged(); return;
                    }
                    if (Commands.Count == 0) return;
                    _owner.Publish(new CompositeEditCommand(Commands.ToArray()), accepted: true);
                }
                finally { _lease?.Dispose(); }
            }
        }
        private sealed class CompositeEditCommand : IUndoableCommand
        {
            private readonly IUndoableCommand[] _commands;
            public CompositeEditCommand(IUndoableCommand[] commands) => _commands = commands;
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter)
            {
                var completed = 0;
                try { foreach (var command in _commands) { command.Execute(parameter); ++completed; } }
                catch { while (completed > 0) _commands[--completed].Undo(); throw; }
            }
            public void Undo()
            {
                var index = _commands.Length - 1;
                try { for (; index >= 0; --index) _commands[index].Undo(); }
                catch { for (var restore = index + 1; restore < _commands.Length; ++restore) _commands[restore].Execute(null); throw; }
            }
        }

        public void Clear()
        {
            if (_transaction != null) throw new InvalidOperationException("编辑事务中不能清空历史。");
            if (_documentHistory != null) throw new InvalidOperationException("根画布历史属于方案会话，不能按单个画布清空。");
            _undoStack.Clear();
            _redoStack.Clear();
            OnCommandStateChanged();
        }
    }
}
