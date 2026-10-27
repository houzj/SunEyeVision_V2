using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using NativePicker = SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView;
using NativePopup = SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls.PopupButton;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>Hosts the original VM.PcUI RGB surface, palette, tabs and advanced picker unchanged.
/// Only adapts the selected value; the caller owns the private draft and its transaction.</summary>
public sealed class NativeColorPresenter : Decorator, IDisposable
{
    private UIElement? _paletteContent;
    private DispatcherOperation? _pendingTextInheritance;
    private bool _disposed;
    private long _generation;
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor), typeof(Color), typeof(NativeColorPresenter),
        new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnColorChanged));

    public static readonly RoutedEvent SelectedColorChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectedColorChanged), RoutingStrategy.Bubble,
        typeof(RoutedPropertyChangedEventHandler<Color>), typeof(NativeColorPresenter));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<Color> SelectedColorChanged
    {
        add => AddHandler(SelectedColorChangedEvent, value);
        remove => RemoveHandler(SelectedColorChangedEvent, value);
    }

    public NativePicker NativeView { get; } = new();

    // This is the original control's content, not a replacement popup/template.
    public NativePopup Palette => ((Grid)NativeView.Content).Children.OfType<NativePopup>().Single();

    public NativeColorPresenter()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/AppsColorFun/colorstyle1.xaml", UriKind.Relative)
        });
        NativeView.SetBinding(NativePicker.CurrentColorProperty, new Binding(nameof(SelectedColor))
        {
            Source = this, Mode = BindingMode.TwoWay, Converter = new ColorBrushAdapter()
        });
        Child = NativeView;
        Palette.DropDownOpened += OnOpened;
        Palette.DropDownClosed += OnClosed;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
    }

    public void ClosePalette()
    {
        InvalidatePendingTextInheritance();
        Palette.IsDropDownOpen = false;
        DetachPaletteContent();
    }

    private void OnOpened(object? sender,EventArgs args)
    {if(_disposed){ClosePalette();return;}QueueTextInheritance();}
    private void OnClosed(object? sender,EventArgs args)
    {InvalidatePendingTextInheritance();DetachPaletteContent();}
    private void OnUnloaded(object sender,RoutedEventArgs args)=>ClosePalette();
    private void OnVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)
    {if(!IsVisible)ClosePalette();}

    private void InvalidatePendingTextInheritance()
    {
        _generation++;
        _pendingTextInheritance?.Abort();
        _pendingTextInheritance=null;
    }

    private void QueueTextInheritance()
    {
        if(_disposed)return;
        InvalidatePendingTextInheritance();
        var generation=_generation;
        _pendingTextInheritance=Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
        if (_disposed || generation!=_generation || !IsLoaded || !Palette.IsDropDownOpen
            || Palette.Template?.FindName("PART_Popup", Palette) is not Popup { Child: { } content }) return;
        _pendingTextInheritance=null;
        if (!ReferenceEquals(_paletteContent, content))
        {
            DetachPaletteContent();
            _paletteContent = content;
            content.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnPalettePageChanged));
        }
        NativeTextInheritance.Restore(content);
        }));
    }

    private void OnPalettePageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is TabControl) QueueTextInheritance();
    }

    private void DetachPaletteContent()
    {
        _paletteContent?.RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnPalettePageChanged));
        _paletteContent = null;
    }

    private static void OnColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var presenter=(NativeColorPresenter)sender;
        if(!presenter._disposed)presenter.RaiseEvent(new RoutedPropertyChangedEventArgs<Color>(
            (Color)e.OldValue, (Color)e.NewValue, SelectedColorChangedEvent));
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;
        ClosePalette();
        Palette.DropDownOpened-=OnOpened;
        Palette.DropDownClosed-=OnClosed;
        Unloaded-=OnUnloaded;
        IsVisibleChanged-=OnVisibilityChanged;
        BindingOperations.ClearBinding(NativeView,NativePicker.CurrentColorProperty);
        Child=null;
    }
}
