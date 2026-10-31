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

/// <summary>Coordinates one borrowed original input without owning its visual, bindings or field state.</summary>
internal sealed class NativeNumberCoordinator : IDisposable
{
    private readonly DeclaredFieldState _state;
    private readonly FrameworkElement _eventRoot;
    private readonly Func<bool>? _isCurrent;
    private readonly Func<Window?>? _getOwner;
    private readonly bool _requireStartBinding;
    private readonly Action<double>? _projectBoundValue;
    private ResourceDictionary? _referenceResources;
    private bool Current()
    {
        if(_disposed||_state.IsDisposed)return false;
        var current=_isCurrent?.Invoke()??true;
        return current&&!_disposed&&!_state.IsDisposed;
    }
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
    private long _attachGeneration;
    private bool _acceptedProjectionPending;
    private long _observedAcceptance;
    private bool _observedReference;
    private void DrainAcceptedProjection(){if(!_syncing&&_acceptedProjectionPending){_acceptedProjectionPending=false;if(_commit?.IsDirty==true)ProjectState();else ProjectAccepted();}}
    public TextBox? TextEditor=>_commit?.Text;
    internal bool HasPendingEdits=>!_disposed&&(_commit?.IsDirty==true||_reference?.HasPendingText==true);
    public NativeNumberCoordinator(DeclaredFieldState state,FieldPresentation? presentation,
        FrameworkElement eventRoot,FrameworkElement nativeInput,ReferenceFieldOptions? references=null,
        Func<OutputReference,string>? format=null,bool requireStartBinding=false,
        Action<double>? projectBoundValue=null,Func<bool>? isCurrent=null,Func<Window?>? getOwner=null)
    {
        _state=state;_layout=presentation?.Layout;_eventRoot=eventRoot;_native=nativeInput;
        _observedAcceptance=state.AcceptanceVersion;
        _observedReference=state.IsReferenceMode;
        _isCurrent=isCurrent;_getOwner=getOwner;_requireStartBinding=requireStartBinding;_projectBoundValue=projectBoundValue;
        _arrange=nativeInput as Numbers.SliderArrangeBase;
        if(_arrange is null&&nativeInput is not Numbers.NumericControl)throw new ArgumentException("必须借用原数值输入。");
        if(requireStartBinding&&(_arrange is null||projectBoundValue is null))throw new ArgumentException("模板数值必须提供真实桥绑定和投影。");
        if(_layout is {DecimalPlaces:null})throw new ArgumentException("原数值控件必须声明固定小数位。");
        if(state.Port.Type.Scalar==ScalarKind.Float64&&_layout?.DecimalPlaces>9)throw new ArgumentException("原浮点控件仅支持0至9位编辑精度。");
        _allowNewReference=references is not null&&presentation?.Layout?.AllowReference!=false;
        _showSlider=_layout is {Minimum:not null,Maximum:not null};
        if(!state.IsReferenceMode&&state.Accepted is ConstantSetting initial)NativeNumericCommit.EnsureRepresentable(initial);
        try
        {
            if(_arrange is not null)
            {
                if((state.Port.EditModes&FieldEditModes.Reference)!=0&&(presentation?.Layout?.AllowReference!=false||state.IsReferenceMode))
                {
                    _referenceResources=new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)};
                    _eventRoot.Resources.MergedDictionaries.Add(_referenceResources);
                    _expansion=new(eventRoot);
                    _reference=new(state,references??ReferenceFieldOptions.Empty,format,expand:_expansion.Open,preference:_expansion.Preference,selectVariable:_expansion.OpenVariable);
                    _arrange.SubscribeTemplate=(DataTemplate)eventRoot.FindResource("ButtonSelecter_sub");
                    _arrange.SubscribeDataLeft=_reference;
                    _referenceScope=new(eventRoot,_reference,()=>Current()&&_state.IsReferenceMode);
                }
                _arrange.OnStartValueChange+=OnNativeValue;_arrange.OnLeftTypeChange+=OnModeChanged;
            }
            else{_leaf=(Numbers.NumericControl)nativeInput;_leaf.OnNumericClick+=OnLeafValue;}
            _state.Changed+=OnStateChanged;_state.ExplicitlyAccepted+=OnExplicitlyAccepted;
            eventRoot.Loaded+=OnLoaded;eventRoot.Unloaded+=OnUnloaded;eventRoot.IsVisibleChanged+=OnVisibilityChanged;
            eventRoot.IsEnabledChanged+=OnEnabledChanged;
            eventRoot.PreviewKeyDown+=OnKey;eventRoot.PreviewMouseWheel+=OnWheel;eventRoot.LostKeyboardFocus+=OnLostFocus;
            AttachInput();ProjectState();
            if(eventRoot.IsLoaded&&Current())OnLoaded(eventRoot,new RoutedEventArgs());
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;
        }
    }
    private void ApplySliderVisibility()
    {
        if(_arrange is null)return;
        if(_arrange is Numbers.SliderArrangeFloat floating)floating.NeedSlider=_showSlider;
        _arrange.SliderVisibility=_showSlider&&!_arrange.IsSubscribeLeft&&!_arrange.IsSubscribeRight?Visibility.Visible:Visibility.Collapsed;
    }
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(!Current())return;_active=true;
        _loadGeneration++;
        AttachInput();
        if(_disposed||_state.IsDisposed)return;
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;
        var owner=_getOwner?.Invoke()??Window.GetWindow(_eventRoot);
        if(!Current())return;
        _owner=owner;if(_owner is not null)_owner.Deactivated+=OnDeactivated;
        ProjectState();_referenceScope?.Refresh();
        _referenceScope?.PrepareForVisibility(!_presentationHidden&&_eventRoot.IsVisible&&_eventRoot.IsEnabled);
        QueueResume();
    }
    public void AttachInput()
    {
        if(!Current())return;
        var syncing=_syncing;_syncing=true;
        try
        {
        var generation=++_attachGeneration;var old=_commit;old?.Suspend();var raw=old?.Pending;
        var acceptance=_state.AcceptanceVersion;var edit=old?.EditVersion;
        _native.ApplyTemplate();_native.UpdateLayout();
        if(!Current()||generation!=_attachGeneration||!ReferenceEquals(old,_commit))return;
        if(_arrange is not null)
        {
            _box=(ComboBox)_arrange.FindName("CBox");_box.ApplyTemplate();
            _leaf=_arrange.EnsureTemplateInputs().Left;
        }
        if(!Current()||generation!=_attachGeneration||!ReferenceEquals(old,_commit))return;
        if(acceptance!=_state.AcceptanceVersion||edit!=old?.EditVersion)raw=null;
        if(_commit is null||!ReferenceEquals(_commit.Text,_leaf!.FindName("ValueText")))
        {
            old?.Dispose();var created=new NativeNumericCommit(_state,_layout,_leaf!,_arrange,_arrange is null?null:Numbers.SliderArrangeBase.StartValueProperty,requireOwnerBinding:_requireStartBinding,isCurrent:CurrentInput,projectBoundValue:_projectBoundValue);
            if(!Current()||generation!=_attachGeneration||!ReferenceEquals(old,_commit)){created.Dispose();return;}
            _commit=created;
            _commit.RestorePending(raw);_commit.ValidateInitial();
        }
        else _commit.RestorePending(raw);
        }
        finally{_syncing=syncing;DrainAcceptedProjection();}
    }
    private void ResumeCommit()
    {
        if(!CurrentInput()||!_eventRoot.IsEnabled)return;
        var syncing=_syncing;_syncing=true;
        try{_commit?.Resume();}finally{_syncing=syncing;DrainAcceptedProjection();}
    }
    private bool CurrentInput()=>Current()&&(_arrange is null||_box?.Template is {} template
        &&ReferenceEquals(template.FindName("T1",_box),_leaf))&&Current();
    private void ProjectState()
    {
        if(!Current()||_syncing)return;
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
        finally{_syncing=false;DrainAcceptedProjection();}
    }
    public void ProjectAccepted()
    {
        if(!Current()||_syncing)return;
        _syncing=true;
        try{_commit?.ProjectAccepted();}
        finally{_syncing=false;DrainAcceptedProjection();}
        ProjectState();
    }
    private void OnStateChanged()
    {
        var accepted=_observedAcceptance!=_state.AcceptanceVersion||_observedReference!=_state.IsReferenceMode;
        _observedAcceptance=_state.AcceptanceVersion;_observedReference=_state.IsReferenceMode;
        if(_syncing&&accepted){_acceptedProjectionPending=true;return;}
        ProjectState();
    }
    private void OnExplicitlyAccepted(){if(_syncing){_acceptedProjectionPending=true;return;}if(_commit?.IsDirty==true)ProjectState();else ProjectAccepted();}
    public bool TrySetConstant(object value)
    {
        if(!CurrentInput()||_committing)return false;
        ConstantSetting proposal;
        try{proposal=_commit!.PrepareValue(value);}
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){_state.SetEditError(error.Message);return false;}
        _committing=true;
        try{return AcceptPrepared(proposal);}
        finally{_committing=false;}
    }
    public bool TryCommitPending(bool retryAccepted=false)
    {
        if(!CurrentInput())return false;
        if(_committing)return !_state.HasInvalidInput;
        if(_state.IsReferenceMode)return _reference?.TryCommitPending()??!_state.HasInvalidInput;
        if(_commit is null)return !_state.HasInvalidInput;
        if(!_commit.IsDirty&&!retryAccepted&&_state.SourceError.Length==0)return !_state.HasInvalidInput;
        _committing=true;
        try
        {
            var commit=_commit;var acceptance=_state.AcceptanceVersion;var edit=commit.EditVersion;
            if(!commit.TryPrepare(out var proposal))return false;
            if(!CurrentInput()||!ReferenceEquals(commit,_commit)||acceptance!=_state.AcceptanceVersion||edit!=commit.EditVersion)return false;
            return AcceptPrepared(proposal);
        }
        finally{_committing=false;}
    }
    private bool AcceptPrepared(ConstantSetting proposal)
    {
        var commit=_commit!;var raw=commit.Pending;var editVersion=commit.EditVersion;
        var acceptance=_state.AcceptanceVersion;var accepted=false;Exception? failure=null;
        try{accepted=_state.TrySet(proposal);}
        catch(Exception error){failure=error;}
        try
        {
            // Notification failures cannot undo an installed state. A reentrant newer edit owns its raw.
            if(CurrentInput()&&ReferenceEquals(commit,_commit)&&editVersion==commit.EditVersion)
            {
                if(_state.AcceptanceVersion!=acceptance)ProjectAccepted();
                else commit.RestoreRejected(raw);
            }
        }
        catch(Exception cleanup){failure=failure is null?cleanup:new AggregateException(failure,cleanup);}
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return accepted&&CurrentInput()&&!_state.HasInvalidInput;
    }
    private void OnNativeValue(object sender,RoutedPropertyChangedEventArgs<double> args)
    {if(_active&&_eventRoot.IsEnabled&&!_syncing&&!_closing&&!_presentationHidden&&CurrentInput()&&!_state.IsReferenceMode)TryCommitPending();}
    private void OnLeafValue(object? sender,EventArgs args)
    {if(_active&&_eventRoot.IsEnabled&&!_syncing&&!_closing&&!_presentationHidden&&CurrentInput())TryCommitPending();}
    private void OnModeChanged(object sender,RoutedPropertyChangedEventArgs<double> args)
    {
        if(_syncing||_presentationHidden||!CurrentInput())return;
        if(!_eventRoot.IsEnabled){ProjectState();return;}
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
                if(!_state.IsReferenceMode&&_state.ConstantBackup is null)_commit?.BeginConstantEntry();
            }
        }
        finally{ProjectState();_referenceScope?.Refresh();}
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(!CurrentInput()||_presentationHidden||!_eventRoot.IsEnabled)return;
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
        _pending=_eventRoot.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>
        {if(!CurrentInput()||generation!=_generation||!_active||_presentationHidden||!_eventRoot.IsVisible||!_eventRoot.IsEnabled)return;_pending=null;action();}));
    }
    private void OnWheel(object sender,MouseWheelEventArgs args)
    {if(!_state.IsReferenceMode&&TextEditor?.IsKeyboardFocused==true)Queue(()=>TryCommitPending());}
    private void OnLostFocus(object sender,KeyboardFocusChangedEventArgs args)
    {
        if(_state.IsReferenceMode||_presentationHidden)return;
        Queue(()=>
        {
            TryCommitPending();
            if(!_eventRoot.IsKeyboardFocusWithin&&_box?.Template.FindName("PART_Popup",_box) is not Popup {Child:{IsKeyboardFocusWithin:true}})ClosePopups();
        });
    }
    private void Cancel(){_generation++;_pending?.Abort();_pending=null;}
    private void ClosePopups()
    {
        Cancel();_closing=true;
        var popupGeneration=_reference?.PopupGeneration;
        try{ParameterCleanup.Run(new Action[]{()=>_box?.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false),
            ()=>{if(popupGeneration==_reference?.PopupGeneration)_referenceScope?.ClosePopups();}});}
        finally{_closing=false;}
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        _active=false;_loadGeneration++;_resume?.Abort();_resume=null;var owner=_owner;_owner=null;
        ParameterCleanup.Run(new Action[]{()=>{if(owner is not null)owner.Deactivated-=OnDeactivated;},
            ()=>_commit?.Suspend(),()=>_referenceScope?.PrepareForVisibility(false),()=>_expansion?.Close(),ClosePopups});
    }
    internal void PrepareForVisibility(bool visible)
    {
        if(_disposed)return;
        if(!visible)
        {
            _presentationHidden=true;_loadGeneration++;_resume?.Abort();_resume=null;
            ParameterCleanup.Run(new Action[]{()=>_commit?.Suspend(),()=>_referenceScope?.PrepareForVisibility(false),
                ()=>_expansion?.Close(),ClosePopups});
        }
        else
        {
            _presentationHidden=false;_referenceScope?.PrepareForVisibility(_eventRoot.IsEnabled);
            if(!_active||!_eventRoot.IsVisible||_commit?.IsSuspended!=true)return;
            QueueResume();
        }
    }
    private void QueueResume()
    {
        var generation=++_loadGeneration;_resume?.Abort();_resume=null;
        if(!Current()||!_active||!_eventRoot.IsVisible||_presentationHidden||!_eventRoot.IsEnabled)return;
        _resume=_eventRoot.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {if(!CurrentInput()||!_active||!_eventRoot.IsVisible||_presentationHidden||!_eventRoot.IsEnabled||generation!=_loadGeneration)return;_resume=null;ResumeCommit();}));
    }
    private void OnEnabledChanged(object sender,DependencyPropertyChangedEventArgs args)
    {
        if(!Current())return;
        if(!_eventRoot.IsEnabled)
        {
            _loadGeneration++;_resume?.Abort();_resume=null;
            ParameterCleanup.Run(new Action[]{()=>_commit?.Suspend(),()=>_referenceScope?.PrepareForVisibility(false),
                ()=>_expansion?.Close(),ClosePopups});
        }
        else
        {
            _referenceScope?.PrepareForVisibility(!_presentationHidden&&_eventRoot.IsVisible);
            QueueResume();
        }
    }
    private void OnVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)=>PrepareForVisibility(_eventRoot.IsVisible);
    private void OnDeactivated(object? sender,EventArgs args)=>ClosePopups();
    public NumericPendingEdit? TakePendingRaw(){_commit?.Suspend();return _commit?.Pending;}
    public void RestorePending(NumericPendingEdit? raw)=>_commit?.RestorePending(raw);
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_active=false;_presentationHidden=true;_loadGeneration++;
        var cleanup=new List<Action>{Cancel,()=>{_resume?.Abort();_resume=null;},()=>_reference?.SetSuspended(true),()=>_commit?.Suspend(),ClosePopups,
            ()=>{_state.Changed-=OnStateChanged;_state.ExplicitlyAccepted-=OnExplicitlyAccepted;},
            ()=>{if(_arrange is not null){_arrange.OnStartValueChange-=OnNativeValue;_arrange.OnLeftTypeChange-=OnModeChanged;
                if(ReferenceEquals(_arrange.SubscribeDataLeft,_reference))_arrange.SubscribeDataLeft=null;}
                else if(_leaf is not null)_leaf.OnNumericClick-=OnLeafValue;},
            ()=>{if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=null;},
            ()=>_expansion?.Dispose(),()=>_commit?.Dispose(),()=>_referenceScope?.Dispose(),()=>_reference?.Dispose(),
            ()=>{_eventRoot.Loaded-=OnLoaded;_eventRoot.Unloaded-=OnUnloaded;_eventRoot.IsVisibleChanged-=OnVisibilityChanged;
                _eventRoot.IsEnabledChanged-=OnEnabledChanged;
                _eventRoot.PreviewKeyDown-=OnKey;_eventRoot.PreviewMouseWheel-=OnWheel;_eventRoot.LostKeyboardFocus-=OnLostFocus;},
            ()=>{if(_referenceResources is not null)_eventRoot.Resources.MergedDictionaries.Remove(_referenceResources);_referenceResources=null;}};
        ParameterCleanup.Run(cleanup);
    }
}
