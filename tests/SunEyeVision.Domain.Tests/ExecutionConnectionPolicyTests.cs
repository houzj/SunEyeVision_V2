using SunEyeVision.Domain.Workflows;
using Xunit;

namespace SunEyeVision.Domain.Tests;

/// <summary>连线合同：同一节点对只允许一条边（与执行边合同一致，不以端口区分边）；
/// 自连与成环继续拒绝，因为执行器不支持回环。</summary>
public sealed class ExecutionConnectionPolicyTests
{
    private static ConnectionDefinition Edge(string id, string source, string target)
        => new(id, source, target, "RightPort", "LeftPort");

    [Fact]
    public void Second_edge_between_same_nodes_is_rejected()
    {
        var existing = new[] { Edge("e1", "A", "B") };
        var reason = ExecutionConnectionPolicy.Rejection(existing, Edge("e2", "A", "B"));
        Assert.NotNull(reason);
        Assert.Contains("执行连线", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Self_connection_is_rejected()
    {
        var reason = ExecutionConnectionPolicy.Rejection(Array.Empty<ConnectionDefinition>(), Edge("e1", "A", "A"));
        Assert.NotNull(reason);
    }

    [Fact]
    public void Missing_endpoint_is_rejected_at_construction()
        => Assert.Throws<ArgumentException>(() => new ConnectionDefinition("e1", "A", " ", "RightPort", "LeftPort"));

    [Fact]
    public void Cycle_is_rejected()
    {
        var existing = new[] { Edge("e1", "B", "C"), Edge("e2", "C", "A") };
        var reason = ExecutionConnectionPolicy.Rejection(existing, Edge("e3", "A", "B"));
        Assert.NotNull(reason);
        Assert.Contains("环", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Parallel_edges_to_different_targets_are_allowed()
    {
        var existing = new[] { Edge("e1", "A", "B") };
        Assert.Null(ExecutionConnectionPolicy.Rejection(existing, Edge("e2", "A", "C")));
    }
}
