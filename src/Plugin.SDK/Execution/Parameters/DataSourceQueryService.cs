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
    /// 2. 通过 SSOT 读取器（委托到 WorkflowContext）实时读取执行结果
    /// 3. 支持类型过滤
    /// 4. 不维护本地结果缓存，全部通过 _ssotReader 委托读取 SSOT
    /// </remarks>
    public class DataSourceQueryService : IDataSourceQueryService
    {
        /// <summary>
        /// SSOT 读取器（委托到 WorkflowContext，实时读取持久结果，不维护本地缓存）
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
        public List<AvailableDataSource> GetImageDisplaySources(string currentNodeId)
        {
            var result = new List<AvailableDataSource>();

            // 图像类型名集合（系统统一使用 IVisionImage）
            var imageTypeNames = new HashSet<string> { "IVisionImage" };

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

            // ★ 合并额外数据源中的图像类型（仅保留节点输出，排除子程序输入端口）
            // 说明：子程序输入端口（SourceNodeId == "__SubroutineInputs__"）供 ImageSourceSelector 使用，
            // 不应出现在图像显示器的 ComboBox 中。
            foreach (var extra in GetExtraDataSources().Where(ds =>
                ds.PropertyType != null && imageTypeNames.Contains(ds.PropertyType.Name)
                && ds.SourceNodeId != "__SubroutineInputs__"))
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
                Result = _ssotReader?.Invoke(nodeId),
                ResultType = _nodeInfoProvider.GetResultType(nodeId)
            };
        }

        /// <summary>
        /// 从 SSOT 读取器获取节点执行结果（无本地缓存，实时读取 WorkflowContext）
        /// </summary>
        private ToolResults? GetNodeResult(string nodeId)
        {
            return _ssotReader?.Invoke(nodeId);
        }

        /// <inheritdoc/>
        public object? GetPropertyValue(string nodeId, string propertyName)
        {
            // 从 SSOT 读取器获取节点结果并反射取值（无本地缓存）
            var result = _ssotReader?.Invoke(nodeId);
            if (result != null)
            {
                return GetNestedPropertyValue(result, propertyName);
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

        /// <inheritdoc/>
        public bool HasNodeExecuted(string nodeId)
        {
            return _ssotReader?.Invoke(nodeId) != null;
        }

        /// <inheritdoc/>
        public bool IsNodeRegistered(string nodeId)
        {
            return _nodeInfoCache.ContainsKey(nodeId);
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
