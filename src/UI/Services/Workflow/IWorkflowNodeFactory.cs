using NodeModel = SunEyeVision.UI.Shared.WorkflowNode;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 工作流节点工厂接口
    /// </summary>
    public interface IWorkflowNodeFactory
    {
        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="algorithmType">算法类型</param>
        /// <param name="name">节点名称（可选）</param>
        /// <param name="workflowId">工作流ID（可选）</param>
        /// <returns>创建的工作流节点</returns>
        NodeModel CreateNode(string algorithmType, string? name = null, string? workflowId = null);

        /// <summary>
        /// 创建节点（使用预计算的索引）
        /// </summary>
        /// <param name="algorithmType">算法类型</param>
        /// <param name="localIndex">预计算的局部索引</param>
        /// <param name="name">节点名称（可选）</param>
        /// <param name="workflowId">工作流ID（可选）</param>
        /// <returns>创建的工作流节点</returns>
        NodeModel CreateIndexedNode(string algorithmType, int localIndex, string? name = null, string? workflowId = null);
    }
}

