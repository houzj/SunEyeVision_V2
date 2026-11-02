using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

/// <summary>Original target-parameter tree template over host-provided tree nodes.</summary>
public partial class TargetParameterTree : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IList), typeof(TargetParameterTree), new PropertyMetadata(null));

    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public event Action<object?>? LeafDoubleClicked;

    public TargetParameterTree() => InitializeComponent();

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        scrollViewer.Focus();
        var amount = Math.Abs(e.Delta / 2.0);
        var target = e.Delta <= 0 ? Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + amount)
            : Math.Max(0, scrollViewer.VerticalOffset - amount);
        if (target == scrollViewer.VerticalOffset) return;
        scrollViewer.ScrollToVerticalOffset(target);
        e.Handled = true;
    }

    private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { Items.Count: 0 } item) LeafDoubleClicked?.Invoke(item.DataContext);
    }
}
