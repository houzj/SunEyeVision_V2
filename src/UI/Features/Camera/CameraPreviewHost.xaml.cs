using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Vision.Hosting;

namespace SunEyeVision.UI.Features.Camera;

/// <summary>
/// 相机预览只负责显示设备实际采集帧；采集/触发命令由参数面板执行。
/// </summary>
public partial class CameraPreviewHost : UserControl, ICameraFrameSink, IAsyncDisposable
{
    private readonly CameraRuntimeManager _runtime;
    private readonly CameraDeviceRef _cameraRef;
    private readonly bool _ownsSession;
    private readonly CancellationTokenSource _lifetime = new();
    private CameraSession? _session;
    private Task? _streamTask;
    private TaskCompletionSource<bool>? _startingStream;
    private CancellationTokenSource? _streamCancellation;
    private Task<CameraSession>? _opening;
    private Task? _drainTask;
    private int _activeConfigurations;
    private TaskCompletionSource<bool>? _configurationsDrained;
    private int _activeTriggers;
    private TaskCompletionSource<bool>? _triggersDrained;
    private Task? _disposeTask;
    private readonly object _disposeGate = new();
    private IDisposable? _captureLease;
    private CameraTriggerMode _activeMode = CameraTriggerMode.Continuous;
    private string _activeDescription = "准备预览";
    private string? _activeCorrelationId;
    private IReadOnlyDictionary<string, object?> _activeCaptureParameters = new Dictionary<string, object?>();
    private bool _configurationValid;
    private bool _activeConfigurationStale;
    private bool _singleFrameMode;
    private long _displayedFrameCount;
    private long _previewGeneration;
    private int _stopped;
    private int _singleFrameStopScheduled;
    private int _singleShotRequested;
    private int _stopRequested;

    public event EventHandler? CaptureStateChanged;
    public bool IsAcquiring { get; private set; }
    public bool IsSingleCaptureInProgress => IsAcquiring
        && (Volatile.Read(ref _singleShotRequested) != 0
            || Volatile.Read(ref _singleFrameStopScheduled) != 0);
    public bool CanStartAcquisition => !IsAcquiring && _configurationValid;
    public bool CanStopAcquisition => IsAcquiring;
    public bool CanToggleAcquisition => IsAcquiring
        ? !IsSingleCaptureInProgress
        : _configurationValid;
    public bool CanCaptureSingle => _configurationValid
        && (!IsAcquiring || !_activeConfigurationStale) && !IsSingleCaptureInProgress;
    public bool CanSoftwareTrigger => IsAcquiring
        && _activeMode == CameraTriggerMode.Software
        && _configurationValid && !_activeConfigurationStale
        && _session?.Adapter.Capabilities.SupportsSoftwareTrigger == true;
    public string ConfigurationSummary { get; private set; } = "正在读取设备采集配置";
    public string CaptureStatus { get; private set; } = "已连接，尚未开始采集";

    public CameraPreviewHost(CameraRuntimeManager runtime, CameraDeviceRef cameraRef, CameraSession? existingSession = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _cameraRef = cameraRef ?? throw new ArgumentNullException(nameof(cameraRef));
        _session = existingSession;
        _ownsSession = existingSession is null;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _ = RefreshConfigurationAsync();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        try { await DisposeAsync(); }
        catch (Exception error) { VisionLogger.Instance.Log(LogLevel.Error, "相机预览卸载清理失败：" + error.Message, "CameraPreviewTrace", error, LogTarget.All); }
    }

    private async Task EnsureSessionAsync()
    {
        if (Volatile.Read(ref _stopped) != 0) throw new OperationCanceledException("相机预览窗口已关闭。");
        if (_session is not null)
        {
            if (!_runtime.TryGetSession(_cameraRef, out var current) || !ReferenceEquals(current, _session))
                throw new InvalidOperationException("相机会话已关闭或重连，请重新打开预览。");
            return;
        }
        _opening ??= _runtime.OpenAsync(_cameraRef, CameraAccessMode.Preview, cancellationToken: _lifetime.Token);
        var opening = _opening;
        CameraSession session;
        try { session = await opening.ConfigureAwait(true); }
        catch { if (ReferenceEquals(_opening, opening)) _opening = null; throw; }
        // Dispose waits for the same open task and owns cleanup after closure.
        if (Volatile.Read(ref _stopped) != 0) throw new OperationCanceledException("相机预览窗口已关闭。");
        _session = session;
    }

