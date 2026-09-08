namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数订阅取值上下文 - 订阅解析的统一取值通道。
    /// 按节点输出变量契约寻址（<see cref="OutputVariableResolver"/>），供 Execution 域订阅链路
    /// （<see cref="NodeResultsParameterContext"/>）与各消费方统一取值，不依赖 UI 层。
    /// </summary>
    public interface IParameterContext
    {
        /// <summary>
        /// 获取节点输出值
        /// </summary>
        /// <param name="nodeId">源节点 ID</param>
        /// <param name="outputName">输出变量契约名</param>
        /// <param name="index">数组下标（可空）</param>
        /// <param name="propertyPath">完整契约路径（可含 [i]/[Type=X]/嵌套字段）；非空时优先</param>
        object? GetNodeOutputValue(string nodeId, string outputName, int? index = null, string? propertyPath = null);
    }
}
