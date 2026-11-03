using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Execution.Tests;

public sealed class ResourceLeaseTests
{
    [Fact]
    public void Shared_resource_excludes_other_workflows_until_lease_is_released()
    {
        var manager = new ExecutionResourceLeaseManager();
        var camera = new ToolCapabilities(new[] { "camera:1" });
        Assert.True(manager.TryAcquire("one", "node", camera, out var first));
        Assert.False(manager.TryAcquire("two", "node", camera, out var blocked));
        Assert.Null(blocked);
        first!.Dispose(); first.Dispose();
        Assert.True(manager.TryAcquire("two", "node", camera, out var second));
        second!.Dispose();
    }

    [Fact]
    public void Empty_declarations_use_node_scoped_fallback()
    {
        var manager = new ExecutionResourceLeaseManager();
        Assert.True(manager.TryAcquire("flow", "one", ToolCapabilities.None, out var one));
        Assert.True(manager.TryAcquire("flow", "two", ToolCapabilities.None, out var two));
        Assert.False(manager.TryAcquire("flow", "one", ToolCapabilities.None, out _));
        one!.Dispose(); two!.Dispose();
    }
}
