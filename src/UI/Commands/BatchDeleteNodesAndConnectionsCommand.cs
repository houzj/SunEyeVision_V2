using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Commands
{
    /// <summary>
    /// 原子化删除选中的节点和连接线（合并为一个可撤销命令，消除两步操作的重叠删除问题）
    /// </summary>
    public class BatchDeleteNodesAndConnectionsCommand : IUndoableCommand
    {
        private readonly ObservableCollection<WorkflowNode> _nodes;
        private readonly ObservableCollection<WorkflowConnection> _connections;
        private readonly List<(WorkflowNode node, int index)> _deletedNodes;
        private readonly List<(WorkflowConnection connection, int index)> _deletedConnections;

        public BatchDeleteNodesAndConnectionsCommand(
            ObservableCollection<WorkflowNode> nodes,
            ObservableCollection<WorkflowConnection> connections,
            List<WorkflowNode> nodesToDelete,
            List<WorkflowConnection> connectionsToDelete)
        {
            _nodes = nodes;
            _connections = connections;

            // 记录每个节点的原始位置
            _deletedNodes = nodesToDelete
                .Select(n => (n, nodes.IndexOf(n)))
                .ToList();

            // 合并待删除连接：显式选中的 + 与被删节点关联的连接（去重）
            var connectionIds = new HashSet<string>();
            _deletedConnections = new List<(WorkflowConnection, int)>();

            foreach (var conn in connectionsToDelete)
            {
                if (connectionIds.Add(conn.Id))
                    _deletedConnections.Add((conn, connections.IndexOf(conn)));
            }

            foreach (var node in nodesToDelete)
            {
                foreach (var conn in connections.Where(c =>
                    c.SourceNodeId == node.Id || c.TargetNodeId == node.Id))
                {
                    if (connectionIds.Add(conn.Id))
                        _deletedConnections.Add((conn, connections.IndexOf(conn)));
                }
            }
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            // 先删连接（按索引降序，避免索引偏移）
            foreach (var (conn, index) in _deletedConnections
                .OrderByDescending(x => x.index))
            {
                _connections.Remove(conn);
            }

            // 再删节点（按索引降序）
            foreach (var (node, index) in _deletedNodes
                .OrderByDescending(x => x.index))
            {
                _nodes.Remove(node);
            }
        }

        public void Undo()
        {
            // 恢复节点（按原始索引升序）
            foreach (var (node, index) in _deletedNodes.OrderBy(x => x.index))
            {
                if (index >= 0 && index <= _nodes.Count)
                    _nodes.Insert(index, node);
                else
                    _nodes.Add(node);
            }

            // 恢复连接（按原始索引升序）
            foreach (var (conn, index) in _deletedConnections.OrderBy(x => x.index))
            {
                if (index >= 0 && index <= _connections.Count)
                    _connections.Insert(index, conn);
                else
                    _connections.Add(conn);
            }
        }
    }
}
