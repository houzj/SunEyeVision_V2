using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Canvas;

namespace SunEyeVision.UI.Services.Interaction
{
    /// <summary>
    /// 节点序号管理器接口，用于管理节点的全局序号和局部序号
    /// </summary>
    public interface INodeSequenceManager
    {
        /// <summary>
        /// 获取下一个全局序号
        /// </summary>
        /// <returns>全局序号</returns>
        int GetNextGlobalIndex();

        /// <summary>
        /// 获取指定工作流和工具类型的下一个局部序号（自动填补空洞）
        /// </summary>
        /// <param name="workflowId">工作流ID</param>
        /// <param name="toolType">工具类型</param>
        /// <returns>局部序号</returns>
        int GetNextLocalIndex(string workflowId, string toolType);

        /// <summary>
        /// 释放局部索引到空洞池
        /// </summary>
        /// <param name="workflowId">工作流ID</param>
        /// <param name="toolType">工具类型</param>
        /// <param name="localIndex">要释放的局部索引</param>
        void ReleaseLocalIndex(string workflowId, string toolType, int localIndex);

        /// <summary>
        /// 释放全局索引到空洞池
        /// </summary>
        /// <param name="globalIndex">要释放的全局索引</param>
        void ReleaseGlobalIndex(int globalIndex);

        /// <summary>
        /// 生成节点名称
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="localIndex">局部序号</param>
        /// <returns>节点名称，格式：{DisplayName}{LocalIndex}</returns>
        string GenerateNodeName(string displayName, int localIndex);

        /// <summary>
        /// 重置所有序号
        /// </summary>
        void Reset();

        /// <summary>
        /// 重置指定工作流的序号
        /// </summary>
        /// <param name="workflowId">工作流ID</param>
        void ResetWorkflow(string workflowId);

        /// <summary>
        /// 从现有节点初始化空洞池（加载解决方案时使用）
        /// </summary>
        /// <param name="workflowId">工作流ID</param>
        /// <param name="nodes">现有节点列表</param>
        void InitializeHolePoolsFromNodes(string workflowId, IEnumerable<WorkflowNode> nodes);
    }
}
