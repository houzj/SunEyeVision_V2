using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.WorkflowUI.Commands
{
    /// <summary>
    /// 添加连接命令
    /// </summary>
    public class AddConnectionCommand : IUndoableCommand
    {
        private readonly ObservableCollection<WorkflowConnection> _connections;
        private readonly WorkflowConnection _connection;
        private readonly Action? _configure;
        private readonly GraphBindingUndoSnapshot? _before;
        private GraphBindingUndoSnapshot? _after;

        public AddConnectionCommand(ObservableCollection<WorkflowConnection> connections, WorkflowConnection connection,
            IEnumerable<WorkflowNodeBase>? bindingNodes = null, Action? configure = null)
        {
            _connections = connections;
            _connection = connection;
            _configure = configure;
            if (configure is not null)
                _before = GraphBindingUndoSnapshot.Capture(bindingNodes ?? throw new ArgumentNullException(nameof(bindingNodes)), null);
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            ExecutionConnectionPolicy.EnsureCanAdd(_connections, _connection);
            try
            {
                _connections.Add(_connection);
                if (_after is not null) _after.Restore();
                else if (_configure is not null)
                {
                    _configure();
                    _after = GraphBindingUndoSnapshot.Capture(_before!.Nodes, null);
                }
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                try { if (_connections.Contains(_connection)) _connections.Remove(_connection); }
                catch (Exception rollback) { errors.Add(rollback); }
                try { _before?.Restore(); }
                catch (Exception rollback) { errors.Add(rollback); }
                if (errors.Count > 1) throw new AggregateException("连线及来源回滚失败。", errors);
                throw;
            }
        }

        public void Undo()
        {
            var index = _connections.IndexOf(_connection);
            if (index < 0) throw new InvalidOperationException("待撤销连线已不存在。");
            var current = _before is null ? null : GraphBindingUndoSnapshot.Capture(_before.Nodes, null);
            try
            {
                _connections.RemoveAt(index);
                _before?.Restore();
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                try { if (!_connections.Contains(_connection)) _connections.Insert(Math.Min(index, _connections.Count), _connection); }
                catch (Exception rollback) { errors.Add(rollback); }
                try { current?.Restore(); }
                catch (Exception rollback) { errors.Add(rollback); }
                if (errors.Count > 1) throw new AggregateException("撤销连线失败且恢复失败。", errors);
                throw;
            }
        }
    }
}
