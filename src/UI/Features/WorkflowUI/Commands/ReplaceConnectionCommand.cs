using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.WorkflowUI.Commands
{
    /// <summary>
    /// 替换连接命令（新连接覆盖旧连接，支持撤销）
    /// </summary>
    public class ReplaceConnectionCommand : IUndoableCommand
    {
        private readonly ObservableCollection<WorkflowConnection> _connections;
        private readonly WorkflowConnection _oldConnection;
        private readonly WorkflowConnection _newConnection;
        private int _oldIndex;

        public ReplaceConnectionCommand(
            ObservableCollection<WorkflowConnection> connections,
            WorkflowConnection oldConnection,
            WorkflowConnection newConnection)
        {
            _connections = connections;
            _oldConnection = oldConnection;
            _newConnection = newConnection;
            _oldIndex = connections.IndexOf(oldConnection);
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            // 删除旧连接
            _connections.Remove(_oldConnection);
            // 添加新连接
            _connections.Add(_newConnection);
        }

        public void Undo()
        {
            // 删除新连接
            _connections.Remove(_newConnection);
            // 恢复旧连接到原位置
            if (_oldIndex >= 0 && _oldIndex <= _connections.Count)
                _connections.Insert(_oldIndex, _oldConnection);
            else
                _connections.Add(_oldConnection);
        }
    }
}
