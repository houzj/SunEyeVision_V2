using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

#if MVS_SDK_AVAILABLE
using MvCamCtrl.NET;
#endif

namespace SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;

public sealed class MvsCameraAdapter : ICameraAdapter
{
#if MVS_SDK_AVAILABLE
    private const int MaxFrameBufferBytes = 64 * 1024 * 1024;
    private MyCamera.MV_CC_DEVICE_INFO _deviceInfo;
    private readonly MyCamera _camera = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;
    private bool _opened;
    private bool _grabbing;
    private bool _disposed;
#endif

    public MvsCameraAdapter(CameraDescriptor descriptor
#if MVS_SDK_AVAILABLE
        , MyCamera.MV_CC_DEVICE_INFO deviceInfo
#endif
    )
    {
        Descriptor = descriptor;
#if MVS_SDK_AVAILABLE
        _deviceInfo = deviceInfo;
#endif
    }

    public string AdapterId => "hikvision-mvs";
    public CameraDescriptor Descriptor { get; }
    public CameraCapabilities Capabilities { get; } = new()
    {
        SupportsContinuousCapture = true,
        SupportsHardwareTrigger = true,
        SupportsSoftwareTrigger = true,
    };
    public CameraState State { get; private set; } = CameraState.Offline;

    public Task ConnectAsync(CameraConnectionOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if MVS_SDK_AVAILABLE
        ThrowIfDisposed();
        if (_opened) return Task.CompletedTask;
        var error = _camera.MV_CC_CreateDevice_NET(ref UnsafeCopyDeviceInfo());
        if (error != MyCamera.MV_OK)
            throw new CameraAdapterException(CameraErrorCode.ConnectionRejected, $"MVS 创建设备句柄失败: 0x{error:X8}");
        error = _camera.MV_CC_OpenDevice_NET();
        if (error != MyCamera.MV_OK)
        {
            _camera.MV_CC_DestroyDevice_NET();
            throw new CameraAdapterException(CameraErrorCode.ConnectionRejected, $"MVS 打开设备失败: 0x{error:X8}");
        }
        _opened = true;
        State = CameraState.Connected;
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        if (_grabbing) StopGrabbing();
        if (_opened)
        {
            _camera.MV_CC_CloseDevice_NET();
            _camera.MV_CC_DestroyDevice_NET();
            _opened = false;
        }
        State = CameraState.Offline;
#endif
        return Task.CompletedTask;
    }

    public async Task<CameraFrame> CaptureAsync(CameraCaptureRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if MVS_SDK_AVAILABLE
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            State = CameraState.Capturing;
            var buffer = Marshal.AllocHGlobal(MaxFrameBufferBytes);
            try
            {
                var info = new MyCamera.MV_FRAME_OUT_INFO_EX();
                var timeout = (int)Math.Clamp(request.Timeout.TotalMilliseconds, 1, int.MaxValue);
                var error = _camera.MV_CC_GetOneFrameTimeout_NET(buffer, MaxFrameBufferBytes, ref info, timeout);
                if (error != MyCamera.MV_OK)
                    throw new CameraAdapterException(CameraErrorCode.FrameTimeout, $"MVS 取帧失败: 0x{error:X8}");

                var length = checked((int)info.nFrameLen);
                var bytes = new byte[length];
                Marshal.Copy(buffer, bytes, 0, length);
                var channels = info.enPixelType.ToString().Contains("Mono", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                var image = new MvsVisionImage((int)info.nWidth, (int)info.nHeight, channels, bytes);
                return new CameraFrame(Descriptor.CameraRef, info.nFrameNum, DateTimeOffset.UtcNow, image,
                    new Dictionary<string, object?> { ["pixelType"] = info.enPixelType.ToString(), ["source"] = "MVS" });
            }
            finally { Marshal.FreeHGlobal(buffer); State = CameraState.Connected; }
        }
        finally { _gate.Release(); }
#else
        await Task.Yield();
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }

    public Task StartStreamAsync(CameraCaptureRequest request, ICameraFrameSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        lock (this)
        {
            if (_streamTask is not null)
                throw new InvalidOperationException("MVS 连续采集已在运行。");
            _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _streamTask = RunStreamAsync(request, sink, _streamCancellation.Token);
            return _streamTask;
        }
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }

    public Task StopStreamAsync(CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        Task? streamTask;
        lock (this)
        {
            _streamCancellation?.Cancel();
            streamTask = _streamTask;
        }
        if (streamTask is not null)
            return StopStreamAndWaitAsync(streamTask, cancellationToken);
#endif
        return Task.CompletedTask;
    }

