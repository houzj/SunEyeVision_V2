using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using NodeModel = SunEyeVision.UI.Shared.WorkflowNode;
using WorkflowModel = SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 工作流节点工厂实现
    /// </summary>
    public class WorkflowNodeFactory : IWorkflowNodeFactory
    {
        private readonly INodeSequenceManager _sequenceManager;
        private readonly SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot _directory;

        public WorkflowNodeFactory(INodeSequenceManager sequenceManager,SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot directory)
        {
            _directory=directory??throw new ArgumentNullException(nameof(directory));
            _sequenceManager = sequenceManager ?? throw new ArgumentNullException(nameof(sequenceManager));
        }

        public NodeModel CreateNode(string algorithmType, string? name = null, string? workflowId = null)
        {
            if (string.IsNullOrWhiteSpace(algorithmType))
            {
                throw new ArgumentException("Algorithm type cannot be null or empty.", nameof(algorithmType));
            }

            // 先完成组件/参数创建，再申请显示序号；失败不消耗编辑身份。
            var prepared = PrepareConfiguration(algorithmType);
            string workflowIdSafe = workflowId ?? Guid.NewGuid().ToString();

            // 获取全局索引（跨所有工作流共享）
            int globalIndex = _sequenceManager.GetNextGlobalIndex();
            // 获取局部序号（优先从空洞池获取）
            int localIndex = _sequenceManager.GetNextLocalIndex(workflowIdSafe, algorithmType);

            return CreateIndexedNodeInternal(algorithmType, globalIndex, localIndex, name, workflowIdSafe, prepared.Parameters, prepared.Runtime);
        }

        public NodeModel CreateIndexedNode(string algorithmType, int localIndex, string? name = null, string? workflowId = null)
        {
            if (string.IsNullOrWhiteSpace(algorithmType))
            {
                throw new ArgumentException("Algorithm type cannot be null or empty.", nameof(algorithmType));
            }

            // 先完成组件/参数创建，再申请显示序号；失败不消耗编辑身份。
            var prepared = PrepareConfiguration(algorithmType);
            string workflowIdSafe = workflowId ?? Guid.NewGuid().ToString();

            // 获取全局索引（跨所有工作流共享）
            int globalIndex = _sequenceManager.GetNextGlobalIndex();

            return CreateIndexedNodeInternal(algorithmType, globalIndex, localIndex, name, workflowIdSafe, prepared.Parameters, prepared.Runtime);
        }

        /// <summary>
        /// 创建节点的内部实现
        /// </summary>
        private NodeModel CreateIndexedNodeInternal(string algorithmType, int globalIndex, int localIndex, string? name, string workflowId, ToolParameters? preparedParameters, SunEyeVision.Workflow.RuntimeToolConfiguration? preparedRuntime)
        {
            if (string.IsNullOrWhiteSpace(algorithmType))
            {
                throw new ArgumentException("Algorithm type cannot be null or empty.", nameof(algorithmType));
            }

            // 获取工具元数据（统一从 ToolRegistry 获取，Condition 已迁移为 ConditionTool）
            var metadata = _directory.GetCatalog(algorithmType);
            string displayName = metadata?.DisplayName ?? algorithmType;

            // 使用Guid生成节点ID
            string nodeId = Guid.NewGuid().ToString();

            // 生成 DispName（用于UI显示）
            string dispName = $"{displayName}{localIndex}";

            // 生成 Name（用于序列化）- 始终使用规范格式：{GlobalIndex} {DisplayName}{LocalIndex}
            // 示例：1 图像采集1, 2 高斯模糊1
            string nodeName = $"{globalIndex} {displayName}{localIndex}";

            var node = new WorkflowModel.WorkflowNode(
                nodeId,
                nodeName,
                dispName,
                algorithmType
            );

            // 设置索引属性
            node.GlobalIndex = globalIndex;
            node.LocalIndex = localIndex;

            // 初始化节点参数
            if (preparedParameters != null) node.Parameters = preparedParameters;
            node.RuntimeConfiguration = preparedRuntime;

            return node;
        }

        private (ToolParameters? Parameters, SunEyeVision.Workflow.RuntimeToolConfiguration? Runtime) PrepareConfiguration(string toolId)
        {
            if (!_directory.Contains(toolId))throw new InvalidOperationException("工具未准入："+toolId);
            return (null, SunEyeVision.Workflow.RuntimeToolConfiguration.Create(_directory,toolId));
        }
    }
}
