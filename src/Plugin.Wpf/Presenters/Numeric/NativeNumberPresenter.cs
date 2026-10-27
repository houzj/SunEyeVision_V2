using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

namespace SunEyeVision.Plugin.Wpf.Presenters.Numeric;

/// <summary>Selects the original single-value composition and subscription template over one borrowed field state.</summary>
internal sealed class NativeNumberPresenter : Decorator,IDisposable
{
    private readonly DeclaredFieldState _state;
    private readonly FieldLayout? _layout;
    private readonly Numbers.SliderArrangeBase? _arrange;
    private readonly FrameworkElement _native;
    private readonly bool _allowNewReference;
    private readonly ReferenceFieldSession? _reference;
    private readonly ReferencePopupScope? _referenceScope;
    private readonly ReferenceExpansionCoordinator? _expansion;
    private NativeNumericCommit? _commit;
    private Numbers.NumericControl? _leaf;
    private ComboBox? _box;
    private Window? _owner;
    private DispatcherOperation? _pending;
    private DispatcherOperation? _resume;
    private bool _active,_disposed,_syncing,_committing,_closing,_showSlider,_presentationHidden;
    private long _generation;
    private long _loadGeneration;
    public TextBox? TextEditor=>_commit?.Text;
    public static bool UsesOriginalModes(Port port,FieldPresentation? presentation)
        =>port.Type.Shape==ValueShape.Scalar&&(port.Type.Scalar==ScalarKind.Int64
            ||port.Type.Scalar==ScalarKind.Float64&&presentation?.Layout is {Minimum:not null,Maximum:not null});

