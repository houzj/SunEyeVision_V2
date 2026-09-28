using System;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.UI.Services.Workflow;

/// <summary>工作流运行期间唯一的相机帧入口；不持有设备连接，也不向 WPF 发布图像。</summary>
internal sealed class CameraWorkflowFrameFeed : ICameraFrameSink, IAsyncDisposable
{
    private readonly CameraSession _session;
    private readonly CameraDeviceRef _cameraRef;
    private readonly TimeSpan _timeout;
    private readonly CameraTriggerMode _mode;
    private readonly CancellationTokenSource _streamCancellation = new();
    private readonly IDisposable _captureLease;
    private readonly object _gate = new();
    private CameraFrame? _pending;
    private TaskCompletionSource<bool>? _waiter;
    private Task? _streamTask;
    private Exception? _streamError;
    private bool _disposed;
    private long _replacedFrames;
    private long _receivedFrames;
    private long _deliveredFrames;

    private CameraWorkflowFrameFeed(
        CameraSession session,
        CameraDeviceRef cameraRef,
        TimeSpan timeout,
        CameraTriggerMode mode,
        IDisposable captureLease)
    {
        _session = session;
        _cameraRef = cameraRef;
        _timeout = timeout;
        _mode = mode;
        _captureLease = captureLease;
    }

    public CameraTriggerMode Mode => _mode;
    public long ReplacedFrames => Interlocked.Read(ref _replacedFrames);
    public long ReceivedFrames => Interlocked.Read(ref _receivedFrames);
    public long DeliveredFrames => Interlocked.Read(ref _deliveredFrames);

    public static async Task<CameraWorkflowFrameFeed> StartAsync(
        CameraRuntimeManager runtime,
        CameraDeviceRef cameraRef,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!runtime.TryGetSession(cameraRef, out var session))
            throw new InvalidOperationException($"相机 {cameraRef.Value} 未连接，请先在设备列表连接。");

        var lease = runtime.AcquireCaptureLease(cameraRef, "工作流");
        CameraCaptureConfiguration config;
        try
        {
            config = await session.Adapter.ReadCaptureConfigurationAsync(cancellationToken).ConfigureAwait(false);
            if (!config.IsValid)
                throw new InvalidOperationException(config.StatusMessage ?? "无法读取相机触发配置");
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        var feed = new CameraWorkflowFrameFeed(session, cameraRef, timeout, config.StreamMode, lease);
        try
        {
            var streamTask = session.Adapter.StartStreamAsync(
                new CameraCaptureRequest
                {
                    CameraRef = cameraRef,
                    TriggerMode = config.StreamMode,
                    Timeout = TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds, 800))
                }, feed, feed._streamCancellation.Token);
            feed._streamTask = streamTask;
            _ = streamTask.ContinueWith(task =>
                feed.Complete(task.IsFaulted
                    ? task.Exception?.GetBaseException() ?? new InvalidOperationException("相机取流失败")
                    : new InvalidOperationException("相机取流已结束")),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (streamTask.IsFaulted)
                await streamTask.ConfigureAwait(false);
        }
        catch
        {
            await feed.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        return feed;
    }

    /// <summary>一轮开始前取得帧；软件触发模式恰好发送一次触发，其余模式消费后台新帧。</summary>
    public async Task<CameraFrame> NextFrameAsync(CancellationToken cancellationToken, bool continuous = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_mode == CameraTriggerMode.Software)
        {
            CameraFrame? stale;
            lock (_gate)
            {
                stale = _pending;
                _pending = null;
            }
            stale?.Dispose();
            await _session.Adapter.ExecuteSoftwareTriggerAsync(cancellationToken).ConfigureAwait(false);
        }

        if (continuous && _mode != CameraTriggerMode.Software)
            return await WaitForFrameAsync(cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try { return await WaitForFrameAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"等待相机 {_cameraRef.Value} 新帧超时（{_timeout.TotalMilliseconds:F0} ms）");
        }
    }

    private async Task<CameraFrame> WaitForFrameAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_pending is { } frame)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _pending = null;
                    Interlocked.Increment(ref _deliveredFrames);
                    return frame;
                }
                if (_streamError is not null)
                    throw new InvalidOperationException("相机工作流取流中断", _streamError);
                _waiter ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                waitTask = _waiter.Task;
            }
            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask OnFrameAsync(CameraFrame frame, CancellationToken cancellationToken)
    {
        CameraFrame? replaced;
        TaskCompletionSource<bool>? waiter;
        lock (_gate)
        {
            if (_disposed)
            {
                frame.Dispose();
                return ValueTask.CompletedTask;
            }
            replaced = _pending;
            _pending = frame;
            Interlocked.Increment(ref _receivedFrames);
            waiter = _waiter;
            _waiter = null;
            if (replaced is not null)
                Interlocked.Increment(ref _replacedFrames);
        }
        replaced?.Dispose();
        waiter?.TrySetResult(true);
        return ValueTask.CompletedTask;
    }

    private void Complete(Exception error)
    {
        TaskCompletionSource<bool>? waiter;
        lock (_gate)
        {
            _streamError = error;
            waiter = _waiter;
            _waiter = null;
        }
        waiter?.TrySetResult(true);
    }

    public async ValueTask DisposeAsync()
    {
        CameraFrame? pending;
        TaskCompletionSource<bool>? waiter;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            pending = _pending;
            _pending = null;
            waiter = _waiter;
            _waiter = null;
        }
        waiter?.TrySetResult(true);
        _streamCancellation.Cancel();
        try
        {
            if (_streamTask is not null)
            {
                try { await _session.Adapter.StopStreamAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                try { await _streamTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            pending?.Dispose();
            _streamCancellation.Dispose();
            _captureLease.Dispose();
        }
    }
}
