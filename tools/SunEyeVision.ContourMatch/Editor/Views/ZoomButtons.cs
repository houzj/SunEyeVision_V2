using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂 pcUiControl:ZoomInButton / ZoomOutButton 的中性化实现
/// （hppyramidfeaturepatmatchdialoguistyle.xaml:766-767）。
/// 原 .pending 依赖未迁移的 HikImageView；本方 ImageViewBox 已提供 Shrink/Enlarge 与
/// EnableZoomIn/EnableZoomOut，故只保留 HookImageViewBox 并把 Enable 绑定到按钮 IsEnabled。
/// 图标三态外观由使用处的内联 ControlTemplate 提供（原厂 PcUi/themes/generic.xaml:3-45）。
/// </summary>
public class ZoomInButton : Button
{
    public static readonly DependencyProperty HookImageViewBoxProperty = DependencyProperty.Register(
        nameof(HookImageViewBox), typeof(FrameworkElement), typeof(ZoomInButton),
        new PropertyMetadata(null, static (d, e) => ZoomButtonHelper.BindEnable(d, e, "EnableZoomIn")));

    public FrameworkElement HookImageViewBox
    {
        get => (FrameworkElement)GetValue(HookImageViewBoxProperty);
        set => SetValue(HookImageViewBoxProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        HookImageViewBox?.GetType().GetMethod("Shrink")?.Invoke(HookImageViewBox, null);
    }
}

public class ZoomOutButton : Button
{
    public static readonly DependencyProperty HookImageViewBoxProperty = DependencyProperty.Register(
        nameof(HookImageViewBox), typeof(FrameworkElement), typeof(ZoomOutButton),
        new PropertyMetadata(null, static (d, e) => ZoomButtonHelper.BindEnable(d, e, "EnableZoomOut")));

    public FrameworkElement HookImageViewBox
    {
        get => (FrameworkElement)GetValue(HookImageViewBoxProperty);
        set => SetValue(HookImageViewBoxProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        HookImageViewBox?.GetType().GetMethod("Enlarge")?.Invoke(HookImageViewBox, null);
    }
}

internal static class ZoomButtonHelper
{
    internal static void BindEnable(DependencyObject d, DependencyPropertyChangedEventArgs e, string path)
    {
        if (d is not Button button || e.NewValue is null) return;
        button.SetBinding(Button.IsEnabledProperty, new System.Windows.Data.Binding(path)
        {
            Source = e.NewValue,
            Mode = System.Windows.Data.BindingMode.TwoWay
        });
    }
}
