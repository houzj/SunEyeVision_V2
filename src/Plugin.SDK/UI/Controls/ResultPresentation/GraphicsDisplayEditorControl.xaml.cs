using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.SDK.UI.Controls.ResultPresentation;

public partial class GraphicsDisplayEditorControl : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(GraphicsDisplayEditorControl), new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public GraphicsDisplayEditorControl() => InitializeComponent();
}
