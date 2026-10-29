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
            if (_nodes.Contains(_node)) throw new InvalidOperationException("节点已存在，不能重复添加。");
            try { _nodes.Add(_node); }
            catch (Exception failure)
            {
                try { if (_nodes.Contains(_node)) _nodes.Remove(_node); }
                catch (Exception rollback) { throw new AggregateException("添加节点失败且回滚通知失败。", failure, rollback); }
                throw;
            }
        }

        public void Undo()
        {
            var index = _nodes.IndexOf(_node);
            if (index < 0) throw new InvalidOperationException("待撤销的节点已不存在。");
            try { _nodes.RemoveAt(index); }
            catch (Exception failure)
            {
                try { if (!_nodes.Contains(_node)) _nodes.Insert(Math.Min(index, _nodes.Count), _node); }
                catch (Exception rollback) { throw new AggregateException("撤销节点失败且恢复通知失败。", failure, rollback); }
                throw;
            }
        }
    }
}
