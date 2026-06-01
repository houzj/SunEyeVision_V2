using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using NodeModel = SunEyeVision.UI.Models.WorkflowNode;
using WorkflowModel = SunEyeVision.UI.Models;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 工作流节点工厂实现
    /// </summary>
    public class WorkflowNodeFactory : IWorkflowNodeFactory
    {
        private readonly INodeSequenceManager _sequenceManager;

        public WorkflowNodeFactory(INodeSequenceManager sequenceManager)
        {
            _sequenceManager = sequenceManager ?? throw new ArgumentNullException(nameof(sequenceManager));
        }

        public NodeModel CreateNode(string algorithmType, string? name = null, string? workflowId = null)
        {
            if (string.IsNullOrWhiteSpace(algorithmType))
            {
                throw new ArgumentException("Algorithm type cannot be null or empty.", nameof(algorithmType));
            }

            string workflowIdSafe = workflowId ?? Guid.NewGuid().ToString();

            // 获取全局索引（跨所有工作流共享）
            int globalIndex = _sequenceManager.GetNextGlobalIndex();
            // 获取局部序号（优先从空洞池获取）
            int localIndex = _sequenceManager.GetNextLocalIndex(workflowIdSafe, algorithmType);

            return CreateIndexedNodeInternal(algorithmType, globalIndex, localIndex, name, workflowIdSafe);
        }

        public NodeModel CreateIndexedNode(string algorithmType, int localIndex, string? name = null, string? workflowId = null)
        {
            if (string.IsNullOrWhiteSpace(algorithmType))
            {
                throw new ArgumentException("Algorithm type cannot be null or empty.", nameof(algorithmType));
            }

            string workflowIdSafe = workflowId ?? Guid.NewGuid().ToString();

            // 获取全局索引（跨所有工作流共享）
            int globalIndex = _sequenceManager.GetNextGlobalIndex();

            return CreateIndexedNodeInternal(algorithmType, globalIndex, localIndex, name, workflowIdSafe);
        }

        /// <summary>
        /// 创建节点的内部实现
        /// </summary>
        private NodeModel CreateIndexedNodeInternal(string algorithmType, int globalIndex, int localIndex, string? name, string workflowId)
        {
            if (string.IsNullOrWhiteSpace(algorithmType))
            {
                throw new ArgumentException("Algorithm type cannot be null or empty.", nameof(algorithmType));
            }

            // 获取工具元数据（控制流节点从 ControlFlowRegistry 获取，算法节点从 ToolRegistry 获取）
            string displayName;
            var flowInfo = ControlFlowRegistry.Get(algorithmType);
            if (flowInfo != null)
            {
                displayName = flowInfo.DisplayName;
            }
            else
            {
                var metadata = ToolRegistry.GetToolMetadata(algorithmType);
                displayName = metadata?.DisplayName ?? algorithmType;
            }

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
            InitializeNodeParameters(node, algorithmType);

            return node;
        }

        /// <summary>
        /// 初始化节点参数（基于 AlgorithmType 创建默认参数）
        /// </summary>
        private void InitializeNodeParameters(NodeModel node, string algorithmType)
        {
            // 控制流节点（子程序、条件判断等）没有对应的 IToolPlugin，跳过参数创建
            if (ControlFlowRegistry.IsControlFlow(algorithmType) || !ToolRegistry.ToolExists(algorithmType))
            {
                return;
            }

            // 从 ToolRegistry 创建默认参数（直接返回 ToolParameters）
            var defaultParams = ToolRegistry.CreateParameters(algorithmType);

            // 直接使用 ToolParameters，不进行转换
            node.Parameters = defaultParams;
        }
    }
}
