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

/// <summary>One original between composition over two borrowed states and one group writer.</summary>
internal sealed class NativeRangePresenter : UserControl,IDisposable,IParameterVisibility
{
    private readonly DeclaredFieldState[] _states;
    private readonly FieldPresentation[] _presentations;
    private readonly NativeNumericCommit?[] _commits=new NativeNumericCommit?[2];
    private readonly ReferenceFieldSession?[] _references=new ReferenceFieldSession?[2];
    private readonly ReferencePopupScope?[] _scopes=new ReferencePopupScope?[2];
    private readonly ReferenceExpansionCoordinator _expansion;
    private readonly Action<IReadOnlyDictionary<string,ParameterSetting>> _write;
    private readonly bool[] _allow;
    private readonly Func<Port,ParameterSetting,string?>? _validate;
    private readonly TextBlock _error=new(){TextWrapping=TextWrapping.Wrap};
    private readonly ComboBox? _box;
    private Window? _owner;
    private DispatcherOperation? _pending,_resume;
    private long _generation;
    private bool _disposed,_active,_syncing,_committing,_closing,_presentationHidden;
    public Numbers.SliderArrangeBase NativeView {get;}
    public TextBox? Input(int endpoint)=>_commits[endpoint]?.Text;
    private bool Invalid=>_states.Any(state=>state.HasInvalidInput);
    public string Label {get;}
    private static DependencyProperty ValueProperty(int i)=>i==0?Numbers.SliderArrangeBase.StartValueProperty:Numbers.SliderArrangeBase.EndValueProperty;
    private static DependencyProperty ModeProperty(int i)=>i==0?Numbers.SliderArrangeBase.IsSubscribeLeftProperty:Numbers.SliderArrangeBase.IsSubscribeRightProperty;

