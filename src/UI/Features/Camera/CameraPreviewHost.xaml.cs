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
    private CameraTriggerMode _activeMode = CameraTriggerMode.Continuous;
    private string _activeDescription = "准备预览";
    private string? _activeCorrelationId;
    private long _displayedFrameCount;
    private long _previewGeneration;
    private int _stopped;

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
        _ = StartConfiguredPreviewAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _ = DisposeAsync().AsTask();

    private async Task EnsureSessionAsync()
    {
        if (_session is null)
            _session = await _runtime.Runtime.OpenAsync(_cameraRef, CameraAccessMode.Preview,
                cancellationToken: _lifetime.Token).ConfigureAwait(true);
    }

    private async Task StartConfiguredPreviewAsync(string? correlationId = null)
    {
        try
        {
            await EnsureSessionAsync().ConfigureAwait(true);
            var values = await _session!.Adapter.ReadParametersAsync(
                new[] { "AcquisitionMode", "TriggerMode", "TriggerSource" }, _lifetime.Token).ConfigureAwait(true);
            var adapterId = _session!.Adapter.AdapterId;
            var generation = Interlocked.Increment(ref _previewGeneration);
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[camera-trace={correlationId ?? "preview-open"}] 预览配置读回 camera={_cameraRef.Value} adapter={adapterId} generation={generation} AcquisitionMode={FormatValue(values, "AcquisitionMode")} TriggerMode={FormatValue(values, "TriggerMode")} TriggerSource={FormatValue(values, "TriggerSource")}",
                "CameraPreviewTrace", target: LogTarget.All);
            var acquisitionMode = values.TryGetValue("AcquisitionMode", out var currentAcquisitionMode)
                ? DescribeDeviceEnum("AcquisitionMode", currentAcquisitionMode, adapterId)
                : "未知";
            var acquisitionDescription = TryGetAcquisitionMode(currentAcquisitionMode, adapterId, out var configuredAcquisitionMode)
                ? configuredAcquisitionMode switch
                {
                    "continuous" => "连续采集",
                    "single" => "单帧采集",
                    "multi" => "多帧采集",
                    _ => $"采集模式 {acquisitionMode}"
                }
                : $"采集模式 {acquisitionMode}";
            if (!values.TryGetValue("TriggerMode", out var triggerMode))
            {
                if (!_session!.Adapter.Capabilities.SupportsSoftwareTrigger
                    && !_session.Adapter.Capabilities.SupportsHardwareTrigger)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"[camera-trace={correlationId ?? "preview-open"}] TriggerMode未读回，适配器声明不支持触发，按无触发能力启动 camera={_cameraRef.Value}",
                        "CameraPreviewTrace", target: LogTarget.All);
                    await StartStreamAsync(CameraTriggerMode.Continuous, null,
                        $"设备不支持触发 · {acquisitionDescription}", correlationId).ConfigureAwait(true);
                    return;
                }
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[camera-trace={correlationId ?? "preview-open"}] TriggerMode未读回且设备支持触发；为避免错误采集而停止启动 camera={_cameraRef.Value}",
                    "CameraPreviewTrace", target: LogTarget.All);
                StatusText.Text = "无法读取相机当前触发配置，已停止启动预览；请检查参数节点读取状态。";
                return;
            }

            if (!IsEnabledTriggerMode(triggerMode))
            {
                var triggerModeText = DescribeDeviceEnum("TriggerMode", triggerMode, adapterId);
                await StartStreamAsync(CameraTriggerMode.Continuous, null,
                    $"相机读回：{acquisitionDescription} · 触发模式 {triggerModeText}", correlationId).ConfigureAwait(true);
                return;
            }

            if (!values.TryGetValue("TriggerSource", out var triggerSource) || triggerSource is null)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"[camera-trace={correlationId ?? "preview-open"}] TriggerMode已开启但TriggerSource未读回；预览未启动 camera={_cameraRef.Value} TriggerMode={Convert.ToString(triggerMode, CultureInfo.InvariantCulture)}",
                    "CameraPreviewTrace", target: LogTarget.All);
                StatusText.Text = "触发已启用，但无法读取触发源；为避免错误地连续采集，预览未启动。";
                return;
            }

            var isSoftware = IsSoftwareTriggerSource(triggerSource);
            var triggerSourceText = DescribeDeviceEnum("TriggerSource", triggerSource, adapterId);
            await StartStreamAsync(
                isSoftware ? CameraTriggerMode.Software : CameraTriggerMode.Hardware,
                new Dictionary<string, object?> { ["TriggerSource"] = triggerSource },
                isSoftware
                    ? $"相机读回：采集模式 {acquisitionMode} · 触发源 {triggerSourceText} · 等待参数树中的软件触发命令"
                    : $"相机读回：采集模式 {acquisitionMode} · 外部触发源 {triggerSourceText}",
                correlationId).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[camera-trace={correlationId ?? "preview-open"}] 预览配置读取/启动失败 camera={_cameraRef.Value}: {ex.Message}",
                "CameraPreviewTrace", ex, LogTarget.All);
            if (IsLoaded)
                StatusText.Text = $"无法启动触发调试：{ex.Message}";
        }
    }

    public Task RefreshFromDeviceParametersAsync(string? correlationId = null)
    {
        if (Interlocked.CompareExchange(ref _stopped, 0, 0) != 0 || !IsLoaded)
            return Task.CompletedTask;
        return StartConfiguredPreviewAsync(correlationId);
    }

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
            {
                await _session!.Adapter.StopStreamAsync(CancellationToken.None).ConfigureAwait(true);
                _streamTask = null;
            }

            if (Interlocked.CompareExchange(ref _stopped, 0, 0) != 0)
                return;

            _activeMode = mode;
            _activeDescription = description;
            _activeCorrelationId = correlationId ?? "preview-open";
            Interlocked.Exchange(ref _displayedFrameCount, 0);
            StatusText.Text = mode == CameraTriggerMode.Hardware
                ? $"{description} · 等待相机触发信号"
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
                $"[camera-trace={_activeCorrelationId}] 预览流请求已提交 camera={_cameraRef.Value} generation={_previewGeneration} requestMode={mode} description={description}",
                "CameraPreviewTrace", target: LogTarget.All);
            if (mode == CameraTriggerMode.Software)
                StatusText.Text = description;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Error,
                $"[camera-trace={correlationId ?? "preview-open"}] 预览流启动失败 camera={_cameraRef.Value} requestMode={mode}: {ex.Message}",
                "CameraPreviewTrace", ex, LogTarget.All);
            if (IsLoaded)
                StatusText.Text = $"预览失败：{ex.Message}";
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
    }

    public ValueTask OnFrameAsync(CameraFrame frame, CancellationToken cancellationToken)
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
                return ValueTask.CompletedTask;
            }

            return new ValueTask(UpdateImageAsync(bitmap, frame.FrameId, cancellationToken));
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
            if (_activeMode == CameraTriggerMode.Continuous)
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

    private static string FormatValue(IReadOnlyDictionary<string, object?> values, string name)
        => values.TryGetValue(name, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>"
            : "<unreadable>";

    private static bool IsEnabledTriggerMode(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return text is "1" or "On" or "ON" or "开启" or "True";
    }

    private static bool IsSoftwareTriggerSource(object value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return text is "7" or "Software" or "SoftwareTrigger"
            || string.Equals(text, "软件", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "软触发", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetAcquisitionMode(object? value, string adapterId, out string mode)
    {
        mode = string.Empty;
        if (value is null || !string.Equals(adapterId, "hikvision-mvs", StringComparison.OrdinalIgnoreCase))
            return false;

        mode = Convert.ToString(value, CultureInfo.InvariantCulture) switch
        {
            "0" => "single",
            "1" => "multi",
            "2" => "continuous",
            _ => string.Empty
        };
        return mode.Length > 0;
    }

    private static string DescribeDeviceEnum(string nodeName, object value, string adapterId)
    {
        var raw = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (!string.Equals(adapterId, "hikvision-mvs", StringComparison.OrdinalIgnoreCase))
            return raw;

        return (nodeName, raw) switch
        {
            ("AcquisitionMode", "0") => "单帧",
            ("AcquisitionMode", "1") => "多帧",
            ("AcquisitionMode", "2") => "连续",
            ("TriggerMode", "0") => "关闭",
            ("TriggerMode", "1") => "开启",
            ("TriggerSource", "0") => "线路 0",
            ("TriggerSource", "1") => "线路 1",
            ("TriggerSource", "2") => "线路 2",
            ("TriggerSource", "3") => "线路 3",
            ("TriggerSource", "7") => "软件触发",
            _ => raw
        };
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
                try { await _session.Adapter.StopStreamAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception) when (_lifetime.IsCancellationRequested) { }
                if (_ownsSession)
                    await _runtime.Runtime.CloseAsync(_cameraRef).ConfigureAwait(false);
                _session = null;
            }
        }
        finally
        {
            _lifetime.Dispose();
        }
    }
}
