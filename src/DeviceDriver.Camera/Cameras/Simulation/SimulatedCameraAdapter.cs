using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.DeviceDriver.Cameras.Simulation;

public sealed class SimulatedCameraAdapter : ICameraAdapter
{
    private readonly SimulatedCameraOptions _options;
    private readonly object _gate = new();
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;
    private readonly SemaphoreSlim _softwareTriggerSignal = new(0, int.MaxValue);
    private bool _softwareTriggerStream;
    private long _frameId;
    private CameraState _state = CameraState.Offline;
    private bool _disposed;
    private int _triggerMode;
    private int _triggerSource = 7;

    public SimulatedCameraAdapter(
        CameraDescriptor descriptor,
        SimulatedCameraOptions? options = null)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _options = options ?? new SimulatedCameraOptions
        {
            DeviceId = descriptor.DeviceId,
            DisplayName = descriptor.DisplayName
        };
    }

    public string AdapterId => "simulation";
    public CameraDescriptor Descriptor { get; }
    public CameraCapabilities Capabilities { get; } = new()
    {
        SupportsSoftwareTrigger = true,
        SupportsHardwareTrigger = false,
        SupportsContinuousCapture = true,
        SupportsRoi = true,
        SupportsExposure = true,
        SupportsGain = true,
        SupportedFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ExposureTime", "Gain", "Width", "Height", "PixelFormat", "FrameRate"
        }
    };

    public CameraState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public async Task ConnectAsync(
        CameraConnectionOptions options,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        SetState(CameraState.Connecting);

        try
        {
            await Task.Delay(_options.ConnectDelay, cancellationToken).ConfigureAwait(false);
            if (_options.FailConnection)
            {
                SetState(CameraState.Faulted);
                throw new InvalidOperationException("Simulated connection failure.");
            }

            SetState(CameraState.Connected);
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SetState(CameraState.Faulted);
            }

            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await StopStreamAsync(cancellationToken).ConfigureAwait(false);
        SetState(CameraState.Offline);
    }

    public async Task<ICameraFrameLease> CaptureAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);request.Validate();
        ThrowIfDisposed();
        EnsureConnected();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        await Task.Delay(_options.FrameDelay, timeout.Token).ConfigureAwait(false);

        if (_options.FailCapture)
        {
            throw new TimeoutException("Simulated capture timeout.");
        }

        return CreateFrame(request.ProducerRun);
    }

    public Task StartStreamAsync(
        CameraCaptureRequest request,
        ICameraFrameSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);request.Validate();
        if(request.Purpose!=CameraRequestPurpose.Preview)throw new InvalidOperationException("设备预览流不能持有工作流身份。");
        ArgumentNullException.ThrowIfNull(sink);
        ThrowIfDisposed();
        EnsureConnected();

        lock (_gate)
        {
            if (_streamTask != null)
            {
                throw new InvalidOperationException("Simulated stream is already running.");
            }

            _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _softwareTriggerStream = request.TriggerMode == CameraTriggerMode.Software;
            SetStateUnsafe(CameraState.Capturing);
            _streamTask = RunStreamAsync(sink, request.TriggerMode, _streamCancellation.Token);
            return _streamTask;
        }
    }

    public Task ExecuteSoftwareTriggerAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_streamTask is null || !_softwareTriggerStream)
                throw new InvalidOperationException("请先启动软件触发预览。");
        }

        _softwareTriggerSignal.Release();
        return Task.CompletedTask;
    }

    public Task ExecuteCommandAsync(string name, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        throw new CameraAdapterException(CameraErrorCode.ParameterUnsupported,
            $"模拟相机未实现命令节点：{name}");
    }

    public async Task StopStreamAsync(CancellationToken cancellationToken)
    {
        Task? streamTask;
        lock (_gate)
        {
            _streamCancellation?.Cancel();
            streamTask = _streamTask;
        }

        if (streamTask != null)
        {
            await streamTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _streamTask = null;
            _streamCancellation?.Dispose();
            _streamCancellation = null;
            if (_state == CameraState.Capturing)
            {
                SetStateUnsafe(CameraState.Connected);
            }
        }
    }

    public Task<CameraParameterSchema> GetParameterSchemaAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return Task.FromResult(new CameraParameterSchema
        {
            AdapterId = AdapterId,
            Parameters = new[]
            {
                new CameraParameterDescriptor
                {
                    Name = "Width", DisplayName = "宽度", StandardFeature = "image.width", Category = "图像格式",
                    Kind = CameraParameterKind.Integer, Value = _options.Width,
                    Minimum = 1, Maximum = _options.Width, IsReadOnly = true
                },
                new CameraParameterDescriptor
                {
                    Name = "Height", DisplayName = "高度", StandardFeature = "image.height", Category = "图像格式",
                    Kind = CameraParameterKind.Integer, Value = _options.Height,
                    Minimum = 1, Maximum = _options.Height, IsReadOnly = true
                },
                new CameraParameterDescriptor
                {
                    Name = "FrameRate", DisplayName = "帧率", StandardFeature = "acquisition.frame-rate", Category = "采集",
                    Kind = CameraParameterKind.Double, Value = _options.FrameRate,
                    Minimum = 1d, Maximum = 240d, Increment = 0.1d
                },
                new CameraParameterDescriptor
                {
                    Name = "TriggerMode", DisplayName = "触发模式", StandardFeature = "trigger.mode", Category = "触发",
                    Kind = CameraParameterKind.Enumeration, Value = _triggerMode.ToString(CultureInfo.InvariantCulture),
                    AffectsSchema = true,
                    EnumValues = new[] { new CameraEnumValue("0", "关闭"), new CameraEnumValue("1", "开启") }
                },
                new CameraParameterDescriptor
                {
                    Name = "TriggerSource", DisplayName = "触发源", StandardFeature = "trigger.source", Category = "触发",
                    Kind = CameraParameterKind.Enumeration, Value = _triggerSource.ToString(CultureInfo.InvariantCulture),
                    EnumValues = new[] { new CameraEnumValue("7", "Software") }
                }
            }
        });
    }

    public Task<IReadOnlyDictionary<string, object?>> ReadParametersAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (name.Equals("Width", StringComparison.OrdinalIgnoreCase)) values[name] = _options.Width;
            if (name.Equals("Height", StringComparison.OrdinalIgnoreCase)) values[name] = _options.Height;
            if (name.Equals("FrameRate", StringComparison.OrdinalIgnoreCase)) values[name] = _options.FrameRate;
            if (name.Equals("TriggerMode", StringComparison.OrdinalIgnoreCase)) values[name] = _triggerMode.ToString(CultureInfo.InvariantCulture);
            if (name.Equals("TriggerSource", StringComparison.OrdinalIgnoreCase)) values[name] = _triggerSource.ToString(CultureInfo.InvariantCulture);
        }

        return Task.FromResult<IReadOnlyDictionary<string, object?>>(values);
    }

    public Task ApplyParametersAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var pair in values)
        {
            if (pair.Value is null)
                continue;
            if (pair.Key.Equals("TriggerMode", StringComparison.OrdinalIgnoreCase))
                _triggerMode = Convert.ToInt32(pair.Value, CultureInfo.InvariantCulture);
            else if (pair.Key.Equals("TriggerSource", StringComparison.OrdinalIgnoreCase))
                _triggerSource = Convert.ToInt32(pair.Value, CultureInfo.InvariantCulture);
        }
        return Task.CompletedTask;
    }

    public Task<CameraCaptureConfiguration> ReadCaptureConfigurationAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var mode = _triggerMode == 0
            ? CameraTriggerMode.Continuous
            : _triggerSource == 7 ? CameraTriggerMode.Software : CameraTriggerMode.Hardware;
        return Task.FromResult(new CameraCaptureConfiguration
        {
            StreamMode = mode,
            AcquisitionModeName = "连续",
            TriggerSourceName = _triggerMode == 0 ? string.Empty : _triggerSource == 7 ? "软件触发" : "外部触发",
            IsValid = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await StopStreamAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            SetState(CameraState.Disposed);
            _softwareTriggerSignal.Dispose();
        }
    }

    private async Task RunStreamAsync(ICameraFrameSink sink, CameraTriggerMode triggerMode, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (triggerMode == CameraTriggerMode.Software)
                    await _softwareTriggerSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(_options.FrameDelay, cancellationToken).ConfigureAwait(false);
                var frameNumber = Interlocked.Read(ref _frameId) + 1;
                if (_options.DropEveryNthFrame > 0 && frameNumber % _options.DropEveryNthFrame == 0)
                {
                    Interlocked.Increment(ref _frameId);
                    continue;
                }

                var frame = CreateFrame(null);
                try
                {
                    await sink.OnFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await frame.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (!_disposed && _state == CameraState.Capturing)
                {
                    SetStateUnsafe(CameraState.Connected);
                }
            }
        }
    }

    private ICameraFrameLease CreateFrame(RunIdentity? producerRun)
    {
        var frameId=Interlocked.Increment(ref _frameId);
        var pixels=new byte[checked(_options.Width*_options.Height)];
        for(var index=0;index<pixels.Length;index++)pixels[index]=(byte)((index+frameId)%256);
        var domain=Enumerable.Range(0,_options.Height).Select(row=>new RegionRun(row,0,_options.Width-1)).ToArray();
        return new CapturedCameraFrame(Descriptor.CameraRef.Value,frameId,DateTimeOffset.UtcNow,
            new ImageLayout(_options.Width,_options.Height,_options.Width,PixelLayout.Gray8),pixels,domain,producerRun);
    }
    private void EnsureConnected()
    {
        if (State is not CameraState.Connected and not CameraState.Capturing)
        {
            throw new InvalidOperationException("Simulated camera is not connected.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SimulatedCameraAdapter));
        }
    }

    private void SetState(CameraState state)
    {
        lock (_gate)
        {
            SetStateUnsafe(state);
        }
    }

    private void SetStateUnsafe(CameraState state) => _state = state;

}
