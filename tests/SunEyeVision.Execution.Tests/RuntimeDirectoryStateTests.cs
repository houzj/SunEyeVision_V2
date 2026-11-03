using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using Xunit;
namespace SunEyeVision.Execution.Tests;

public sealed class RuntimeDirectoryStateTests
{
    private static RuntimeDirectorySnapshot Create()
    {
        var registration = new ToolComponentRegistration(new ToolComponentMetadata("directory-state", "ResultLifetime",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(ResultLifetimeDefinition));
        return (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { new[] { registration } })!;
    }
    [Fact]
    public async Task Active_read_does_not_take_lifetime_gate()
    {
        var directory = Create();
        var gate = typeof(RuntimeDirectorySnapshot).GetField("_lifetimeGate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(directory)!;
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var holder = Task.Run(() => { lock (gate) { entered.Set(); release.Wait(); } });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(3)));
        try { Assert.True(await Task.Run(() => directory.IsActive).WaitAsync(TimeSpan.FromSeconds(2))); }
        finally { release.Set(); await holder.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
    [Fact]
    public async Task Retirement_visibility_precedes_cancellation_and_still_waits_for_leases()
    {
        var directory = Create();
        var lease = directory.AcquireInvocation(); var token = lease.RetirementToken;
        var drained = (Task)typeof(RuntimeDirectorySnapshot).GetMethod("BeginRetirement", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(directory, null)!;
        Assert.False(directory.IsActive); Assert.False(token.IsCancellationRequested); Assert.False(drained.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => directory.AcquireInvocation());
        lease.Dispose(); await drained.WaitAsync(TimeSpan.FromSeconds(3));
        typeof(RuntimeDirectorySnapshot).GetMethod("SignalRetirement", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(directory, null);
        Assert.True(token.IsCancellationRequested);
    }
}
