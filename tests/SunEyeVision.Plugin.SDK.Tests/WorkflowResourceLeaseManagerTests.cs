using System;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class WorkflowResourceLeaseManagerTests
{
    [Fact]
    public void LegacyExclusive_BlocksAnotherNodeUntilReleased()
    {
        var manager = new WorkflowResourceLeaseManager();

        Assert.True(manager.TryAcquire("flow", "first", ToolExecutionProfile.LegacyExclusive, out var first));
        Assert.False(manager.TryAcquire("flow", "second", ToolExecutionProfile.LegacyExclusive, out _));

        first.Dispose();

        Assert.True(manager.TryAcquire("flow", "second", ToolExecutionProfile.LegacyExclusive, out var second));
        second.Dispose();
    }

    [Fact]
    public void ParallelSafe_DoesNotClaimAResource()
    {
        var manager = new WorkflowResourceLeaseManager();
        var profile = new ToolExecutionProfile(ToolConcurrencyKind.ParallelSafe);

        Assert.True(manager.TryAcquire("flow", "first", profile, out var first));
        Assert.True(manager.TryAcquire("flow", "second", profile, out var second));

        first.Dispose();
        second.Dispose();
    }
}
