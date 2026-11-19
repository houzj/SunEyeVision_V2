using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// 相机选择槽：设备下拉 + 状态文本 + 打开按钮。
/// 设备列表、状态与打开命令都由宿主注入，平台不直接依赖设备驱动。
/// </summary>
public sealed partial class DeclaredFieldRow
{
    private ICameraSelectionSource? _cameraSource;
    private Action? _detachCamera;
    private long _cameraGeneration;

    internal bool UsesCameraSelector => _cameraSource is not null;

    internal void UseCameraSelector(ICameraSelectionSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_port.Type != TypeRef.ScalarOf(ScalarKind.Text) || _port.EditModes != FieldEditModes.Constant)
            throw new ArgumentException("A camera selector requires a declared constant text field.");
        ArgumentNullException.ThrowIfNull(source);

        var generation = ++_cameraGeneration;
        DetachCamera();
        if (_disposed || generation != _cameraGeneration)
            return;
        _cameraSource = source;

        var slot = CreateCameraSlot();
        if (_disposed || generation != _cameraGeneration)
            return;
        _slotContent = slot;
        if (_model.TemplateKey != DeclaredFieldTemplateRegistry.CameraSelecter)
        {
            _model.TemplateKey = DeclaredFieldTemplateRegistry.CameraSelecter;
            ContentTemplateSelector = null;
            if (_disposed || generation != _cameraGeneration)
                return;
        }
        ContentTemplateSelector = ParameterRowTemplateSelector.Instance;
        if (!_disposed && generation == _cameraGeneration)
            _model.SetCameraSelector(slot);
    }

    private void DetachCamera()
    {
        var detach = _detachCamera;
        _detachCamera = null;
        detach?.Invoke();
    }

    private UIElement CreateCameraSlot()
    {
        var generation = _cameraGeneration;
        var retired = false;
        var source = _cameraSource!;
        var refreshing = false;

        bool Current() => !retired && !_disposed && generation == _cameraGeneration;

        var host = new DockPanel { LastChildFill = true };

        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(status, Dock.Right);

        var open = new Button { Content = "打开", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(open, Dock.Right);

        var combo = new ComboBox { DisplayMemberPath = nameof(CameraChoiceItem.Name), SelectedValuePath = nameof(CameraChoiceItem.Id) };
        combo.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("SelectedItem.Name") { Source = combo });

        host.Children.Add(status);
        host.Children.Add(open);
        host.Children.Add(combo);

        void Refresh()
        {
            if (!Current() || refreshing)
                return;
            refreshing = true;
            try
            {
                var choices = source.Choices;
                var unchanged = combo.ItemsSource is IReadOnlyList<CameraChoiceItem> previous && previous.SequenceEqual(choices);
                if (!unchanged)
                    combo.ItemsSource = choices;
                combo.SelectedValue = _setting is ConstantSetting { Value: string device } ? device : null;
                status.Text = source.Status(combo.SelectedValue as string);
                open.IsEnabled = combo.SelectedValue is string;
            }
            finally
            {
                refreshing = false;
            }
        }

        void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            // 刷新期间的选中回填不算用户编辑。
            if (!Current() || refreshing || args.AddedItems.Count == 0)
                return;
            if (combo.SelectedValue is not string device)
                return;
            Set(new ConstantSetting(device));
            status.Text = source.Status(device);
            open.IsEnabled = true;
        }

        void OnOpen(object sender, RoutedEventArgs args)
        {
            if (Current() && combo.SelectedValue is string device)
                source.Open(device);
        }

        void OnSourceChanged()
        {
            if (Current())
                Refresh();
        }

        combo.SelectionChanged += OnSelectionChanged;
        open.Click += OnOpen;
        source.Changed += OnSourceChanged;
        _detachCamera = () =>
        {
            retired = true;
            combo.SelectionChanged -= OnSelectionChanged;
            open.Click -= OnOpen;
            source.Changed -= OnSourceChanged;
            combo.ItemsSource = null;
        };

        Refresh();
        return host;
    }
}
