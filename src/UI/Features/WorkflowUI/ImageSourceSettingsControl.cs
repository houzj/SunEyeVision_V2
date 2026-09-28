using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Features.Camera;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.WorkflowUI;

/// <summary>图像源的轻量配置入口；本地图片仍由主界面现有预览器管理。</summary>
public sealed class ImageSourceSettingsControl : UserControl
{
    private readonly WorkflowNode _node;
    private readonly MainWindowViewModel _main;
    private readonly TextBlock _localSummary = new();
    private readonly TextBlock _cameraStatus = new();
    private readonly StackPanel _localPanel = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly StackPanel _cameraPanel = new() { Margin = new Thickness(0, 12, 0, 0) };

    public ImageSourceSettingsControl(WorkflowNode node, MainWindowViewModel main)
    {
        _node = node;
        _main = main;
        _node.ImageSourceConfig ??= new ImageSourceConfig { NodeId = node.Id };

        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock { Text = "图像源", FontSize = 16, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "图像来源", Margin = new Thickness(0, 15, 0, 4) });
        var sourcePicker = new ComboBox();
        sourcePicker.Items.Add("本地图像");
        sourcePicker.Items.Add("相机");
        sourcePicker.SelectedIndex = _node.ImageSourceConfig.SourceKind == ImageSourceKind.Camera ? 1 : 0;
        sourcePicker.SelectionChanged += (_, _) =>
        {
            _node.ImageSourceConfig.SourceKind = sourcePicker.SelectedIndex == 1
                ? ImageSourceKind.Camera : ImageSourceKind.Local;
            RefreshMode();
            _main.ForceRefreshImagePreview();
        };
        root.Children.Add(sourcePicker);

        _localPanel.Children.Add(new TextBlock { Text = "本地图像", FontWeight = FontWeights.SemiBold });
        _localPanel.Children.Add(_localSummary);
        _localPanel.Children.Add(new TextBlock
        {
            Text = "请在图像结果区下方的预览器添加、选择或切换图片。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });
        var locatePreview = new Button { Content = "定位到图像预览器", Margin = new Thickness(0, 12, 0, 0) };
        locatePreview.Click += (_, _) =>
        {
            _main.ForceRefreshImagePreview();
            Application.Current?.Windows.OfType<MainWindow>()
                .FirstOrDefault()?.ImageResultHost.FocusLocalPreview();
        };
        _localPanel.Children.Add(locatePreview);
        root.Children.Add(_localPanel);

        _cameraPanel.Children.Add(new TextBlock { Text = "关联相机", FontWeight = FontWeights.SemiBold });
        var cameraPicker = new ComboBox { Margin = new Thickness(0, 6, 0, 8) };
        cameraPicker.Items.Add(new ComboBoxItem { Content = "请选择方案中的相机", Tag = string.Empty });
        var devices = ServiceInitializer.SolutionManager.CurrentSolution?.Devices
            .Where(device => device.Type == DeviceType.Camera) ?? Enumerable.Empty<Device>();
        foreach (var device in devices)
            cameraPicker.Items.Add(new ComboBoxItem { Content = device.Name, Tag = device.Id });
        cameraPicker.SelectedIndex = 0;
        for (var i = 1; i < cameraPicker.Items.Count; i++)
        {
            if (cameraPicker.Items[i] is ComboBoxItem item
                && string.Equals(item.Tag as string, _node.ImageSourceConfig.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                cameraPicker.SelectedIndex = i;
                break;
            }
        }
        if (cameraPicker.SelectedIndex == 0 && !string.IsNullOrWhiteSpace(_node.ImageSourceConfig.CameraDeviceId))
        {
            cameraPicker.Items.Add(new ComboBoxItem
            {
                Content = $"原设备已移除：{_node.ImageSourceConfig.CameraDeviceId}",
                Tag = _node.ImageSourceConfig.CameraDeviceId
            });
            cameraPicker.SelectedIndex = cameraPicker.Items.Count - 1;
        }
        cameraPicker.SelectionChanged += (_, _) =>
        {
            _node.ImageSourceConfig.CameraDeviceId =
                (cameraPicker.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            RefreshCameraStatus();
        };
        _cameraPanel.Children.Add(cameraPicker);
        _cameraPanel.Children.Add(_cameraStatus);
        _cameraPanel.Children.Add(new TextBlock { Text = "取帧超时（毫秒）", Margin = new Thickness(0, 12, 0, 4) });
        var timeout = new TextBox { Text = _node.ImageSourceConfig.CameraTimeoutMs.ToString() };
        timeout.LostFocus += (_, _) =>
        {
            if (int.TryParse(timeout.Text, out var value) && value > 0)
                _node.ImageSourceConfig.CameraTimeoutMs = value;
            else
                timeout.Text = _node.ImageSourceConfig.CameraTimeoutMs.ToString();
        };
        _cameraPanel.Children.Add(timeout);
        var managerButton = new Button { Content = "相机设置 / 预览…", Margin = new Thickness(0, 12, 0, 0) };
        managerButton.Click += (_, _) =>
        {
            var manager = new CameraManagerViewModel(ServiceInitializer.SolutionManager);
            var camera = manager.Cameras.FirstOrDefault(item =>
                string.Equals(item.DeviceId, _node.ImageSourceConfig.CameraDeviceId, StringComparison.OrdinalIgnoreCase));
            if (camera == null)
            {
                _cameraStatus.Text = "请先选择方案中的相机";
                return;
            }
            manager.OpenPreview(camera);
            RefreshCameraStatus();
        };
        _cameraPanel.Children.Add(managerButton);
        root.Children.Add(_cameraPanel);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var input = _node.EnsureInputSource();
        input.CurrentImageChanged += OnCurrentImageChanged;
        input.CollectionChanged += OnImageCollectionChanged;
        Unloaded += (_, _) =>
        {
            input.CurrentImageChanged -= OnCurrentImageChanged;
            input.CollectionChanged -= OnImageCollectionChanged;
        };
        RefreshMode();
    }

    private void OnCurrentImageChanged(object? sender, int index) => RefreshLocalSummary();
    private void OnImageCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => RefreshLocalSummary();

    private void RefreshMode()
    {
        var local = _node.ImageSourceConfig?.SourceKind != ImageSourceKind.Camera;
        _localPanel.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        _cameraPanel.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        RefreshLocalSummary();
        RefreshCameraStatus();
    }

    private void RefreshLocalSummary()
    {
        var input = _node.InputSource;
        _localSummary.Text = input?.CurrentImage is { } image
            ? $"当前图像：{image.Name} · 第 {input.CurrentIndex + 1} / {input.Count} 张"
            : $"当前未选图像 · 共 {input?.Count ?? 0} 张";
    }

    private void RefreshCameraStatus()
    {
        var id = _node.ImageSourceConfig?.CameraDeviceId;
        _cameraStatus.Text = string.IsNullOrWhiteSpace(id)
            ? "设备状态：未选择相机"
            : ServiceInitializer.CameraRuntime.Runtime.TryGetSession(new CameraDeviceRef(id), out _)
                ? "设备状态：已连接" : "设备状态：未连接（请在设备列表连接）";
    }
}
