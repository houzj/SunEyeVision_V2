using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Devices.Camera;

/// <summary>
/// 一次采集得到的统一图像帧。
/// Frame 持有 Image 的所有权，处理完成后必须释放 Frame。
/// </summary>
public sealed class CameraFrame : IDisposable
{
    private bool _disposed;

    public CameraFrame(
        CameraDeviceRef cameraRef,
        long frameId,
        DateTimeOffset timestamp,
        IVisionImage image,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        CameraRef = cameraRef ?? throw new ArgumentNullException(nameof(cameraRef));
        Image = image ?? throw new ArgumentNullException(nameof(image));
        FrameId = frameId;
        Timestamp = timestamp;
        Metadata = metadata ?? new Dictionary<string, object?>();
    }

    public CameraDeviceRef CameraRef { get; }
    public long FrameId { get; }
    public DateTimeOffset Timestamp { get; }
    public IVisionImage Image { get; }
    public IReadOnlyDictionary<string, object?> Metadata { get; }
    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Image.Dispose();
    }
}
