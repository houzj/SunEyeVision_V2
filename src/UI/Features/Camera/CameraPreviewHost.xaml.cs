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
    private readonly CameraRuntimeComposition _runtime;
    private readonly CameraDeviceRef _cameraRef;
    private readonly bool _ownsSession;
    private readonly CancellationTokenSource _lifetime = new();
    private CameraSession? _session;
    private Task? _streamTask;
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

    public CameraPreviewHost(CameraRuntimeComposition runtime, CameraDeviceRef cameraRef, CameraSession? existingSession = null)
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

    private void OnUnloaded(object sender, RoutedEventArgs e) => _ = DisposeAsync().AsTask();

    private async Task EnsureSessionAsync()
    {
        if (_session is null)
            _session = await _runtime.Runtime.OpenAsync(_cameraRef, CameraAccessMode.Preview,
                cancellationToken: _lifetime.Token).ConfigureAwait(true);
    }

    public async Task RefreshConfigurationAsync(string? correlationId = null)
    {
        try
        {
            await EnsureSessionAsync().ConfigureAwait(true);
            var configuration = await _session!.Adapter.ReadCaptureConfigurationAsync(_lifetime.Token).ConfigureAwait(true);
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
            if (IsLoaded)
            {
                _configurationValid = false;
                ConfigurationSummary = "采集配置读取失败";
                CaptureStatus = ex.Message;
                RaiseCaptureStateChanged();
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
        if (!IsAcquiring || _session is null)
            return;

        Interlocked.Exchange(ref _singleShotRequested, 0);
        Interlocked.Exchange(ref _stopRequested, 1);
        try
        {
            await _session.Adapter.StopStreamAsync(CancellationToken.None).ConfigureAwait(true);
            CaptureStatus = "采集已停止；图像控件保留最后一帧";
        }
        finally
        {
            _streamTask = null;
            Interlocked.Exchange(ref _stopRequested, 0);
            _captureLease?.Dispose();
            _captureLease = null;
            IsAcquiring = false;
            Interlocked.Exchange(ref _singleFrameStopScheduled, 0);
            RaiseCaptureStateChanged();
        }
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

            if (_activeMode == CameraTriggerMode.Software)
            {
                await TriggerOnceAsync().ConfigureAwait(true);
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
        if (!CanSoftwareTrigger || _session is null)
            throw new InvalidOperationException("当前设备配置不允许软件触发；请检查采集状态、触发模式和触发源。");

        await _session.Adapter.ExecuteSoftwareTriggerAsync(_lifetime.Token).ConfigureAwait(true);
        CaptureStatus = "软件触发命令已发送，等待相机帧";
        RaiseCaptureStateChanged();
    }

    private void RaiseCaptureStateChanged() => CaptureStateChanged?.Invoke(this, EventArgs.Empty);

    private async Task StartStreamAsync(
        CameraTriggerMode mode,
        IReadOnlyDictionary<string, object?>? parameters,
        string description,
        string? correlationId)
    {
        try
        {
            await EnsureSessionAsync().ConfigureAwait(true);
            if (_streamTask is not null)
                throw new InvalidOperationException("采集任务状态不一致：已有取流任务。");

            _captureLease = _runtime.Runtime.AcquireCaptureLease(_cameraRef, "相机设置预览");

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
            _streamTask = _session!.Adapter.StartStreamAsync(request, this, _lifetime.Token);
            _ = ObserveStreamAsync(_streamTask);
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[camera-trace={_activeCorrelationId}] 用户启动相机取流 camera={_cameraRef.Value} generation={_previewGeneration} requestMode={mode} description={description}",
                "CameraPreviewTrace", target: LogTarget.All);
        }
        catch
        {
            _captureLease?.Dispose();
            _captureLease = null;
            IsAcquiring = false;
            CaptureStatus = "启动采集失败，请检查相机日志";
            RaiseCaptureStateChanged();
            throw;
        }
    }

    private async Task ObserveStreamAsync(Task streamTask)
    {
        try
        {
            await streamTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
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
                // 取流异常自行结束时仍需释放适配器的流状态；主动停止/窗口销毁
                // 由各自入口清理，避免与其 StopStreamAsync 并发。
                if (Volatile.Read(ref _stopRequested) == 0
                    && Volatile.Read(ref _stopped) == 0 && _session is not null)
                {
                    try { await _session.Adapter.StopStreamAsync(CancellationToken.None).ConfigureAwait(true); }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"预览异常退出后清理取流失败: {ex.Message}", "CameraPreviewTrace");
                    }
                }
                _streamTask = null;
                IsAcquiring = false;
                _captureLease?.Dispose();
                _captureLease = null;
                RaiseCaptureStateChanged();
            }
        }
    }

    public async ValueTask OnFrameAsync(CameraFrame frame, CancellationToken cancellationToken)
    {
        try
        {
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
                if (IsLoaded)
                    StatusText.Text = "预览失败：当前帧无法转换为显示图像";
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
            ImageDisplay.SourceImage = bitmap;
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return;

        _lifetime.Cancel();
        try
        {
            if (_session is not null)
            {
                if (_streamTask is not null)
                {
                    try { await _session.Adapter.StopStreamAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch (Exception) when (_lifetime.IsCancellationRequested) { }
                }
                if (_ownsSession)
                    await _runtime.Runtime.CloseAsync(_cameraRef).ConfigureAwait(false);
                _session = null;
            }
        }
        finally
        {
            _captureLease?.Dispose();
            _captureLease = null;
            _lifetime.Dispose();
        }
    }
}
