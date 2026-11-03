using System.Linq;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;

/// <summary>Execution-only decision for the immutable root-workspace configuration.</summary>
public static class RootWorkspaceExecutionRules
{
    public static bool ShouldStopAfter(this RootWorkspaceSettings settings, ExecutionResult result) =>
        settings.StopOnNg && result.NodeResults.Values.Any(node => node.Result?.Judgment == BusinessJudgment.Fail);
}