    public NativeNumberPresenter(DeclaredFieldState state,FieldPresentation? presentation,ReferenceFieldOptions? references,Func<OutputReference,string>? format)
    {
        _state=state;_layout=presentation?.Layout;
        _allowNewReference=references is not null&&presentation?.Layout?.AllowReference!=false;
        if(!state.IsReferenceMode&&state.Accepted is ConstantSetting initial)NativeNumericCommit.EnsureRepresentable(initial);
        if(state.Port.Type.Scalar==ScalarKind.Float64&&_layout?.DecimalPlaces>9)
            throw new ArgumentException("原浮点控件仅支持0至9位编辑精度。");
        Language=XmlLanguage.GetLanguage("en-US");Height=32;
        _showSlider=_layout is {Minimum:not null,Maximum:not null};
        if(UsesOriginalModes(state.Port,presentation))
        {
            double minimum,maximum;
            if(_showSlider){minimum=_layout!.Minimum!.Value;maximum=_layout.Maximum!.Value;}
            else
            {
                var range=NumericStorageRules.IntegerRange(NumericStorageRules.Effective(state.Port.Type,state.Port.Storage)!.Value);
                minimum=range.Minimum;maximum=range.Maximum;
            }
            _arrange=state.Port.Type.Scalar==ScalarKind.Int64?new Numbers.SliderArrange():new Numbers.SliderArrangeFloat{DecimalDigits=_layout?.DecimalPlaces??2};
            _arrange.Minimum=minimum;_arrange.Maximum=maximum;
            _arrange.MinimumLeft=minimum;_arrange.MaximumLeft=maximum;
            _arrange.MinimumRight=minimum;_arrange.MaximumRight=maximum;
            _arrange.SliderTickFrequency=_layout?.Increment??1;
            _arrange.EndValue=maximum;
            _native=_arrange;
            if((state.Port.EditModes&FieldEditModes.Reference)!=0
                &&(presentation?.Layout?.AllowReference!=false||state.IsReferenceMode))
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary
                {Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)});
                _expansion=new(this);
                _reference=new(state,references??ReferenceFieldOptions.Empty,format,expand:_expansion.Open,preference:_expansion.Preference);
                _arrange.SubscribeTemplate=(DataTemplate)FindResource("ButtonSelecter_sub");
                _arrange.SubscribeDataLeft=_reference;
                _arrange.IsSubscribeVisible=(references is not null||state.IsReferenceMode)&&(state.Port.EditModes&FieldEditModes.Constant)!=0?Visibility.Visible:Visibility.Collapsed;
                _referenceScope=new(this,_reference,()=>!_state.IsDisposed&&_state.IsReferenceMode);
            }
            _arrange.OnStartValueChange+=OnNativeValue;
            _arrange.OnLeftTypeChange+=OnModeChanged;
        }
        else
        {
            _leaf=new Numbers.NumericControl{Minimum=_layout?.Minimum??-double.MaxValue,Maximum=_layout?.Maximum??double.MaxValue,Increment=_layout?.Increment??1};
            _leaf.OnNumericClick+=OnLeafValue;
            _native=_leaf;
        }
        _native.SetValue(Control.ForegroundProperty,new SolidColorBrush(Color.FromRgb(102,102,102)));
        _native.SetValue(Control.FontSizeProperty,14d);
        Child=_native;
        _state.Changed+=OnStateChanged;
        Loaded+=OnLoaded;Unloaded+=OnUnloaded;IsVisibleChanged+=OnVisibilityChanged;
        PreviewKeyDown+=OnKey;PreviewMouseWheel+=OnWheel;LostKeyboardFocus+=OnLostFocus;
        AttachInput();
        ProjectState();
    }
    private void ApplySliderVisibility()
    {
        if(_arrange is null)return;
        if(_arrange is Numbers.SliderArrangeFloat floating)floating.NeedSlider=_showSlider;
        _arrange.SliderVisibility=_showSlider&&!_arrange.IsSubscribeLeft&&!_arrange.IsSubscribeRight?Visibility.Visible:Visibility.Collapsed;
    }
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(_disposed)return;_active=true;
        var generation=++_loadGeneration;
        AttachInput();
        if(_disposed||_state.IsDisposed)return;
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;
        _owner=Window.GetWindow(this);if(_owner is not null)_owner.Deactivated+=OnDeactivated;
        ProjectState();_referenceScope?.Refresh();
        _resume?.Abort();
        _resume=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {if(_disposed||!_active||!IsVisible||_presentationHidden||generation!=_loadGeneration)return;_resume=null;_commit?.Resume();}));
    }
    private void AttachInput()
    {
        _native.ApplyTemplate();_native.UpdateLayout();
        if(_arrange is not null)
        {
            _box=(ComboBox)_arrange.FindName("CBox");_box.ApplyTemplate();
            _leaf=_arrange.EnsureTemplateInputs().Left;
        }
        if(_commit is null||!ReferenceEquals(_commit.Text,_leaf!.FindName("ValueText")))
        {
            var raw=_commit?.PendingRaw;
            _commit?.Dispose();_commit=new(_state,_layout,_leaf!,_arrange,_arrange is null?null:Numbers.SliderArrangeBase.StartValueProperty);
            _commit.RestorePending(raw);_commit.ValidateInitial();
        }
    }
    private void ProjectState()
    {
        if(_disposed||_syncing)return;
        _syncing=true;
        try
        {
            if(_arrange is not null)
            {
                _arrange.SetCurrentValue(Numbers.SliderArrangeBase.IsSubscribeLeftProperty,_state.IsReferenceMode);
                _arrange.IsSubscribeVisible=_reference is not null&&(_state.Port.EditModes&FieldEditModes.Constant)!=0
                    &&(_allowNewReference||_state.IsReferenceMode)?Visibility.Visible:Visibility.Collapsed;
                if(!_state.IsReferenceMode)_referenceScope?.ClosePopups();
            }
            if(_commit is not {IsDirty:true})_commit?.ProjectAccepted();
            else if(_state.IsReferenceMode)_commit?.ProjectAccepted();
            if(_commit is null&&(_state.Accepted as ConstantSetting??_state.ConstantBackup) is {} initial&&_arrange is not null)
                _arrange.StartValue=Convert.ToDouble(initial.Value,CultureInfo.InvariantCulture);
            ApplySliderVisibility();
        }
        finally{_syncing=false;}
    }
    public void ProjectAccepted(){_commit?.ProjectAccepted();ProjectState();}
    private void OnStateChanged()=>ProjectState();
    public bool TrySetConstant(object value)
    {
        if(_disposed||_committing)return false;
        ConstantSetting proposal;
        try{proposal=_commit!.PrepareValue(value);}
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){_state.SetEditError(error.Message);return false;}
        _committing=true;
        try{var accepted=_state.TrySet(proposal);if(_disposed||_state.IsDisposed)return false;if(accepted)ProjectAccepted();return accepted&&!_state.HasInvalidInput;}
        finally{_committing=false;}
    }
    public bool TryCommitPending(bool retryAccepted=false)
    {
        if(_disposed||_state.IsDisposed)return false;
        if(_committing)return !_state.HasInvalidInput;
        if(_state.IsReferenceMode)return _reference?.TryCommitPending()??!_state.HasInvalidInput;
        if(_commit is null)return !_state.HasInvalidInput;
        if(!_commit.IsDirty&&!retryAccepted)return !_state.HasInvalidInput;
        _committing=true;
        try
        {
            if(!_commit.TryPrepare(out var proposal))return false;
            if(_disposed||_state.IsDisposed)return false;
            // Host and observer failures are outside raw parsing and binding validation.
            var raw=_commit.PendingRaw;
            var accepted=_state.TrySet(proposal);
            if(_disposed||_state.IsDisposed)return false;
            if(accepted)_commit.ProjectAccepted();else _commit.RestoreRejected(raw);
            return accepted&&!_state.HasInvalidInput;
        }
        finally{_committing=false;}
    }
    private void OnNativeValue(object sender,RoutedPropertyChangedEventArgs<double> args)
    {if(_active&&!_disposed&&!_syncing&&!_closing&&!_presentationHidden&&!_state.IsReferenceMode)TryCommitPending();}
    private void OnLeafValue(object? sender,EventArgs args)
    {if(_active&&!_disposed&&!_syncing&&!_closing&&!_presentationHidden)TryCommitPending();}
    private void OnModeChanged(object sender,RoutedPropertyChangedEventArgs<double> args)
    {
        if(_disposed||_syncing||_presentationHidden)return;
        try
        {
            if(_arrange!.IsSubscribeLeft)
            {
                if(!_allowNewReference){_state.SetEditError("此字段不允许新增订阅。");return;}
                if(TryCommitPending())_state.EnterReferenceMode();
            }
            else
            {
                try{if(_state.ConstantBackup is {} backup)_commit!.ValidateStored(backup);}
                catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){_state.SetEditError(error.Message);return;}
                _state.TryUseConstant();
            }
        }
        finally{ProjectState();_referenceScope?.Refresh();}
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(_state.IsReferenceMode)return;
        if(args.Key==Key.Escape){ClosePopups();args.Handled=true;return;}
        if(args.Key!=Key.Enter)return;
        args.Handled=true;
        if(TryCommitPending(retryAccepted:true))
        {
            ClosePopups();
            if(TextEditor is {} text)FocusManager.SetFocusedElement(FocusManager.GetFocusScope(text),null);
            Keyboard.ClearFocus();
        }
    }
    private void Queue(Action action)
    {
        Cancel();var generation=_generation;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>
        {if(_disposed||generation!=_generation||!_active||!IsVisible)return;_pending=null;action();}));
    }
    private void OnWheel(object sender,MouseWheelEventArgs args)
    {if(!_state.IsReferenceMode&&TextEditor?.IsKeyboardFocused==true)Queue(()=>TryCommitPending());}
    private void OnLostFocus(object sender,KeyboardFocusChangedEventArgs args)
    {
        if(_state.IsReferenceMode||_presentationHidden)return;
        Queue(()=>
        {
            TryCommitPending();
            if(!IsKeyboardFocusWithin&&_box?.Template.FindName("PART_Popup",_box) is not Popup {Child:{IsKeyboardFocusWithin:true}})ClosePopups();
        });
    }
    private void Cancel(){_generation++;_pending?.Abort();_pending=null;}
    private void ClosePopups()
    {
        Cancel();_closing=true;
        try{if(_box is not null)_box.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);_referenceScope?.ClosePopups();}
        finally{_closing=false;}
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {_active=false;_loadGeneration++;_resume?.Abort();_resume=null;_commit?.Suspend();ClosePopups();if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=null;}
    internal void PrepareForVisibility(bool visible)
    {
        _referenceScope?.PrepareForVisibility(visible);
        if(_disposed)return;
        if(!visible)
        {
            _presentationHidden=true;_loadGeneration++;_resume?.Abort();_resume=null;
            _commit?.Suspend();ClosePopups();
        }
        else
        {
            _presentationHidden=false;
            if(!_active||!IsVisible||_commit?.IsSuspended!=true)return;
            var generation=++_loadGeneration;_resume?.Abort();
            _resume=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
            {if(_disposed||!_active||!IsVisible||_presentationHidden||generation!=_loadGeneration)return;_resume=null;_commit?.Resume();}));
        }
    }
    private void OnVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)=>PrepareForVisibility(IsVisible);
    private void OnDeactivated(object? sender,EventArgs args)=>ClosePopups();
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_active=false;ClosePopups();
        _loadGeneration++;_resume?.Abort();_resume=null;
        _state.Changed-=OnStateChanged;
        if(_arrange is not null){_arrange.OnStartValueChange-=OnNativeValue;_arrange.OnLeftTypeChange-=OnModeChanged;_arrange.SubscribeDataLeft=null;}
        else if(_leaf is not null)_leaf.OnNumericClick-=OnLeafValue;
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=null;
        _expansion?.Dispose();_commit?.Dispose();_referenceScope?.Dispose();_reference?.Dispose();
        Loaded-=OnLoaded;Unloaded-=OnUnloaded;IsVisibleChanged-=OnVisibilityChanged;
        PreviewKeyDown-=OnKey;PreviewMouseWheel-=OnWheel;LostKeyboardFocus-=OnLostFocus;
        Child=null;
    }
}
