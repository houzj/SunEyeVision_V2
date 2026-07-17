using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// Parameters 自描述输出契约（用于动态工具的设计时显示）。
/// 静态工具不需要（OutputVariables 已在 ResultType 中固定）；
/// 动态工具（Script/Subroutine）由 Parameters 中用户配置的 Outputs 声明输出结构。
/// </summary>
public interface IOutputContractProvider
{
    /// <summary>返回设计时输出变量列表（从 Parameters 的配置导入）。</summary>
    IReadOnlyList<OutputVariable> GetOutputContract();
}
