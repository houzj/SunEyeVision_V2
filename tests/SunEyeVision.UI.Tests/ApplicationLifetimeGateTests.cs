using System;
using System.Threading;
using SunEyeVision.UI.Services;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ApplicationLifetimeGateTests
{
    private static (string Running, string Maintenance) Names()
    {
        var id = Guid.NewGuid().ToString("N");
        return ($@"Local\SunEyeVision.Test.Running.{id}", $@"Local\SunEyeVision.Test.Maintenance.{id}");
    }

    [Fact]
    public void Registration_keeps_marker_present_until_disposed()
    {
        var names = Names();
        using (var registration = ApplicationLifetimeGate.TryRegister(names.Running, names.Maintenance))
        {
            Assert.NotNull(registration);
            using var marker = Mutex.OpenExisting(names.Running);
        }
        Assert.False(Mutex.TryOpenExisting(names.Running, out var remaining));
        remaining?.Dispose();
    }

    [Fact]
    public void Closing_one_instance_does_not_remove_other_instance_marker()
    {
        var names = Names();
        using var first = ApplicationLifetimeGate.TryRegister(names.Running, names.Maintenance);
        using (var second = ApplicationLifetimeGate.TryRegister(names.Running, names.Maintenance))
            Assert.NotNull(second);
        using var marker = Mutex.OpenExisting(names.Running);
        Assert.NotNull(first);
    }

    [Fact]
    public void Maintenance_blocks_startup_until_released()
    {
        var names = Names();
        using var maintenance = new Mutex(false, names.Maintenance);
        maintenance.WaitOne();
        try
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var registration = ApplicationLifetimeGate.TryRegister(names.Running, names.Maintenance);
                    Assert.Null(registration);
                }
                catch (Exception ex) { error = ex; }
            });
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
            if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            Assert.False(Mutex.TryOpenExisting(names.Running, out var remaining));
            remaining?.Dispose();
        }
        finally { maintenance.ReleaseMutex(); }
        using var allowed = ApplicationLifetimeGate.TryRegister(names.Running, names.Maintenance);
        Assert.NotNull(allowed);
    }
}
