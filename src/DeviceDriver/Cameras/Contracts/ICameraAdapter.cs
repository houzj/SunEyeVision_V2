using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public interface ICameraFrameSink
{
    /// <summary>
    /// 接收方在返回成功后取得 frame 所有权，并负责释放它。
    /// </summary>
    ValueTask OnFrameAsync(CameraFrame frame, CancellationToken cancellationToken);
}

public interface ICameraAdapter : IAsyncDisposable
{
    string AdapterId { get; }
    CameraDescriptor Descriptor { get; }
    CameraCapabilities Capabilities { get; }
    CameraState State { get; }

    Task ConnectAsync(
        CameraConnectionOptions options,
        CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    Task<CameraFrame> CaptureAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken);

    Task StartStreamAsync(
        CameraCaptureRequest request,
        ICameraFrameSink sink,
        CancellationToken cancellationToken);

    Task StopStreamAsync(CancellationToken cancellationToken);

    Task ExecuteSoftwareTriggerAsync(CancellationToken cancellationToken);

    Task<CameraParameterSchema> GetParameterSchemaAsync(
        CancellationToken cancellationToken);

    Task<CameraCaptureConfiguration> ReadCaptureConfigurationAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object?>> ReadParametersAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken);

    Task ApplyParametersAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken);

    Task ExecuteCommandAsync(string name, CancellationToken cancellationToken);
}
