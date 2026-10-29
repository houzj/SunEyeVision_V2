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

    public CameraDeviceRef CameraRef { get; }
    public CameraAccessMode AccessMode { get; }
    public ICameraAdapter Adapter { get; }

    // Public release is coordinated by the manager, which rejects closing an active capture.
    public ValueTask DisposeAsync() => _disposeRequested(this);

    internal Task? CloseResult
    {
        get { lock (_disposeGate) return _closeCompletion?.Task; }
    }

    internal TaskCompletionSource<bool> BeginClose()
    {
        lock (_disposeGate)
            return _closeCompletion ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
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
