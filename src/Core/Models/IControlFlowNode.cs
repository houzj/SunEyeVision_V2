namespace SunEyeVision.Core.Models
{
    /// <summary>
    /// 控制流节点类型
    /// </summary>
    public enum ControlFlowType
    {
        Subroutine,
        Condition,
        Switch,
        Loop
    }

    /// <summary>
    /// 控制流节点接口 - 表示工作流控制流结构（子程序、条件分支等）
    /// 与 IToolPlugin（算法工具）分离，遵循接口隔离原则 (ISP)
    /// </summary>
    public interface IControlFlowNode
    {
        /// <summary>
        /// 控制流类型
        /// </summary>
        ControlFlowType FlowType { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 图标
        /// </summary>
        string Icon { get; }
    }
}
