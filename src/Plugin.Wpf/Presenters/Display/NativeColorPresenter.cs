using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using NativePicker = SunEyeVision.Plugin.Wpf.Native.Input.Interaction.ListView.ColorPickerControlView;
using NativePopup = SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls.PopupButton;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>Hosts the original Interaction.Pc RGB surface, palette, tabs and advanced picker unchanged.
/// Only adapts the selected value; the caller owns the private draft and its transaction.</summary>
public sealed class NativeColorPresenter : Decorator, IDisposable
{
    private readonly NativeColorPaletteScope _scope;
    private bool _disposed;
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
        {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Palettes/colorstyle1.xaml",UriKind.Relative)});
        NativeView.SetBinding(NativePicker.CurrentColorProperty,new Binding(nameof(SelectedColor))
        {Source=this,Mode=BindingMode.TwoWay,Converter=new ColorBrushAdapter()});
        Child=NativeView;
        try{_scope=new(NativeView,this);}
        catch{BindingOperations.ClearBinding(NativeView,NativePicker.CurrentColorProperty);Child=null;throw;}
    }
    public void ClosePalette()=>_scope.ClosePalette();
    private static void OnColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var presenter=(NativeColorPresenter)sender;
        if(!presenter._disposed)presenter.RaiseEvent(new RoutedPropertyChangedEventArgs<Color>(
            (Color)e.OldValue, (Color)e.NewValue, SelectedColorChangedEvent));
    }

    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        ParameterCleanup.Run(new Action[]{_scope.Dispose,
            ()=>BindingOperations.ClearBinding(NativeView,NativePicker.CurrentColorProperty),()=>Child=null});
    }
}
