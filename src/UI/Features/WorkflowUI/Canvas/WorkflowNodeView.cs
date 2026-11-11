using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;

/// <summary>
/// 节点视图宿主：对应原件 DiagramItem，由控件自身维护命中状态与端口显隐。
/// 状态与可见性都由代码直接控制，不依赖模板内的绑定解析。
/// </summary>
public class WorkflowNodeView : ContentControl
{
    public static readonly DependencyProperty IsHitedProperty = DependencyProperty.Register(
        nameof(IsHited),
        typeof(bool),
        typeof(WorkflowNodeView),
        new PropertyMetadata(false, OnIsHitedChanged));

    public static readonly DependencyProperty IsWorkAreaLockedProperty = DependencyProperty.Register(
        nameof(IsWorkAreaLocked), typeof(bool), typeof(WorkflowNodeView),
        new PropertyMetadata(false, OnIsHitedChanged));

    public bool IsWorkAreaLocked
    {
        get => (bool)GetValue(IsWorkAreaLockedProperty);
        set => SetValue(IsWorkAreaLockedProperty, value);
    }

    private const string PortLayerName = "PortLayer";
    private FrameworkElement? _portLayer;

    public WorkflowNodeView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>节点是否被鼠标命中；命中时四向端口显示，与原件 DiagramItem.IsHited 同名同义。</summary>
    public bool IsHited
    {
        get => (bool)GetValue(IsHitedProperty);
        set => SetValue(IsHitedProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty)
        {
            IsHited = (bool)e.NewValue;
        }
        else if (e.Property == IsEnabledProperty)
        {
            ApplyPortVisibility();
        }
    }

    private static void OnIsHitedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((WorkflowNodeView)d).ApplyPortVisibility();

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyPortVisibility();

    private void OnUnloaded(object sender, RoutedEventArgs e) => _portLayer = null;

    /// <summary>按当前命中状态直接设置端口层可见性。</summary>
    private void ApplyPortVisibility()
    {
        _portLayer ??= FindDescendantByName(this, PortLayerName);
        if (_portLayer is null)
        {
            return;
        }

        _portLayer.Visibility = IsHited && IsEnabled && !IsWorkAreaLocked
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private static FrameworkElement? FindDescendantByName(DependencyObject root, string name)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            FrameworkElement? found = FindDescendantByName(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
