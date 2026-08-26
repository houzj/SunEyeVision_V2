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

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

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

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnCommandStateChanged();
        }
    }
}
