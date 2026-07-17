using System;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// Parameters 输出契约变更通知（用于动态工具设计态实时刷新模块结果树）。
/// 工具 Parameters 的输出配置（Outputs）可能在调试窗口打开时被用户修改，
/// 实现此接口可在修改发生后立即通知 UI 重新读取 GetOutputContract() 并刷新显示。
/// </summary>
/// <remarks>
/// 当前实现者: ScriptParameters, SubroutineParameters
/// </remarks>
public interface INotifyOutputContractChanged
{
    /// <summary>输出契约发生变更时触发（新增/删除/重命名/改类型/改分组等）。</summary>
    event Action? OutputContractChanged;
}
