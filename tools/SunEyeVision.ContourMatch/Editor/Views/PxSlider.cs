using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂 VM.ModuleControl.PxSlider 的最小兼容实现（hppyramidfeaturepatmatchdialoguistyle.xaml:679/721）。
/// 模板窗口用它调整橡皮擦/画笔范围：Value 双向绑定，外加供 Interaction.EventTrigger 调用的
/// ThumbMouseLeftButtonDown/Up 方法（原厂用于拖拽期间抑制形状绘制）。
/// </summary>
public sealed class PxSlider : Slider
{
    private bool _syncing;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(PxSlider),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            static (d, e) => ((PxSlider)d).OnValueChanged(e)));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public PxSlider()
    {
        ValueChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            Value = base.Value;
            _syncing = false;
        };
    }

    static PxSlider()
        => DefaultStyleKeyProperty.OverrideMetadata(typeof(PxSlider), new FrameworkPropertyMetadata(typeof(Slider)));

    private void OnValueChanged(DependencyPropertyChangedEventArgs e)
    {
        if (_syncing || e.NewValue is not double value || value == base.Value) return;
        _syncing = true;
        base.Value = value;
        _syncing = false;
    }

    public void ThumbMouseLeftButtonDown() { }
    public void ThumbMouseLeftButtonUp() { }
}
