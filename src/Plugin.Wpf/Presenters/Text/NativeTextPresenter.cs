using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using NativePopup=SunEyeVision.Plugin.Wpf.Controls.Popups.PopupButton;
using NativeText=SunEyeVision.Plugin.Wpf.Controls.References.Primitives.DeleteTextBox;

namespace SunEyeVision.Plugin.Wpf.Presenters.Text;

/// <summary>The original StringConfig leaf and source popup, editing a constant string.</summary>
internal sealed class NativeTextPresenter : Decorator,IDisposable
{
    private readonly NativeTextSession _session;
    private readonly ContentControl _content;
    private NativePopup? _popup;
    private NativeText? _text;
    private Window? _owner;
    private DispatcherOperation? _pending;
    private bool _disposed,_hidden,_closing,_listening,_attaching;
    private long _generation,_loadGeneration;
    internal TextBox? TextEditor=>_text;
    internal bool HasPendingEdits=>!_disposed&&_session.HasPendingEdits;
    internal NativeTextPresenter(DeclaredFieldState state,TextExpressionOptions? options,Func<bool> isCurrent)
    {
        _session=new(state,options,()=>!_disposed&&isCurrent()&&(_text is null
            ||Visuals<NativeText>(_content).Any(text=>ReferenceEquals(text,_text))));
        // The original StringConfig host is 32 DIP and stretches its popup template vertically.
        MinHeight=32;
        _content=new ContentControl{Content=_session,HorizontalContentAlignment=HorizontalAlignment.Stretch,
            VerticalContentAlignment=VerticalAlignment.Stretch};
        try
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Text/TextField.xaml",UriKind.Relative)});
            _content.ContentTemplate=(DataTemplate)FindResource("StringConfig_sub");Child=_content;
            _session.ProjectInsertionRequested+=ProjectInsertion;
            Loaded+=OnLoaded;Unloaded+=OnUnloaded;LayoutUpdated+=OnLayout;
            IsVisibleChanged+=OnVisibility;IsEnabledChanged+=OnEnabled;
            PreviewKeyDown+=OnKey;LostKeyboardFocus+=OnLostFocus;
            AttachInputs();
        }
        catch(Exception failure)
        {try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T value)yield return value;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private void AttachInputs()
    {
        if(_disposed||_attaching)return;
        _attaching=true;
        try
        {
        _content.ApplyTemplate();
        _content.UpdateLayout();
        var popup=Visuals<NativePopup>(_content).FirstOrDefault();
        if(popup is null){_popup=null;_text=null;_session.AttachText(null);return;}
        popup.ApplyTemplate();
        foreach(var toggle in Visuals<ToggleButton>(popup).ToArray())toggle.ApplyTemplate();
        var text=Visuals<NativeText>(popup).FirstOrDefault();
        if(text is null){_popup=popup;_text=null;_session.AttachText(null);return;}
        _popup=popup;_text=text;_session.AttachText(text);
        }
        finally{_attaching=false;}
    }
    private void OnLayout(object? sender,EventArgs args)
    {if(!_disposed&&(_text is null||!Visuals<NativeText>(_content).Any(text=>ReferenceEquals(text,_text))))AttachInputs();}
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(_disposed)return;++_loadGeneration;AttachInputs();
        if(!_listening){InputManager.Current.PreProcessInput+=OnInput;_listening=true;}
        if(_owner is not null)_owner.Deactivated-=OnDeactivate;
        _owner=Window.GetWindow(this);if(_owner is not null)_owner.Deactivated+=OnDeactivate;
        _session.SetSuspended(_hidden||!IsLoaded||!IsVisible||!IsEnabled);
    }
    internal bool TrySetConstant(object value)=>!_disposed&&_session.TrySetConstant(value);
    internal bool TryCommitPending()=>!_disposed&&_session.TryCommitPending();
    internal void PrepareForVisibility(bool visible)
    {
        if(_disposed)return;_hidden=!visible;
        _session.SetSuspended(!visible||!IsVisible||!IsEnabled||!IsLoaded);
        if(!visible)Close();
    }
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args)
    {
        if(_disposed)return;
        _session.SetSuspended(_hidden||!IsVisible||!IsEnabled);
        if(!IsVisible)Close();
    }
    private void OnEnabled(object sender,DependencyPropertyChangedEventArgs args)
    {
        if(_disposed)return;
        _session.SetSuspended(_hidden||!IsVisible||!IsEnabled);
        if(!IsEnabled)Close();
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(_disposed||_hidden||!IsEnabled)return;
        if(args.Key==Key.Escape){Close();args.Handled=true;return;}
        if(args.Key!=Key.Enter)return;
        if(!IsLoaded||!IsVisible||_text is not {IsKeyboardFocused:true} text
            ||args.OriginalSource is not DependencyObject origin||!IsWithinText(origin,text))return;
        // Intercept at the parent before the original HikTextBox MoveFocus(Next).
        if(!TryCommitPending()){args.Handled=true;return;}
        if(_disposed){args.Handled=true;return;}
        Close();
    }
    private static bool IsWithinText(DependencyObject? node,TextBox text)
    {
        var visited=new HashSet<DependencyObject>();
        while(node is not null&&visited.Add(node))
        {
            if(ReferenceEquals(node,text))return true;
            node=LogicalTreeHelper.GetParent(node)??(node is Visual?VisualTreeHelper.GetParent(node):null);
        }
        return false;
    }
    private void OnLostFocus(object sender,KeyboardFocusChangedEventArgs args)
    {
        if(_disposed||_closing||_hidden||!IsEnabled)return;
        CancelPending();var generation=_generation;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>
        {
            if(_disposed||generation!=_generation||_hidden||!IsLoaded||!IsVisible||!IsEnabled)return;
            _pending=null;
            if(IsKeyboardFocusWithin||_popup?.Template?.FindName("PART_Popup",_popup) is Popup {Child:{IsKeyboardFocusWithin:true}})return;
            TryCommitPending();if(!_disposed)Close();
        }));
    }
    private void ProjectInsertion(Action project,string proposed,int caret,Func<bool> ownsCaret)
    {
        if(_disposed||!ownsCaret()||_text is not {} text){project();return;}
        var binding=text.GetBindingExpression(TextBox.TextProperty);
        bool Current()=>!_disposed&&ReferenceEquals(text,_text)&&IsLoaded&&IsVisible&&IsEnabled
            &&ReferenceEquals(binding,text.GetBindingExpression(TextBox.TextProperty))&&ownsCaret();
        try{Close();}
        catch(Exception failure){try{project();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
        if(!Current()){project();return;}
        try{text.Focus();}
        catch(Exception failure){try{project();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
        if(!Current()||!text.IsKeyboardFocused){project();return;}
        text.BeginChange();
        Exception? error=null;
        try
        {
            project();
            if(Current())binding?.UpdateTarget();
            if(Current()&&text.Text==proposed)text.Select(Math.Clamp(caret,0,text.Text.Length),0);
        }
        catch(Exception failure){error=failure;}
        try{text.EndChange();}
        catch(Exception failure){error=error is null?failure:new AggregateException(error,failure);}
        if(error is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private bool Owns(DependencyObject? node)
    {
        var visited=new HashSet<DependencyObject>();
        while(node is not null&&visited.Add(node))
        {
            if(ReferenceEquals(node,this)||ReferenceEquals(node,_popup))return true;
            node=node is Popup popup?popup.PlacementTarget:LogicalTreeHelper.GetParent(node)
                ??(node is Visual?VisualTreeHelper.GetParent(node):null);
        }
        return false;
    }
    private void OnInput(object sender,PreProcessInputEventArgs args)
    {
        if(_disposed||_popup?.IsDropDownOpen!=true)return;
        if(args.StagingItem.Input is MouseButtonEventArgs {ButtonState:MouseButtonState.Pressed} mouse
            &&!Owns(Mouse.DirectlyOver as DependencyObject??mouse.OriginalSource as DependencyObject))Close();
    }
    private void CancelPending(){_generation++;_pending?.Abort();_pending=null;}
    private void Close()
    {
        CancelPending();var wasClosing=_closing;_closing=true;
        var popup=_popup;var generation=_generation;
        try{ParameterCleanup.Run(new Action[]{_session.Close,()=>
        {
            if(generation==_generation&&ReferenceEquals(popup,_popup)&&!_session.IsTextDropDownOpen)
                popup?.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);
        }});}
        finally{_closing=wasClosing;}
    }
    private void OnDeactivate(object? sender,EventArgs args)=>Close();
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        var generation=++_loadGeneration;var owner=_owner;_owner=null;
        bool StillUnloaded()=>!_disposed&&generation==_loadGeneration&&!IsLoaded;
        ParameterCleanup.Run(new Action[]{()=>{if(owner is not null)owner.Deactivated-=OnDeactivate;},StopListening,
            ()=>{if(StillUnloaded())_session.SetSuspended(true);},
            ()=>{if(StillUnloaded())Close();}});
    }
    private void StopListening()
    {if(_listening)InputManager.Current.PreProcessInput-=OnInput;_listening=false;}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        ParameterCleanup.Run(new Action[]{()=>_session.SetSuspended(true),Close,
            StopListening,
            ()=>{if(_owner is not null)_owner.Deactivated-=OnDeactivate;_owner=null;},
            ()=>{Loaded-=OnLoaded;Unloaded-=OnUnloaded;LayoutUpdated-=OnLayout;IsVisibleChanged-=OnVisibility;IsEnabledChanged-=OnEnabled;
                PreviewKeyDown-=OnKey;LostKeyboardFocus-=OnLostFocus;_session.ProjectInsertionRequested-=ProjectInsertion;},
            _session.Dispose,()=>{_content.Content=null;Child=null;_text=null;_popup=null;}});
    }
}
