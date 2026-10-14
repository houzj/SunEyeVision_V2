using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;

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
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationReceiveService { get; set; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationSendService { get; set; }
        public Action<string>? CommitOutputConfiguration { get; set; }
        public string? WorkflowId { get; set; }
        public Func<string, IReadOnlyList<string>>? BindingCandidateNodeIds { get; set; }
        public Func<string, IReadOnlyList<string>>? ExistingBindingSourceNodeIds { get; set; }
        public Func<string, IReadOnlyList<OutputVariable>?>? FormalOutputContractReader { get; set; }
        public Func<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource?>? SelectReceiveVariable { get; set; }
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

        /// <summary>调试执行前可用的方案级全局变量快照。</summary>
        public IDictionary<string, object?> GlobalVariables { get; private set; }
            = new Dictionary<string, object?>(StringComparer.Ordinal);

        public void SetGlobalVariables(IDictionary<string, object?>? variables)
        {
            GlobalVariables = variables == null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(variables, StringComparer.Ordinal);
        }

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
            => GetParentNodes(nodeId, false);

        private List<ParentNodeInfo> GetParentNodes(string nodeId, bool includeOtherSources)
        {
            var parentNodes = new List<ParentNodeInfo>();

            if (_connectionProvider == null)
            {
                // 如果没有连接提供者，返回空列表
                return parentNodes;
            }

            // 获取父节点ID列表
            var upstream = _connectionProvider.GetParentNodeIds(nodeId).Distinct().ToArray();
            var allowed = BindingCandidateNodeIds?.Invoke(nodeId);
            var existing = ExistingBindingSourceNodeIds?.Invoke(nodeId) ?? Array.Empty<string>();
            var hasExistingOutside = existing.Any(id => !upstream.Contains(id));
            var parentNodeIds = (includeOtherSources && allowed != null
                ? upstream.Concat(allowed).Distinct()
                : upstream.Concat(existing).Distinct()).Where(id => id != nodeId && (allowed == null || allowed.Contains(id)));

            int order = 0;

            foreach (var parentNodeId in parentNodeIds)
            {
                // 直接创建父节点信息
                var nodeInfo = new ParentNodeInfo
                {
                    CandidateSection = includeOtherSources || hasExistingOutside
                        ? (upstream.Contains(parentNodeId) ? "上游节点" : includeOtherSources ? "其他可绑定节点" : "当前绑定来源（无连线）") : null,
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
        public List<ParentNodeInfo> GetChildNodes(string nodeId)
        {
            var childNodes = new List<ParentNodeInfo>();

            if (_connectionProvider == null)
            {
                // 如果没有连接提供者，返回空列表
                return childNodes;
            }

            // 获取直接下游节点ID列表（画布连接驱动：Connection.SourceNodeId == 当前节点）
            var childNodeIds = _connectionProvider.GetChildNodeIds(nodeId);

            int order = 0;

            foreach (var childNodeId in childNodeIds)
            {
                var nodeInfo = new ParentNodeInfo
                {
                    NodeId = childNodeId,
                    NodeName = _nodeInfoProvider?.GetNodeName(childNodeId) ?? childNodeId,
                    NodeType = _nodeInfoProvider?.GetNodeType(childNodeId) ?? "Unknown",
                    NodeIcon = _nodeInfoProvider?.GetNodeIcon(childNodeId),
                    ConnectionOrder = order++
                };

                childNodes.Add(nodeInfo);
            }

            return childNodes;
        }

        /// <inheritdoc/>
        public List<AvailableDataSource> GetAvailableDataSources(string nodeId, Type? targetType = null)
            => GetAvailableDataSources(nodeId, targetType, false);

        public List<AvailableDataSource> GetAvailableDataSources(string nodeId, Type? targetType, bool includeOtherSources)
        {
            // 获取父节点
            var parentNodes = GetParentNodes(nodeId, includeOtherSources);
            var allDataSources = new List<AvailableDataSource>();

            foreach (var parent in parentNodes)
            {
                // 获取父节点的输出属性
                var outputProps = parent.OutputProperties;
                
                // 类型过滤：统一通过 Mapper 词汇匹配（VariableType 字符串），
                // 取代之前基于 CLR PropertyType 的双向数组/元素兼容逻辑。
                // 类型表中只有数组类型（rule-008 零兼容），单值 T 通过
                // SdkTypeVocabulary.ToTypeVocabulary 自动提升为 T[]。
                if (targetType != null)
                {
                    var targetVarType = SdkTypeVocabulary.ToTypeVocabulary(targetType);
                    if (targetVarType != null)
                    {
                        outputProps = outputProps
                            .Where(p => p.VariableType == targetVarType)
                            .ToList();
                    }
                }

                // 添加到结果列表
                foreach (var property in outputProps)
                {
                    var dataSource = new AvailableDataSource
                    {
                        CandidateSection = parent.CandidateSection,
                        SourceNodeId = parent.NodeId,
                        SourceWorkflowId = WorkflowId, SourceFieldId = property.SourceFieldId,
                        SourceNodeName = parent.NodeName,
                        SourceNodeType = parent.NodeType,
                        PropertyName = property.PropertyName,
                        DisplayName = property.DisplayName,
                        PropertyType = property.PropertyType,
                        VariableType = property.VariableType,
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
                    var extraVarType = SdkTypeVocabulary.ToTypeVocabulary(targetType);
                    if (extraVarType == null || extra.VariableType != extraVarType)
                        continue;
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

            // 图像类型识别统一按 AvailableDataSource.VariableType == "Image"（#18），
            // 取代之前基于 CLR PropertyType 的 IsImageType 和内嵌的 SdkTypeVocabulary 调用。
            bool IsImageSource(AvailableDataSource ds) =>
                ds.VariableType == "Image";

            // 步骤 1: 当前节点自身的图像输出 (Distance=0)
            var nodeType = _nodeInfoProvider?.GetNodeType(currentNodeId);
            var nodeName = _nodeInfoProvider?.GetNodeName(currentNodeId) ?? currentNodeId;
            if (nodeType != null)
            {
                var metadataList = _metadataProvider?.GetAllPropertyMetadata(nodeType);
                if (metadataList != null)
                {
                    // 临时构造 AvailableDataSource 用 VariableType 过滤
                    foreach (var meta in metadataList)
                    {
                        var vt = meta.PropertyType != null
                            ? SdkTypeVocabulary.ToTypeVocabulary(meta.PropertyType)
                            : null;
                        if (vt != "Image")
                            continue;
                        result.Add(new AvailableDataSource
                        {
                            SourceNodeId = currentNodeId,
                            SourceNodeName = nodeName,
                            PropertyName = meta.PropertyName,
                            DisplayName = meta.DisplayName,
                            PropertyType = meta.PropertyType,
                            VariableType = vt,
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

                            // 获取该节点的图像输出属性（GetNodeOutputProperties 已填充 VariableType）
                            foreach (var prop in GetNodeOutputProperties(parentId)
                                .Where(IsImageSource))
                            {
                                prop.Distance = childDistance;
                                result.Add(prop);
                            }

                            queue.Enqueue((parentId, childDistance));
                        }
                    }
                }
            }

            // 合并额外数据源中的图像类型（仅保留节点输出，排除子程序输入端口）
            foreach (var extra in GetExtraDataSources().Where(ds =>
                ds.VariableType == "Image"
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

            if (FormalOutputContractReader != null)
            {
                foreach (var output in FormalOutputContractReader(parentNodeId) ?? Array.Empty<OutputVariable>())
                {
                    if (output.Kind != OutputVariableKind.Data) continue;
                    var declared = output.DeclaredType ?? SdkTypeVocabulary.ToSystemType(output.VariableType);
                    var type = output.IsCollection == true && declared?.IsArray == false ? declared.MakeArrayType() : declared;
                    object? value = null;
                    if (context?.Result is { } result) { try { value = OutputVariableResolver.Resolve(result, output.Name); } catch { } }
                    properties.Add(new AvailableDataSource { SourceNodeId = parentNodeId, SourceWorkflowId = WorkflowId, SourceFieldId = output.FieldId,
                        SourceNodeName = context?.NodeName ?? _nodeInfoProvider?.GetNodeName(parentNodeId) ?? "上游节点", PropertyName = output.Name,
                        DisplayName = string.IsNullOrEmpty(output.DisplayName) ? output.Name : output.DisplayName,
                        PropertyType = type, VariableType = output.VariableType, CurrentValue = value, GroupName = output.Group });
                }
                return properties;
            }

            if (context == null || context.ResultType == null)
            {
                return properties;
            }

            // 动态输出契约优先：节点参数实现 IOutputContractProvider（如子程序）时，
            // 输出属性由 GetOutputContract() 提供（含用户动态配置的 Outputs），走正常通道受连线约束
            if (context.Parameters is IOutputContractProvider contractProvider)
            {
                foreach (var output in contractProvider.GetOutputContract())
                {
                    var propertyType = SdkTypeVocabulary.ToSystemType(output.VariableType);
                    var variableType = string.IsNullOrEmpty(output.VariableType)
                        ? (propertyType != null ? SdkTypeVocabulary.ToTypeVocabulary(propertyType) : null)
                        : output.VariableType;

                    var dataSource = new AvailableDataSource
                    {
                        SourceNodeId = parentNodeId,
                        SourceNodeName = context.NodeName,
                        SourceNodeType = context.NodeType,
                        PropertyName = output.Name,
                        DisplayName = string.IsNullOrEmpty(output.DisplayName) ? output.Name : output.DisplayName,
                        PropertyType = propertyType,
                        VariableType = variableType,
                        CurrentValue = output.RuntimeValue,
                        GroupName = output.Group,
                        Description = output.Kind == OutputVariableKind.ControlSignal ? "流程控制信号" : null,
                    };

                    // 运行期值：从 SSOT 结果解析
                    if (context.Result != null && output.Name != null)
                    {
                        try
                        {
                            dataSource.CurrentValue = OutputVariableResolver.Resolve(context.Result, output.Name);
                        }
                        catch
                        {
                            // 契约未命中不抛（设计期预期行为）
                        }
                    }

                    properties.Add(dataSource);
                }

                return properties;
            }

            // 从变量池获取属性元数据（不再反射）
            var metadataList = _metadataProvider?.GetAllPropertyMetadata(context.NodeType);

            if (metadataList == null)
            {
                return properties;
            }

            foreach (var metadata in metadataList)
            {
                // VariableType 从 PropertyType 派生（与 OutputVariable 契约同源）
                var variableType = metadata.PropertyType != null
                    ? SdkTypeVocabulary.ToTypeVocabulary(metadata.PropertyType)
                    : null;

                // 运行期值：优先从输出变量契约取（支持动态工具），反射兜底已删除
                object? currentValue = null;
                if (context.Result != null && metadata.PropertyName != null)
                {
                    try
                    {
                        currentValue = OutputVariableResolver.Resolve(context.Result, metadata.PropertyName);
                    }
                    catch
                    {
                        // 契约未找到时不抛（设计期预期行为），currentValue 保留 null
                    }
                }

                var dataSource = new AvailableDataSource
                {
                    SourceNodeId = parentNodeId,
                    SourceNodeName = context.NodeName,
                    SourceNodeType = context.NodeType,
                    PropertyName = metadata.PropertyName,
                    DisplayName = metadata.DisplayName,
                    PropertyType = metadata.PropertyType,
                    VariableType = variableType,
                    CurrentValue = currentValue,
                    Unit = null,
                    Description = metadata.Description,
                    GroupName = metadata.GroupName,
                };

                // 数组属性暴露 [i] 子数据源（决策1：支持 T[i] 绑定路径直接取单元素）
                if (metadata.PropertyType?.IsArray == true)
                {
                    var elementType = metadata.PropertyType.GetElementType();
                    var elementVarType = elementType != null
                        ? SdkTypeVocabulary.ToTypeVocabulary(elementType)
                        : null;
                    object? elementValue = null;
                    if (currentValue is Array arr && arr.Length > 0)
                        elementValue = arr.GetValue(0);

                    dataSource.Children = new List<AvailableDataSource>
                    {
                        new AvailableDataSource
                        {
                            SourceNodeId = parentNodeId,
                            SourceNodeName = context.NodeName,
                            SourceNodeType = context.NodeType,
                            PropertyName = metadata.PropertyName + "[i]",
                            DisplayName = (metadata.DisplayName ?? metadata.PropertyName) + "[i]",
                            PropertyType = elementType,
                            VariableType = elementVarType,
                            CurrentValue = elementValue,
                            Description = metadata.Description,
                            GroupName = metadata.GroupName,
                        }
                    };
                }

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
                ResultType = _nodeInfoProvider.GetResultType(nodeId),
                Parameters = _nodeInfoProvider.GetParameters(nodeId)
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
            // 从 SSOT 读取器获取节点结果并委托 OutputVariableResolver 取值（无反射、无 GetResultItems 兜底）
            var result = _ssotReader?.Invoke(nodeId);
            if (result != null)
            {
                try
                {
                    return OutputVariableResolver.Resolve(result, propertyName, nodeId);
                }
                catch
                {
                    return null;
                }
            }

            return null;
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

        /// <summary>
        /// 获取节点参数对象（用于动态输出契约 IOutputContractProvider 查询）
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>节点参数，无法获取则返回 null</returns>
        ToolParameters? GetParameters(string nodeId);
    }
}
