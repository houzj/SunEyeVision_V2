using System;
using System.Windows;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.UI.Features.Camera;

public partial class CameraPreviewWindow : Window
{
    private readonly CameraPreviewHost _host;

    public CameraPreviewWindow(CameraRuntimeComposition runtime, CameraDeviceRef cameraRef)
    {
        InitializeComponent();
        _host = new CameraPreviewHost(runtime ?? throw new ArgumentNullException(nameof(runtime)), cameraRef);
        Content = _host;
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _host.DisposeAsync();
        base.OnClosed(e);
    }
}
