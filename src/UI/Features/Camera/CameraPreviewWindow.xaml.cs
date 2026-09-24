using System;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.UI.Features.Camera;

public partial class CameraPreviewWindow : ThemeDialogWindow
{
    private readonly CameraPreviewHost _host;
    private readonly CameraDetailViewModel _viewModel;

    public CameraPreviewWindow(CameraRuntimeComposition runtime, CameraDeviceRef cameraRef, CameraSession existingSession, CameraDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        _host = new CameraPreviewHost(runtime ?? throw new ArgumentNullException(nameof(runtime)), cameraRef, existingSession);
        _host.CaptureStateChanged += OnCaptureStateChanged;
        PreviewHostContainer.Content = _host;
        Loaded += (_, _) => UpdateCaptureControls();
    }

    public bool IsAcquiring => _host.IsAcquiring;

    public Task RefreshFromDeviceParametersAsync(string? correlationId = null)
        => _host.RefreshConfigurationAsync(correlationId);

    public Task RefreshConfigurationAsync(string? correlationId = null)
        => _host.RefreshConfigurationAsync(correlationId);

    public Task StopAcquisitionAsync() => _host.StopAcquisitionAsync();

    public Task StartAcquisitionAsync() => _host.StartAcquisitionAsync();

    private async void OnToggleAcquisitionClick(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            if (_host.IsAcquiring)
            {
                await _host.StopAcquisitionAsync();
            }
            else
            {
                if (!await _viewModel.ApplyPendingParametersAsync())
                    return;
                await _host.RefreshConfigurationAsync("before-start");
                await _host.StartAcquisitionAsync();
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportOperationStatus($"采集操作失败：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
            UpdateCaptureControls();
        }
    }

    private async void OnSingleCaptureClick(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            if (!await _viewModel.ApplyPendingParametersAsync())
                return;
            await _host.CaptureSingleFrameAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportOperationStatus($"单次采集失败：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
            UpdateCaptureControls();
        }
    }

    private async void OnTriggerOnceClick(object sender, RoutedEventArgs e)
    {
        try { await _host.TriggerOnceAsync(); }
        catch (Exception ex) { _viewModel.ReportOperationStatus($"触发失败：{ex.Message}"); }
        finally { UpdateCaptureControls(); }
    }

    private void OnCaptureStateChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(UpdateCaptureControls);
            return;
        }
        UpdateCaptureControls();
    }

    private void UpdateCaptureControls()
    {
        if (!IsLoaded)
            return;
        ConfigurationSummaryText.Text = _host.ConfigurationSummary;
        CaptureStatusText.Text = _host.CaptureStatus;
        ToggleAcquisitionButton.Content = _host.IsAcquiring && !_host.IsSingleCaptureInProgress
            ? "停止采集"
            : "连续采集";
        ToggleAcquisitionButton.IsEnabled = _host.CanToggleAcquisition;
        SingleCaptureButton.IsEnabled = _host.CanCaptureSingle;
        TriggerButton.IsEnabled = _host.CanSoftwareTrigger;
    }

    private void SetBusy(bool busy)
    {
        ToggleAcquisitionButton.IsEnabled = !busy && _host.CanToggleAcquisition;
        SingleCaptureButton.IsEnabled = !busy && _host.CanCaptureSingle;
        TriggerButton.IsEnabled = !busy && _host.CanSoftwareTrigger;
    }

    protected override async void OnClosed(EventArgs e)
    {
        _host.CaptureStateChanged -= OnCaptureStateChanged;
        await _host.DisposeAsync();
        await _viewModel.ClosePreviewSessionAsync();
        base.OnClosed(e);
    }
}
