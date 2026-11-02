using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Controls.Foundation;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

/// <summary>Original target-parameter selection composition; the host owns candidates and transactions.</summary>
public partial class TargetParameterSelectionDialog : BaseWindow
{
    public event Action<object?>? RemoveRequested;
    public event EventHandler? ConfirmRequested;

    public TargetParameterSelectionDialog()
    {
        InitializeComponent();
        SetResourceReference(StyleProperty, typeof(BaseWindow));
        Closed += OnDialogClosed;
    }

    private void Delete_OnHandler(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element) RemoveRequested?.Invoke(element.DataContext);
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => ConfirmRequested?.Invoke(this, EventArgs.Empty);

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        Closed -= OnDialogClosed;
        RemoveRequested = null;
        ConfirmRequested = null;
        DataContext = null;
    }
}
