using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Node;

namespace SunEyeVision.UI.Services.Node
{
    /// <summary>
    /// 节点索引管理器 - 提供O(1)的节点查找
    /// </summary>
    public class NodeIndexManager
    {
        private readonly Dictionary<string, WorkflowNode> _nodeIndex;
        private readonly Dictionary<string, List<WorkflowConnection>> _sourceConnectionIndex;
        private readonly Dictionary<string, List<WorkflowConnection>> _targetConnectionIndex;
        private readonly object _lockObj;

        /// <summary>
        /// 节点集合
        /// </summary>
        public ObservableCollection<WorkflowNode> Nodes { get; }

        /// <summary>
        /// 连接集合
        /// </summary>
        public ObservableCollection<WorkflowConnection> Connections { get; }

        /// <summary>
        /// 节点数量
        /// </summary>
        public int NodeCount => _nodeIndex.Count;

        /// <summary>
        /// 连接数量
        /// </summary>
        public int ConnectionCount => Connections.Count;

        public NodeIndexManager(
            ObservableCollection<WorkflowNode> nodes,
            ObservableCollection<WorkflowConnection> connections)
        {
            _nodeIndex = new Dictionary<string, WorkflowNode>();
            _sourceConnectionIndex = new Dictionary<string, List<WorkflowConnection>>();
            _targetConnectionIndex = new Dictionary<string, List<WorkflowConnection>>();
            _lockObj = new object();

            Nodes = nodes;
            Connections = connections;

            BuildIndex();
            SubscribeToCollections();
        }

        /// <summary>
        /// 根据ID查找节点 - O(1)
        /// </summary>
        public WorkflowNode? GetNodeById(string nodeId)
        {
            lock (_lockObj)
            {
                return _nodeIndex.TryGetValue(nodeId, out var node) ? node : null;
            }
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        public bool ContainsNode(string nodeId)
        {
            lock (_lockObj)
            {
                return _nodeIndex.ContainsKey(nodeId);
            }
        }

        /// <summary>
        /// 获取节点的所有输出连接
        /// </summary>
        public List<WorkflowConnection> GetSourceConnections(string nodeId)
        {
            lock (_lockObj)
            {
                return _sourceConnectionIndex.TryGetValue(nodeId, out var connections)
                    ? new List<WorkflowConnection>(connections)
                    : new List<WorkflowConnection>();
            }
        }

        /// <summary>
        /// 获取节点的所有输入连接
        /// </summary>
        public List<WorkflowConnection> GetTargetConnections(string nodeId)
        {
            lock (_lockObj)
            {
                return _targetConnectionIndex.TryGetValue(nodeId, out var connections)
                    ? new List<WorkflowConnection>(connections)
                    : new List<WorkflowConnection>();
            }
        }

        /// <summary>
        /// 检查连接是否已存在
        /// </summary>
        public bool ConnectionExists(string sourceNodeId, string targetNodeId)
        {
            lock (_lockObj)
            {
                if (!_sourceConnectionIndex.TryGetValue(sourceNodeId, out var connections))
                    return false;

                return connections.Any(c => c.TargetNodeId == targetNodeId);
            }
        }

        /// <summary>
        /// 检查是否存在反向连接
        /// </summary>
        public bool ReverseConnectionExists(string sourceNodeId, string targetNodeId)
        {
            lock (_lockObj)
            {
                if (!_sourceConnectionIndex.TryGetValue(targetNodeId, out var connections))
                    return false;

                return connections.Any(c => c.TargetNodeId == sourceNodeId);
            }
        }

        /// <summary>
        /// 获取连接数
        /// </summary>
        public int GetConnectionCount(string nodeId)
        {
            lock (_lockObj)
            {
                int count = 0;
                if (_sourceConnectionIndex.TryGetValue(nodeId, out var sourceConns))
                    count += sourceConns.Count;
                if (_targetConnectionIndex.TryGetValue(nodeId, out var targetConns))
                    count += targetConns.Count;
                return count;
            }
        }

        /// <summary>
        /// 重建索引
        /// </summary>
        public void RebuildIndex()
        {
            lock (_lockObj)
            {
                BuildIndex();
            }
        }

        private void BuildIndex()
        {
            _nodeIndex.Clear();
            _sourceConnectionIndex.Clear();
            _targetConnectionIndex.Clear();

            foreach (var node in Nodes)
            {
                _nodeIndex[node.Id] = node;
            }

            foreach (var connection in Connections)
            {
                if (!_sourceConnectionIndex.ContainsKey(connection.SourceNodeId))
                {
                    _sourceConnectionIndex[connection.SourceNodeId] = new List<WorkflowConnection>();
                }
                _sourceConnectionIndex[connection.SourceNodeId].Add(connection);

                if (!_targetConnectionIndex.ContainsKey(connection.TargetNodeId))
                {
                    _targetConnectionIndex[connection.TargetNodeId] = new List<WorkflowConnection>();
                }
                _targetConnectionIndex[connection.TargetNodeId].Add(connection);
            }
        }

        private void SubscribeToCollections()
        {
            Nodes.CollectionChanged += (s, e) =>
            {
                lock (_lockObj)
                {
                    if (e.OldItems != null)
                    {
                        foreach (WorkflowNode node in e.OldItems)
                        {
                            _nodeIndex.Remove(node.Id);
                        }
                    }

                    if (e.NewItems != null)
                    {
                        foreach (WorkflowNode node in e.NewItems)
                        {
                            _nodeIndex[node.Id] = node;
                        }
                    }
                }
            };

            Connections.CollectionChanged += (s, e) =>
            {
                lock (_lockObj)
                {
                    if (e.OldItems != null)
                    {
                        foreach (WorkflowConnection connection in e.OldItems)
                        {
                            RemoveFromConnectionIndex(connection);
                        }
                    }

                    if (e.NewItems != null)
                    {
                        foreach (WorkflowConnection connection in e.NewItems)
                        {
                            AddToConnectionIndex(connection);
                        }
                    }
                }
            };
        }

        private void AddToConnectionIndex(WorkflowConnection connection)
        {
            if (!_sourceConnectionIndex.ContainsKey(connection.SourceNodeId))
            {
                _sourceConnectionIndex[connection.SourceNodeId] = new List<WorkflowConnection>();
            }
            _sourceConnectionIndex[connection.SourceNodeId].Add(connection);

            if (!_targetConnectionIndex.ContainsKey(connection.TargetNodeId))
            {
                _targetConnectionIndex[connection.TargetNodeId] = new List<WorkflowConnection>();
            }
            _targetConnectionIndex[connection.TargetNodeId].Add(connection);
        }

        private void RemoveFromConnectionIndex(WorkflowConnection connection)
        {
            if (_sourceConnectionIndex.TryGetValue(connection.SourceNodeId, out var sourceConns))
            {
                sourceConns.Remove(connection);
                if (sourceConns.Count == 0)
                {
                    _sourceConnectionIndex.Remove(connection.SourceNodeId);
                }
            }

            if (_targetConnectionIndex.TryGetValue(connection.TargetNodeId, out var targetConns))
            {
                targetConns.Remove(connection);
                if (targetConns.Count == 0)
                {
                    _targetConnectionIndex.Remove(connection.TargetNodeId);
                }
            }
        }
    }
}
