using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 流程控制信号强类型接口。实现此接口的工具结果，
    /// 工作流引擎强类型读取分支信号（NextNodeIds），取代反射 GetProperty("NextNodeId") 字符串魔法。
    /// 控制信号不进数据变量面板 / 绑定候选，实现控制流与数据流分离。
    /// </summary>
    public interface IControlFlowResult
    {
        /// <summary>所有命中的分支目标节点 ID（流程控制信号，不进数据变量契约；未命中时为空集合）</summary>
        IReadOnlyList<string> NextNodeIds { get; }

        /// <summary>
        /// 是否已产生流程控制信号。条件分支总为 true（无论命中/未命中）；
        /// 普通非控制节点为 false。引擎据此区分"未命中终止"与"普通节点无信号"。
        /// </summary>
        bool HasControlSignal { get; }
    }
}
