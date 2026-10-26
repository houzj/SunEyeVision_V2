using System;
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

        public AddConnectionCommand(ObservableCollection<WorkflowConnection> connections, WorkflowConnection connection)
        {
            _connections = connections;
            _connection = connection;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            ExecutionConnectionPolicy.EnsureCanAdd(_connections, _connection);
            _connections.Add(_connection);
        }

        public void Undo()
        {
            _connections.Remove(_connection);
        }
    }
}
