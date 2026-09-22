using System;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public sealed class CameraSession : IAsyncDisposable
{
    internal CameraSession(
        CameraDeviceRef cameraRef,
        CameraAccessMode accessMode,
        ICameraAdapter adapter,
        Func<CameraSession, Task>? onDisposed = null)
    {
        CameraRef = cameraRef;
        AccessMode = accessMode;
        Adapter = adapter;
        _onDisposed = onDisposed;
    }

    private readonly Func<CameraSession, Task>? _onDisposed;
    private int _disposeState;

    public CameraDeviceRef CameraRef { get; }
    public CameraAccessMode AccessMode { get; }
    public ICameraAdapter Adapter { get; }

    public async ValueTask DisposeAsync()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        try
        {
            await Adapter.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            if (_onDisposed is not null)
                await _onDisposed(this).ConfigureAwait(false);
        }
    }
}
