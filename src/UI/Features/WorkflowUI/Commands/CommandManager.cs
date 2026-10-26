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

        public bool CanUndo => _transaction == null && _undoStack.Count > 0;
        public bool CanRedo => _transaction == null && _redoStack.Count > 0;

        public string LastCommandDescription => _undoStack.Count > 0 ? _undoStack.Peek().GetType().Name : "";

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
            command.Execute(null);
            if (_transaction != null) { _transaction.Commands.Add(command); return; }
            _undoStack.Push(command);
            _redoStack.Clear();
            OnCommandStateChanged();
        }

        public void Undo()
        {
            if (CanUndo)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
                OnCommandStateChanged();
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                var command = _redoStack.Pop();
                command.Execute(null);
                _undoStack.Push(command);
                OnCommandStateChanged();
            }
        }

        private Transaction? _transaction;
        public Transaction BeginTransaction()
        {
            if (_transaction != null) throw new InvalidOperationException("已有未结束的编辑事务。");
            return _transaction = new Transaction(this);
        }
        public sealed class Transaction : IDisposable
        {
            private readonly CommandManager _owner;
            internal readonly List<IUndoableCommand> Commands = new();
            private bool _complete, _disposed;
            internal Transaction(CommandManager owner) => _owner = owner;
            public void Complete() => _complete = true;
            public void Dispose()
            {
                if (_disposed) return; _disposed = true; _owner._transaction = null;
                if (!_complete)
                {
                    for (var i = Commands.Count - 1; i >= 0; --i) Commands[i].Undo();
                    _owner.OnCommandStateChanged(); return;
                }
                if (Commands.Count == 0) return;
                _owner._undoStack.Push(new CompositeEditCommand(Commands.ToArray()));
                _owner._redoStack.Clear(); _owner.OnCommandStateChanged();
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
            public void Undo() { for (var i = _commands.Length - 1; i >= 0; --i) _commands[i].Undo(); }
        }

        public void Clear()
        {
            if (_transaction != null) throw new InvalidOperationException("编辑事务中不能清空历史。");
            _undoStack.Clear();
            _redoStack.Clear();
            OnCommandStateChanged();
        }
    }
}
