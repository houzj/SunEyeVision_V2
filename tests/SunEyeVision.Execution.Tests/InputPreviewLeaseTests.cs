using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Execution.Tests;

public sealed class InputPreviewLeaseTests
{
    [Fact]
    public void Stale_preview_releases_its_reader_once()
    {
        var releases = 0;
        var lease = new ExecutionInputPreviewLease(new InputValues(Array.Empty<Port>(), new Dictionary<string, object?>()),
            Array.Empty<RunIdentity>(), () => false, () => releases++);
        Assert.False(lease.IsCurrent);
        Assert.False(lease.IsCurrent);
        lease.Dispose();
        Assert.Equal(1, releases);
    }

    [Fact]
    public void Current_check_reentry_is_rejected_without_losing_lease()
    {
        var releases = 0; ExecutionInputPreviewLease? lease = null;
        lease = new ExecutionInputPreviewLease(new InputValues(Array.Empty<Port>(), new Dictionary<string, object?>()),
            Array.Empty<RunIdentity>(), () => !lease!.IsCurrent, () => releases++);
        Assert.True(lease.IsCurrent);
        Assert.Equal(0, releases);
        lease.Dispose();
        Assert.Equal(1, releases);
    }
}
