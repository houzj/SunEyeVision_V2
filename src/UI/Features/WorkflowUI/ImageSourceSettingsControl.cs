using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private readonly SolutionSession? _session;
    private readonly List<Action> _refreshValues = new();
    private readonly TextBlock _editStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private bool _refreshing;
    private bool _observing;
    private ImageInputSource? _observedInput;
    private ImageSourceConfig Configuration => _node.ImageSourceConfig ?? new ImageSourceConfig { NodeId = _node.Id };
    private readonly TextBlock _localSummary = new();
    private readonly TextBlock _cameraStatus = new();
    private readonly StackPanel _localPanel = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly StackPanel _cameraPanel = new() { Margin = new Thickness(0, 12, 0, 0) };

    public ImageSourceSettingsControl(WorkflowNode node, MainWindowViewModel main)
    {
        _node = node;
        _main = main;
        _session = main.DocumentManager.Session;

        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock { Text = "基本参数", FontSize = 16, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "更改即时接纳；关闭设置后可在主界面撤销。", TextWrapping = TextWrapping.Wrap });
        root.Children.Add(_editStatus);
        root.Children.Add(new TextBlock { Text = "图像来源", Margin = new Thickness(0, 15, 0, 4) });
        var sourcePicker = new ComboBox();
        sourcePicker.Items.Add("本地图像");
        sourcePicker.Items.Add("相机");
        sourcePicker.SelectedIndex = Configuration.SourceKind == ImageSourceKind.Camera ? 1 : 0;
        sourcePicker.SelectionChanged += (_, _) =>
        {
            var selected = sourcePicker.SelectedIndex == 1 ? ImageSourceKind.Camera : ImageSourceKind.Local;
            Edit(configuration => configuration.SourceKind = selected);
        };
        _refreshValues.Add(() => sourcePicker.SelectedIndex = Configuration.SourceKind == ImageSourceKind.Camera ? 1 : 0);
        root.Children.Add(sourcePicker);

        _localPanel.Children.Add(new TextBlock { Text = "本地图像", FontWeight = FontWeights.SemiBold });
        _localPanel.Children.Add(_localSummary);
        _localPanel.Children.Add(new TextBlock
        {
            Text = "请在图像结果区下方的预览器添加、选择或切换图片。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });
        var autoSwitch = new CheckBox
        {
            Content = "自动切换（成功处理后切换到下一张）",
            IsChecked = Configuration.AutoSwitchEnabled,
            Margin = new Thickness(0, 12, 0, 0)
        };
        autoSwitch.Checked += (_, _) => SetAutoSwitch(true);
        autoSwitch.Unchecked += (_, _) => SetAutoSwitch(false);
        _refreshValues.Add(() => autoSwitch.IsChecked = Configuration.AutoSwitchEnabled);
        _localPanel.Children.Add(autoSwitch);
        var stopAtLast = new CheckBox
        {
            Content = "最后一张处理成功后停止连续运行",
            IsChecked = Configuration.StopAtLastImage,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stopAtLast.Checked += (_, _) => Edit(configuration => configuration.StopAtLastImage = true);
        stopAtLast.Unchecked += (_, _) => Edit(configuration => configuration.StopAtLastImage = false);
        _refreshValues.Add(() => stopAtLast.IsChecked = Configuration.StopAtLastImage);
        _localPanel.Children.Add(stopAtLast);
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
                && string.Equals(item.Tag as string, Configuration.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                cameraPicker.SelectedIndex = i;
                break;
            }
        }
        if (cameraPicker.SelectedIndex == 0 && !string.IsNullOrWhiteSpace(Configuration.CameraDeviceId))
        {
            cameraPicker.Items.Add(new ComboBoxItem
            {
                Content = $"原设备已移除：{Configuration.CameraDeviceId}",
                Tag = Configuration.CameraDeviceId
            });
            cameraPicker.SelectedIndex = cameraPicker.Items.Count - 1;
        }
        cameraPicker.SelectionChanged += (_, _) =>
        {
            var id = (cameraPicker.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            Edit(configuration => configuration.CameraDeviceId = id);
        };
        _refreshValues.Add(() =>
        {
            var id = Configuration.CameraDeviceId;
            var match = cameraPicker.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, id, StringComparison.OrdinalIgnoreCase));
            if (match is null && !string.IsNullOrWhiteSpace(id))
            {
                match = new ComboBoxItem { Content = $"原设备已移除：{id}", Tag = id };
                cameraPicker.Items.Add(match);
            }
            cameraPicker.SelectedItem = match ?? cameraPicker.Items[0];
        });
        _cameraPanel.Children.Add(cameraPicker);
        _cameraPanel.Children.Add(_cameraStatus);
        _cameraPanel.Children.Add(new TextBlock { Text = "取帧超时（毫秒）", Margin = new Thickness(0, 12, 0, 4) });
        var timeout = new TextBox { Text = Configuration.CameraTimeoutMs.ToString() };
        timeout.LostFocus += (_, _) =>
        {
            if (int.TryParse(timeout.Text, out var value) && value > 0)
                Edit(configuration => configuration.CameraTimeoutMs = value);
            else
                timeout.Text = Configuration.CameraTimeoutMs.ToString();
        };
        _refreshValues.Add(() => timeout.Text = Configuration.CameraTimeoutMs.ToString());
        _cameraPanel.Children.Add(timeout);
        var managerButton = new Button { Content = "相机设置 / 预览…", Margin = new Thickness(0, 12, 0, 0) };
        managerButton.Click += (_, _) =>
        {
            var manager = new CameraManagerViewModel(ServiceInitializer.SolutionManager);
            var camera = manager.Cameras.FirstOrDefault(item =>
                string.Equals(item.DeviceId, Configuration.CameraDeviceId, StringComparison.OrdinalIgnoreCase));
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

        var advanced = new StackPanel { Margin = new Thickness(14) };
        advanced.Children.Add(new TextBlock { Text = "输出格式", Margin = new Thickness(0, 4, 0, 4) });
        var formatPicker = new ComboBox
        {
            ItemsSource = new[] { "保持原图", "MONO8", "RGB24" },
            SelectedIndex = Enum.IsDefined(Configuration.OutputFormat)
                ? (int)Configuration.OutputFormat : 0
        };
        formatPicker.SelectionChanged += (_, _) =>
        {
            var format = (ImageOutputFormat)formatPicker.SelectedIndex;
            Edit(configuration => configuration.OutputFormat = format);
        };
        _refreshValues.Add(() => formatPicker.SelectedIndex = (int)Configuration.OutputFormat);
        advanced.Children.Add(formatPicker);
        advanced.Children.Add(new TextBlock { Text = "SN 初始值", Margin = new Thickness(0, 12, 0, 4) });
        var sn = new TextBox { Text = Configuration.SNInitialValue.ToString() };
        sn.LostFocus += (_, _) =>
        {
            if (long.TryParse(sn.Text, out var value) && value >= 0)
                Edit(configuration => configuration.SNInitialValue = value);
            else
                sn.Text = Configuration.SNInitialValue.ToString();
        };
        _refreshValues.Add(() => sn.Text = Configuration.SNInitialValue.ToString());
        advanced.Children.Add(sn);
        advanced.Children.Add(new TextBlock
        {
            Text = "每次开始运行从此值计数；仅图像源成功输出时递增。设备帧号另行显示。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 4, 0, 0)
        });

        Content = new TabControl
        {
            Items =
            {
                new TabItem
                {
                    Header = "基本参数",
                    Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
                },
                new TabItem
                {
                    Header = "高级",
                    Content = new ScrollViewer { Content = advanced, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
                }
            }
        };
        Loaded += (_, _) => StartObserving();
        Unloaded += (_, _) => StopObserving();
        RefreshValues();
    }

    private void OnCurrentImageChanged(object? sender, int index) => RefreshLocalSummary();
    private void OnImageCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => RefreshLocalSummary();

    private void SetAutoSwitch(bool enabled) => Edit(configuration => configuration.AutoSwitchEnabled = enabled);
    private void Edit(Action<ImageSourceConfig> change)
    {
        if (_refreshing) return;
        _editStatus.Text = _main.TryEditImageSourceSettings(_session, _node, this, change, out var error) ? string.Empty : error;
        RefreshValues();
    }
    private void RefreshValues()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            foreach (var refresh in _refreshValues) refresh();
            RefreshMode();
            RefreshEditState();
        }
        finally { _refreshing = false; }
    }
    private void RefreshEditState()
    {
        if (Content is TabControl tabs)
            tabs.IsEnabled = _main.CanEditImageSourceSettings(_session, _node, this);
    }
    private void StartObserving()
    {
        if (_observing) return;
        _observing = true;
        _main.PropertyChanged += OnMainStateChanged;
        if (_session is not null) _session.History.Changed += OnHistoryChanged;
        BindInputObservation();
        RefreshValues();
    }
    private void StopObserving()
    {
        if (!_observing) return;
        _observing = false;
        _main.PropertyChanged -= OnMainStateChanged;
        if (_session is not null) _session.History.Changed -= OnHistoryChanged;
        BindInputObservation();
    }
    private void BindInputObservation()
    {
        var next = _observing ? _node.InputSource : null;
        if (ReferenceEquals(next, _observedInput)) return;
        if (_observedInput is { } old)
        {
            old.CurrentImageChanged -= OnCurrentImageChanged;
            old.CollectionChanged -= OnImageCollectionChanged;
        }
        _observedInput = next;
        if (next is not null)
        {
            next.CurrentImageChanged += OnCurrentImageChanged;
            next.CollectionChanged += OnImageCollectionChanged;
        }
    }
    private void OnMainStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        BindInputObservation();
        RefreshEditState();
    }
    private void OnHistoryChanged(object? sender, EventArgs args) => RefreshValues();

    private void RefreshMode()
    {
        var local = Configuration?.SourceKind != ImageSourceKind.Camera;
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
        var id = Configuration?.CameraDeviceId;
        _cameraStatus.Text = string.IsNullOrWhiteSpace(id)
            ? "设备状态：未选择相机"
            : ServiceInitializer.CameraRuntime.Runtime.TryGetSession(new CameraDeviceRef(id), out _)
                ? "设备状态：已连接" : "设备状态：未连接（请在设备列表连接）";
    }
}
