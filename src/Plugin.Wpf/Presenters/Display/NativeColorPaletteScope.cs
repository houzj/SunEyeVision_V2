using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using NativePicker=SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView.ColorPickerControlView;
using NativePopup=SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls.PopupButton;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>Owns palette subscriptions over an existing original picker, never its visual or bindings.</summary>
internal sealed class NativeColorPaletteScope : IDisposable
{
    private readonly NativePopup _palette;
    private readonly FrameworkElement _eventRoot;
    private UIElement? _paletteContent;
    private DispatcherOperation? _pendingTextInheritance;
    private bool _disposed;
    private long _generation;
    internal NativePopup Palette=>_palette;
    internal NativeColorPaletteScope(NativePicker view,FrameworkElement eventRoot)
    {
        _palette=((Grid)view.Content).Children.OfType<NativePopup>().Single();_eventRoot=eventRoot;
        try
        {
            Palette.DropDownOpened+=OnOpened;Palette.DropDownClosed+=OnClosed;
            eventRoot.Unloaded+=OnUnloaded;eventRoot.IsVisibleChanged+=OnVisibilityChanged;
        }
        catch(Exception failure)
        {try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    public void ClosePalette()
    {
        ParameterCleanup.Run(new Action[]{InvalidatePendingTextInheritance,
            ()=>Palette.IsDropDownOpen=false,DetachPaletteContent});
    }

    private void OnOpened(object? sender,EventArgs args)
    {if(_disposed){ClosePalette();return;}QueueTextInheritance();}
    private void OnClosed(object? sender,EventArgs args)
    {InvalidatePendingTextInheritance();DetachPaletteContent();}
    private void OnUnloaded(object sender,RoutedEventArgs args)=>ClosePalette();
    private void OnVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)
    {if(!_eventRoot.IsVisible)ClosePalette();}

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
        _pendingTextInheritance=_eventRoot.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
        if (_disposed || generation!=_generation || !_eventRoot.IsLoaded || !Palette.IsDropDownOpen
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


    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        ParameterCleanup.Run(new Action[]{ClosePalette,DetachPaletteContent,
            ()=>{Palette.DropDownOpened-=OnOpened;Palette.DropDownClosed-=OnClosed;},
            ()=>{_eventRoot.Unloaded-=OnUnloaded;_eventRoot.IsVisibleChanged-=OnVisibilityChanged;}});
    }
}
