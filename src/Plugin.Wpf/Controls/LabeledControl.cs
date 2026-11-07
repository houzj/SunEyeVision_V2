using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Controls;

/// <summary>统一的参数标签和内容布局容器。</summary>
public class LabeledControl : HeaderedContentControl
{
    static LabeledControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LabeledControl),
            new FrameworkPropertyMetadata(typeof(LabeledControl)));
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(LabeledControl), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty LabelWidthProperty =
        DependencyProperty.Register(nameof(LabelWidth), typeof(double), typeof(LabeledControl), new PropertyMetadata(80d));
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(LabeledControl), new PropertyMetadata(Orientation.Horizontal));
    public static readonly DependencyProperty RequiredProperty =
        DependencyProperty.Register(nameof(Required), typeof(bool), typeof(LabeledControl), new PropertyMetadata(false));
    public static readonly DependencyProperty ContentHorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(ContentHorizontalAlignment), typeof(HorizontalAlignment), typeof(LabeledControl), new PropertyMetadata(HorizontalAlignment.Stretch));
    public static readonly DependencyProperty LabelVerticalAlignmentProperty =
        DependencyProperty.Register(nameof(LabelVerticalAlignment), typeof(VerticalAlignment), typeof(LabeledControl), new PropertyMetadata(VerticalAlignment.Center));
    public static readonly DependencyProperty ContentVerticalAlignmentProperty =
        DependencyProperty.Register(nameof(ContentVerticalAlignment), typeof(VerticalAlignment), typeof(LabeledControl), new PropertyMetadata(VerticalAlignment.Center));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool Required { get => (bool)GetValue(RequiredProperty); set => SetValue(RequiredProperty, value); }
    public HorizontalAlignment ContentHorizontalAlignment { get => (HorizontalAlignment)GetValue(ContentHorizontalAlignmentProperty); set => SetValue(ContentHorizontalAlignmentProperty, value); }
    public VerticalAlignment LabelVerticalAlignment { get => (VerticalAlignment)GetValue(LabelVerticalAlignmentProperty); set => SetValue(LabelVerticalAlignmentProperty, value); }
    public VerticalAlignment ContentVerticalAlignment { get => (VerticalAlignment)GetValue(ContentVerticalAlignmentProperty); set => SetValue(ContentVerticalAlignmentProperty, value); }
}
