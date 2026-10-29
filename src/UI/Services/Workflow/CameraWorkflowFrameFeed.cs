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
    private Task? _disposeTask;
    private int _activeReads;
    private TaskCompletionSource<bool>? _readsDrained;
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
        var lease = runtime.AcquireCaptureLease(cameraRef, "工作流");
        CameraSession session;
        CameraCaptureConfiguration config;
        try
        {
            if (!runtime.TryGetSession(cameraRef, out session))
                throw new InvalidOperationException($"相机 {cameraRef.Value} 未连接，请先在设备列表连接。");
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
        catch (Exception failure)
        {
            try { await feed.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { throw new AggregateException("相机取流启动及清理均失败。", failure, cleanup); }
            throw;
        }
        return feed;
    }

    /// <summary>一轮开始前取得帧；软件触发模式恰好发送一次触发，其余模式消费后台新帧。</summary>
    public async Task<CameraFrame> NextFrameAsync(CancellationToken cancellationToken, bool continuous = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_streamError is not null) throw new InvalidOperationException("相机工作流取流中断", _streamError);
            ++_activeReads;
        }
        try
        {
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _streamCancellation.Token);
            var token = lifetime.Token;
            if (_mode == CameraTriggerMode.Software)
            {
                CameraFrame? stale;
                lock (_gate)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    stale = _pending; _pending = null;
                }
                stale?.Dispose();
                await _session.Adapter.ExecuteSoftwareTriggerAsync(token).ConfigureAwait(false);
            }
            if (continuous && _mode != CameraTriggerMode.Software)
                return await WaitForFrameAsync(token).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(_timeout);
            try { return await WaitForFrameAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { throw new TimeoutException($"等待相机 {_cameraRef.Value} 新帧超时（{_timeout.TotalMilliseconds:F0} ms）"); }
        }
        finally
        {
            TaskCompletionSource<bool>? drained = null;
            lock (_gate) { if (--_activeReads == 0) { drained = _readsDrained; _readsDrained = null; } }
            drained?.TrySetResult(true);
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

    public ValueTask DisposeAsync()
    {
        CameraFrame? pending;
        TaskCompletionSource<bool>? waiter;
        TaskCompletionSource<bool> completion;
        Task readsDrained;
        lock (_gate)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            _disposed = true;
            pending = _pending;
            _pending = null;
            waiter = _waiter;
            _waiter = null;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            readsDrained = _activeReads == 0 ? Task.CompletedTask :
                (_readsDrained ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
        waiter?.TrySetResult(true);
        _ = DrainAsync(pending, readsDrained, completion);
        return new ValueTask(completion.Task);
    }

    private async Task DrainAsync(CameraFrame? pending, Task readsDrained, TaskCompletionSource<bool> completion)
    {
        var failures = new System.Collections.Generic.List<Exception>();
        try { _streamCancellation.Cancel(); } catch (Exception error) { failures.Add(error); }
        Task? stopping = null;
        if (_streamTask is not null)
            try { stopping = _session.Adapter.StopStreamAsync(CancellationToken.None); }
            catch (OperationCanceledException) when (_streamCancellation.IsCancellationRequested) { }
            catch (Exception error) { failures.Add(error); }
        // Stop may be required to unblock an in-flight trigger, but ownership stays
        // until both the trigger/read and the real producer have actually exited.
        try { await readsDrained.ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
        if (stopping is not null)
            try { await stopping.ConfigureAwait(false); }
            catch (OperationCanceledException) when (_streamCancellation.IsCancellationRequested) { }
            catch (Exception error) { failures.Add(error); }
        if (_streamTask is { } stream)
        {
            // The driver may throw before its producer exits. Keep acquisition ownership
            // until the actual stream has ended, including when stop reported an error.
            try { await stream.ConfigureAwait(false); }
            catch (OperationCanceledException) when (_streamCancellation.IsCancellationRequested) { }
            catch (Exception error) { failures.Add(error); }
        }
        try { pending?.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { _streamCancellation.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { _captureLease.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 0) completion.TrySetResult(true);
        else completion.TrySetException(new AggregateException("相机取流排空或清理失败。", failures));
    }
}
