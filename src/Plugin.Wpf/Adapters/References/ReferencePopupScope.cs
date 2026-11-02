using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using NativePopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using NativeText=SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>Owns only listeners and popup lifetime for one borrowed original subscription session.</summary>
internal sealed class ReferencePopupScope : IDisposable
{
    private readonly ReferenceFieldSession _session;
    private readonly FrameworkElement _root;
    private readonly Func<bool> _isActive;
    private NativeText? _text;
    private Window? _owner;
    private DispatcherOperation? _pending;
    private bool _disposed,_presentationAllowed=true;
    private long _generation;
    public ReferencePopupScope(FrameworkElement root,ReferenceFieldSession session,Func<bool> isActive)
    {
        _root=root;_session=session;_isActive=isActive;
        _session.CloseRequested+=ClosePopups;
        _root.Loaded+=OnLoaded;_root.Unloaded+=OnUnloaded;_root.IsVisibleChanged+=OnVisibilityChanged;_root.IsEnabledChanged+=OnVisibilityChanged;
        _root.PreviewKeyDown+=OnKey;_root.LostKeyboardFocus+=OnLostFocus;
        _session.SetSuspended(!_root.IsLoaded||!_root.IsVisible||!_root.IsEnabled);
    }
    private IEnumerable<NativePopup> Popups()=>Visuals<NativePopup>(_root).Where(popup=>ReferenceEquals(popup.DataContext,_session));
    public void Refresh(){if(!_disposed&&_root.IsLoaded)Attach();}
    public bool TryCommitPending()=>!_disposed&&_session.TryCommitPendingFromUi();
    public void PrepareForVisibility(bool visible)
    {
        if(_disposed)return;
        _presentationAllowed=visible;
        UpdateAvailability();
    }
    private void UpdateAvailability()
    {
        var suspended=_disposed||!_presentationAllowed||!_root.IsLoaded||!_root.IsVisible||!_root.IsEnabled;
        _session.SetSuspended(suspended);
        if(suspended)
        {
            // Suspend before closing: native LostFocus bindings must only preserve the edit buffer.
            SunEyeVision.Plugin.Wpf.Adapters.Fields.ParameterCleanup.Run(new Action[]{_session.InvalidateExpansion,ClosePopups});
        }
    }
    private IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T match)yield return match;
        if(root is not Visual)yield break;
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)
            foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(root,index)))yield return child;
    }
    private void OnLoaded(object sender,RoutedEventArgs args)
    {if(ReferenceEquals(args.OriginalSource,_root))Attach();}
    private void Attach()
    {
        if(_disposed)return;
        UpdateAvailability();
        var generation=_generation;
        _root.ApplyTemplate();_root.UpdateLayout();
        if(_disposed||generation!=_generation||!_root.IsLoaded)return;
        DetachText();_text=Visuals<NativeText>(_root).SingleOrDefault(text=>ReferenceEquals(text.DataContext,_session));
        if(_text is not null)_text.TextChanged+=OnTextChanged;
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;
        _owner=Window.GetWindow(_root);if(_owner is not null)_owner.Deactivated+=OnDeactivated;
    }
    private void DetachText(){if(_text is not null)_text.TextChanged-=OnTextChanged;_text=null;}
    private void OnTextChanged(object sender,TextChangedEventArgs args)
    {if(!_disposed&&_isActive()&&_text is not null&&ReferenceEquals(sender,_text))_session.ObserveText(_text.Text);}
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(_disposed||!_isActive())return;
        if(_text?.IsKeyboardFocusWithin!=true&&!Popups().Any(popup=>popup.IsKeyboardFocusWithin
            ||popup.Template?.FindName("PART_Popup",popup)
            is System.Windows.Controls.Primitives.Popup {Child:{IsKeyboardFocusWithin:true}}))return;
        if(args.Key==Key.Escape){ClosePopups();args.Handled=true;return;}
        if(args.Key==Key.Enter)
        {
            args.Handled=true;
            var text=_text;var popupGeneration=_session.PopupGeneration;
            bool OwnsFocus()=>!_disposed&&ReferenceEquals(text,_text)&&_root.IsLoaded&&_root.IsVisible
                &&popupGeneration==_session.PopupGeneration
                &&(text?.IsKeyboardFocusWithin==true||Popups().Any(popup=>popup.IsKeyboardFocusWithin
                    ||popup.Template?.FindName("PART_Popup",popup) is System.Windows.Controls.Primitives.Popup {Child:{IsKeyboardFocusWithin:true}}));
            if(TryCommitPending()&&OwnsFocus()){ClosePopups();if(OwnsFocus())Keyboard.ClearFocus();}
        }
    }
    private void OnLostFocus(object sender,KeyboardFocusChangedEventArgs args)
    {
        Cancel();var generation=_generation;
        _pending=_root.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            if(_disposed||generation!=_generation||!_root.IsLoaded)return;
            _pending=null;
            var popups=Popups().ToArray();
            if(_text?.IsKeyboardFocusWithin!=true&&!popups.Any(popup=>popup.IsKeyboardFocusWithin
                ||popup.Template?.FindName("PART_Popup",popup)
                is System.Windows.Controls.Primitives.Popup {Child:{IsKeyboardFocusWithin:true}}))ClosePopups();
        }));
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        if(!ReferenceEquals(args.OriginalSource,_root))return;
        Cancel();var owner=_owner;_owner=null;DetachText();
        ParameterCleanup.Run(new Action[]{()=>{if(owner is not null)owner.Deactivated-=OnDeactivated;},
            ()=>{if(!_root.IsLoaded)_session.SetSuspended(true);},
            ()=>{if(!_root.IsLoaded)_session.InvalidateExpansion();},()=>{if(!_root.IsLoaded)ClosePopups();}});
    }
    private void OnVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args){if(!_disposed)UpdateAvailability();}
    private void OnDeactivated(object? sender,EventArgs args)=>ClosePopups();
    private void Cancel(){_generation++;_pending?.Abort();_pending=null;}
    public void ClosePopups()
    {
        Cancel();var generation=_session.PopupGeneration;var popups=Popups().ToArray();
        // The original IsPopupOpen callback is focus-gated; final close must target the actual popup DP.
        ParameterCleanup.Run(new Action[]{_session.Close}.Concat(popups.Select(popup=>(Action)(()=>
        {
            if(generation==_session.PopupGeneration&&ReferenceEquals(popup.DataContext,_session))
                popup.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);
        }))));
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Cancel();var owner=_owner;_owner=null;DetachText();
        _root.Loaded-=OnLoaded;_root.Unloaded-=OnUnloaded;_root.IsVisibleChanged-=OnVisibilityChanged;_root.IsEnabledChanged-=OnVisibilityChanged;
        _root.PreviewKeyDown-=OnKey;_root.LostKeyboardFocus-=OnLostFocus;
        _session.CloseRequested-=ClosePopups;
        ParameterCleanup.Run(new Action[]{()=>{if(owner is not null)owner.Deactivated-=OnDeactivated;},
            ()=>_session.SetSuspended(true),_session.InvalidateExpansion,ClosePopups});
    }
}
