using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
/// <summary>Recovered result workspace composition. All publications, commands and image ownership remain with its host.</summary>
public partial class ResultWorkspaceShell : UserControl
{
    public static readonly DependencyProperty InputItemsProperty = DependencyProperty.Register(nameof(InputItems), typeof(IEnumerable), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public IEnumerable? InputItems { get => (IEnumerable?)GetValue(InputItemsProperty); set => SetValue(InputItemsProperty, value); }
    public static readonly DependencyProperty OutputItemsProperty = DependencyProperty.Register(nameof(OutputItems), typeof(IEnumerable), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public IEnumerable? OutputItems { get => (IEnumerable?)GetValue(OutputItemsProperty); set => SetValue(OutputItemsProperty, value); }
    public static readonly DependencyProperty SelectedInputItemProperty = DependencyProperty.Register(nameof(SelectedInputItem), typeof(object), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public object? SelectedInputItem { get => (object?)GetValue(SelectedInputItemProperty); set => SetValue(SelectedInputItemProperty, value); }
    public static readonly DependencyProperty SelectedOutputItemProperty = DependencyProperty.Register(nameof(SelectedOutputItem), typeof(object), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public object? SelectedOutputItem { get => (object?)GetValue(SelectedOutputItemProperty); set => SetValue(SelectedOutputItemProperty, value); }
    /// <summary>Borrow the actual original views through NativeResultViewport.Attach; the shell owns no image.</summary>
    public FrameworkElement InputViewport=>imageViewIn;
    public FrameworkElement OutputViewport=>imageView;
    public static readonly DependencyProperty IsDoubleImageProperty = DependencyProperty.Register(nameof(IsDoubleImage), typeof(bool), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, LayoutChanged));
    public bool IsDoubleImage { get => (bool)GetValue(IsDoubleImageProperty); set => SetValue(IsDoubleImageProperty, value); }
    public static readonly DependencyProperty SourceStripContentProperty = DependencyProperty.Register(nameof(SourceStripContent), typeof(object), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public object? SourceStripContent { get => (object?)GetValue(SourceStripContentProperty); set => SetValue(SourceStripContentProperty, value); }
    public static readonly DependencyProperty PropertyListContentProperty = DependencyProperty.Register(nameof(PropertyListContent), typeof(object), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public object? PropertyListContent { get => (object?)GetValue(PropertyListContentProperty); set => SetValue(PropertyListContentProperty, value); }
    public static readonly DependencyProperty CurrentResultContentProperty = DependencyProperty.Register(nameof(CurrentResultContent), typeof(object), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public object? CurrentResultContent { get => (object?)GetValue(CurrentResultContentProperty); set => SetValue(CurrentResultContentProperty, value); }
    public static readonly DependencyProperty HelpContentProperty = DependencyProperty.Register(nameof(HelpContent), typeof(string), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public string? HelpContent { get => (string?)GetValue(HelpContentProperty); set => SetValue(HelpContentProperty, value); }
    public static readonly DependencyProperty IsInfoExpandedProperty = DependencyProperty.Register(nameof(IsInfoExpanded), typeof(bool), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, LayoutChanged));
    public bool IsInfoExpanded { get => (bool)GetValue(IsInfoExpandedProperty); set => SetValue(IsInfoExpandedProperty, value); }
    public static readonly DependencyProperty SelectedResultTabIndexProperty = DependencyProperty.Register(nameof(SelectedResultTabIndex), typeof(int), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public int SelectedResultTabIndex { get => (int)GetValue(SelectedResultTabIndexProperty); set => SetValue(SelectedResultTabIndexProperty, value); }
    public static readonly DependencyProperty SelectOverlayCommandProperty = DependencyProperty.Register(nameof(SelectOverlayCommand), typeof(ICommand), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public ICommand? SelectOverlayCommand { get => (ICommand?)GetValue(SelectOverlayCommandProperty); set => SetValue(SelectOverlayCommandProperty, value); }
    public static readonly DependencyProperty SelectBackgroundCommandProperty = DependencyProperty.Register(nameof(SelectBackgroundCommand), typeof(ICommand), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public ICommand? SelectBackgroundCommand { get => (ICommand?)GetValue(SelectBackgroundCommandProperty); set => SetValue(SelectBackgroundCommandProperty, value); }
    public static readonly DependencyProperty PreviousImageCommandProperty = DependencyProperty.Register(nameof(PreviousImageCommand), typeof(ICommand), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public ICommand? PreviousImageCommand { get => (ICommand?)GetValue(PreviousImageCommandProperty); set => SetValue(PreviousImageCommandProperty, value); }
    public static readonly DependencyProperty NextImageCommandProperty = DependencyProperty.Register(nameof(NextImageCommand), typeof(ICommand), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public ICommand? NextImageCommand { get => (ICommand?)GetValue(NextImageCommandProperty); set => SetValue(NextImageCommandProperty, value); }
    public static readonly DependencyProperty CrossLinesCommandProperty = DependencyProperty.Register(nameof(CrossLinesCommand), typeof(ICommand), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public ICommand? CrossLinesCommand { get => (ICommand?)GetValue(CrossLinesCommandProperty); set => SetValue(CrossLinesCommandProperty, value); }
    public static readonly DependencyProperty FullscreenCommandProperty = DependencyProperty.Register(nameof(FullscreenCommand), typeof(ICommand), typeof(ResultWorkspaceShell), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public ICommand? FullscreenCommand { get => (ICommand?)GetValue(FullscreenCommandProperty); set => SetValue(FullscreenCommandProperty, value); }
    public ResultWorkspaceShell() { InitializeComponent(); Loaded += (_, _) => ApplyLayout(); }
    private static void LayoutChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((ResultWorkspaceShell)sender).ApplyLayout();
    private void ApplyLayout()
    {
        if (colInput is null || InfoRow is null) return;
        colInput.Width = IsDoubleImage ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        InfoRow.Height = new GridLength(IsInfoExpanded ? 235 : 40);
    }
    private void EnlargeClick(object sender, RoutedEventArgs e) => imageView.Enlarge();
    private void ShrinkClick(object sender, RoutedEventArgs e) => imageView.Shrink();
    private void ResetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag as string == "Input") imageViewIn.Init(); else imageView.Init();
    }
}
