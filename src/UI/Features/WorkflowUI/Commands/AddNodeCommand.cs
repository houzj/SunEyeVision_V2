using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Features.WorkflowUI.Commands
{
    /// <summary>
    /// 添加节点命令
    /// </summary>
    public class AddNodeCommand : IUndoableCommand
    {
        private readonly ObservableCollection<WorkflowNodeBase> _nodes;
        private readonly WorkflowNodeBase _node;

        public AddNodeCommand(ObservableCollection<WorkflowNodeBase> nodes, WorkflowNodeBase node)
        {
            _nodes = nodes;
            _node = node;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            _nodes.Add(_node);
        }

        public void Undo()
        {
            _nodes.Remove(_node);
        }
    }
}
