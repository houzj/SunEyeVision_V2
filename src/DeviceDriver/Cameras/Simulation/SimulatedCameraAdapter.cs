using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.DeviceDriver.Cameras.Simulation;

public sealed class SimulatedCameraAdapter : ICameraAdapter
{
    private readonly SimulatedCameraOptions _options;
    private readonly object _gate = new();
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;
    private long _frameId;
    private CameraState _state = CameraState.Offline;
    private bool _disposed;

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

    public async Task<CameraFrame> CaptureAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureConnected();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        await Task.Delay(_options.FrameDelay, timeout.Token).ConfigureAwait(false);

        if (_options.FailCapture)
        {
            throw new TimeoutException("Simulated capture timeout.");
        }

        return CreateFrame();
    }

    public Task StartStreamAsync(
        CameraCaptureRequest request,
        ICameraFrameSink sink,
        CancellationToken cancellationToken)
    {
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
            SetStateUnsafe(CameraState.Capturing);
            _streamTask = RunStreamAsync(sink, _streamCancellation.Token);
            return _streamTask;
        }
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
                    Name = "Width", DisplayName = "宽度", Category = "图像格式",
                    Kind = CameraParameterKind.Integer, Value = _options.Width,
                    Minimum = 1, Maximum = _options.Width, IsReadOnly = true
                },
                new CameraParameterDescriptor
                {
                    Name = "Height", DisplayName = "高度", Category = "图像格式",
                    Kind = CameraParameterKind.Integer, Value = _options.Height,
                    Minimum = 1, Maximum = _options.Height, IsReadOnly = true
                },
                new CameraParameterDescriptor
                {
                    Name = "FrameRate", DisplayName = "帧率", Category = "采集",
                    Kind = CameraParameterKind.Double, Value = _options.FrameRate,
                    Minimum = 1d, Maximum = 240d, Increment = 0.1d
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
        }

        return Task.FromResult<IReadOnlyDictionary<string, object?>>(values);
    }

    public Task ApplyParametersAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return Task.CompletedTask;
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
        }
    }

    private async Task RunStreamAsync(ICameraFrameSink sink, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_options.FrameDelay, cancellationToken).ConfigureAwait(false);
                var frameNumber = Interlocked.Read(ref _frameId) + 1;
                if (_options.DropEveryNthFrame > 0 && frameNumber % _options.DropEveryNthFrame == 0)
                {
                    Interlocked.Increment(ref _frameId);
                    continue;
                }

                var frame = CreateFrame();
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
            lock (_gate)
            {
                if (!_disposed && _state == CameraState.Capturing)
                {
                    SetStateUnsafe(CameraState.Connected);
                }
            }
        }
    }

    private CameraFrame CreateFrame()
    {
        var frameId = Interlocked.Increment(ref _frameId);
        var image = new SimulatedVisionImage(_options.Width, _options.Height, frameId);
        return new CameraFrame(
            Descriptor.CameraRef,
            frameId,
            DateTimeOffset.UtcNow,
            image,
            new Dictionary<string, object?>
            {
                ["Source"] = "Simulation",
                ["FrameRate"] = _options.FrameRate
            });
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

    private sealed class SimulatedVisionImage : IVisionImage
    {
        private byte[]? _pixels;

        public SimulatedVisionImage(int width, int height, long frameId)
        {
            Width = width;
            Height = height;
            _pixels = new byte[width * height];
            for (var index = 0; index < _pixels.Length; index++)
            {
                _pixels[index] = (byte)((index + frameId) % 256);
            }
        }

        public int Width { get; }
        public int Height { get; }
        public int Channels => 1;
        public bool IsEmpty => _pixels == null;

        public byte[] GetPixelData() => _pixels ?? Array.Empty<byte>();

        public IVisionImage Clone()
        {
            var clone = new SimulatedVisionImage(Width, Height, 0);
            if (_pixels != null)
            {
                Buffer.BlockCopy(_pixels, 0, clone._pixels!, 0, _pixels.Length);
            }

            return clone;
        }

        public void Dispose() => _pixels = null;
    }
}
