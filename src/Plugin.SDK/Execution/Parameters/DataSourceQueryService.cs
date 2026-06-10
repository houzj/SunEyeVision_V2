using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 数据源查询服务实现
    /// </summary>
    /// <remarks>
    /// 提供查询父节点及其输出属性的能力。
    /// 
    /// 核心功能：
    /// 1. 基于工作流连接关系查找父节点
    /// 2. 从执行结果缓存提取输出属性
    /// 3. 支持类型过滤
    /// 4. 线程安全的结果缓存
    /// 
    /// 设计优化历史：
    /// - 支持可选注入 IPropertyMetadataProvider（设计时优化）
    /// - SDK 层不依赖 Infrastructure 层（依赖倒置原则）
    /// </remarks>
    public class DataSourceQueryService : IDataSourceQueryService
    {
        /// <summary>
        /// 节点结果缓存（本地覆盖，用于"正在执行的实时数据"）
        /// </summary>
        private readonly ConcurrentDictionary<string, ToolResults> _nodeResults = new ConcurrentDictionary<string, ToolResults>();

        /// <summary>
        /// SSOT 读取器（引用 WorkflowContext 中的持久结果，仅作为回退读取，不写回）
        /// </summary>
        private Func<string, ToolResults?>? _ssotReader;

        /// <summary>
        /// Logger
        /// </summary>
        private readonly ILogger? _logger;

        /// <summary>
        /// 节点信息缓存
        /// </summary>
        private readonly ConcurrentDictionary<string, ParentNodeInfo> _nodeInfoCache = new ConcurrentDictionary<string, ParentNodeInfo>();

        /// <summary>
        /// 节点输出缓存（节点ID -> 属性名 -> 值）
        /// </summary>
        private readonly ConcurrentDictionary<string, Dictionary<string, object?>> _nodeOutputs = new ConcurrentDictionary<string, Dictionary<string, object?>>();

        /// <summary>
        /// 额外数据源提供者委托列表（每次查询时动态调用，支持输入名称实时更新）
        /// </summary>
        private readonly List<Func<List<AvailableDataSource>>> _extraDataSourceProviders = new();

        /// <summary>
        /// 注册额外数据源提供者委托
        /// </summary>
        /// <param name="provider">每次查询时动态调用的委托，返回额外数据源列表</param>
        public void AddExtraDataSourceProvider(Func<List<AvailableDataSource>> provider)
        {
            lock (_extraDataSourceProviders)
            {
                _extraDataSourceProviders.Add(provider);
            }
        }

        /// <summary>
        /// 从所有注册提供者获取当前额外数据源（动态合并，支持实时更新）
        /// </summary>
        private List<AvailableDataSource> GetExtraDataSources()
        {
            var result = new List<AvailableDataSource>();
            lock (_extraDataSourceProviders)
            {
                foreach (var provider in _extraDataSourceProviders)
                {
                    try
                    {
                        var sources = provider();
                        if (sources == null) continue;
                        foreach (var ds in sources)
                        {
                            if (!result.Any(e => e.SourceNodeId == ds.SourceNodeId && e.PropertyName == ds.PropertyName))
                                result.Add(ds);
                        }
                    }
                    catch { /* 提供者异常不传播 */ }
                }
            }
            return result;
        }

        /// <summary>
        /// 工作流连接提供者（外部注入）
        /// </summary>
        private readonly IWorkflowConnectionProvider? _connectionProvider;

        /// <summary>
        /// 节点信息提供者（外部注入）
        /// </summary>
        private readonly INodeInfoProvider? _nodeInfoProvider;

        /// <summary>
        /// 属性元数据提供者(可选,用于设计时优化)
        /// </summary>
        private readonly IPropertyMetadataProvider? _metadataProvider;

        /// <summary>
        /// 输出变更订阅字典
        /// </summary>
        private readonly ConcurrentDictionary<string, List<Action<object?>>> _outputSubscriptions = new ConcurrentDictionary<string, List<Action<object?>>>();

        /// <summary>
        /// 当前节点ID
        /// </summary>
        private string? _currentNodeId;

        /// <summary>
        /// 当前节点ID
        /// </summary>
        public string? CurrentNodeId
        {
            get => _currentNodeId;
            set => _currentNodeId = value;
        }

        /// <summary>
        /// 创建数据源查询服务
        /// </summary>
        public DataSourceQueryService()
        {
        }

        /// <summary>
        /// 创建数据源查询服务（带依赖注入）
        /// </summary>
        /// <param name="connectionProvider">工作流连接提供者</param>
        /// <param name="nodeInfoProvider">节点信息提供者</param>
        public DataSourceQueryService(
            IWorkflowConnectionProvider? connectionProvider,
            INodeInfoProvider? nodeInfoProvider = null)
        {
            _connectionProvider = connectionProvider;
            _nodeInfoProvider = nodeInfoProvider;
        }

        /// <summary>
        /// 创建数据源查询服务（带依赖注入和 Logger）
        /// </summary>
        /// <param name="connectionProvider">工作流连接提供者</param>
        /// <param name="nodeInfoProvider">节点信息提供者</param>
        /// <param name="logger">Logger</param>
        public DataSourceQueryService(
            IWorkflowConnectionProvider? connectionProvider,
            INodeInfoProvider? nodeInfoProvider,
            ILogger? logger = null)
        {
            _connectionProvider = connectionProvider;
            _nodeInfoProvider = nodeInfoProvider;
            _logger = logger;
        }

        /// <summary>
        /// 创建数据源查询服务（带依赖注入和 Logger 和属性元数据提供者）
        /// </summary>
        /// <param name="connectionProvider">工作流连接提供者</param>
        /// <param name="nodeInfoProvider">节点信息提供者</param>
        /// <param name="logger">Logger</param>
        /// <param name="metadataProvider">属性元数据提供者(可选)</param>
        public DataSourceQueryService(
            IWorkflowConnectionProvider? connectionProvider,
            INodeInfoProvider? nodeInfoProvider,
            ILogger? logger = null,
            IPropertyMetadataProvider? metadataProvider = null)
        {
            _connectionProvider = connectionProvider;
            _nodeInfoProvider = nodeInfoProvider;
            _logger = logger;
            _metadataProvider = metadataProvider;
        }

        /// <summary>
        /// 设置 SSOT 读取器，使 DSQS 能回退到外部持久化结果
        /// </summary>
        /// <param name="reader">给定节点ID返回 ToolResults 的委托</param>
        public void SetSootReader(Func<string, ToolResults?> reader)
        {
            _ssotReader = reader;
        }

        /// <summary>
        /// 从外部源批量初始化本地缓存（创建 DSQS 时调用，预加载已有执行结果）
        /// </summary>
        /// <param name="source">节点ID到 ToolResults 的映射</param>
        public void InitializeFromSource(Dictionary<string, ToolResults> source)
        {
            foreach (var kvp in source)
            {
                _nodeResults[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// 从外部源批量初始化本地缓存（创建 DSQS 时调用，预加载已有执行结果）
        /// </summary>
        /// <param name="source">节点ID到 ToolResults 的映射</param>
        public void InitializeFromSource(IEnumerable<KeyValuePair<string, ToolResults>> source)
        {
            foreach (var kvp in source)
            {
                _nodeResults[kvp.Key] = kvp.Value;
            }
        }

        /// <inheritdoc/>
        public List<ParentNodeInfo> GetParentNodes(string nodeId)
        {
            var parentNodes = new List<ParentNodeInfo>();

            if (_connectionProvider == null)
            {
                // 如果没有连接提供者，返回空列表
                return parentNodes;
            }

            // 获取父节点ID列表
            var parentNodeIds = _connectionProvider.GetParentNodeIds(nodeId);

            int order = 0;

            foreach (var parentNodeId in parentNodeIds)
            {
                // 直接创建父节点信息
                var nodeInfo = new ParentNodeInfo
                {
                    NodeId = parentNodeId,
                    NodeName = _nodeInfoProvider?.GetNodeName(parentNodeId) ?? parentNodeId,
                    NodeType = _nodeInfoProvider?.GetNodeType(parentNodeId) ?? "Unknown",
                    NodeIcon = _nodeInfoProvider?.GetNodeIcon(parentNodeId)
                };

                // 获取该节点的输出属性
                var outputProperties = GetNodeOutputProperties(parentNodeId);
                nodeInfo.OutputProperties = outputProperties;

                parentNodes.Add(nodeInfo);
            }

            return parentNodes;
        }

        /// <inheritdoc/>
        public List<AvailableDataSource> GetAvailableDataSources(string nodeId, Type? targetType = null)
        {
            // 获取父节点
            var parentNodes = GetParentNodes(nodeId);
            var allDataSources = new List<AvailableDataSource>();

            foreach (var parent in parentNodes)
            {
                // 获取父节点的输出属性
                var outputProps = parent.OutputProperties;
                
                // 类型过滤（精确匹配 + 双向数组/元素兼容）
                // 场景1: targetType=double[]  → 匹配 double[] (精确) 或 double (元素)
                // 场景2: targetType=double   → 匹配 double (精确) 或 double[] (数组版本)
                // 原因：视觉工具输出通常是数组类型（BlobAreas=double[], BlobBoundingBoxes=WpfRect[]），
                //       但参数声明通常使用元素类型（Double, Circle），需要双向兼容。
                if (targetType != null)
                {
                    outputProps = outputProps
                        .Where(p => p.PropertyType == targetType ||
                                    (targetType.IsArray && p.PropertyType == targetType.GetElementType()) ||
                                    (!targetType.IsArray && p.PropertyType.IsArray && p.PropertyType.GetElementType() == targetType))
                        .ToList();
                }

                // 添加到结果列表
                foreach (var property in outputProps)
                {
                    var dataSource = new AvailableDataSource
                    {
                        SourceNodeId = parent.NodeId,
                        SourceNodeName = parent.NodeName,
                        SourceNodeType = parent.NodeType,
                        PropertyName = property.PropertyName,
                        DisplayName = property.DisplayName,
                        PropertyType = property.PropertyType,
                        CurrentValue = property.CurrentValue,
                        Unit = property.Unit,
                        Description = property.Description,
                        GroupName = property.GroupName,
                    };
                    allDataSources.Add(dataSource);
                }
            }

            // ★ 合并额外数据源（子程序输入端口等，支持实时更新）
            foreach (var extra in GetExtraDataSources())
            {
                if (targetType != null)
                {
                    bool typeMatch = extra.PropertyType == targetType ||
                        (targetType.IsArray && extra.PropertyType == targetType.GetElementType()) ||
                        (!targetType.IsArray && extra.PropertyType.IsArray && extra.PropertyType.GetElementType() == targetType);
                    if (!typeMatch) continue;
                }
                if (!allDataSources.Any(ds => ds.SourceNodeId == extra.SourceNodeId && ds.PropertyName == extra.PropertyName))
                    allDataSources.Add(extra);
            }

            return allDataSources;
        }

        /// <summary>
        /// 获取图像显示用的数据源集合（当前节点 + 所有上游节点）
        /// 统一供 ImageControl 使用，与 ImageSourceSelector 共享相同的元数据源
        /// </summary>
        /// <param name="currentNodeId">当前选中的节点ID</param>
        /// <returns>按距离排序的图像数据源列表</returns>
        public List<AvailableDataSource> GetMatDisplaySources(string currentNodeId)
        {
            var result = new List<AvailableDataSource>();

            // 图像类型名集合（兼容 Mat / IVisionImage / HImage 等）
            var imageTypeNames = new HashSet<string> { "Mat", "IVisionImage", "HImage" };

            // 步骤 1: 当前节点自身的图像输出 (Distance=0)
            var nodeType = _nodeInfoProvider?.GetNodeType(currentNodeId);
            var nodeName = _nodeInfoProvider?.GetNodeName(currentNodeId) ?? currentNodeId;
            if (nodeType != null)
            {
                var metadataList = _metadataProvider?.GetAllPropertyMetadata(nodeType);
                if (metadataList != null)
                {
                    foreach (var meta in metadataList.Where(m =>
                        m.PropertyType != null && imageTypeNames.Contains(m.PropertyType.Name)))
                    {
                        result.Add(new AvailableDataSource
                        {
                            SourceNodeId = currentNodeId,
                            SourceNodeName = nodeName,
                            PropertyName = meta.PropertyName,
                            DisplayName = meta.DisplayName,
                            PropertyType = meta.PropertyType,
                            Distance = 0,
                        });
                    }
                }
            }

            // 步骤 2: BFS 遍历所有上游节点的图像输出 (Distance>=1)
            if (_connectionProvider != null)
            {
                var visited = new HashSet<string> { currentNodeId };
                var queue = new Queue<(string NodeId, int Distance)>();
                queue.Enqueue((currentNodeId, 0));

                while (queue.Count > 0)
                {
                    var (currentId, currentDistance) = queue.Dequeue();
                    var parentIds = _connectionProvider.GetParentNodeIds(currentId);

                    foreach (var parentId in parentIds)
                    {
                        if (!visited.Contains(parentId))
                        {
                            visited.Add(parentId);
                            int childDistance = currentDistance + 1;

                            // 获取该节点的图像输出属性
                            foreach (var prop in GetNodeOutputProperties(parentId)
                                .Where(p => p.PropertyType != null && imageTypeNames.Contains(p.PropertyType.Name)))
                            {
                                prop.Distance = childDistance;
                                result.Add(prop);
                            }

                            queue.Enqueue((parentId, childDistance));
                        }
                    }
                }
            }

            // ★ 合并额外数据源中的图像类型（子程序输入端口等）
            var imageTypeNames2 = new HashSet<string> { "Mat", "IVisionImage", "HImage" };
            foreach (var extra in GetExtraDataSources().Where(ds =>
                ds.PropertyType != null && imageTypeNames2.Contains(ds.PropertyType.Name)))
            {
                if (!result.Any(r => r.SourceNodeId == extra.SourceNodeId && r.PropertyName == extra.PropertyName))
                {
                    extra.Distance = 1;
                    result.Add(extra);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public List<AvailableDataSource> GetNodeOutputProperties(string parentNodeId)
        {
            var properties = new List<AvailableDataSource>();
            var context = GetNodeContext(parentNodeId);

            if (context == null || context.ResultType == null)
            {
                return properties;
            }

            // 从变量池获取属性元数据（不再反射）
            // 注意：需要通过元数据提供者获取，避免直接依赖 Infrastructure 层
            var metadataList = _metadataProvider?.GetAllPropertyMetadata(context.NodeType);

            if (metadataList == null)
            {
                return properties;
            }

            foreach (var metadata in metadataList)
            {
                // 直接从元数据创建，不再反射
                object? currentValue = null;
                if (context.Result != null && metadata.PropertyType != null)
                {
                    // 运行时：填充实际值
                    var propInfo = context.ResultType.GetProperty(metadata.PropertyName);
                    currentValue = propInfo?.GetValue(context.Result);
                }

                var dataSource = new AvailableDataSource
                {
                    SourceNodeId = parentNodeId,
                    SourceNodeName = context.NodeName,
                    SourceNodeType = context.NodeType,
                    PropertyName = metadata.PropertyName,
                    DisplayName = metadata.DisplayName,
                    PropertyType = metadata.PropertyType,
                    CurrentValue = currentValue,
                    Unit = null,
                    Description = metadata.Description,
                    GroupName = metadata.GroupName,
                };

                properties.Add(dataSource);
            }

            return properties;
        }

        /// <inheritdoc/>
        public List<AvailableDataSource> GetWorkflowNodeDataSources()
        {
            var result = new List<AvailableDataSource>();
            var allNodeIds = _connectionProvider?.GetAllNodeIds();
            if (allNodeIds == null) return result;

            foreach (var nodeId in allNodeIds)
            {
                var props = GetNodeOutputProperties(nodeId);
                result.AddRange(props);
            }
            return result;
        }

        /// <inheritdoc/>
        public NodeExecutionContext? GetNodeContext(string nodeId)
        {
            if (_nodeInfoProvider == null)
            {
                return null;
            }

            return new NodeExecutionContext
            {
                NodeId = nodeId,
                NodeName = _nodeInfoProvider.GetNodeName(nodeId) ?? nodeId,
                NodeType = _nodeInfoProvider.GetNodeType(nodeId) ?? "Unknown",
                NodeIcon = _nodeInfoProvider.GetNodeIcon(nodeId),
                Result = GetNodeResultFromCache(nodeId),
                ResultType = _nodeInfoProvider.GetResultType(nodeId)
            };
        }

        /// <summary>
        /// 从缓存或 SSOT 获取节点执行结果
        /// </summary>
        private ToolResults? GetNodeResultFromCache(string nodeId)
        {
            if (_nodeResults.TryGetValue(nodeId, out var result))
                return result;
            if (_ssotReader != null)
                return _ssotReader(nodeId);
            return null;
        }

        /// <inheritdoc/>
        public object? GetPropertyValue(string nodeId, string propertyName)
        {
            // 解析属性名（去掉索引标记）
            var (pureName, arrayIndex) = PropertyPathParser.ParsePropertyPart(propertyName);

            // 从节点输出中获取属性值
            if (_nodeOutputs.TryGetValue(nodeId, out var outputs))
            {
                if (outputs.TryGetValue(pureName, out var value))
                {
                    // ★ 诊断：位置修正矩阵查询通过 _nodeOutputs 命中
                    if (pureName.Contains("PositionCorrection", StringComparison.Ordinal) || pureName.Contains("BestPosition", StringComparison.Ordinal))
                    {
                        var valueType = value?.GetType().Name ?? "null";
                        VisionLogger.Instance?.Log(LogLevel.Info,
                            $"[DSQS.GetPropertyValue] _nodeOutputs 命中 | NodeId={nodeId} | Property={pureName} | ValueType={valueType} | Value={value}",
                            "DSQS");
                    }
                    // 处理数组索引
                    if (arrayIndex.HasValue && value is Array array)
                    {
                        if (arrayIndex.Value >= 0 && arrayIndex.Value < array.Length)
                            return array.GetValue(arrayIndex.Value);
                        return null;  // 索引越界
                    }
                    return value;
                }
            }

            // 从节点结果中获取属性值（使用反射）
            var result = GetNodeResultFromCache(nodeId);
            if (result != null)
            {
                var val = GetNestedPropertyValue(result, propertyName);

                // ★ 诊断：位置修正矩阵查询通过 _nodeResults 反射命中
                if (pureName.Contains("PositionCorrection", StringComparison.Ordinal) || pureName.Contains("BestPosition", StringComparison.Ordinal))
                {
                    var valType = val?.GetType().Name ?? "null";
                    var resultType = result.GetType().Name;
                    VisionLogger.Instance?.Log(LogLevel.Info,
                        $"[DSQS.GetPropertyValue] 反射回退命中 | NodeId={nodeId} | Property={pureName} | ResultType={resultType} | ValueType={valType} | Value={val}",
                        "DSQS");
                }

                return val;
            }

            // ★ 诊断：位置修正矩阵查询完全未命中缓存
            if (pureName.Contains("PositionCorrection", StringComparison.Ordinal) || pureName.Contains("BestPosition", StringComparison.Ordinal))
            {
                var cachedResultTypes = string.Join(", ", _nodeResults.Keys);
                VisionLogger.Instance?.Log(LogLevel.Warning,
                    $"[DSQS.GetPropertyValue] 缓存未命中 | NodeId={nodeId} | Property={pureName} | _nodeResults.Keys=[{cachedResultTypes}] | _ssotReaderSet={_ssotReader != null}",
                    "DSQS");
            }

            return null;
        }

        /// <summary>
        /// 获取嵌套属性值（支持数组索引）
        /// </summary>
        private object? GetNestedPropertyValue(object obj, string propertyPath)
        {
            var parsedPath = PropertyPathParser.ParsePropertyPath(propertyPath);
            object? current = obj;

            foreach (var (propName, arrayIndex) in parsedPath)
            {
                if (current == null)
                    return null;

                var type = current.GetType();
                var property = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                    return null;

                try
                {
                    current = property.GetValue(current);

                    // 如果当前值是数组，根据索引处理
                    if (current is Array array)
                    {
                        if (arrayIndex.HasValue)
                        {
                            if (arrayIndex.Value >= 0 && arrayIndex.Value < array.Length)
                                current = array.GetValue(arrayIndex.Value);
                            else
                                return null;  // 索引越界
                        }
                        // [] 或无括号 → 返回整个数组（current 不变）
                    }
                    else if (arrayIndex.HasValue)
                    {
                        // 非数组但有索引 → 类型错误
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return current;
        }

        #region IDataSourceQueryService Runtime Methods

        /// <inheritdoc/>
        public bool HasNodeExecuted(string nodeId)
        {
            if (_nodeResults.ContainsKey(nodeId))
                return true;
            if (_ssotReader != null)
            {
                var result = _ssotReader(nodeId);
                if (result != null)
                    return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public void RefreshNodeData(string nodeId)
        {
            // TODO: 实现节点数据刷新逻辑
        }

        /// <inheritdoc/>
        public void RefreshAll()
        {
            // TODO: 实现所有数据刷新逻辑
        }

        /// <inheritdoc/>
        public void SetNodeResult(string nodeId, ToolResults result)
        {
            _nodeResults[nodeId] = result;
        }

        /// <inheritdoc/>
        public void ClearNodeResult(string nodeId)
        {
            _nodeResults.TryRemove(nodeId, out _);
        }

        /// <inheritdoc/>
        public void ClearAllResults()
        {
            _nodeResults.Clear();
        }

        /// <inheritdoc/>
        public bool HasNodeOutput(string nodeId)
        {
            return _nodeOutputs.ContainsKey(nodeId);
        }

        /// <inheritdoc/>
        public void UpdateNodeOutput(string nodeId, string propertyName, object? value)
        {
            if (!_nodeOutputs.ContainsKey(nodeId))
            {
                _nodeOutputs[nodeId] = new Dictionary<string, object?>();
            }
            _nodeOutputs[nodeId][propertyName] = value;
        }

        /// <inheritdoc/>
        public void ClearNodeOutput(string nodeId)
        {
            _nodeOutputs.TryRemove(nodeId, out _);
        }

        /// <inheritdoc/>
        public object? GetCurrentBindingValue(string nodeId, string propertyName, string? bindingPath = null)
        {
            // TODO: 实现绑定值获取逻辑
            return null;
        }

        /// <inheritdoc/>
        public string GetBindingDisplayPath(string nodeId, string propertyName, string? bindingPath = null)
        {
            // TODO: 实现绑定显示路径逻辑
            return propertyName;
        }

        /// <inheritdoc/>
        public IDisposable SubscribeOutputChanged(string nodeId, string propertyName, string? bindingPath, Action<object?> callback)
        {
            // TODO: 实现输出变更订阅逻辑

            // 返回一个空的订阅令牌
            return new SubscriptionToken(() => { });
        }

        /// <inheritdoc/>
        public void RefreshOutputs()
        {
            // TODO: 实现输出刷新逻辑
        }

        /// <inheritdoc/>
        public bool IsNodeRegistered(string nodeId)
        {
            return _nodeInfoCache.ContainsKey(nodeId);
        }

        #endregion

        // ========================================
        // 辅助方法
        // ========================================

        /// <summary>
        /// 创建数据源对象
        /// </summary>
        private AvailableDataSource? CreateDataSource(ParentNodeInfo node, ToolPropertyMetadata metadata)
        {
            // 获取当前值
            object? currentValue = null;
            var property = node.OutputProperties.FirstOrDefault(p => p.PropertyName == metadata.PropertyName);
            if (property != null)
            {
                currentValue = property.CurrentValue;
            }
            
            return new AvailableDataSource
            {
                SourceNodeId = node.NodeId,
                SourceNodeName = node.NodeName,
                SourceNodeType = node.NodeType,
                PropertyName = metadata.PropertyName,
                DisplayName = metadata.DisplayName,
                PropertyType = metadata.PropertyType,
                CurrentValue = currentValue,
                Unit = null,
                Description = metadata.Description,
                GroupName = metadata.GroupName,
            };
        }

        /// <summary>
        /// 订阅令牌
        /// </summary>
        private class SubscriptionToken : IDisposable
        {
            private readonly Action _disposeAction;
            private bool _disposed;

            public SubscriptionToken(Action disposeAction)
            {
                _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposeAction();
                    _disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// 工作流连接提供者接口
    /// </summary>
    /// <remarks>
    /// 提供工作流节点连接关系查询能力。
    /// 由工作流引擎或UI层实现。
    /// </remarks>
    public interface IWorkflowConnectionProvider
    {
        /// <summary>
        /// 获取父节点ID列表
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        /// <returns>父节点ID列表</returns>
        List<string> GetParentNodeIds(string nodeId);

        /// <summary>
        /// 获取子节点ID列表
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        /// <returns>子节点ID列表</returns>
        List<string> GetChildNodeIds(string nodeId);

        /// <summary>
        /// 获取所有节点ID
        /// </summary>
        /// <returns>所有节点ID列表</returns>
        List<string> GetAllNodeIds();
    }

    /// <summary>
    /// 节点信息提供者接口
    /// </summary>
    /// <remarks>
    /// 提供节点基本信息查询能力。
    /// 由工作流引擎或UI层实现。
    /// </remarks>
    public interface INodeInfoProvider
    {
        /// <summary>
        /// 获取节点名称
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>节点名称</returns>
        string GetNodeName(string nodeId);

        /// <summary>
        /// 获取节点类型
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>节点类型</returns>
        string GetNodeType(string nodeId);

        /// <summary>
        /// 获取节点图标
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>节点图标路径或名称</returns>
        string? GetNodeIcon(string nodeId);

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>是否存在</returns>
        bool NodeExists(string nodeId);

        /// <summary>
        /// 获取节点结果类型
        /// </summary>
        /// <remarks>
        /// 用于设计时推断输出属性。UI 层实现时，可以从工具元数据中获取 ResultType。
        /// </remarks>
        /// <param name="nodeId">节点ID</param>
        /// <returns>结果类型，如果无法获取则返回 null</returns>
        Type? GetResultType(string nodeId);
    }
}
