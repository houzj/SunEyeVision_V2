namespace SunEyeVision.Core.Models
{
    /// <summary>
    /// 统一的工作流节点类型
    /// </summary>
    public enum NodeType
    {
        // ==================== 数据处理类型 ====================

        /// <summary>
        /// 算法节点 - 图像处理节点（默认类型）
        /// </summary>
        Algorithm,

        // ==================== 逻辑控制类型 ====================

        /// <summary>
        /// 子程序节点 - 可复用的子工作流。
        /// </summary>
        Subroutine,

        /// <summary>
        /// 条件节点 - 条件分支控制
        /// </summary>
        Condition,

        /// <summary>
        /// 分支节点 - 多路分支
        /// </summary>
        Switch
    }
}
