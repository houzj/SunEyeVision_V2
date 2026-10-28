using SunEyeVision.Plugin.Runtime;
using System;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;
namespace SunEyeVision.Workflow;

/// <summary>根流程持久化工作区配置；运行时只读取已捕获的不可变实例。</summary>
public sealed record RootWorkspaceSettings
{
    public int RunIntervalMs { get; init; } = 80;
    public bool StopOnNg { get; init; }
    public double? OverviewX { get; init; }
    public double? OverviewY { get; init; }
    public void Validate()
    {
        if(RunIntervalMs is <0 or >100000)throw new ArgumentOutOfRangeException(nameof(RunIntervalMs),"运行间隔应为0到100000毫秒。");
        if(OverviewX.HasValue!=OverviewY.HasValue||OverviewX is {} x&&(!double.IsFinite(x)||x<0||x>4860)
            ||OverviewY is {} y&&(!double.IsFinite(y)||y<0||y>4880))throw new ArgumentException("流程总览位置必须成对位于5000画布内。");
    }
    public bool ShouldStopAfter(ExecutionResult result)
        => StopOnNg && result.NodeResults.Values.Any(node=>node.Result?.Judgment==SunEyeVision.Plugin.Contracts.BusinessJudgment.Fail);
}
