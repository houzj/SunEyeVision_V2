using NSubstitute;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class ToolDebugResultSnapshotTests
{
    [Fact]
    public void Captures_required_sources_and_ignores_global_pseudo_nodes()
    {
        var result = new GenericToolResults { Status = ExecutionStatus.Success };
        var query = Substitute.For<IDataSourceQueryService>();
        query.GetNodeContext("image").Returns(new NodeExecutionContext { NodeId = "image", Result = result });

        var snapshot = ToolDebugResultSnapshot.Capture(query, new string?[] { "image", "image", "__GlobalVars__", null });

        Assert.Single(snapshot);
        Assert.Same(result, snapshot["image"]);
    }

    [Fact]
    public void Rejects_a_source_replaced_during_capture_instead_of_mixing_runs()
    {
        var first = new GenericToolResults { Status = ExecutionStatus.Success };
        var next = new GenericToolResults { Status = ExecutionStatus.Success };
        var query = Substitute.For<IDataSourceQueryService>();
        query.GetNodeContext("upstream").Returns(
            new NodeExecutionContext { NodeId = "upstream", Result = first },
            new NodeExecutionContext { NodeId = "upstream", Result = next });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ToolDebugResultSnapshot.Capture(query, new[] { "upstream" }));

        Assert.Contains("不同轮次数据", exception.Message);
    }

    [Fact]
    public void Missing_required_source_is_reported_before_tool_execution()
    {
        var query = Substitute.For<IDataSourceQueryService>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ToolDebugResultSnapshot.Capture(query, new[] { "image" }));

        Assert.Contains("尚无结果", exception.Message);
    }
}