    public Task<CameraParameterSchema> GetParameterSchemaAsync(CancellationToken cancellationToken)
        => Task.FromResult(new CameraParameterSchema
        {
            AdapterId = AdapterId,
            Parameters = new[]
            {
                new CameraParameterDescriptor { Name = "ExposureTime", DisplayName = "曝光时间", Kind = CameraParameterKind.Double, Description = "MVS GenICam 节点 ExposureTime" },
                new CameraParameterDescriptor { Name = "Gain", DisplayName = "增益", Kind = CameraParameterKind.Double, Description = "MVS GenICam 节点 Gain" },
                new CameraParameterDescriptor { Name = "TriggerMode", DisplayName = "触发模式", Kind = CameraParameterKind.Enumeration, Description = "MVS GenICam 节点 TriggerMode" }
            }
        });

    public Task<IReadOnlyDictionary<string, object?>> ReadParametersAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (name.Equals("ExposureTime", StringComparison.OrdinalIgnoreCase) || name.Equals("Gain", StringComparison.OrdinalIgnoreCase))
            {
                var value = new MyCamera.MVCC_FLOATVALUE();
                if (_camera.MV_CC_GetFloatValue_NET(name, ref value) == MyCamera.MV_OK)
                    values[name] = value.fCurValue;
            }
            else if (name.Equals("TriggerMode", StringComparison.OrdinalIgnoreCase))
            {
                var value = new MyCamera.MVCC_ENUMVALUE();
                if (_camera.MV_CC_GetEnumValue_NET(name, ref value) == MyCamera.MV_OK)
                    values[name] = value.nCurValue;
            }
        }
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(values);
#else
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>());
#endif
    }

    public Task ApplyParametersAsync(IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    {
#if MVS_SDK_AVAILABLE
        EnsureConnected();
        foreach (var item in values)
        {
            if (item.Key.Equals("ExposureTime", StringComparison.OrdinalIgnoreCase) || item.Key.Equals("Gain", StringComparison.OrdinalIgnoreCase))
            {
                var value = Convert.ToSingle(item.Value, System.Globalization.CultureInfo.InvariantCulture);
                var error = _camera.MV_CC_SetFloatValue_NET(item.Key, value);
                if (error != MyCamera.MV_OK)
                    throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported, $"MVS 参数 {item.Key} 写入失败: 0x{error:X8}");
            }
            else if (item.Key.Equals("TriggerMode", StringComparison.OrdinalIgnoreCase))
            {
                var value = Convert.ToUInt32(item.Value, System.Globalization.CultureInfo.InvariantCulture);
                var error = _camera.MV_CC_SetEnumValue_NET(item.Key, value);
                if (error != MyCamera.MV_OK)
                    throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported, $"MVS 参数 {item.Key} 写入失败: 0x{error:X8}");
            }
        }
#endif
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
#if MVS_SDK_AVAILABLE
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
#else
        await Task.CompletedTask;
#endif
        State = CameraState.Disposed;
    }

#if MVS_SDK_AVAILABLE
    private ref MyCamera.MV_CC_DEVICE_INFO UnsafeCopyDeviceInfo()
    {
        // 结构体在构造时已由官方 SDK 初始化，保留独立副本避免被调用方修改。
        return ref _deviceInfo;
    }

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!_opened) throw new CameraAdapterException(CameraErrorCode.ConnectionRejected, "相机尚未连接");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void StopGrabbing()
    {
        if (!_grabbing) return;
        _camera.MV_CC_StopGrabbing_NET();
        _grabbing = false;
    }

    private async Task StopStreamAndWaitAsync(Task streamTask, CancellationToken cancellationToken)
    {
        await streamTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (this)
        {
            _streamTask = null;
            _streamCancellation?.Dispose();
            _streamCancellation = null;
            if (!_disposed) State = CameraState.Connected;
        }
    }

    private async Task RunStreamAsync(CameraCaptureRequest request, ICameraFrameSink sink, CancellationToken cancellationToken)
    {
        State = CameraState.Capturing;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await CaptureAsync(request, cancellationToken).ConfigureAwait(false);
                try
                {
                    await sink.OnFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    frame.Dispose();
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed) State = CameraState.Connected;
        }
    }

    private sealed class MvsVisionImage : IVisionImage
    {
        private byte[]? _data;
        public MvsVisionImage(int width, int height, int channels, byte[] data) { Width = width; Height = height; Channels = channels; _data = data; }
        public int Width { get; }
        public int Height { get; }
        public int Channels { get; }
        public bool IsEmpty => _data is null || _data.Length == 0;
        public byte[] GetPixelData() => _data is null ? throw new ObjectDisposedException(nameof(MvsVisionImage)) : (byte[])_data.Clone();
        public IVisionImage Clone() => new MvsVisionImage(Width, Height, Channels, GetPixelData());
        public void Dispose() { _data = null; }
    }
#endif
}
