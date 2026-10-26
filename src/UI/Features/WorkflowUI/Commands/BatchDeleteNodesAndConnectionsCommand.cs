using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.WorkflowUI.Commands
{
    /// <summary>
    /// 原子化删除选中的节点和连接线（合并为一个可撤销命令，消除两步操作的重叠删除问题）
    /// </summary>
    public class BatchDeleteNodesAndConnectionsCommand : IUndoableCommand
    {
        private readonly ObservableCollection<WorkflowNodeBase> _nodes;
        private readonly ObservableCollection<WorkflowConnection> _connections;
        private readonly List<(WorkflowNodeBase node, int index)> _deletedNodes;
        private readonly List<WorkflowConnection> _explicitConnections;
        private readonly List<WorkflowConnection> _before;
        private List<WorkflowConnection>? _after;
        private readonly List<EditConnectionPortsCommand> _portEdits = new();

        public BatchDeleteNodesAndConnectionsCommand(
            ObservableCollection<WorkflowNodeBase> nodes,
            ObservableCollection<WorkflowConnection> connections,
            List<WorkflowNodeBase> nodesToDelete,
            List<WorkflowConnection> connectionsToDelete)
        {
            _nodes = nodes;
            _connections = connections;
            _before = connections.ToList();
            _explicitConnections = connectionsToDelete.ToList();

            // 记录每个节点的原始位置
            _deletedNodes = nodesToDelete
                .Select(n => (n, nodes.IndexOf(n)))
                .ToList();


        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (_after != null)
            {
                foreach (var edit in _portEdits) edit.Execute(null);
                ApplyConnections(_after);
                foreach (var (node, _) in _deletedNodes) _nodes.Remove(node);
                return;
            }
            // 原件按节点顺序先补全部前驱/后继，再释放该节点；连续删除会消费前一步补线。
            foreach (var (node, _) in _deletedNodes)
            {
                var incoming = _connections.Where(c => c.TargetNodeId == node.Id).ToArray();
                var outgoing = _connections.Where(c => c.SourceNodeId == node.Id).ToArray();
                foreach (var next in outgoing)
                foreach (var previous in incoming)
                {
                    if (previous.SourceNodeId == next.TargetNodeId) continue;
                    var existing = _connections.FirstOrDefault(c => c.SourceNodeId == previous.SourceNodeId && c.TargetNodeId == next.TargetNodeId);
                    if (existing != null)
                    {
                        var edit = new EditConnectionPortsCommand(existing, previous.SourcePort, next.TargetPort);
                        edit.Execute(null); _portEdits.Add(edit);
                    }
                    else
                    {
                        var bridge = new WorkflowConnection { Id = Guid.NewGuid().ToString("N"),
                            SourceNodeId = previous.SourceNodeId, TargetNodeId = next.TargetNodeId,
                            SourcePort = previous.SourcePort, TargetPort = next.TargetPort };
                        if (ExecutionConnectionPolicy.Rejection(_connections, bridge) == null)
                            _connections.Add(bridge); // isAutoLink:false，不生成参数绑定。
                    }
                }
                foreach (var edge in incoming.Concat(outgoing).Distinct()) _connections.Remove(edge);
                _nodes.Remove(node);
            }
            foreach (var edge in _explicitConnections) _connections.Remove(edge);
            _after = _connections.ToList();
        }

        private void ApplyConnections(List<WorkflowConnection> target)
        {
            foreach (var edge in _connections.Where(c => !target.Contains(c)).ToArray()) _connections.Remove(edge);
            for (var i = 0; i < target.Count; i++)
            {
                var current = _connections.IndexOf(target[i]);
                if (current < 0) _connections.Insert(i, target[i]);
                else if (current != i) _connections.Move(current, i);
            }
        }

        public void Undo()
        {
            // 恢复节点（按原始索引升序）
            foreach (var (node, index) in _deletedNodes.OrderBy(x => x.index))
            {
                if (_nodes.Contains(node)) continue;
                if (index >= 0 && index <= _nodes.Count)
                    _nodes.Insert(index, node);
                else
                    _nodes.Add(node);
            }

            for (var i = _portEdits.Count - 1; i >= 0; i--) _portEdits[i].Undo();
            ApplyConnections(_before);
        }
    }
}
