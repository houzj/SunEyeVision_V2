using SunEyeVision.Core.Models;

namespace SunEyeVision.UI.Features.WorkflowUI
{
    /// <summary>
    /// WorkflowNode 扩展方法
    /// </summary>
    public static class WorkflowNodeExtensions
    {
        /// <summary>
        /// 将UI层的WorkflowNode转换为Workflow层的WorkflowNodeBase
        /// </summary>
        public static SunEyeVision.Workflow.WorkflowNodeBase ToWorkflowNode(this SunEyeVision.UI.Shared.WorkflowNode uiNode)
        {
            // 工具插件创建的节点始终为 AlgorithmNode
            // 控制节点由专门的 UI 操作创建
            var workflowNode = new SunEyeVision.Workflow.WorkflowNodeBase(uiNode.Id, uiNode.Name, uiNode.DispName, "Algorithm");
            workflowNode.ToolType = uiNode.ToolType;
            workflowNode.LocalIndex = uiNode.LocalIndex;
            workflowNode.GlobalIndex = uiNode.GlobalIndex;
            workflowNode.SubroutineId = uiNode.SubroutineId;
            return workflowNode;
        }
    }
}
