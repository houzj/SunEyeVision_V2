using System;
using System.Windows;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.UI.Features.Camera;

    public partial class CameraPreviewWindow : ThemeDialogWindow
{
    private readonly CameraPreviewHost _host;

    public CameraPreviewWindow(CameraRuntimeComposition runtime, CameraDeviceRef cameraRef, CameraSession? existingSession = null)
    {
        InitializeComponent();
        _host = new CameraPreviewHost(runtime ?? throw new ArgumentNullException(nameof(runtime)), cameraRef, existingSession);
        Content = _host;
    }

    public Task RefreshFromDeviceParametersAsync(string? correlationId = null)
        => _host.RefreshFromDeviceParametersAsync(correlationId);

    protected override async void OnClosed(EventArgs e)
    {
        await _host.DisposeAsync();
        base.OnClosed(e);
    }
}
