using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Workflow
{
/// <summary>
/// 工作流（执行流定义）
/// </summary>
/// <remarks>
/// 定义检测任务的执行逻辑,包括节点拓扑和连接关系。
///
/// 特性：
/// - 可复用：多个Solution可以共享同一个Workflow
/// - 可升级：修改Workflow不影响NodeParameters数据
/// - 可版本化：支持多个版本共存
/// </remarks>
public class Workflow : ObservableObject, IWorkflowConnectionProvider, INodeInfoProvider
{
    [JsonIgnore]
    public ToolExecutionOwner ExecutionOwner { get; internal set; } = new();
    public void AttachExecutionOwner(ToolExecutionOwner owner)
    {
        if (ReferenceEquals(ExecutionOwner, owner)) return;
        ExecutionOwner.Retire();
        ExecutionOwner = owner;
    }
    [System.Text.Json.Serialization.JsonExtensionData]
    public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }

    private string _name = "新建工作流";

    /// <summary>
    /// 工作流唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 工作流名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, "工作流名称");
    }

    /// <summary>
    /// 是否为子程序工作流（不显示独立 Tab，由子程序节点双击进入）
    /// </summary>
    [JsonInclude]
    public bool IsSubroutine { get; set; }

    /// <summary>
    /// 节点列表（ObservableCollection 支持UI直接绑定）
    /// </summary>
    public ObservableCollection<WorkflowNodeBase> Nodes { get; set; } = new();

    /// <summary>
    /// 连接列表（ObservableCollection 支持UI直接绑定）
    /// </summary>
    public ObservableCollection<WorkflowConnection> Connections { get; set; } = new();

    /// <summary>
    /// 节点添加事件
    /// </summary>
    public event EventHandler<WorkflowNodeEventArgs>? NodeAdded;

    /// <summary>
    /// 节点移除事件
    /// </summary>
    public event EventHandler<WorkflowNodeEventArgs>? NodeRemoved;

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public Workflow()
    {
    }

    /// <summary>
    /// 两参数构造函数
    /// </summary>
    /// <param name="id">工作流ID</param>
    /// <param name="name">工作流名称</param>
    public Workflow(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// 克隆工作流
    /// </summary>
    public Workflow Clone()
    {
        var cloned = new Workflow
        {
            Id = Guid.NewGuid().ToString(),
            AdditionalData = AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone()),
            Name = $"{Name} (副本)"
        };

        // 克隆节点
        foreach (var node in Nodes)
        {
            cloned.Nodes.Add(node.Clone());
        }

        // 克隆连接
        foreach (var connection in Connections)
        {
            cloned.Connections.Add(new WorkflowConnection
            {
                Id = Guid.NewGuid().ToString(),
                SourceNodeId = connection.SourceNodeId,
                SourcePort = connection.SourcePort,
                TargetNodeId = connection.TargetNodeId,
                TargetPort = connection.TargetPort,
                ManualPathPoints = connection.ManualPathPoints?.ToArray()
            });
        }

        return cloned;
    }

        /// <summary>
        /// 验证工作流
        /// </summary>
        public (bool IsValid, List<string> Errors) Validate()
        {
            var errors = new List<string>();

            // 检查是否有节点
            if (Nodes.Count == 0)
            {
                errors.Add("工作流没有节点");
            }

            // 检查节点ID唯一性
            var nodeIds = new HashSet<string>();
            foreach (var node in Nodes)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    errors.Add($"节点 {node.Name} 没有ID");
                }
                else if (!nodeIds.Add(node.Id))
                {
                    errors.Add($"节点ID重复: {node.Id}");
                }
            }

            // 检查连接的有效性
            var nodeIdsSet = new HashSet<string>(Nodes.Select(n => n.Id));
            foreach (var connection in Connections)
            {
                if (!nodeIdsSet.Contains(connection.SourceNodeId))
                {
                    errors.Add($"连接源节点不存在: {connection.SourceNodeId}");
                }

                if (!nodeIdsSet.Contains(connection.TargetNodeId))
                {
                    errors.Add($"连接目标节点不存在: {connection.TargetNodeId}");
                }
            }

            // 检查是否有孤立节点（既没有输入也没有输出）
            var connectedNodes = new HashSet<string>();
            foreach (var connection in Connections)
            {
                connectedNodes.Add(connection.SourceNodeId);
                connectedNodes.Add(connection.TargetNodeId);
            }

            var isolatedNodes = Nodes
                .Where(n => !connectedNodes.Contains(n.Id))
                .Select(n => n.Name)
                .ToList();

            if (isolatedNodes.Count > 0)
            {
                errors.Add($"存在孤立节点: {string.Join(", ", isolatedNodes)}");
            }

            // 检查循环依赖
            if (HasCycle())
            {
                errors.Add("工作流存在循环依赖");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// 检查是否存在循环依赖
        /// </summary>
        private bool HasCycle()
        {
            var graph = new Dictionary<string, List<string>>();
            foreach (var node in Nodes)
            {
                graph[node.Id] = new List<string>();
            }

            foreach (var connection in Connections)
            {
                graph[connection.SourceNodeId].Add(connection.TargetNodeId);
            }

            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var node in Nodes)
            {
                if (HasCycleUtil(node.Id, graph, visited, recursionStack))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasCycleUtil(
            string nodeId,
            Dictionary<string, List<string>> graph,
            HashSet<string> visited,
            HashSet<string> recursionStack)
        {
            if (recursionStack.Contains(nodeId))
            {
                return true;
            }

            if (visited.Contains(nodeId))
            {
                return false;
            }

            visited.Add(nodeId);
            recursionStack.Add(nodeId);

            foreach (var neighbor in graph.GetValueOrDefault(nodeId, new List<string>()))
            {
                if (HasCycleUtil(neighbor, graph, visited, recursionStack))
                {
                    return true;
                }
            }

            recursionStack.Remove(nodeId);
            return false;
        }

        /// <summary>
        /// 获取节点
        /// </summary>
        public WorkflowNodeBase? GetNode(string nodeId)
        {
            return Nodes.FirstOrDefault(n => n.Id == nodeId);
        }

        #region IWorkflowConnectionProvider / INodeInfoProvider 实现

        /// <summary>
        /// BFS 递归查找所有上游节点（包括直接父节点和间接父节点）
        /// </summary>
        public List<string> GetUpstreamNodeIds(string nodeId)
        {
            if (Connections == null)
                return new List<string>();

            var upstreamNodeIds = new List<string>();
            var queue = new Queue<string>();
            var visited = new HashSet<string>();

            queue.Enqueue(nodeId);
            visited.Add(nodeId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                foreach (var conn in Connections)
                {
                    if (conn.TargetNodeId == currentId && visited.Add(conn.SourceNodeId))
                    {
                        upstreamNodeIds.Add(conn.SourceNodeId);
                        queue.Enqueue(conn.SourceNodeId);
                    }
                }
            }

            return upstreamNodeIds;
        }

        // IWorkflowConnectionProvider
        public List<string> GetParentNodeIds(string nodeId) => GetUpstreamNodeIds(nodeId);

        public List<string> GetChildNodeIds(string nodeId)
        {
            if (Connections == null) return new List<string>();
            return Connections
                .Where(c => c.SourceNodeId == nodeId)
                .Select(c => c.TargetNodeId)
                .Distinct()
                .ToList();
        }

        public List<string> GetAllNodeIds() => Nodes?.Select(n => n.Id).ToList() ?? new List<string>();

        // INodeInfoProvider
        public string GetNodeName(string nodeId) => GetNode(nodeId)?.Name ?? nodeId;
        public string GetNodeType(string nodeId) => GetNode(nodeId)?.ToolType ?? "Unknown";
        public string? GetNodeIcon(string nodeId) => GetNode(nodeId)?.Icon;
        public bool NodeExists(string nodeId) => Nodes?.Any(n => n.Id == nodeId) ?? false;

        public ToolParameters? GetParameters(string nodeId) => GetNode(nodeId)?.Parameters;

        public Type? GetResultType(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return null;
            var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
            var toolType = toolMetadata?.ToolType;
            if (toolType == null) return null;
            foreach (var iface in toolType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IToolPlugin<,>))
                {
                    var genericArgs = iface.GetGenericArguments();
                    if (genericArgs.Length >= 2) return genericArgs[1];
                }
            }
            return null;
        }

        #endregion


        /// <summary>
        /// 获取节点的输出类型列表
        /// </summary>
        /// <param name="node">节点</param>
        /// <returns>输出类型列表</returns>
        private List<Type> GetNodeOutputTypes(WorkflowNodeBase node)
        {
            var outputTypes = new List<Type>();

            if (node == null)
            {
                return outputTypes;
            }

            // 从工具元数据中获取 ResultType
            var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
            Type? toolType = toolMetadata?.ToolType;

            if (toolType == null)
            {
                return outputTypes;
            }

            // 从 ToolType 推断 ResultType
            foreach (var iface in toolType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IToolPlugin<,>))
                {
                    var genericArgs = iface.GetGenericArguments();
                    if (genericArgs.Length >= 2)
                    {
                        var resultType = genericArgs[1]; // TResult 是第二个泛型参数

                        // 从 ResultType 中提取所有公共属性的类型
                        var properties = resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in properties)
                        {
                            // 跳过基类属性和特殊属性
                            if (prop.DeclaringType == typeof(ToolResults) ||
                                prop.Name == "Status" ||
                                prop.Name == "ErrorMessage" ||
                                prop.Name == "ExecutionTimeMs" ||
                                prop.Name == "Timestamp" ||
                                prop.Name == "ToolName" ||
                                prop.Name == "ToolId")
                            {
                                continue;
                            }

                            if (!outputTypes.Contains(prop.PropertyType))
                            {
                                outputTypes.Add(prop.PropertyType);
                            }
                        }

                        break;
                    }
                }
            }

            return outputTypes;
        }

        /// <summary>
        /// 连接建立时更新上游节点池（增量更新）
        /// </summary>
        /// <param name="connection">新建立的连接</param>
        private void UpdateUpstreamPoolOnConnectionAdded(WorkflowConnection connection)
        {
            var targetNode = GetNode(connection.TargetNodeId);
            var sourceNode = GetNode(connection.SourceNodeId);

            if (targetNode == null || sourceNode == null)
            {
                return;
            }

            // 获取源节点的输出类型
            var outputTypes = GetNodeOutputTypes(sourceNode);

            // 更新目标节点的上游节点池
            targetNode.AddUpstreamNode(sourceNode.Id, outputTypes);
        }

        /// <summary>
        /// 连接删除时更新上游节点池（增量更新）
        /// </summary>
        /// <param name="connection">被删除的连接</param>
        private void UpdateUpstreamPoolOnConnectionRemoved(WorkflowConnection connection)
        {
            var targetNode = GetNode(connection.TargetNodeId);
            var sourceNode = GetNode(connection.SourceNodeId);

            if (targetNode == null || sourceNode == null)
            {
                return;
            }

            // 获取源节点的输出类型
            var outputTypes = GetNodeOutputTypes(sourceNode);

            // 更新目标节点的上游节点池
            targetNode.RemoveUpstreamNode(sourceNode.Id, outputTypes);
        }

    /// <summary>
    /// 添加节点
    /// </summary>
    public void AddNode(WorkflowNodeBase node)
    {
        Nodes.Add(node);

        // 触发节点添加事件
        NodeAdded?.Invoke(this, new WorkflowNodeEventArgs { Node = node, Workflow = this });
    }

    /// <summary>
    /// 移除节点
    /// </summary>
    public bool RemoveNode(string nodeId)
    {
        var node = GetNode(nodeId);
        if (node == null)
            return false;

        // 移除相关连接（ObservableCollection 不支持 RemoveAll，使用循环删除）
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            if (Connections[i].SourceNodeId == nodeId || Connections[i].TargetNodeId == nodeId)
            {
                Connections.RemoveAt(i);
            }
        }

        ExecutionOwner.RetireNode(Id + "/" + nodeId);
        Nodes.Remove(node);

        // 触发节点移除事件
        NodeRemoved?.Invoke(this, new WorkflowNodeEventArgs { Node = node, Workflow = this });

        return true;
    }

    /// <summary>
    /// 添加连接
    /// </summary>
    public void AddConnection(WorkflowConnection connection)
    {
        if (!Nodes.Any(n => n.Id == connection.SourceNodeId))
            throw new ArgumentException($"源节点不存在: {connection.SourceNodeId}", nameof(connection));
        if (!Nodes.Any(n => n.Id == connection.TargetNodeId))
            throw new ArgumentException($"目标节点不存在: {connection.TargetNodeId}", nameof(connection));

        Connections.Add(connection);

        // 增量更新：连接建立时，更新目标节点的上游节点池
        UpdateUpstreamPoolOnConnectionAdded(connection);
    }

    /// <summary>
    /// 连接两个节点
    /// </summary>
    /// <param name="sourceNodeId">源节点ID</param>
    /// <param name="targetNodeId">目标节点ID</param>
    /// <returns>创建的连接</returns>
    public WorkflowConnection ConnectNodes(string sourceNodeId, string targetNodeId)
    {
        if (!Nodes.Any(n => n.Id == sourceNodeId))
            throw new ArgumentException($"源节点不存在: {sourceNodeId}", nameof(sourceNodeId));
        if (!Nodes.Any(n => n.Id == targetNodeId))
            throw new ArgumentException($"目标节点不存在: {targetNodeId}", nameof(targetNodeId));

        var connection = new WorkflowConnection
        {
            Id = Guid.NewGuid().ToString(),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            SourcePort = "output",
            TargetPort = "input"
        };
        Connections.Add(connection);
        return connection;
    }

    /// <summary>
    /// 移除连接
    /// </summary>
    public bool RemoveConnection(string connectionId)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection == null)
            return false;

        Connections.Remove(connection);

        // 增量更新：连接删除时，更新目标节点的上游节点池
        UpdateUpstreamPoolOnConnectionRemoved(connection);

        return true;
    }

        /// <summary>
        /// 检测循环依赖
        /// </summary>
        public List<string> DetectCycles()
        {
            var cycles = new List<string>();
            var graph = new Dictionary<string, List<string>>();

            // 构建邻接表
            foreach (var node in Nodes)
            {
                graph[node.Id] = new List<string>();
            }

            foreach (var connection in Connections)
            {
                graph[connection.SourceNodeId].Add(connection.TargetNodeId);
            }

            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();
            var path = new List<string>();

            foreach (var node in Nodes)
            {
                if (FindCycles(node.Id, graph, visited, recursionStack, path, cycles))
                {
                    break;
                }
            }

            return cycles;
        }

        private bool FindCycles(
            string nodeId,
            Dictionary<string, List<string>> graph,
            HashSet<string> visited,
            HashSet<string> recursionStack,
            List<string> path,
            List<string> cycles)
        {
            if (recursionStack.Contains(nodeId))
            {
                // 找到循环，记录循环路径
                var cycleIndex = path.IndexOf(nodeId);
                if (cycleIndex >= 0)
                {
                    var cyclePath = path.Skip(cycleIndex).Concat(new[] { nodeId });
                    cycles.Add(string.Join(" -> ", cyclePath));
                }
                return true;
            }

            if (visited.Contains(nodeId))
            {
                return false;
            }

            visited.Add(nodeId);
            recursionStack.Add(nodeId);
            path.Add(nodeId);

            foreach (var neighbor in graph.GetValueOrDefault(nodeId, new List<string>()))
            {
                if (FindCycles(neighbor, graph, visited, recursionStack, path, cycles))
                {
                    recursionStack.Remove(nodeId);
                    path.RemoveAt(path.Count - 1);
                    return true;
                }
            }

            recursionStack.Remove(nodeId);
            path.RemoveAt(path.Count - 1);
            return false;
        }

        /// <summary>
        /// 获取并行执行组（基于执行链识别）
        /// </summary>
        public List<List<string>> GetParallelExecutionGroupsByChains()
        {
            var groups = new List<List<string>>();
            var visited = new HashSet<string>();
            var inDegree = new Dictionary<string, int>();

            // 计算入度
            foreach (var node in Nodes)
            {
                inDegree[node.Id] = 0;
            }

            // 不变量：Connections 中所有 SourceNodeId/TargetNodeId 必在 Nodes 中
            // （由 AddConnection/RemoveNode 入口保证 + WorkflowJsonConverter 加载过滤）
            // 此处信任不变量，不再做防御检查。
            foreach (var connection in Connections)
            {
                inDegree[connection.TargetNodeId]++;
            }

            // 使用队列进行拓扑排序
            var queue = new Queue<string>();
            foreach (var node in Nodes)
            {
                if (inDegree[node.Id] == 0)
                {
                    queue.Enqueue(node.Id);
                }
            }

            while (queue.Count > 0)
            {
                // 处理当前层（可以并行执行的节点）
                var currentGroup = new List<string>();
                var currentLevelSize = queue.Count;

                for (int i = 0; i < currentLevelSize; i++)
                {
                    var nodeId = queue.Dequeue();
                    currentGroup.Add(nodeId);
                    visited.Add(nodeId);

                    // 更新相邻节点的入度
                    foreach (var connection in Connections.Where(c => c.SourceNodeId == nodeId))
                    {
                        inDegree[connection.TargetNodeId]--;
                        if (inDegree[connection.TargetNodeId] == 0)
                        {
                            queue.Enqueue(connection.TargetNodeId);
                        }
                    }
                }

                groups.Add(currentGroup);
            }

            return groups;
        }
    }

    /// <summary>
    /// 工作流连接线（统一模型，UI层和数据层共享同一引用）
    /// </summary>
    /// <remarks>
    /// 重构说明（2026-03-27）：
    /// - 合并原 UI 层 WorkflowConnection 和数据层 Connection 为单一类
    /// - UI 渲染属性（路径数据、箭头位置等）使用 [JsonIgnore] 隔离
    /// - 数据属性（SourceNodeId/TargetNodeId 等）参与序列化
    /// - 与节点相同策略：通过共享 Solution 资源实现零同步
    /// </remarks>
    public class WorkflowConnection : ObservableObject
    {
        private string _id = string.Empty;
        private string _sourceNodeId = string.Empty;
        private string _targetNodeId = string.Empty;
        private string _sourcePort = "output";
        private string _targetPort = "input";
        private Point _sourcePosition;
        private Point _targetPosition;
        private Point _arrowPosition;
        private double _arrowAngle = 0;
        private ConnectionStatus _status = ConnectionStatus.Idle;
        private bool _showPathPoints = false;
        private string _pathData = string.Empty;
        private bool _isSelected = false;
        private bool _isHovered = false;
        private int _pathUpdateCounter = 0;
        private bool _isVisible = true;

        /// <summary>
        /// 连接唯一标识符
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 源节点ID
        /// </summary>
        public string SourceNodeId
        {
            get => _sourceNodeId;
            set => SetProperty(ref _sourceNodeId, value);
        }

        /// <summary>
        /// 目标节点ID
        /// </summary>
        public string TargetNodeId
        {
            get => _targetNodeId;
            set => SetProperty(ref _targetNodeId, value);
        }

        /// <summary>
        /// 源端口名称
        /// </summary>
        public string SourcePort
        {
            get => _sourcePort;
            set => SetProperty(ref _sourcePort, value);
        }

        /// <summary>
        /// 目标端口名称
        /// </summary>
        public string TargetPort
        {
            get => _targetPort;
            set => SetProperty(ref _targetPort, value);
        }

        /// <summary>
        /// 箭头角度（UI渲染用，不序列化）
        /// </summary>
        [JsonIgnore]
        public double ArrowAngle
        {
            get => _arrowAngle;
            set => SetProperty(ref _arrowAngle, value);
        }

        /// <summary>
        /// 箭头位置（UI渲染用，不序列化）
        /// </summary>
        [JsonIgnore]
        public Point ArrowPosition
        {
            get => _arrowPosition;
            set
            {
                if (SetProperty(ref _arrowPosition, value))
                {
                    OnPropertyChanged(nameof(ArrowX));
                    OnPropertyChanged(nameof(ArrowY));
                }
            }
        }

        [JsonIgnore]
        public double ArrowX => ArrowPosition.X;

        [JsonIgnore]
        public double ArrowY => ArrowPosition.Y;

        [JsonIgnore]
        public double ArrowSize => 10;

        [JsonIgnore]
        public double ArrowScale => 1.0;

        /// <summary>
        /// 连接状态（UI渲染用，不序列化）
        /// </summary>
        [JsonIgnore]
        public ConnectionStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        [JsonIgnore]
        public string StatusColor => Status switch
        {
            ConnectionStatus.Idle => "#666666",
            ConnectionStatus.Transmitting => "#FF9500",
            ConnectionStatus.Completed => "#34C759",
            ConnectionStatus.Error => "#FF3B30",
            _ => "#666666"
        };

        [JsonIgnore]
        public bool ShowPathPoints
        {
            get => _showPathPoints;
            set => SetProperty(ref _showPathPoints, value);
        }

        /// <summary>
        /// 连线路径数据（UI渲染用，不序列化）
        /// </summary>
        [JsonIgnore]
        public string PathData
        {
            get => _pathData;
            set => SetProperty(ref _pathData, value);
        }

        [JsonIgnore]
        public ObservableCollection<Point> PathPoints { get; set; } = new();

        /// <summary>用户编辑的连线路径；自动计算路径仍只存于渲染缓存。</summary>
        public Point[]? ManualPathPoints { get; set; }

        [JsonIgnore]
        public Point[]? PreviewPathPoints { get; set; }


        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        [JsonIgnore]
        public bool IsHovered
        {
            get => _isHovered;
            set => SetProperty(ref _isHovered, value);
        }

        [JsonIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        [JsonIgnore]
        public Point SourcePosition
        {
            get => _sourcePosition;
            set
            {
                if (SetProperty(ref _sourcePosition, value))
                {
                    OnPropertyChanged(nameof(StartX));
                    OnPropertyChanged(nameof(StartY));
                }
            }
        }

        [JsonIgnore]
        public Point TargetPosition
        {
            get => _targetPosition;
            set
            {
                if (SetProperty(ref _targetPosition, value))
                {
                    OnPropertyChanged(nameof(EndX));
                    OnPropertyChanged(nameof(EndY));
                }
            }
        }

        [JsonIgnore]
        public double StartX => SourcePosition.X;

        [JsonIgnore]
        public double StartY => SourcePosition.Y;

        [JsonIgnore]
        public double EndX => TargetPosition.X;

        [JsonIgnore]
        public double EndY => TargetPosition.Y;

        public WorkflowConnection()
        {
            Id = string.Empty;
            SourceNodeId = string.Empty;
            TargetNodeId = string.Empty;
            SourcePosition = new Point(0, 0);
            TargetPosition = new Point(0, 0);
            ArrowPosition = new Point(0, 0);
        }

        public WorkflowConnection(string id, string sourceNodeId, string targetNodeId)
        {
            Id = id;
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
            SourcePosition = new Point(0, 0);
            TargetPosition = new Point(0, 0);
            ArrowPosition = new Point(0, 0);
        }

        /// <summary>
        /// 路径更新计数器（UI渲染用，不序列化）
        /// </summary>
        [JsonIgnore]
        public int PathUpdateCounter
        {
            get => _pathUpdateCounter;
            private set
            {
                if (_pathUpdateCounter != value)
                {
                    _pathUpdateCounter = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 触发路径相关属性的更新
        /// </summary>
        public void InvalidatePath()
        {
            _pathUpdateCounter++;
            OnPropertyChanged(nameof(PathUpdateCounter));
        }
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ConnectionStatus
    {
        Idle,
        Transmitting,
        Completed,
        Error
    }

    /// <summary>
    /// 工作流节点事件参数
    /// </summary>
    public class WorkflowNodeEventArgs : EventArgs
    {
        /// <summary>
        /// 节点
        /// </summary>
        public WorkflowNodeBase? Node { get; set; }

        /// <summary>
        /// 所属工作流
        /// </summary>
        public Workflow? Workflow { get; set; }
    }
}
