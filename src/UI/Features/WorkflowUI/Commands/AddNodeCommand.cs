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
        private readonly ObservableCollection<WorkflowNode> _nodes;
        private readonly WorkflowNode _node;

        public AddNodeCommand(ObservableCollection<WorkflowNode> nodes, WorkflowNode node)
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
