using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Contracts;
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
        public IResourcePathResolver? ResourcePaths { get; set; }

        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationReceiveService { get; set; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationSendService { get; set; }
        public Action<string>? CommitOutputConfiguration { get; set; }
        public string? WorkflowId { get; set; }
        public SunEyeVision.Plugin.Runtime.ToolExecutionOwner? ExecutionOwner { get; set; }
        /// <summary>Optional private editor lifetime; production queries leave this unset.</summary>
        public System.Threading.CancellationToken EditScopeCancellation { get; set; }
        /// <summary>Editor run boundary captures published values, never another scope's pending variables.</summary>
        public Func<IDictionary<string, object?>?>? CapturePublishedGlobals { get; set; }
        /// <summary>Host's configuration snapshot factory, shared with workflow execution.</summary>
        public Func<ToolParameters, ToolParameters>? CreateParameterSnapshot { get; set; }
        public Func<string, IReadOnlyList<string>>? BindingCandidateNodeIds { get; set; }
        public Func<string, IReadOnlyList<string>>? ExistingBindingSourceNodeIds { get; set; }
        public ToolSchema? GetSchema(string nodeId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
            var provider = _nodeInfoProvider ?? throw new InvalidOperationException("来源查询缺少当前作用域的节点声明提供者。");
            if (!provider.NodeExists(nodeId)) return null;
            return provider.GetSchema(nodeId) ?? throw new InvalidOperationException("节点存在但缺少已准入声明：" + nodeId);
        }
        public Func<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource?>? SelectReceiveVariable { get; set; }
        /// <summary>
        /// SSOT 读取器（委托到 WorkflowContext，实时读取持久结果，不维护本地缓存）
        /// </summary>
        private Func<string, DeclaredOutputSnapshot?>? _ssotReader;

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
                            if (!result.Contains(ds))
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
        /// 设置 SSOT 读取器，使 DSQS 能回退到外部持久化结果
        /// </summary>
        /// <param name="reader">给定节点ID返回 DeclaredOutputSnapshot 的委托</param>
        public void SetSootReader(Func<string, DeclaredOutputSnapshot?> reader)
        {
            _ssotReader = reader;
        }

        /// <inheritdoc/>
        public List<ParentNodeInfo> GetParentNodes(string nodeId)
            => GetParentNodes(nodeId, false);

        /// <summary>Frozen candidate node names only; never observes a result, output value or execution resource.</summary>
        public IReadOnlyDictionary<string,string> GetBindingCandidateNodeNames(string targetNodeId)
        {
            IEnumerable<string> ids=BindingCandidateNodeIds?.Invoke(targetNodeId)
                ??(IReadOnlyList<string>?)_connectionProvider?.GetParentNodeIds(targetNodeId)
                ??Array.Empty<string>();
            var names=ids.Where(id=>id!=targetNodeId).Distinct(StringComparer.Ordinal).ToDictionary(id=>id,id=>
            {
                var name=_nodeInfoProvider?.GetNodeName(id);
                return string.IsNullOrWhiteSpace(name)?id:name;
            },StringComparer.Ordinal);
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(names);
        }

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
            // The owning workflow graph validates binding dependencies, including sources without a canvas edge.
            var candidates = includeOtherSources && allowed != null ? upstream.Concat(allowed) : upstream;
            var parentNodeIds = candidates.Where(id => id != nodeId && (allowed == null || allowed.Contains(id))).Distinct();

            int order = 0;

            foreach (var parentNodeId in parentNodeIds)
            {
                // 直接创建父节点信息
                var nodeInfo = new ParentNodeInfo
                {
                    CandidateSection = upstream.Contains(parentNodeId) ? "上游节点" : "其他可绑定节点",
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
                
                if (targetType != null)
                {
                    if (!BindingClrAdapter.TryDescribe(targetType, out var target, out var storage)) outputProps = new();
                    else outputProps = outputProps.Where(property => IsCompatible(property, target, storage)).ToList();
                }
                foreach (var property in outputProps)
                    allDataSources.Add(property.WithCandidateSection(parent.CandidateSection));
            }

            // ★ 合并额外数据源（子程序输入端口等，支持实时更新）
            foreach (var extra in GetExtraDataSources())
            {
                if (targetType != null)
                {
                    if (!BindingClrAdapter.TryDescribe(targetType, out var target, out var storage) || !IsCompatible(extra, target, storage)) continue;
                }
                if (!allDataSources.Contains(extra))
                    allDataSources.Add(extra);
            }

            return allDataSources;
        }

        private static bool IsCompatible(AvailableDataSource source, TypeRef target, NumericStorage? targetStorage)
        {
            TypeRef actual; NumericStorage? storage;
            switch(source.Binding)
            {
                case ReferenceSetting reference:
                    actual=ParameterReferenceRules.SelectedType(reference.Reference);storage=reference.Reference.Storage;break;
                case VariableSetting variable:
                    actual=variable.Reference.Type;storage=variable.Reference.Storage;
                    if(variable.Reference.Selection is not BindingSelection.AllItems && actual.Shape==ValueShape.Sequence)actual=actual.Element!;
                    break;
                default: throw new InvalidOperationException("来源缺少中立身份。");
            }
            var state=new ParameterBindingService().CompareValueSource(new Port("source",actual,Storage:storage),new Port("target",target,Storage:targetStorage),BindingConversion.Automatic,out _);
            return state is BindingCompatibility.Compatible or BindingCompatibility.Conditional;
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
            bool IsImageSource(AvailableDataSource ds) => ds.Binding is ReferenceSetting reference &&
                ParameterReferenceRules.SelectedType(reference.Reference) == TypeRef.ResourceOf(ResourceKind.Image);

            // 步骤 1: 当前节点自身的图像输出 (Distance=0)
            foreach(var source in GetNodeOutputProperties(currentNodeId).Where(IsImageSource))
            {source.Distance=0;result.Add(source);}
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
            foreach (var extra in GetExtraDataSources().Where(IsImageSource))
            {
                if (!result.Contains(extra))
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
            if(string.IsNullOrWhiteSpace(WorkflowId))throw new InvalidOperationException("来源查询缺少固定流程作用域。");
            var schema=GetSchema(parentNodeId);
            if(schema is null)return new List<AvailableDataSource>();
            var context=GetNodeContext(parentNodeId);
            var properties=new List<AvailableDataSource>();
            foreach(var port in schema.Outputs)
            {
                var reference=new OutputReference(parentNodeId,port.Id,port.Type,scopeId:WorkflowId,storage:port.Storage);
                var declared=port.Type;
                var element=declared.Shape==ValueShape.Sequence?declared.Element!:declared;
                Type clr=element.Shape switch {
                    ValueShape.Scalar when element.Scalar==ScalarKind.Boolean=>typeof(bool),
                    ValueShape.Scalar when element.Scalar==ScalarKind.Int64=>typeof(long),
                    ValueShape.Scalar when element.Scalar==ScalarKind.Float64=>typeof(double),
                    ValueShape.Scalar when element.Scalar==ScalarKind.Text=>typeof(string),
                    ValueShape.Resource when element.Resource==ResourceKind.Image=>typeof(IReadOnlyImage),
                    ValueShape.Resource when element.Resource==ResourceKind.Region=>typeof(IReadOnlyRegion),
                    _=>typeof(object)
                };
                if(declared.Shape==ValueShape.Sequence)clr=clr.MakeArrayType();
                object? value=null;
                if(declared.Shape==ValueShape.Scalar && context?.Result is { } result)
                {
                    try
                    {
                        var output=result.Schema.Outputs.SingleOrDefault(item=>item.Id==port.Id);
                        if(result.Identity.NodeId==parentNodeId && result.SourceWorkflowId==WorkflowId && output is not null && output.Type==port.Type && output.Storage==port.Storage)
                        {
                            using var reader=result.AcquireReader();
                            if(reader.Values is { } values && values.Contains(port.Id))
                            {
                                var candidate=values.Get<object?>(port.Id);
                                if(candidate is bool or string or long or int or double or float or decimal)value=candidate;
                            }
                        }
                    }
                    catch(InvalidOperationException) { }
                }
                properties.Add(new AvailableDataSource { Binding=new ReferenceSetting(reference),
                    SourceNodeName=context?.NodeName??parentNodeId,SourceNodeType=context?.NodeType,
                    DisplayName=schema.FindOutputDisplay(port.Id)?.DisplayName??port.Id,
                    PropertyType=clr,VariableType=declared.Shape==ValueShape.Resource&&declared.Resource==ResourceKind.Image?"Image":SdkTypeVocabulary.ToTypeVocabulary(clr),
                    CurrentValue=value,HasExecuted=context?.HasResult==true,IsDesignTime=context?.HasResult!=true });
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
            if (string.IsNullOrWhiteSpace(WorkflowId)) throw new InvalidOperationException("来源查询缺少固定流程作用域。");
            var schema=GetSchema(nodeId);
            if(schema is null)return null;
            var result=_ssotReader?.Invoke(nodeId);
            if(result is not null && !IsCurrentResult(result,nodeId,schema))result=null;

            return new NodeExecutionContext
            {
                NodeId = nodeId,
                NodeName = _nodeInfoProvider.GetNodeName(nodeId) ?? nodeId,
                NodeType = _nodeInfoProvider.GetNodeType(nodeId) ?? "Unknown",
                NodeIcon = _nodeInfoProvider.GetNodeIcon(nodeId),
                Result = result,
                Schema = schema
            };
        }

        private bool IsCurrentResult(DeclaredOutputSnapshot result,string nodeId,ToolSchema schema)
        {
            if(result.Status==ExecutionStatus.NotExecuted || result.Identity.NodeId!=nodeId || result.SourceWorkflowId!=WorkflowId ||
               result.ExecutionIdentity is not { IsDomainCurrent:true } identity ||
               new ToolSchema(Array.Empty<Port>(),schema.Outputs).Fingerprint!=result.Schema.Fingerprint)return false;
            try
            {
                using var reader=result.AcquireReader();
                if(result.IsSuccess && reader.Values is null)return false;
                return identity.IsDomainCurrent;
            }
            catch(ObjectDisposedException){return false;}
            catch(InvalidOperationException){return false;}
        }

        /// <summary>
        /// 从 SSOT 读取器获取节点执行结果（无本地缓存，实时读取 WorkflowContext）
        /// </summary>
        private DeclaredOutputSnapshot? GetNodeResult(string nodeId)
        {
            return _ssotReader?.Invoke(nodeId);
        }

        /// <inheritdoc/>
        public bool HasNodeExecuted(string nodeId)
        {
            return GetNodeContext(nodeId)?.HasResult == true;
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

        ToolSchema? GetSchema(string nodeId);
    }
}
