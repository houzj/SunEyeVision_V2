using NSubstitute;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.TestShared;
using SunEyeVision.UI.Features.Camera;
using SunEyeVision.UI.Services.Workflow;
using System.Windows.Threading;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>Actual workflow and preview owners share the real device manager's capture lease.</summary>
public sealed class CameraStreamOwnershipTests
{
    private sealed class ControlledCamera : IAsyncDisposable
    {
        public readonly ICameraAdapter Adapter = Substitute.For<ICameraAdapter>();
        public readonly ICameraAdapterFactory Factory = Substitute.For<ICameraAdapterFactory>();
        public readonly TaskCompletionSource<bool> Stream = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly CameraDeviceRef Reference = new("controlled-stream");
        public readonly CameraRuntimeManager Runtime;
        public CameraSession Session = null!;
        public ControlledCamera()
        {
            Factory.CanCreate(Arg.Any<CameraDescriptor>()).Returns(true);
            Factory.Create(Arg.Any<CameraDescriptor>()).Returns(Adapter);
            Adapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CameraCaptureConfiguration()));
            Adapter.StartStreamAsync(Arg.Any<CameraCaptureRequest>(), Arg.Any<ICameraFrameSink>(), Arg.Any<CancellationToken>())
                .Returns(_ => Stream.Task);
            Adapter.StopStreamAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException(new InvalidOperationException("controlled-stop-failure")));
            Adapter.DisposeAsync().Returns(ValueTask.CompletedTask);
            Runtime = new CameraRuntimeManager(new[] { Factory });
            Runtime.RegisterDevice(new CameraDescriptor { DeviceId = Reference.Value, CameraRef = Reference });
        }
        public async Task OpenAsync() => Session = await Runtime.OpenAsync(Reference, CameraAccessMode.Exclusive);
        public async ValueTask DisposeAsync() { Stream.TrySetResult(true); await Runtime.DisposeAsync(); }
    }

    [Fact]
    public async Task Workflow_stop_failure_keeps_lease_until_stream_exits_and_all_closers_observe_failure()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var feed = await CameraWorkflowFrameFeed.StartAsync(camera.Runtime, camera.Reference, TimeSpan.FromSeconds(1), CancellationToken.None);
        var first = feed.DisposeAsync().AsTask(); var second = feed.DisposeAsync().AsTask();
        Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => camera.Runtime.AcquireCaptureLease(camera.Reference, "相机设置预览"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => camera.Runtime.CaptureAsync(
            new CameraCaptureRequest { CameraRef = camera.Reference, Timeout = TimeSpan.FromSeconds(1) }, CancellationToken.None));
        camera.Stream.SetResult(true);
        var error = await Assert.ThrowsAsync<AggregateException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<AggregateException>(() => second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains(error.InnerExceptions, e => e.Message.Contains("controlled-stop-failure"));
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "相机设置预览");
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var actual)); Assert.Same(camera.Session, actual);
    }

    [Fact]
    public async Task Workflow_reports_both_stream_and_stop_failures_and_releases_the_lease()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var feed = await CameraWorkflowFrameFeed.StartAsync(camera.Runtime, camera.Reference, TimeSpan.FromSeconds(1), CancellationToken.None);
        camera.Stream.SetException(new InvalidOperationException("controlled-stream-failure"));
        var error = await Assert.ThrowsAsync<AggregateException>(() => feed.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains(error.InnerExceptions, e => e.Message.Contains("controlled-stop-failure"));
        Assert.Contains(error.InnerExceptions, e => e.Message.Contains("controlled-stream-failure"));
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "相机设置预览");
    }

    [Fact]
    public void Preview_stop_and_close_wait_for_same_live_stream_without_closing_borrowed_session()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference, camera.Session);
        await host.RefreshConfigurationAsync(); await host.StartAcquisitionAsync(); Assert.True(host.IsAcquiring);
        var stop = host.StopAcquisitionAsync(); var close = host.DisposeAsync().AsTask(); var closeAgain = host.DisposeAsync().AsTask();
        Assert.False(stop.IsCompleted); Assert.False(close.IsCompleted); Assert.False(closeAgain.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流"));
        camera.Stream.SetResult(true);
        await Assert.ThrowsAsync<AggregateException>(() => stop.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<AggregateException>(() => close.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<AggregateException>(() => closeAgain.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(host.IsAcquiring); Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var retained)); Assert.Same(camera.Session, retained);
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流");
    }));

    [Fact]
    public void Preview_successful_stop_drains_stream_before_workflow_can_reuse_borrowed_session()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.StopStreamAsync(Arg.Any<CancellationToken>()).Returns(_ => { camera.Stream.TrySetResult(true); return Task.CompletedTask; });
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference, camera.Session);
        await host.RefreshConfigurationAsync(); await host.StartAcquisitionAsync();
        await host.StopAcquisitionAsync().WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(host.IsAcquiring);
        using (camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流")) { }
        await host.DisposeAsync(); Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var retained)); Assert.Same(camera.Session, retained);
    }));

    [Fact]
    public void Closing_during_inflight_open_waits_and_releases_only_the_session_it_opened()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera();
        var connecting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(_ => connecting.Task);
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference);
        var refresh = host.RefreshConfigurationAsync(); var close = host.DisposeAsync().AsTask(); var again = host.DisposeAsync().AsTask();
        Assert.False(close.IsCompleted); Assert.False(again.IsCompleted);
        connecting.SetResult(true);
        await close.WaitAsync(TimeSpan.FromSeconds(5)); await again.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        _ = camera.Adapter.Received(1).DisposeAsync();
    }));

    private static void Pump(Func<Task> action)
    {
        var frame = new DispatcherFrame(); Exception? failure = null;
        _ = Invoke(); Dispatcher.PushFrame(frame);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        async Task Invoke()
        {
            try { await action().WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception error) { failure = error; }
            finally { frame.Continue = false; }
        }
    }
}
