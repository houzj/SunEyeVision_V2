using System.Collections.Generic;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Core
{
    /// <summary>
    /// 工作流上下文服务 - 按工作流ID缓存独立执行上下文。
    /// 每个工作流拥有独立的 WorkflowContext，切换时互不干扰。
    /// </summary>
    public interface IWorkflowContextService
    {
        /// <summary>
        /// 获取或创建指定工作流ID的执行上下文（懒加载）
        /// </summary>
        WorkflowContext GetOrCreate(string workflowId, string workflowName);

        /// <summary>
        /// 获取指定工作流ID的执行上下文（不存在返回 null）
        /// </summary>
        WorkflowContext? Get(string workflowId);

        /// <summary>
        /// 移除指定工作流ID的执行上下文
        /// </summary>
        void Remove(string workflowId);

        /// <summary>
        /// 清空所有执行上下文
        /// </summary>
        void Clear();
    }
}
