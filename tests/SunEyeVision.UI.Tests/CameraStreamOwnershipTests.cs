using NSubstitute;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;
using System.Windows;
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

    [Fact]
    public async Task Software_trigger_after_close_is_rejected_without_touching_the_adapter()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CameraCaptureConfiguration { StreamMode = CameraTriggerMode.Software }));
        var feed = await CameraWorkflowFrameFeed.StartAsync(camera.Runtime, camera.Reference, TimeSpan.FromSeconds(1), CancellationToken.None);
        camera.Stream.SetResult(true);
        await Assert.ThrowsAsync<AggregateException>(() => feed.DisposeAsync().AsTask());
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "新采集");
        await Assert.ThrowsAsync<ObjectDisposedException>(() => feed.NextFrameAsync(CancellationToken.None));
        _ = camera.Adapter.DidNotReceive().ExecuteSoftwareTriggerAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Close_waits_for_inflight_software_trigger_before_releasing_capture_ownership()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CameraCaptureConfiguration { StreamMode = CameraTriggerMode.Software }));
        var trigger = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ExecuteSoftwareTriggerAsync(Arg.Any<CancellationToken>()).Returns(_ => trigger.Task);
        var feed = await CameraWorkflowFrameFeed.StartAsync(camera.Runtime, camera.Reference, TimeSpan.FromSeconds(1), CancellationToken.None);
        var read = feed.NextFrameAsync(CancellationToken.None);
        var close = feed.DisposeAsync().AsTask();
        Assert.False(close.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => camera.Runtime.AcquireCaptureLease(camera.Reference, "新采集"));
        camera.Stream.SetResult(true);
        Assert.False(close.IsCompleted);
        trigger.SetResult(true);
        var readFailure = await Record.ExceptionAsync(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(readFailure is OperationCanceledException or ObjectDisposedException);
        await Assert.ThrowsAsync<AggregateException>(() => close.WaitAsync(TimeSpan.FromSeconds(5)));
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "新采集");
    }

    [Fact]
    public void Preview_close_reentered_by_start_waits_for_the_returned_stream()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.StopStreamAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference, camera.Session);
        await host.RefreshConfigurationAsync();
        Task? close = null;
        camera.Adapter.StartStreamAsync(Arg.Any<CameraCaptureRequest>(), Arg.Any<ICameraFrameSink>(), Arg.Any<CancellationToken>())
            .Returns(_ => { close = host.DisposeAsync().AsTask(); return camera.Stream.Task; });
        var start = host.StartAcquisitionAsync();
        Assert.NotNull(close); Assert.False(close!.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流"));
        camera.Stream.SetResult(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start.WaitAsync(TimeSpan.FromSeconds(5)));
        await close.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var retained)); Assert.Same(camera.Session, retained);
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流");
    }));

    [Fact]
    public void Preview_close_waits_for_inflight_software_trigger_before_releasing_capture_ownership()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.Capabilities.Returns(new CameraCapabilities { SupportsSoftwareTrigger = true });
        camera.Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CameraCaptureConfiguration { StreamMode = CameraTriggerMode.Software }));
        camera.Adapter.StopStreamAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var trigger = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ExecuteSoftwareTriggerAsync(Arg.Any<CancellationToken>()).Returns(_ => trigger.Task);
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference, camera.Session);
        await host.RefreshConfigurationAsync(); await host.StartAcquisitionAsync();
        var fire = host.TriggerOnceAsync();
        var close = host.DisposeAsync().AsTask();
        camera.Stream.SetResult(true);
        Assert.False(close.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流"));
        trigger.SetResult(true);
        await fire.WaitAsync(TimeSpan.FromSeconds(5));
        await close.WaitAsync(TimeSpan.FromSeconds(5));
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流");
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.TriggerOnceAsync());
        _ = camera.Adapter.Received(1).ExecuteSoftwareTriggerAsync(Arg.Any<CancellationToken>());
    }));

    [Fact]
    public void Preview_null_conversion_from_driver_thread_is_marshaled_and_releases_frame()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var previous = VisionImageConverterLocator.Current;
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference, camera.Session);
        var window = new Window { Content = host, ShowActivated = false };
        try
        {
            VisionImageConverterLocator.Current = null;
            window.Show();
            var image = Substitute.For<IVisionImage>();
            var frame = new CameraFrame(camera.Reference, 1, DateTimeOffset.UtcNow, image);
            await Task.Run(async () => await host.OnFrameAsync(frame, CancellationToken.None));
            Assert.True(frame.IsDisposed);
            image.Received(1).Dispose();
        }
        finally
        {
            await host.DisposeAsync(); window.Close(); VisionImageConverterLocator.Current = previous;
        }
    }));

    [Fact]
    public async Task Active_workflow_rejects_manual_close_and_session_disposal_until_stream_is_drained()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var feed = await CameraWorkflowFrameFeed.StartAsync(camera.Runtime, camera.Reference, TimeSpan.FromSeconds(1), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => camera.Runtime.CloseAsync(camera.Reference));
        await Assert.ThrowsAsync<InvalidOperationException>(() => camera.Session.DisposeAsync().AsTask());
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var retained)); Assert.Same(camera.Session, retained);
        _ = camera.Adapter.DidNotReceive().DisconnectAsync(Arg.Any<CancellationToken>());
        var closeFeed = feed.DisposeAsync().AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => camera.Runtime.CloseAsync(camera.Reference));
        camera.Stream.SetResult(true);
        await Assert.ThrowsAsync<AggregateException>(() => closeFeed.WaitAsync(TimeSpan.FromSeconds(5)));
        await camera.Runtime.CloseAsync(camera.Reference);
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        _ = camera.Adapter.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Disconnect_serializes_reopen_and_old_session_disposal_never_closes_the_new_session()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var old = camera.Session;
        var newAdapter = Substitute.For<ICameraAdapter>();
        newAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        newAdapter.DisposeAsync().Returns(ValueTask.CompletedTask);
        camera.Factory.Create(Arg.Any<CameraDescriptor>()).Returns(newAdapter);
        var disconnect = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(_ => disconnect.Task);
        var closing = camera.Runtime.CloseAsync(camera.Reference);
        var reopening = camera.Runtime.OpenAsync(camera.Reference, CameraAccessMode.Exclusive);
        Assert.False(closing.IsCompleted); Assert.False(reopening.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CameraWorkflowFrameFeed.StartAsync(
            camera.Runtime, camera.Reference, TimeSpan.FromSeconds(1), CancellationToken.None));
        disconnect.SetResult(true);
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        var current = await reopening.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotSame(old, current);
        using (camera.Runtime.AcquireCaptureLease(camera.Reference, "新采集"))
            await old.DisposeAsync();
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var retained)); Assert.Same(current, retained);
        Assert.Same(newAdapter, current.Adapter);
        _ = camera.Adapter.Received(1).DisposeAsync();
        _ = newAdapter.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public void Preview_borrowing_a_retired_session_cannot_start_on_the_reconnected_device()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var old = camera.Session;
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference, old);
        await host.RefreshConfigurationAsync();
        await old.DisposeAsync();
        var newAdapter = Substitute.For<ICameraAdapter>();
        newAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        newAdapter.DisposeAsync().Returns(ValueTask.CompletedTask);
        camera.Factory.Create(Arg.Any<CameraDescriptor>()).Returns(newAdapter);
        await camera.OpenAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAcquisitionAsync());
        Assert.Contains("会话已关闭或重连", error.Message);
        _ = camera.Adapter.DidNotReceive().StartStreamAsync(Arg.Any<CameraCaptureRequest>(), Arg.Any<ICameraFrameSink>(), Arg.Any<CancellationToken>());
        _ = camera.Adapter.Received(1).ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>());
        _ = newAdapter.DidNotReceive().DisposeAsync();
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var retained)); Assert.Same(camera.Session, retained);
        await host.DisposeAsync();
    }));

    [Fact]
    public async Task Temporary_capture_cleanup_failure_disposes_the_unreturned_frame()
    {
        await using var camera = new ControlledCamera();
        var image = Substitute.For<IVisionImage>();
        var frame = new CameraFrame(camera.Reference, 1, DateTimeOffset.UtcNow, image);
        camera.Adapter.CaptureAsync(Arg.Any<CameraCaptureRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(frame));
        camera.Adapter.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(new InvalidOperationException("disconnect-failure")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => camera.Runtime.CaptureAsync(
            new CameraCaptureRequest { CameraRef = camera.Reference, Timeout = TimeSpan.FromSeconds(1) }, CancellationToken.None));
        Assert.Contains("disconnect-failure", error.Message); Assert.True(frame.IsDisposed); image.Received(1).Dispose();
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
    }

    [Fact]
    public async Task Temporary_capture_waits_for_device_operations_then_closes_its_session()
    {
        await using var camera = new ControlledCamera();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<CameraFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var image = Substitute.For<IVisionImage>();
        var frame = new CameraFrame(camera.Reference, 1, DateTimeOffset.UtcNow, image);
        camera.Adapter.CaptureAsync(Arg.Any<CameraCaptureRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { entered.TrySetResult(true); return pending.Task; });
        var capture = camera.Runtime.CaptureAsync(
            new CameraCaptureRequest { CameraRef = camera.Reference, Timeout = TimeSpan.FromSeconds(1) }, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var session));
        using var operation = camera.Runtime.AcquireSessionOperationLease(session, "参数读取");
        pending.TrySetResult(frame);
        Task? concurrentClose = null;
        try
        {
            var closeResult = typeof(CameraSession).GetProperty("CloseResult", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (closeResult.GetValue(session) is null && !capture.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(10);
            Assert.NotNull(closeResult.GetValue(session));
            Assert.False(capture.IsCompleted);
            concurrentClose = session.DisposeAsync().AsTask();Assert.False(concurrentClose.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => camera.Runtime.AcquireSessionOperationLease(session, "迟到参数读取"));
            _ = camera.Adapter.DidNotReceive().DisconnectAsync(Arg.Any<CancellationToken>());
        }
        finally { operation.Dispose(); }
        using var returned = await capture.WaitAsync(TimeSpan.FromSeconds(5));
        await concurrentClose!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(frame, returned);Assert.False(returned.IsDisposed);
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        _ = camera.Adapter.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
        _ = camera.Adapter.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Stop_is_requested_before_waiting_for_a_workflow_trigger_that_needs_stop_to_finish()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CameraCaptureConfiguration { StreamMode = CameraTriggerMode.Software }));
        var trigger = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ExecuteSoftwareTriggerAsync(Arg.Any<CancellationToken>()).Returns(_ => trigger.Task);
        camera.Adapter.StopStreamAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        { trigger.TrySetResult(true); camera.Stream.TrySetResult(true); return Task.CompletedTask; });
        var feed = await CameraWorkflowFrameFeed.StartAsync(camera.Runtime, camera.Reference, TimeSpan.FromSeconds(1), CancellationToken.None);
        var read = feed.NextFrameAsync(CancellationToken.None);
        await feed.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var error = await Record.ExceptionAsync(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(error is OperationCanceledException or ObjectDisposedException);
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "新采集");
    }

    [Fact]
    public void Stop_is_requested_before_waiting_for_a_preview_trigger_that_needs_stop_to_finish()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.Capabilities.Returns(new CameraCapabilities { SupportsSoftwareTrigger = true });
        camera.Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CameraCaptureConfiguration { StreamMode = CameraTriggerMode.Software }));
        var trigger = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ExecuteSoftwareTriggerAsync(Arg.Any<CancellationToken>()).Returns(_ => trigger.Task);
        camera.Adapter.StopStreamAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        { trigger.TrySetResult(true); camera.Stream.TrySetResult(true); return Task.CompletedTask; });
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference, camera.Session);
        await host.RefreshConfigurationAsync(); await host.StartAcquisitionAsync();
        var fire = host.TriggerOnceAsync();
        await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await fire.WaitAsync(TimeSpan.FromSeconds(5));
        using var next = camera.Runtime.AcquireCaptureLease(camera.Reference, "工作流");
    }));

    [Fact]
    public async Task Concurrent_session_disposals_share_the_same_disconnect_failure()
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var disconnect = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(_ => disconnect.Task);
        var first = camera.Session.DisposeAsync().AsTask();
        var second = camera.Session.DisposeAsync().AsTask();
        Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
        disconnect.SetException(new InvalidOperationException("disconnect-failure"));
        var one = await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
        var two = await Assert.ThrowsAsync<InvalidOperationException>(() => second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("disconnect-failure", one.Message); Assert.Equal(one.Message, two.Message);
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        _ = camera.Adapter.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manager_shutdown_attempts_other_devices_when_one_device_is_still_capturing()
    {
        var firstRef = new CameraDeviceRef("shutdown-busy");
        var secondRef = new CameraDeviceRef("shutdown-idle");
        var firstAdapter = Substitute.For<ICameraAdapter>();
        var secondAdapter = Substitute.For<ICameraAdapter>();
        firstAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        secondAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        firstAdapter.DisposeAsync().Returns(ValueTask.CompletedTask); secondAdapter.DisposeAsync().Returns(ValueTask.CompletedTask);
        var factory = Substitute.For<ICameraAdapterFactory>();
        factory.CanCreate(Arg.Any<CameraDescriptor>()).Returns(true);
        factory.Create(Arg.Any<CameraDescriptor>()).Returns(call =>
            call.Arg<CameraDescriptor>().CameraRef.Value == firstRef.Value ? firstAdapter : secondAdapter);
        var runtime = new CameraRuntimeManager(new[] { factory });
        runtime.RegisterDevice(new CameraDescriptor { DeviceId = firstRef.Value, CameraRef = firstRef });
        runtime.RegisterDevice(new CameraDescriptor { DeviceId = secondRef.Value, CameraRef = secondRef });
        await runtime.OpenAsync(firstRef, CameraAccessMode.Exclusive);
        await runtime.OpenAsync(secondRef, CameraAccessMode.Exclusive);
        using (runtime.AcquireCaptureLease(firstRef, "工作流"))
        {
            var failure = await Assert.ThrowsAsync<AggregateException>(() => runtime.DisposeAsync().AsTask());
            Assert.Contains(failure.InnerExceptions, error => error.Message.Contains("工作流"));
            Assert.True(runtime.TryGetSession(firstRef, out _));
            Assert.False(runtime.TryGetSession(secondRef, out _));
            _ = firstAdapter.DidNotReceive().DisposeAsync();
            _ = secondAdapter.Received(1).DisposeAsync();
        }
        await runtime.DisposeAsync();
        Assert.False(runtime.TryGetSession(firstRef, out _));
    }

    [Fact]
    public void Disconnect_completion_does_not_change_the_newly_selected_camera()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var disconnect = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(_ => disconnect.Task);
        var vm = new CameraDetailViewModel(camera.Runtime);
        var first = new CameraDevice { DeviceId = camera.Reference.Value };
        var second = new CameraDevice { DeviceId = "other-camera", State = CameraState.Capturing };
        await vm.SetConnectionAsync(first, true);
        var closing = vm.SetConnectionAsync(first, false);
        Assert.False(closing.IsCompleted);
        vm.SelectedCamera = second;
        disconnect.SetResult(true);
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(second, vm.SelectedCamera);
        Assert.Equal(CameraState.Capturing, second.State);
        Assert.Equal(CameraState.Offline, first.State);
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Parameter_gate_is_released_when_temporary_camera_close_fails()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera();
        var closeCalls = 0;
        camera.Adapter.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            ++closeCalls == 1 ? Task.FromException(new InvalidOperationException("disconnect-failure")) : Task.CompletedTask);
        var vm = new CameraDetailViewModel(camera.Runtime);
        vm.SelectedCamera = new CameraDevice { DeviceId = camera.Reference.Value };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ApplyPendingParametersAsync());
        Assert.Contains("disconnect-failure", error.Message);
        Assert.Contains("临时相机关闭失败", vm.ParameterStatus);
        Assert.True(await vm.ApplyPendingParametersAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        await vm.DisposeAsync();
    }));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Old_preview_closes_only_its_own_session_after_switching_devices(bool ownsSession)
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var firstSession = camera.Session;
        var secondRef = new CameraDeviceRef("second-device");
        var secondAdapter = Substitute.For<ICameraAdapter>();
        secondAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        secondAdapter.DisposeAsync().Returns(ValueTask.CompletedTask);
        camera.Runtime.RegisterDevice(new CameraDescriptor { DeviceId = secondRef.Value, CameraRef = secondRef });
        camera.Factory.Create(Arg.Any<CameraDescriptor>()).Returns(secondAdapter);
        var vm = new CameraDetailViewModel(camera.Runtime);
        var first = new CameraDevice { DeviceId = camera.Reference.Value };
        var second = new CameraDevice { DeviceId = secondRef.Value };
        await vm.SetConnectionAsync(first, true);
        await vm.SetConnectionAsync(second, true);
        Assert.True(vm.IsConnected);
        await vm.ClosePreviewSessionAsync(first, firstSession, ownsSession);
        Assert.Same(second, vm.SelectedCamera); Assert.Equal(CameraState.Connected, second.State); Assert.True(vm.IsConnected);
        Assert.True(camera.Runtime.TryGetSession(secondRef, out var secondSession)); Assert.Same(secondAdapter, secondSession.Adapter);
        _ = secondAdapter.DidNotReceive().DisposeAsync();
        Assert.Equal(!ownsSession, camera.Runtime.TryGetSession(camera.Reference, out _));
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Connection_completion_after_selection_changes_does_not_claim_the_new_device()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera();
        var connecting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(_ => connecting.Task);
        var vm = new CameraDetailViewModel(camera.Runtime);
        var first = new CameraDevice { DeviceId = camera.Reference.Value };
        var second = new CameraDevice { DeviceId = "second-device", State = CameraState.Capturing };
        var pending = vm.SetConnectionAsync(first, true);
        Assert.False(pending.IsCompleted);
        vm.SelectedCamera = second;
        connecting.SetResult(true);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(CameraState.Connected, first.State);
        Assert.Equal(CameraState.Capturing, second.State);
        Assert.False(vm.IsConnected);
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out _));
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Disconnect_during_pending_connect_retires_the_late_session()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera();
        var connecting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(_ => connecting.Task);
        var vm = new CameraDetailViewModel(camera.Runtime);
        var selected = new CameraDevice { DeviceId = camera.Reference.Value };
        var pending = vm.SetConnectionAsync(selected, true);
        Assert.False(pending.IsCompleted);
        await vm.SetConnectionAsync(selected, false);
        connecting.SetResult(true);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(CameraState.Offline, selected.State);
        Assert.False(vm.IsConnected);
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        _ = camera.Adapter.Received(1).DisposeAsync();
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Reload_after_selecting_another_camera_reads_the_selected_device_session()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var secondRef = new CameraDeviceRef("schema-second");
        var secondAdapter = Substitute.For<ICameraAdapter>();
        secondAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        secondAdapter.DisposeAsync().Returns(ValueTask.CompletedTask);
        secondAdapter.GetParameterSchemaAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CameraParameterSchema
        { Parameters = new[] { new CameraParameterDescriptor { Name = "SecondValue", Value = 1 } } }));
        secondAdapter.ReadParametersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>()));
        camera.Runtime.RegisterDevice(new CameraDescriptor { DeviceId = secondRef.Value, CameraRef = secondRef });
        camera.Factory.Create(Arg.Any<CameraDescriptor>()).Returns(secondAdapter);
        await camera.Runtime.OpenAsync(secondRef, CameraAccessMode.Exclusive);
        var vm = new CameraDetailViewModel(camera.Runtime);
        await vm.SetConnectionAsync(new CameraDevice { DeviceId = camera.Reference.Value }, true);
        vm.SelectedCamera = new CameraDevice { DeviceId = secondRef.Value };
        vm.ResetParamsCommand.Execute(null);
        _ = secondAdapter.Received(1).GetParameterSchemaAsync(Arg.Any<CancellationToken>());
        _ = camera.Adapter.DidNotReceive().GetParameterSchemaAsync(Arg.Any<CancellationToken>());
        Assert.Single(vm.Parameters); Assert.Equal("SecondValue", vm.Parameters[0].Name);
        Assert.True(vm.IsConnected);
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Preview_preparation_is_single_entry_and_abandoned_session_is_released()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera();
        var schema = new TaskCompletionSource<CameraParameterSchema>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.GetParameterSchemaAsync(Arg.Any<CancellationToken>()).Returns(_ => schema.Task);
        camera.Adapter.ReadParametersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>()));
        var vm = new CameraDetailViewModel(camera.Runtime);
        vm.SelectedCamera = new CameraDevice { DeviceId = camera.Reference.Value };
        var first = vm.PreviewAsync();
        Assert.False(first.IsCompleted);
        await vm.PreviewAsync().WaitAsync(TimeSpan.FromSeconds(5));
        _ = camera.Adapter.Received(1).GetParameterSchemaAsync(Arg.Any<CancellationToken>());
        vm.SelectedCamera = new CameraDevice { DeviceId = "other" };
        schema.SetResult(new CameraParameterSchema());
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        _ = camera.Adapter.Received(1).DisposeAsync();
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Parameter_command_after_switch_targets_the_selected_camera()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var secondRef = new CameraDeviceRef("command-second");
        var secondAdapter = Substitute.For<ICameraAdapter>();
        secondAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        secondAdapter.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        secondAdapter.DisposeAsync().Returns(ValueTask.CompletedTask);
        camera.Runtime.RegisterDevice(new CameraDescriptor { DeviceId = secondRef.Value, CameraRef = secondRef });
        camera.Factory.Create(Arg.Any<CameraDescriptor>()).Returns(secondAdapter);
        await camera.Runtime.OpenAsync(secondRef, CameraAccessMode.Exclusive);
        var vm = new CameraDetailViewModel(camera.Runtime);
        await vm.SetConnectionAsync(new CameraDevice { DeviceId = camera.Reference.Value }, true);
        vm.SelectedCamera = new CameraDevice { DeviceId = secondRef.Value };
        var command = new CameraParameterItemViewModel(new CameraParameterDescriptor
        { Name = "UserSetLoad", Kind = CameraParameterKind.Command }, null);
        vm.ExecuteParameterCommand.Execute(command);
        _ = secondAdapter.Received(1).ExecuteCommandAsync("UserSetLoad", Arg.Any<CancellationToken>());
        _ = camera.Adapter.DidNotReceive().ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Switching_camera_retires_old_dirty_parameters_and_cancels_pending_auto_apply()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        camera.Adapter.GetParameterSchemaAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CameraParameterSchema
        { Parameters = new[] { new CameraParameterDescriptor { Name = "ExposureTime", Kind = CameraParameterKind.Integer, Value = 1 } } }));
        camera.Adapter.ReadParametersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>()));
        var secondRef = new CameraDeviceRef("parameter-second");
        var secondAdapter = Substitute.For<ICameraAdapter>();
        secondAdapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        secondAdapter.DisposeAsync().Returns(ValueTask.CompletedTask);
        camera.Runtime.RegisterDevice(new CameraDescriptor { DeviceId = secondRef.Value, CameraRef = secondRef });
        camera.Factory.Create(Arg.Any<CameraDescriptor>()).Returns(secondAdapter);
        await camera.Runtime.OpenAsync(secondRef, CameraAccessMode.Exclusive);
        var vm = new CameraDetailViewModel(camera.Runtime);
        await vm.SetConnectionAsync(new CameraDevice { DeviceId = camera.Reference.Value }, true);
        vm.ResetParamsCommand.Execute(null);
        Assert.Single(vm.Parameters);
        vm.Parameters[0].Value = 2;
        vm.SelectedCamera = new CameraDevice { DeviceId = secondRef.Value };
        Assert.Empty(vm.Parameters);
        await Task.Delay(450);
        Assert.True(await vm.ApplyPendingParametersAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        _ = secondAdapter.DidNotReceive().ApplyParametersAsync(Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>());
        _ = camera.Adapter.DidNotReceive().ApplyParametersAsync(Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>());
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Reentrant_selection_from_parameter_tree_change_preserves_the_latest_device()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera();
        var vm = new CameraDetailViewModel(camera.Runtime);
        var first = new CameraDevice { DeviceId = "first" };
        var second = new CameraDevice { DeviceId = "second" };
        var latest = new CameraDevice { DeviceId = "latest" };
        vm.SelectedCamera = first;
        vm.Parameters.Add(new CameraParameterItemViewModel(new CameraParameterDescriptor
        { Name = "OldValue", Kind = CameraParameterKind.Integer, Value = 1 }, 1));
        var reentered = false;
        vm.Parameters.CollectionChanged += (_, _) =>
        {
            if (reentered) return;
            reentered = true;
            vm.SelectedCamera = latest;
        };
        vm.SelectedCamera = second;
        Assert.True(reentered);
        Assert.Same(latest, vm.SelectedCamera);
        Assert.Empty(vm.Parameters);
        await vm.DisposeAsync();
    }));

    [Fact]
    public void Preview_close_drains_its_inflight_configuration_read_before_releasing_owned_session()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera();
        var configuration = new TaskCompletionSource<CameraCaptureConfiguration>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>()).Returns(_ => configuration.Task);
        var host = new CameraPreviewHost(camera.Runtime, camera.Reference);
        var read = host.RefreshConfigurationAsync();
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var session));
        await Assert.ThrowsAsync<InvalidOperationException>(() => camera.Runtime.CloseAsync(camera.Reference));
        var close = host.DisposeAsync().AsTask();
        Assert.False(close.IsCompleted);
        _ = camera.Adapter.DidNotReceive().DisposeAsync();
        configuration.SetResult(new CameraCaptureConfiguration());
        await read.WaitAsync(TimeSpan.FromSeconds(5));
        await close.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        _ = camera.Adapter.Received(1).DisposeAsync();
    }));

    [Fact]
    public void Parameter_command_inflight_blocks_disconnect_until_the_actual_driver_call_returns()
        => WpfTestDispatcher.Invoke(() => Pump(async () =>
    {
        await using var camera = new ControlledCamera(); await camera.OpenAsync();
        var commandDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        camera.Adapter.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => commandDone.Task);
        var vm = new CameraDetailViewModel(camera.Runtime);
        var selected = new CameraDevice { DeviceId = camera.Reference.Value };
        await vm.SetConnectionAsync(selected, true);
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.ParameterStatus) && vm.ParameterStatus.StartsWith("命令已执行"))
                finished.TrySetResult(true);
        };
        var command = new CameraParameterItemViewModel(new CameraParameterDescriptor
        { Name = "UserSetLoad", Kind = CameraParameterKind.Command }, null);
        vm.ExecuteParameterCommand.Execute(command);
        _ = camera.Adapter.Received(1).ExecuteCommandAsync("UserSetLoad", Arg.Any<CancellationToken>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => camera.Session.DisposeAsync().AsTask());
        Assert.True(camera.Runtime.TryGetSession(camera.Reference, out var live)); Assert.Same(camera.Session, live);
        commandDone.SetResult(true);
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await camera.Session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(camera.Runtime.TryGetSession(camera.Reference, out _));
        await vm.DisposeAsync();
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