    public NativeRangePresenter(DeclaredFieldState left,DeclaredFieldState right,FieldPresentation lower,FieldPresentation upper,
        Action<IReadOnlyDictionary<string,ParameterSetting>> write,ReferenceFieldOptions? references,Func<OutputReference,string>? format)
    {
        _expansion=new(this);
        _states=new[]{left,right};_presentations=new[]{lower,upper};_write=write;_validate=references?.ValidateProposal;
        _allow=_presentations.Select(p=>references is not null&&p.Layout?.AllowReference!=false).ToArray();
        Language=XmlLanguage.GetLanguage("en-US");Margin=new Thickness(0);MinHeight=32;
        Label=lower.DisplayName.Replace("下限","范围");
        var bounds=_states.Select((s,i)=>Bounds(s.Port,_presentations[i].Layout!)).ToArray();
        NativeView=left.Port.Type.Scalar==ScalarKind.Int64?new Numbers.SliderArrange():new Numbers.SliderArrangeFloat{DecimalDigits=lower.Layout!.DecimalPlaces};
        try
        {
        NativeView.Minimum=Math.Min(bounds[0].Min,bounds[1].Min);NativeView.Maximum=Math.Max(bounds[0].Max,bounds[1].Max);
        NativeView.MinimumLeft=bounds[0].Min;NativeView.MaximumLeft=bounds[0].Max;
        NativeView.MinimumRight=bounds[1].Min;NativeView.MaximumRight=bounds[1].Max;
        NativeView.IsNeedCheckExtremumByXML=true;
        NativeView.SliderType=Numbers.SliderArrangeType.between;NativeView.SliderTickFrequency=lower.Layout!.Increment;
        NativeView.Foreground=new SolidColorBrush(Color.FromRgb(102,102,102));NativeView.FontSize=14;
        NativeView.EndValue=NativeView.Maximum;
        Resources.MergedDictionaries.Add(EditorResources.Create());_error.Style=EditorResources.ErrorStyle();
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/References/ReferenceField.xaml",UriKind.Relative)});
        NativeView.SubscribeTemplate=(DataTemplate)FindResource("ButtonSelecter_sub");
        for(var i=0;i<2;i++)
        {
            var endpoint=i;
            if((_states[i].Port.EditModes&FieldEditModes.Reference)!=0&&(_allow[i]||_states[i].IsReferenceMode))
            {
                _references[i]=new(_states[i],references??ReferenceFieldOptions.Empty,format,proposal=>TryAccept(endpoint,proposal),()=>TryCommitPending(true),_expansion.Open,_expansion.Preference);
                _scopes[i]=new(this,_references[i]!,()=>!_disposed&&_states[endpoint].IsReferenceMode);
            }
            _states[i].Changed+=OnStateChanged;
        }
        NativeView.SubscribeDataLeft=_references[0];NativeView.SubscribeDataRight=_references[1];
        var row=new SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab.LeftRightControl();
        row.LeftContent=new TextBlock{Text=Label,ToolTip=Label,FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
        var body=new StackPanel();body.Children.Add(NativeView);_error.Margin=new Thickness(0,2,0,0);body.Children.Add(_error);
        row.RightContent=body;Content=row;
        NativeView.OnStartValueChange+=OnNativeValue;NativeView.OnEndValueChange+=OnNativeValue;
        NativeView.OnLeftTypeChange+=OnLeftMode;NativeView.OnRightTypeChange+=OnRightMode;
        Loaded+=OnLoaded;Unloaded+=OnUnloaded;IsVisibleChanged+=OnVisibility;
        PreviewKeyDown+=OnKey;PreviewMouseWheel+=OnWheel;LostKeyboardFocus+=OnLostFocus;
        _box=(ComboBox)NativeView.FindName("CBox");AttachInputs();Project(false);
        if(left.Accepted is ConstantSetting l&&right.Accepted is ConstantSetting r&&Convert.ToDouble(l.Value,CultureInfo.InvariantCulture)>Convert.ToDouble(r.Value,CultureInfo.InvariantCulture))
            foreach(var state in _states)state.SetEditError("范围下限不能大于上限。");
        }
        catch{Dispose();throw;}
    }
    private static (double Min,double Max) Bounds(Port port,FieldLayout layout)
    {
        if(port.Type.Scalar==ScalarKind.Int64)
        {
            var r=NumericStorageRules.IntegerRange(NumericStorageRules.Effective(port.Type,port.Storage)!.Value);
            return(Math.Max(layout.Minimum??r.Minimum,r.Minimum),Math.Min(layout.Maximum??r.Maximum,r.Maximum));
        }
        return(layout.Minimum??-double.MaxValue,layout.Maximum??double.MaxValue);
    }
    private void AttachInputs()
    {
        NativeView.ApplyTemplate();NativeView.UpdateLayout();_box!.ApplyTemplate();
        var pair=NativeView.EnsureTemplateInputs();var leaves=new[]{pair.Left,pair.Right};
        for(var i=0;i<2;i++)
        {
            if(_commits[i] is {} current&&ReferenceEquals(current.Text,leaves[i].FindName("ValueText")))continue;
            var raw=_commits[i]?.PendingRaw;_commits[i]?.Dispose();
            _commits[i]=new(_states[i],_presentations[i].Layout,leaves[i],NativeView,ValueProperty(i),paired:true);
            _commits[i]!.RestorePending(raw);_commits[i]!.ValidateInitial();
        }
    }
    private void OnStateChanged(){if(!_committing)Project(false);}
    private void Project(bool accepted)
    {
        if(_disposed||_syncing)return;_syncing=true;
        try
        {
            for(var i=0;i<2;i++)NativeView.SetCurrentValue(ModeProperty(i),_states[i].IsReferenceMode);
            NativeView.IsSubscribeVisible=_references.Any(session=>session is not null)?Visibility.Visible:Visibility.Collapsed;
            for(var i=0;i<2;i++)
            {
                if(accepted||_states[i].IsReferenceMode||_commits[i]?.IsDirty!=true)_commits[i]?.ProjectAccepted();
                if(accepted)_references[i]?.ProjectAccepted();
                if(!_states[i].IsReferenceMode)_scopes[i]?.ClosePopups();
            }
            foreach(var toggle in Visuals<ToggleButton>(NativeView))
            {
                var path=toggle.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path?.Path;
                var i=path=="IsSubscribeLeft"?0:path=="IsSubscribeRight"?1:-1;
                if(i>=0)toggle.Visibility=_references[i] is not null&&(_states[i].Port.EditModes&FieldEditModes.Constant)!=0&&(_allow[i]||_states[i].IsReferenceMode)?Visibility.Visible:Visibility.Collapsed;
            }
            _error.Text=string.Join("；",_states.Where(state=>state.Error.Length!=0).Select(state=>state.Error).Distinct());
        }
        finally{_syncing=false;}
    }
    public bool TrySetConstant(int endpoint,object value)
    {
        if(_disposed||_committing)return false;
        ConstantSetting proposal;
        try{proposal=_commits[endpoint]!.PrepareValue(value);}
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){_states[endpoint].SetEditError(error.Message);return false;}
        return TryAccept(endpoint,proposal);
    }
    private bool Prepare(int i,out ParameterSetting proposal)
    {
        proposal=null!;
        if(_states[i].IsReferenceMode)return _references[i]?.TryPreparePending(out proposal)??false;
        if(_commits[i]!.IsDirty){if(!_commits[i]!.TryReadProposal(out var value))return false;proposal=value;return true;}
        proposal=_states[i].Accepted;
        if(proposal is not ConstantSetting stored)return false;
        try{_commits[i]!.ValidateStored(stored);return true;}
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){_states[i].SetEditError(error.Message);return false;}
    }
    private bool TryAccept(int? endpoint,ParameterSetting? explicitProposal)
    {
        if(_disposed||_states.Any(state=>state.IsDisposed||state.SourceError.Length!=0))return false;
        if(_committing)return false;
        _committing=true;
        var raw=_commits.Select(commit=>commit?.PendingRaw).ToArray();var candidateInstalled=false;var accepted=false;
        try
        {
            var proposals=new ParameterSetting[2];
            for(var i=0;i<2;i++)
            {
                if(endpoint==i)proposals[i]=explicitProposal!;
                else if(!Prepare(i,out proposals[i]))return false;
            }
            for(var i=0;i<2;i++)
            {
                var error=_validate?.Invoke(_states[i].Port,proposals[i]);
                if(_disposed||_states.Any(state=>state.IsDisposed))return false;
                if(!string.IsNullOrEmpty(error)){_states[i].SetEditError(error);return false;}
            }
            if(proposals[0] is ConstantSetting low&&proposals[1] is ConstantSetting high&&
                Convert.ToDouble(low.Value,CultureInfo.InvariantCulture)>Convert.ToDouble(high.Value,CultureInfo.InvariantCulture))
            {foreach(var state in _states)state.SetEditError("范围下限不能大于上限。");return false;}
            // Prepare both first, then establish the complete candidate context for both original RangeRules.
            _syncing=true;candidateInstalled=true;
            try
            {
                _commits[0]!.IgnoreChanges(()=>_commits[1]!.IgnoreChanges(()=>
                {
                    for(var i=0;i<2;i++)if(proposals[i] is ConstantSetting number)
                        NativeView.SetCurrentValue(ValueProperty(i),Convert.ToDouble(number.Value,CultureInfo.InvariantCulture));
                }));
                for(var i=0;i<2;i++)if(proposals[i] is ConstantSetting number&&!_commits[i]!.ValidatePreparedBindings(number))return false;
            }
            finally{_syncing=false;}
            accepted=DeclaredFieldState.TrySetGroup(new[]{(_states[0],proposals[0]),(_states[1],proposals[1])},_write);
            if(_disposed||_states.Any(state=>state.IsDisposed))return false;
            if(accepted)Project(true);return accepted&&!Invalid;
        }
        finally
        {
            if(!_disposed&&candidateInstalled&&!accepted&&!_states.Any(state=>state.IsDisposed))
            {
                _syncing=true;
                try
                {
                    _commits[0]!.IgnoreChanges(()=>_commits[1]!.IgnoreChanges(()=>
                    {
                        for(var i=0;i<2;i++)if((_states[i].Accepted as ConstantSetting??_states[i].ConstantBackup) is {} number)
                            NativeView.SetCurrentValue(ValueProperty(i),Convert.ToDouble(number.Value,CultureInfo.InvariantCulture));
                        for(var i=0;i<2;i++)_commits[i]!.RestoreRejected(raw[i]);
                    }));
                }
                finally{_syncing=false;}
            }
            _committing=false;if(!_disposed)Project(false);
        }
    }
    public bool TryCommitPending(bool explicitRetry=false)
    {
        if(_disposed)return false;
        if(_committing)return !Invalid;
        if(!explicitRetry&&!_commits.Any(commit=>commit?.IsDirty==true)&&!_references.Any(session=>session?.HasPendingText==true))return !Invalid;
        if(explicitRetry)
        {
            // Explicit Enter may retry the accepted current value after a refused program proposal.
            for(var i=0;i<2;i++)if(!_states[i].IsReferenceMode&&_commits[i]?.IsDirty!=true&&_states[i].Accepted is ConstantSetting value)
                return TryAccept(i,value);
        }
        return TryAccept(null,null);
    }
    private void OnNativeValue(object sender,RoutedPropertyChangedEventArgs<double> args)
    {if(_active&&!_disposed&&!_syncing&&!_closing&&!_presentationHidden)TryCommitPending();}
    private void OnLeftMode(object sender,RoutedPropertyChangedEventArgs<double> args)=>ChangeMode(0);
    private void OnRightMode(object sender,RoutedPropertyChangedEventArgs<double> args)=>ChangeMode(1);
    private void ChangeMode(int i)
    {
        if(_disposed||_syncing||_presentationHidden)return;
        try
        {
            if((bool)NativeView.GetValue(ModeProperty(i)))
            {
                if(!_allow[i]){_states[i].SetEditError("此字段不允许新增订阅。");return;}
                if(TryCommitPending())_states[i].EnterReferenceMode();
            }
            else if(_states[i].ConstantBackup is {} backup)TrySetConstant(i,backup.Value!);
            else _states[i].SetEditError("没有已保存的常量值。");
        }
        finally{Project(false);foreach(var scope in _scopes)scope?.Refresh();}
    }
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(_disposed)return;_active=true;var generation=++_generation;AttachInputs();
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=Window.GetWindow(this);if(_owner is not null)_owner.Deactivated+=OnDeactivated;
        Project(false);foreach(var scope in _scopes)scope?.Refresh();
        _resume?.Abort();_resume=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {if(_disposed||!_active||!IsVisible||_presentationHidden||generation!=_generation)return;_resume=null;foreach(var commit in _commits)commit?.Resume();}));
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        var i=Array.FindIndex(_commits,commit=>commit?.Text.IsKeyboardFocusWithin==true);
        if(i<0||_states[i].IsReferenceMode)return;
        if(args.Key==Key.Escape){ClosePopups();args.Handled=true;return;}
        if(args.Key!=Key.Enter)return;args.Handled=true;
        if(TryCommitPending(true)){ClosePopups();FocusManager.SetFocusedElement(FocusManager.GetFocusScope(Input(i)!),null);Keyboard.ClearFocus();}
    }
    private void Queue(Action action)
    {
        _pending?.Abort();var generation=_generation;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{if(_disposed||!_active||!IsVisible||generation!=_generation)return;_pending=null;action();}));
    }
    private void OnWheel(object sender,MouseWheelEventArgs args)
    {if(_commits.Any(commit=>commit?.Text.IsKeyboardFocused==true))Queue(()=>TryCommitPending());}
    private void OnLostFocus(object sender,KeyboardFocusChangedEventArgs args)
    {
        if(_presentationHidden)return;
        Queue(()=>{TryCommitPending();if(!IsKeyboardFocusWithin&&_box!.Template.FindName("PART_Popup",_box) is not Popup {Child:{IsKeyboardFocusWithin:true}})ClosePopups();});
    }
    private void ClosePopups()
    {
        _pending?.Abort();_pending=null;_closing=true;
        try{_box?.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);foreach(var scope in _scopes)scope?.ClosePopups();}
        finally{_closing=false;}
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {_active=false;_generation++;_resume?.Abort();_resume=null;foreach(var commit in _commits)commit?.Suspend();ClosePopups();if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=null;}
    void IParameterVisibility.PrepareForVisibility(bool visible)=>PrepareForVisibility(visible);
    private void PrepareForVisibility(bool visible)
    {
        if(_disposed)return;
        if(!visible)
        {
            _presentationHidden=true;_generation++;_resume?.Abort();_resume=null;
            foreach(var commit in _commits)commit?.Suspend();ClosePopups();
        }
        else
        {
            _presentationHidden=false;
            if(!_active||!IsVisible||!_commits.Any(commit=>commit?.IsSuspended==true))return;
            var generation=++_generation;_resume?.Abort();
            _resume=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
            {if(_disposed||!_active||!IsVisible||_presentationHidden||generation!=_generation)return;_resume=null;foreach(var commit in _commits)commit?.Resume();}));
        }
    }
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args)=>PrepareForVisibility(IsVisible);
    private void OnDeactivated(object? sender,EventArgs args)=>ClosePopups();
    private static IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T match)yield return match;if(root is not Visual)yield break;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Visuals<T>(VisualTreeHelper.GetChild(root,i)))yield return item;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_active=false;_generation++;_resume?.Abort();_resume=null;ClosePopups();
        NativeView.OnStartValueChange-=OnNativeValue;NativeView.OnEndValueChange-=OnNativeValue;
        NativeView.OnLeftTypeChange-=OnLeftMode;NativeView.OnRightTypeChange-=OnRightMode;
        NativeView.SubscribeDataLeft=null;NativeView.SubscribeDataRight=null;
        _expansion.Dispose();
        for(var i=0;i<2;i++){_states[i].Changed-=OnStateChanged;_commits[i]?.Dispose();_scopes[i]?.Dispose();_references[i]?.Dispose();}
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=null;
        Loaded-=OnLoaded;Unloaded-=OnUnloaded;IsVisibleChanged-=OnVisibility;PreviewKeyDown-=OnKey;PreviewMouseWheel-=OnWheel;LostKeyboardFocus-=OnLostFocus;Content=null;
    }
}
