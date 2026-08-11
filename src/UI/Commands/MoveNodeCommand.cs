using System;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Commands
{
    /// <summary>
    /// 移动节点命令
    /// </summary>
    public class MoveNodeCommand : IUndoableCommand
    {
        private readonly WorkflowNode _node;
        private readonly Point _oldPosition;
        private readonly Point _newPosition;

        public MoveNodeCommand(WorkflowNode node, Point oldPosition, Point newPosition)
        {
            _node = node;
            _oldPosition = oldPosition;
            _newPosition = newPosition;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            _node.Position = _newPosition;
        }

        public void Undo()
        {
            _node.Position = _oldPosition;
        }
    }
}