    public async Task RefreshConfigurationAsync(string? correlationId = null)
    {
        if (!Dispatcher.CheckAccess())
        { await Dispatcher.InvokeAsync(() => RefreshConfigurationAsync(correlationId)).Task.Unwrap().ConfigureAwait(false); return; }
        if (Volatile.Read(ref _stopped) != 0) throw new OperationCanceledException("相机预览窗口已关闭。");
        ++_activeConfigurations;
        try
        {
            await EnsureSessionAsync().ConfigureAwait(true);
            var session = _session!;
            using var operation = _runtime.AcquireSessionOperationLease(session, "读取预览采集配置");
            var configuration = await session.Adapter.ReadCaptureConfigurationAsync(_lifetime.Token).ConfigureAwait(true);
            if (Volatile.Read(ref _stopped) != 0) return;
            var generation = Interlocked.Increment(ref _previewGeneration);
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[camera-trace={correlationId ?? "preview-open"}] 标准化采集配置 camera={_cameraRef.Value} adapter={_session.Adapter.AdapterId} generation={generation} acquisition={configuration.AcquisitionModeName} streamMode={configuration.StreamMode} triggerSource={configuration.TriggerSourceName} valid={configuration.IsValid}",
                "CameraPreviewTrace", target: LogTarget.All);
            _configurationValid = configuration.IsValid;
            var sourceDescription = string.IsNullOrWhiteSpace(configuration.TriggerSourceName)
                ? configuration.StatusMessage
                : configuration.TriggerSourceName;
            var configuredDescription = configuration.StreamMode switch
            {
                CameraTriggerMode.Software => $"{configuration.AcquisitionModeName} · 等待软件触发",
                CameraTriggerMode.Hardware => $"{configuration.AcquisitionModeName} · 等待外部触发 {sourceDescription}",
                _ => $"{configuration.AcquisitionModeName} · {sourceDescription ?? "连续取流"}"
            };
            ConfigurationSummary = string.IsNullOrWhiteSpace(sourceDescription)
                ? configuration.AcquisitionModeName
                : $"{configuration.AcquisitionModeName} · {sourceDescription}";
            if (IsAcquiring)
            {
                // A running request keeps its original mode until stopped. Never relabel that stream
                // as the newly read device mode merely because a parameter changed elsewhere.
                _activeConfigurationStale |= !configuration.IsValid
                    || _activeMode != configuration.StreamMode
                    || _singleFrameMode != configuration.IsSingleFrame
                    || !string.Equals(_activeDescription, configuredDescription, StringComparison.Ordinal);
                CaptureStatus = _activeConfigurationStale
                    ? "设备采集配置已改变，请停止后重新启动"
                    : _activeMode == CameraTriggerMode.Software ? "采集中，等待软件触发"
                    : _activeMode == CameraTriggerMode.Hardware ? "采集中，等待外部触发" : "采集中";
            }
            else
            {
                _activeMode = configuration.StreamMode;
                _singleFrameMode = configuration.IsSingleFrame;
                _activeDescription = configuredDescription;
                _activeCaptureParameters = new Dictionary<string, object?>();
                _activeConfigurationStale = false;
                CaptureStatus = configuration.IsValid ? "已连接，尚未开始采集"
                    : configuration.StatusMessage ?? "采集配置无效";
            }
            RaiseCaptureStateChanged();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[camera-trace={correlationId ?? "preview-config"}] 读取相机采集配置失败 camera={_cameraRef.Value}: {ex.Message}",
                "CameraPreviewTrace", ex, LogTarget.All);
            _configurationValid = false;
            ConfigurationSummary = ex.Message;
            if (IsLoaded)
            {
                CaptureStatus = ex.Message;
                RaiseCaptureStateChanged();
            }
        }
        finally
        {
            if (--_activeConfigurations == 0)
            {
                var drained = _configurationsDrained; _configurationsDrained = null;
                drained?.TrySetResult(true);
            }
        }
    }

    public async Task StartAcquisitionAsync()
    {
        if (Interlocked.CompareExchange(ref _stopped, 0, 0) != 0)
            throw new InvalidOperationException("相机预览窗口已关闭。");
        if (IsAcquiring)
            return;

        await RefreshConfigurationAsync().ConfigureAwait(true);
        if (!_configurationValid)
            throw new InvalidOperationException(ConfigurationSummary);

        await StartStreamAsync(_activeMode, _activeCaptureParameters, _activeDescription, "user-start").ConfigureAwait(true);
        if (_streamTask is not null)
        {
            IsAcquiring = true;
            CaptureStatus = _activeMode switch
            {
                CameraTriggerMode.Software => "采集已启动，等待软件触发",
                CameraTriggerMode.Hardware => "采集已启动，等待外部触发",
                _ when _singleFrameMode => "单帧采集已启动，等待图像",
                _ => "连续采集已启动"
            };
            RaiseCaptureStateChanged();
        }
    }

    public async Task StopAcquisitionAsync()
    {
        if (!Dispatcher.CheckAccess())
        { await Dispatcher.InvokeAsync(StopAcquisitionAsync).Task.Unwrap().ConfigureAwait(false); return; }
        if (_startingStream is { } starting)
            await starting.Task.ConfigureAwait(true);
        var stream = _streamTask;
        if (stream is null) { if (_drainTask is not null) await _drainTask.ConfigureAwait(true); return; }
        Interlocked.Exchange(ref _singleShotRequested, 0);
        await DrainStreamAsync(stream).ConfigureAwait(true);
        if (Volatile.Read(ref _stopped) == 0 && _streamTask is null) CaptureStatus = "采集已停止；图像控件保留最后一帧";
    }

    private Task DrainStreamAsync(Task stream)
    {
        if (_drainTask is not null) return _drainTask;
        if (!ReferenceEquals(_streamTask, stream)) return Task.CompletedTask;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _drainTask = completion.Task;
        Interlocked.Exchange(ref _stopRequested, 1);
        _ = DrainStreamCoreAsync(stream, _session!, _streamCancellation, _captureLease, completion);
        return completion.Task;
    }

    private async Task DrainStreamCoreAsync(Task stream, CameraSession session, CancellationTokenSource? cancellation,
        IDisposable? lease, TaskCompletionSource<bool> completion)
    {
        var failures = new List<Exception>();
        try { cancellation?.Cancel(); } catch (Exception error) { failures.Add(error); }
        Task? stopping = null;
        try { stopping = session.Adapter.StopStreamAsync(CancellationToken.None); }
        catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true) { }
        catch (Exception error) { failures.Add(error); }
        if (_activeTriggers != 0)
        {
            _triggersDrained ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try { await _triggersDrained.Task.ConfigureAwait(true); } catch (Exception error) { failures.Add(error); }
        }
        if (stopping is not null)
            try { await stopping.ConfigureAwait(true); }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true) { }
            catch (Exception error) { failures.Add(error); }
        try { await stream.ConfigureAwait(true); }
        catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true) { }
        catch (Exception error) { failures.Add(error); }
        try { cancellation?.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { lease?.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (ReferenceEquals(_streamTask, stream))
        {
            _streamTask = null; _streamCancellation = null; _captureLease = null;
            IsAcquiring = false; Interlocked.Exchange(ref _singleFrameStopScheduled, 0);
            Interlocked.Exchange(ref _stopRequested, 0);
            _drainTask = null;
            try { RaiseCaptureStateChanged(); } catch (Exception error) { failures.Add(error); }
        }
        else if (ReferenceEquals(_drainTask, completion.Task)) _drainTask = null;
        if (failures.Count == 0) completion.TrySetResult(true);
        else completion.TrySetException(new AggregateException("相机预览取流排空或清理失败。", failures));
    }

    public async Task CaptureSingleFrameAsync()
    {
        if (Interlocked.CompareExchange(ref _stopped, 0, 0) != 0)
            throw new InvalidOperationException("相机预览窗口已关闭。");
        await RefreshConfigurationAsync("single-capture").ConfigureAwait(true);
        if (!_configurationValid)
            throw new InvalidOperationException(ConfigurationSummary);
        if (_activeConfigurationStale)
            throw new InvalidOperationException("设备采集配置已改变，请先停止并重新启动采集。");

        Interlocked.Exchange(ref _singleShotRequested, 1);
        RaiseCaptureStateChanged();
        try
        {
            if (!IsAcquiring)
                await StartAcquisitionAsync().ConfigureAwait(true);
            if (Volatile.Read(ref _stopped) != 0 || Volatile.Read(ref _stopRequested) != 0)
                throw new OperationCanceledException("相机预览已停止。");

            if (_activeMode == CameraTriggerMode.Software)
            {
                await TriggerOnceAsync().ConfigureAwait(true);
                if (Volatile.Read(ref _stopped) != 0 || Volatile.Read(ref _stopRequested) != 0)
                    throw new OperationCanceledException("相机预览已停止。");
                CaptureStatus = "单次采集已触发，等待图像";
            }
            else if (_activeMode == CameraTriggerMode.Hardware)
            {
                CaptureStatus = "单次采集已启动，等待一次外部触发";
            }
            else
            {
                CaptureStatus = "单次采集已启动，收到下一帧后停止";
            }
            RaiseCaptureStateChanged();
        }
        catch
        {
            Interlocked.Exchange(ref _singleShotRequested, 0);
            throw;
        }
    }

    public async Task TriggerOnceAsync()
    {
        if (!Dispatcher.CheckAccess())
        { await Dispatcher.InvokeAsync(TriggerOnceAsync).Task.Unwrap().ConfigureAwait(false); return; }
        if (Volatile.Read(ref _stopped) != 0 || Volatile.Read(ref _stopRequested) != 0
            || !CanSoftwareTrigger || _session is null || _streamTask is null)
            throw new InvalidOperationException("当前设备配置不允许软件触发；请检查采集状态、触发模式和触发源。");

        ++_activeTriggers; // Register before invoking a driver that may synchronously close this host.
        try
        {
            await _session.Adapter.ExecuteSoftwareTriggerAsync(_lifetime.Token).ConfigureAwait(true);
            if (Volatile.Read(ref _stopped) == 0 && Volatile.Read(ref _stopRequested) == 0)
            {
                CaptureStatus = "软件触发命令已发送，等待相机帧";
                RaiseCaptureStateChanged();
            }
        }
        finally
        {
            if (--_activeTriggers == 0)
            {
                var drained = _triggersDrained; _triggersDrained = null;
                drained?.TrySetResult(true);
            }
        }
    }

    private void RaiseCaptureStateChanged() => CaptureStateChanged?.Invoke(this, EventArgs.Empty);

    private async Task StartStreamAsync(
        CameraTriggerMode mode,
        IReadOnlyDictionary<string, object?>? parameters,
        string description,
        string? correlationId)
    {
        await EnsureSessionAsync().ConfigureAwait(true);
        if (Volatile.Read(ref _stopped) != 0)
            throw new OperationCanceledException("相机预览已关闭。");
        if (_streamTask is not null || _startingStream is not null)
            throw new InvalidOperationException("采集任务状态不一致：已有取流任务。");

        // Publish before calling the driver: it may synchronously reenter DisposeAsync.
        var starting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _startingStream = starting;
        IDisposable? acquired = null;
        Task? started = null;
        Exception? cleanupFailure = null;
        try
        {
            acquired = _runtime.AcquireCaptureLease(_cameraRef, "相机设置预览");
            _captureLease = acquired;
            if (Volatile.Read(ref _stopped) != 0)
                throw new OperationCanceledException("相机预览已关闭。");
            if (!_runtime.TryGetSession(_cameraRef, out var current) || !ReferenceEquals(_session, current))
                throw new InvalidOperationException("相机会话已关闭或重连，请重新打开预览。");

            _activeMode = mode;
            _activeDescription = description;
            _activeCorrelationId = correlationId ?? "user-start";
            Interlocked.Exchange(ref _displayedFrameCount, 0);
            Interlocked.Exchange(ref _singleFrameStopScheduled, 0);
            StatusText.Text = mode == CameraTriggerMode.Hardware
                ? $"{description} · 等待相机触发信号"
                : mode == CameraTriggerMode.Software
                    ? $"{description} · 等待软件触发"
                    : $"{description} · 正在启动";

            var request = new CameraCaptureRequest
            {
                CameraRef = _cameraRef,
                TriggerMode = mode,
                Timeout = TimeSpan.FromMilliseconds(mode == CameraTriggerMode.Continuous ? 2000 : 800),
                DiagnosticCorrelationId = _activeCorrelationId,
                Parameters = parameters ?? new Dictionary<string, object?>()
            };
            _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            started = _session!.Adapter.StartStreamAsync(request, this, _streamCancellation.Token);
            _streamTask = started;
            _ = ObserveStreamAsync(started);
            if (Volatile.Read(ref _stopped) != 0)
                throw new OperationCanceledException("相机预览已关闭。");
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[camera-trace={_activeCorrelationId}] 用户启动相机取流 camera={_cameraRef.Value} generation={_previewGeneration} requestMode={mode} description={description}",
                "CameraPreviewTrace", target: LogTarget.All);
        }
        catch (Exception failure)
        {
            // A rejected second start does not own or stop the first stream.
            if (acquired is null) throw;
            Exception? cleanup = null;
            try
            {
                if (started is not null && ReferenceEquals(_streamTask, started))
                    await DrainStreamAsync(started).ConfigureAwait(true);
                else if (started is null && ReferenceEquals(_captureLease, acquired))
                { _streamCancellation?.Dispose(); _streamCancellation = null; _captureLease = null; acquired.Dispose(); }
            }
            catch (Exception error) { cleanup = error; cleanupFailure = error; }
            if (Volatile.Read(ref _stopped) == 0 && _streamTask is null && _captureLease is null)
            {
                IsAcquiring = false;
                CaptureStatus = "启动采集失败，请检查相机日志";
                RaiseCaptureStateChanged();
            }
            if (cleanup is not null) throw new AggregateException("相机启动和清理均失败。", failure, cleanup);
            throw;
        }
        finally
        {
            if (ReferenceEquals(_startingStream, starting)) _startingStream = null;
            if (cleanupFailure is null) starting.TrySetResult(true);
            else starting.TrySetException(cleanupFailure);
        }
    }

    private async Task ObserveStreamAsync(Task streamTask)
    {
        try
        {
            await streamTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || Volatile.Read(ref _stopRequested) != 0)
        {
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[camera-trace={_activeCorrelationId ?? "preview-open"}] 预览采集任务异常 camera={_cameraRef.Value}: {ex.Message}",
                "CameraPreviewTrace", ex, LogTarget.All);
            if (IsLoaded && ReferenceEquals(_streamTask, streamTask))
                StatusText.Text = $"预览采集已停止：{ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_streamTask, streamTask))
            {
                try { await DrainStreamAsync(streamTask).ConfigureAwait(true); }
                catch (Exception error)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"预览取流排空失败: {error.Message}", "CameraPreviewTrace");
                }
            }
        }
    }

    public async ValueTask OnFrameAsync(CameraFrame frame, CancellationToken cancellationToken)
    {
        try
        {
            if (Volatile.Read(ref _stopped) != 0 || cancellationToken.IsCancellationRequested) return;
            var frameIndex = Interlocked.Increment(ref _displayedFrameCount);
            if (frameIndex == 1 || frameIndex % 120 == 0)
            {
                var pixelType = frame.Metadata.TryGetValue("pixelType", out var value) ? value : "unknown";
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[camera-trace={_activeCorrelationId ?? "preview-open"}] 收到图像帧 camera={_cameraRef.Value} frame={frame.FrameId} streamFrameIndex={frameIndex} size={frame.Image.Width}x{frame.Image.Height} channels={frame.Image.Channels} pixelType={pixelType} frameAgeMs={(DateTimeOffset.UtcNow - frame.Timestamp).TotalMilliseconds:F1}",
                    "CameraPreviewTrace", target: LogTarget.All);
            }
            var bitmap = VisionImageConverterLocator.Current?.ToBitmapSource(frame.Image);
            if (bitmap is null)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[camera-trace={_activeCorrelationId ?? "preview-open"}] 图像转换器返回空图像 camera={_cameraRef.Value} frame={frame.FrameId} size={frame.Image.Width}x{frame.Image.Height} channels={frame.Image.Channels}",
                    "CameraPreviewTrace", target: LogTarget.All);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (Volatile.Read(ref _stopped) == 0 && !cancellationToken.IsCancellationRequested && IsLoaded)
                        StatusText.Text = "预览失败：当前帧无法转换为显示图像";
                });
                return;
            }

            await UpdateImageAsync(bitmap, frame.FrameId, cancellationToken).ConfigureAwait(false);
            if (((_singleFrameMode && frameIndex == 1)
                    || Interlocked.CompareExchange(ref _singleShotRequested, 0, 1) == 1)
                && Interlocked.Exchange(ref _singleFrameStopScheduled, 1) == 0)
            {
                // 不在 SDK 帧回调里直接等待 StopStream，避免停止操作等待当前回调而互锁。
                _ = Dispatcher.BeginInvoke(new Action(async () =>
                {
                    if (Volatile.Read(ref _stopped) != 0 || cancellationToken.IsCancellationRequested) return;
                    try { await StopAcquisitionAsync(); }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Error,
                            $"[camera-trace={_activeCorrelationId ?? "preview-open"}] 单帧采集自动停止失败 camera={_cameraRef.Value}: {ex.Message}",
                            "CameraPreviewTrace", ex, LogTarget.All);
                    }
                }));
            }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[camera-trace={_activeCorrelationId ?? "preview-open"}] 图像转码/提交预览失败 camera={_cameraRef.Value} frame={frame.FrameId}: {ex.Message}",
                "CameraPreviewTrace", ex, LogTarget.All);
            throw;
        }
        finally
        {
            // 转成独立 BitmapSource 后，当前回调释放相机帧。
            frame.Dispose();
        }
    }

    private async Task UpdateImageAsync(BitmapSource bitmap, long frameId, CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            if (Volatile.Read(ref _stopped) != 0 || cancellationToken.IsCancellationRequested) return;
            ImageDisplay.ImageDataSource = CameraBitmapImageData.Create(bitmap);
            if (_activeConfigurationStale)
                StatusText.Text = "设备采集配置已改变，请停止后重新启动";
            else if (_activeMode == CameraTriggerMode.Continuous)
                StatusText.Text = $"{_activeDescription} · 帧 {frameId}";
            else if (_activeMode == CameraTriggerMode.Software)
                StatusText.Text = $"已收到软件触发帧 {frameId}";
            else
                StatusText.Text = $"已收到外部触发 · 帧 {frameId}";
        });
        if (Interlocked.Read(ref _displayedFrameCount) == 1
            || Interlocked.Read(ref _displayedFrameCount) % 120 == 0)
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[camera-trace={_activeCorrelationId ?? "preview-open"}] 图像已提交预览控件 camera={_cameraRef.Value} frame={frameId} streamFrameIndex={Interlocked.Read(ref _displayedFrameCount)} displaySize={bitmap.PixelWidth}x{bitmap.PixelHeight}",
                "CameraPreviewTrace", target: LogTarget.All);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public ValueTask DisposeAsync()
    {
        if (!Dispatcher.CheckAccess())
            return new ValueTask(Dispatcher.InvokeAsync(() => DisposeAsync().AsTask()).Task.Unwrap());
        TaskCompletionSource<bool> completion;
        lock (_disposeGate)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            Interlocked.Exchange(ref _stopped, 1);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
        }
        _ = DisposeCoreAsync(completion);
        return new ValueTask(completion.Task);
    }

    private async Task DisposeCoreAsync(TaskCompletionSource<bool> completion)
    {
        var failures = new List<Exception>();
        try { _lifetime.Cancel(); } catch (Exception error) { failures.Add(error); }
        if (_startingStream is { } starting)
            try { await starting.Task.ConfigureAwait(true); } catch (Exception error) { failures.Add(error); }
        if (_streamTask is { } stream)
            try { await DrainStreamAsync(stream).ConfigureAwait(true); } catch (Exception error) { failures.Add(error); }
        else if (_drainTask is { } draining)
            try { await draining.ConfigureAwait(true); } catch (Exception error) { failures.Add(error); }
        if (_activeConfigurations != 0)
        {
            _configurationsDrained ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try { await _configurationsDrained.Task.ConfigureAwait(true); } catch (Exception error) { failures.Add(error); }
        }
        if (_ownsSession)
        {
            var session = _session;
            if (session is null && _opening is { } opening)
                try { session = await opening.ConfigureAwait(true); }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
                catch (Exception error) { failures.Add(error); }
            if (session is not null)
                try { await session.DisposeAsync().ConfigureAwait(true); } catch (Exception error) { failures.Add(error); }
        }
        _session = null;
        try { ImageDisplay.ImageDataSource = null; } catch (Exception error) { failures.Add(error); }
        try { _lifetime.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 0) completion.TrySetResult(true);
        else completion.TrySetException(new AggregateException("相机预览关闭或清理失败。", failures));
    }
}
