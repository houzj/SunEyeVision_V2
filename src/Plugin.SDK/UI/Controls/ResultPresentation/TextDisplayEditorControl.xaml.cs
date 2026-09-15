using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.SDK.UI.Controls.ResultPresentation;

public partial class TextDisplayEditorControl : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(TextDisplayEditorControl), new PropertyMetadata(null));
    public static readonly DependencyProperty AvailableDataSourcesProperty = DependencyProperty.Register(
        nameof(AvailableDataSources), typeof(IList), typeof(TextDisplayEditorControl), new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IList? AvailableDataSources
    {
        get => (IList?)GetValue(AvailableDataSourcesProperty);
        set => SetValue(AvailableDataSourcesProperty, value);
    }

    public TextDisplayEditorControl() => InitializeComponent();
}
