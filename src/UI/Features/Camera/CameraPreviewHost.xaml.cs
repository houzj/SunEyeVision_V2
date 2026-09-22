using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Hosting;

namespace SunEyeVision.UI.Features.Camera;

/// <summary>
/// 相机实时预览宿主。它只负责把运行时帧交给 SDK 图像控件，
/// 不显示图像源选择器，也不把厂商参数塞进预览界面。
/// </summary>
public partial class CameraPreviewHost : UserControl, ICameraFrameSink, IAsyncDisposable
{
    private readonly CameraRuntimeComposition _runtime;
    private readonly CameraDeviceRef _cameraRef;
    private readonly CancellationTokenSource _lifetime = new();
    private CameraSession? _session;
    private Task? _streamTask;
    private int _stopped;

    public CameraPreviewHost(CameraRuntimeComposition runtime, CameraDeviceRef cameraRef)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _cameraRef = cameraRef ?? throw new ArgumentNullException(nameof(cameraRef));
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _ = StartAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _ = DisposeAsync().AsTask();
    }

    private async Task StartAsync()
    {
        try
        {
            _session = await _runtime.Runtime.OpenAsync(_cameraRef, CameraAccessMode.Preview,
                cancellationToken: _lifetime.Token).ConfigureAwait(true);
            StatusText.Text = "正在采集";
            var request = new CameraCaptureRequest
            {
                CameraRef = _cameraRef,
                TriggerMode = CameraTriggerMode.Continuous,
                Timeout = TimeSpan.FromSeconds(2)
            };
            _streamTask = _session.Adapter.StartStreamAsync(request, this, _lifetime.Token);
            await _streamTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (IsLoaded)
                StatusText.Text = $"预览失败：{ex.Message}";
        }
    }

    public ValueTask OnFrameAsync(CameraFrame frame, CancellationToken cancellationToken)
    {
        try
        {
            var bitmap = VisionImageConverterLocator.Current?.ToBitmapSource(frame.Image);
            if (bitmap is null)
                return ValueTask.CompletedTask;

            return new ValueTask(UpdateImageAsync(bitmap, frame.FrameId, cancellationToken));
        }
        finally
        {
            // BitmapSource 已经是独立快照，帧所有权在回调返回前释放。
            frame.Dispose();
        }
    }

    private async Task UpdateImageAsync(BitmapSource bitmap, long frameId, CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            ImageDisplay.SourceImage = bitmap;
            StatusText.Text = $"实时采集 · 帧 {frameId}";
        });
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
                try { await _session.Adapter.StopStreamAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception) when (_lifetime.IsCancellationRequested) { }
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
