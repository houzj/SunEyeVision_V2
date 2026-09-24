using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Features.Camera;

public partial class CameraParameterPanel : UserControl
{
    public static readonly DependencyProperty ShowCategoryNavigationProperty =
        DependencyProperty.Register(nameof(ShowCategoryNavigation), typeof(bool), typeof(CameraParameterPanel),
            new PropertyMetadata(true, OnShowCategoryNavigationChanged));

    public bool ShowCategoryNavigation
    {
        get => (bool)GetValue(ShowCategoryNavigationProperty);
        set => SetValue(ShowCategoryNavigationProperty, value);
    }

    public CameraParameterPanel()
    {
        InitializeComponent();
        UpdateCategoryNavigationLayout();
    }

    private static void OnShowCategoryNavigationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CameraParameterPanel panel)
            panel.UpdateCategoryNavigationLayout();
    }

    private void UpdateCategoryNavigationLayout()
    {
        if (CategoryColumn is null)
            return;

        CategoryColumn.Width = ShowCategoryNavigation ? new GridLength(190) : new GridLength(0);
        CategorySplitterColumn.Width = ShowCategoryNavigation ? new GridLength(8) : new GridLength(0);
        CategoryNavigationPanel.Visibility = ShowCategoryNavigation ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnParameterCategorySelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is CameraDetailViewModel viewModel
            && e.NewValue is CameraParameterGroupViewModel category)
            viewModel.SelectedParameterCategory = category;
    }
}
