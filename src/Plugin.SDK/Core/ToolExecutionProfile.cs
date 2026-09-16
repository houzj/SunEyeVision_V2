using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.Core;

/// <summary>工具在一次运行中的并发和资源声明。</summary>
/// <remarks>这是运行时契约，不属于可被 UI 元数据缓存替代的静态展示信息。</remarks>
public sealed record ToolExecutionProfile(
    ToolConcurrencyKind ConcurrencyKind,
    IReadOnlyList<string>? ResourceKeys = null,
    int? MaxInstances = null)
{
    /// <summary>旧工具的安全默认值：同一根工作流中不与其他节点并发。</summary>
    public static ToolExecutionProfile LegacyExclusive { get; } =
        new(ToolConcurrencyKind.WorkflowExclusive, Array.Empty<string>(), 1);
}

public enum ToolConcurrencyKind
{
    WorkflowExclusive,
    ResourceExclusive,
    ParallelSafe
}

/// <summary>
/// 工具可选实现的运行时并发声明。
/// 未实现时框架使用 <see cref="ToolExecutionProfile.LegacyExclusive"/>。
/// </summary>
public interface IToolExecutionProfileProvider
{
    ToolExecutionProfile DescribeExecution(ToolParameters parameters);
}
