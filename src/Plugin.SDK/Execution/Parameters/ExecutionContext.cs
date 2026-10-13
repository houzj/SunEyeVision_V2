using System.Collections.Generic;
using System.Threading;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 工具执行上下文 - 提供执行期间的运行时服务
    /// </summary>
    /// <remarks>
    /// 在工具执行期间注入的上下文对象，提供日志、取消令牌等服务。
    /// 
    /// 设计原则：
    /// 1. 可选依赖 - 工具可以选择性使用
    /// 2. 向后兼容 - 现有工具无需修改
    /// 3. 轻量级 - 只包含必要的服务
    /// 
    /// 使用示例：
    /// <code>
    /// public class MyTool : IToolPlugin&lt;MyParams, MyResult&gt;
    /// {
    ///     public MyResult Run(Mat image, MyParams parameters)
    ///     {
    ///         // 通过扩展方法记录日志
    ///         parameters.LogInfo("开始执行工具");
    ///         
    ///         // 检查取消
    ///         if (parameters.IsCancellationRequested())
    ///             return MyResult.Cancelled();
    ///         
    ///         // 直接访问上下文
    ///         var logger = parameters.Context?.Logger;
    ///         logger?.Info("详细日志", parameters.Context?.GetSourceName());
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class ExecutionContext
    {
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationInputs { get; init; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationOutputs { get; init; }
        /// <summary>
        /// 日志记录器（可选）
        /// </summary>
        /// <remarks>
        /// 工具可通过此属性记录执行过程中的日志信息。
        /// 如果为null，表示日志功能未启用。
        /// </remarks>
        public ILogger? Logger { get; init; }

        /// <summary>
        /// 工具名称（用于日志来源）
        /// </summary>
        public string? ToolName { get; init; }

        /// <summary>
        /// 节点名称（用于日志来源，工作流场景）
        /// </summary>
        /// <remarks>
        /// 在工作流执行时，此属性为节点的显示名称。
        /// 单独执行工具时，此属性通常与ToolName相同或为null。
        /// </remarks>
        public string? NodeName { get; init; }

        /// <summary>
        /// 节点ID（用于日志来源标识）
        /// </summary>
        public string? NodeId { get; init; }

        /// <summary>
        /// 工作流名称（用于日志来源层级）
        /// </summary>
        /// <remarks>
        /// 在工作流执行时，此属性为工作流的显示名称。
        /// 用于构建层级化的日志来源，如"运行.检测流程.边缘检测"。
        /// </remarks>
        public string? WorkflowName { get; init; }

        /// <summary>
        /// 日志分类（一级）
        /// </summary>
        /// <remarks>
        /// 默认为"运行"，可选值：系统/运行/设备/UI
        /// </remarks>
        public string Category { get; init; } = "运行";

        /// <summary>
        /// 日志子分类（二级）
        /// </summary>
        /// <remarks>
        /// 通常为工作流名称，如"检测流程"、"标定流程"等
        /// </remarks>
        public string? SubCategory { get; init; }

        /// <summary>
        /// 追踪ID（用于关联同一执行链的日志）
        /// </summary>
        /// <remarks>
        /// 格式示例：wf_001_node_003（工作流ID_节点ID）
        /// </remarks>
        public string? TraceId { get; init; }

        /// <summary>
        /// 取消令牌（可选）
        /// </summary>
        /// <remarks>
        /// 用于支持长时间运行工具的取消操作。
        /// 工具应定期检查此令牌以响应取消请求。
        /// </remarks>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>
        /// 全局变量字典（用于 __GlobalVars__.xxx 绑定解析）
        /// </summary>
        /// <remarks>
        /// 包含解决方案级全局变量值，键为变量名，值为运行时值。
        /// 工具可通过此字典解析 "__GlobalVars__.变量名" 格式的绑定源。
        /// 如果为 null，表示当前上下文未注入全局变量。
        /// </remarks>
        public IDictionary<string, object?>? GlobalVariables { get; init; }

        /// <summary>
        /// 创建执行上下文
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="toolName">工具名称</param>
        /// <param name="nodeName">节点名称</param>
        /// <param name="nodeId">节点ID</param>
        /// <param name="workflowName">工作流名称</param>
        /// <param name="category">日志分类</param>
        /// <param name="subCategory">日志子分类</param>
        /// <param name="traceId">追踪ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="globalVariables">全局变量字典</param>
        public ExecutionContext(
            ILogger? logger = null,
            string? toolName = null,
            string? nodeName = null,
            string? nodeId = null,
            string? workflowName = null,
            string category = "运行",
            string? subCategory = null,
            string? traceId = null,
            CancellationToken cancellationToken = default,
            IDictionary<string, object?>? globalVariables = null,
            SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? communicationInputs = null,
            SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? communicationOutputs = null)
        {
            Logger = logger;
            ToolName = toolName;
            NodeName = nodeName;
            NodeId = nodeId;
            WorkflowName = workflowName;
            Category = category;
            SubCategory = subCategory;
            TraceId = traceId;
            CancellationToken = cancellationToken;
            GlobalVariables = globalVariables;
            CommunicationInputs = communicationInputs;
            CommunicationOutputs = communicationOutputs;
        }

        /// <summary>
        /// 空上下文（用于无日志场景）
        /// </summary>
        public static ExecutionContext Empty { get; } = new();

        /// <summary>
        /// 获取层级化的日志来源名称
        /// </summary>
        /// <returns>格式：分类.子分类.节点名 或 分类.节点名 或 节点名</returns>
        /// <remarks>
        /// 优先级：
        /// 1. 如果有分类和子分类，返回"分类.子分类.节点名"
        /// 2. 如果有分类和工作流名（作为子分类），返回"分类.工作流名.节点名"
        /// 3. 如果只有分类，返回"分类.节点名"
        /// 4. 兼容旧逻辑：返回节点名或工具名
        /// </remarks>
        public string GetSourceName()
        {
            // 构建层级来源
            var subCat = SubCategory ?? WorkflowName;
            
            if (!string.IsNullOrEmpty(Category) && !string.IsNullOrEmpty(subCat))
            {
                if (!string.IsNullOrEmpty(NodeName))
                    return $"{Category}.{subCat}.{NodeName}";
                return $"{Category}.{subCat}";
            }
            
            if (!string.IsNullOrEmpty(Category))
            {
                if (!string.IsNullOrEmpty(NodeName))
                    return $"{Category}.{NodeName}";
                return Category;
            }
            
            // 兼容旧逻辑
            return NodeName ?? ToolName ?? "Tool";
        }

        /// <summary>
        /// 检查是否请求取消
        /// </summary>
        public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;

        /// <summary>
        /// 如果请求取消则抛出异常
        /// </summary>
        public void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();
    }
}
