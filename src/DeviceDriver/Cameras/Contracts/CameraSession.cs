using System;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public sealed class CameraSession : IAsyncDisposable
{
    internal CameraSession(CameraDeviceRef cameraRef, CameraAccessMode accessMode, ICameraAdapter adapter,
        Func<CameraSession, ValueTask> disposeRequested, Func<CameraSession, Task> onDisposed)
    {
        CameraRef = cameraRef;
        AccessMode = accessMode;
        Adapter = adapter;
        _disposeRequested = disposeRequested;
        _onDisposed = onDisposed;
    }

    private readonly Func<CameraSession, ValueTask> _disposeRequested;
    private readonly Func<CameraSession, Task> _onDisposed;
    private readonly object _disposeGate = new();
    private Task? _disposeTask;
    private TaskCompletionSource<bool>? _closeCompletion;
    private TaskCompletionSource<bool>? _operationsDrained;
    private int _activeOperations;

    public CameraDeviceRef CameraRef { get; }
    public CameraAccessMode AccessMode { get; }
    public ICameraAdapter Adapter { get; }

    // Public release is coordinated by the manager, which rejects closing an active capture.
    public ValueTask DisposeAsync() => _disposeRequested(this);

    internal Task? CloseResult
    {
        get { lock (_disposeGate) return _closeCompletion?.Task; }
    }

    internal IDisposable AcquireOperation(string purpose)
    {
        lock (_disposeGate)
        {
            if (_closeCompletion is not null)
                throw new InvalidOperationException($"相机 {CameraRef.Value} 正在关闭，不能开始{purpose}。");
            ++_activeOperations;
            return new OperationLease(this);
        }
    }

    internal (TaskCompletionSource<bool> Completion, Task OperationsDrained) BeginClose(bool waitForOperations)
    {
        lock (_disposeGate)
        {
            if (_closeCompletion is not null)
                return (_closeCompletion, _operationsDrained?.Task ?? Task.CompletedTask);
            if (_activeOperations != 0 && !waitForOperations)
                throw new InvalidOperationException($"相机 {CameraRef.Value} 存在进行中的参数或设备操作，请等待完成后关闭。");
            _closeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_activeOperations != 0)
                _operationsDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return (_closeCompletion, _operationsDrained?.Task ?? Task.CompletedTask);
        }
    }

    private sealed class OperationLease(CameraSession owner) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _released, 1) != 0) return;
            TaskCompletionSource<bool>? drained = null;
            lock (owner._disposeGate)
            {
                if (--owner._activeOperations == 0)
                { drained = owner._operationsDrained; owner._operationsDrained = null; }
            }
            drained?.TrySetResult(true);
        }
    }

    internal Task DisposeCoreAsync()
    {
        TaskCompletionSource<bool> completion;
        lock (_disposeGate)
        {
            if (_disposeTask is not null) return _disposeTask;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
        }
        _ = ReleaseAsync(completion);
        return completion.Task;
    }

    private async Task ReleaseAsync(TaskCompletionSource<bool> completion)
    {
        Exception? failure = null;
        try { await Adapter.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { failure = error; }
        try { await _onDisposed(this).ConfigureAwait(false); }
        catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
        if (failure is null) completion.TrySetResult(true);
        else completion.TrySetException(failure);
    }
}
