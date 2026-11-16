using SunEyeVision.Domain.Workflows;
using Xunit;

namespace SunEyeVision.Domain.Tests;

/// <summary>连线合同：同一节点对的数据边与控制边语义不同，可共存；同类边仍只能一条。
/// 自连与成环继续拒绝，因为执行器不支持回环。</summary>
public sealed class ExecutionConnectionPolicyTests
{
    private static ConnectionDefinition Edge(string id, string source, string target, string sourcePort)
        => new(id, source, target, sourcePort, "target.input");

    [Fact]
    public void Data_and_control_edge_between_same_nodes_coexist()
    {
        var existing = new[] { Edge("e1", "A", "B", "output.image") };
        var control = Edge("e2", "A", "B", "control.ok");
        Assert.Null(ExecutionConnectionPolicy.Rejection(existing, control));
    }

    [Fact]
    public void Second_data_edge_between_same_nodes_is_rejected()
    {
        var existing = new[] { Edge("e1", "A", "B", "output.image") };
        var reason = ExecutionConnectionPolicy.Rejection(existing, Edge("e2", "A", "B", "output.mask"));
        Assert.NotNull(reason);
        Assert.Contains("执行连线", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Second_control_edge_between_same_nodes_is_rejected()
    {
        var existing = new[] { Edge("e1", "A", "B", "control.ok") };
        var reason = ExecutionConnectionPolicy.Rejection(existing, Edge("e2", "A", "B", "control.ng"));
        Assert.NotNull(reason);
        Assert.Contains("控制连线", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Self_connection_is_rejected()
    {
        var reason = ExecutionConnectionPolicy.Rejection(Array.Empty<ConnectionDefinition>(), Edge("e1", "A", "A", "control.ok"));
        Assert.NotNull(reason);
    }

    [Fact]
    public void Cycle_through_control_edges_is_rejected()
    {
        var existing = new[] { Edge("e1", "B", "C", "control.ok"), Edge("e2", "C", "A", "control.ok") };
        var reason = ExecutionConnectionPolicy.Rejection(existing, Edge("e3", "A", "B", "control.ok"));
        Assert.NotNull(reason);
        Assert.Contains("环", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Control_port_identity_is_parsed_from_the_edge()
    {
        var edge = Edge("e1", "A", "B", "control.ok");
        Assert.True(edge.IsControlEdge);
        Assert.Equal("ok", edge.ControlOutputId);
        var data = Edge("e2", "A", "B", "output.image");
        Assert.False(data.IsControlEdge);
        Assert.Null(data.ControlOutputId);
    }
}
