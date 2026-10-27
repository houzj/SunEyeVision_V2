using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using NativePopup=SunEyeVision.Plugin.Wpf.Native.Input.Apps.PcUI.Controls.PopupButton;
using NativeText=SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.DeleteTextBox;

namespace SunEyeVision.Plugin.Wpf.Presenters.References;

/// <summary>The original two-popup subscription composition over a borrowed field state.</summary>
internal sealed class NativeReferencePresenter : Decorator,IDisposable
{
    private readonly ReferenceFieldSession _session;
    private readonly ContentControl _content;
    private NativeText? _text;
    private Window? _owner;
    private DispatcherOperation? _pending;
    private bool _disposed;
    private long _generation;
    internal NativeReferencePresenter(DeclaredFieldState state,ReferenceFieldOptions options,Func<SunEyeVision.Plugin.Contracts.OutputReference,string>? format)
    {
        _session=new(state,options,format);
        _session.CloseRequested+=ClosePopups;
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)});
        _content=new ContentControl{Content=_session,ContentTemplate=(DataTemplate)FindResource("ButtonSelecter_sub")};Child=_content;
        Loaded+=OnLoaded;Unloaded+=OnUnloaded;IsVisibleChanged+=OnVisibilityChanged;
        PreviewKeyDown+=OnKey;LostKeyboardFocus+=OnLostFocus;
    }
    public bool TryCommitPending()=>!_disposed&&_session.TryCommitPending();
    private IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T match)yield return match;
        if(root is not Visual)yield break;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)
            foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(root,index)))yield return child;
    }
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(_disposed)return;
        _content.ApplyTemplate();_content.UpdateLayout();
        DetachText();_text=Visuals<NativeText>(_content).SingleOrDefault();
        if(_text is not null)_text.TextChanged+=OnTextChanged;
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;
        _owner=Window.GetWindow(this);if(_owner is not null)_owner.Deactivated+=OnDeactivated;
    }
    private void DetachText(){if(_text is not null)_text.TextChanged-=OnTextChanged;_text=null;}
    private void OnTextChanged(object sender,TextChangedEventArgs args)
    {if(!_disposed&&_text is not null)_session.ObserveText(_text.Text);}
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(args.Key==Key.Escape){ClosePopups();args.Handled=true;return;}
        if(args.Key==Key.Enter)
        {
            args.Handled=true;
            if(TryCommitPending()){ClosePopups();Keyboard.ClearFocus();}
        }
    }
    private void OnLostFocus(object sender,KeyboardFocusChangedEventArgs args)
    {
        Cancel();var generation=_generation;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            if(_disposed||generation!=_generation||!IsLoaded)return;
            _pending=null;
            var popups=Visuals<NativePopup>(_content).ToArray();
            if(!IsKeyboardFocusWithin&&!popups.Any(popup=>popup.Template?.FindName("PART_Popup",popup)
                is System.Windows.Controls.Primitives.Popup {Child:{IsKeyboardFocusWithin:true}}))ClosePopups();
        }));
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        ClosePopups();DetachText();if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=null;
    }
    private void OnVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args){if(!IsVisible)ClosePopups();}
    private void OnDeactivated(object? sender,EventArgs args)=>ClosePopups();
    private void Cancel(){_generation++;_pending?.Abort();_pending=null;}
    private void ClosePopups()
    {
        Cancel();_session.Close();
        // The original IsPopupOpen callback is focus-gated; final close must target the actual popup DP.
        foreach(var popup in Visuals<NativePopup>(_content))popup.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;ClosePopups();DetachText();
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=null;
        Loaded-=OnLoaded;Unloaded-=OnUnloaded;IsVisibleChanged-=OnVisibilityChanged;
        PreviewKeyDown-=OnKey;LostKeyboardFocus-=OnLostFocus;
        _session.CloseRequested-=ClosePopups;_session.Dispose();_content.Content=null;Child=null;
    }
}
