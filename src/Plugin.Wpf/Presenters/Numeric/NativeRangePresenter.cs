using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.ListView;

namespace SunEyeVision.Plugin.Wpf.Presenters.Numeric;

/// <summary>One original between composition over two borrowed states and one group writer.</summary>
internal sealed class NativeRangePresenter : UserControl,IDisposable,IParameterVisibility
{
    private readonly DeclaredFieldState[] _states;
    private readonly FieldPresentation[] _presentations;
    private readonly NativeNumericCommit?[] _commits=new NativeNumericCommit?[2];
    private readonly Numbers.NumericControl?[] _leaves=new Numbers.NumericControl?[2];
    private readonly ReferenceFieldSession?[] _references=new ReferenceFieldSession?[2];
    private readonly ReferencePopupScope?[] _scopes=new ReferencePopupScope?[2];
    private readonly ReferenceExpansionCoordinator _expansion;
    private readonly Action<IReadOnlyDictionary<string,ParameterSetting>> _write;
    private readonly Func<IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)>,Action,bool>? _acceptPrepared;
    private readonly Func<IDisposable?>? _beginGroup;
    private readonly bool[] _allow;
    private readonly Func<Port,ParameterSetting,string?>? _validate;
    private readonly TextBlock _error=new(){TextWrapping=TextWrapping.Wrap};
    private readonly ComboBox? _box;
    private Window? _owner;
    private DispatcherOperation? _pending,_resume;
    private long _generation;
    private long _attachGeneration;
    private readonly long[] _observedAcceptance=new long[2];
    private readonly bool[] _observedReference=new bool[2];
    private bool _acceptedProjectionPending;
    private bool _disposed,_active,_syncing,_committing,_closing,_presentationHidden;
    public Numbers.SliderArrangeBase NativeView {get;}
    public TextBox? Input(int endpoint)=>_commits[endpoint]?.Text;
    internal bool HasPendingEdits(int endpoint)=>!_disposed&&(_commits[endpoint]?.IsDirty==true||_references[endpoint]?.HasPendingText==true);
    private bool Invalid=>_states.Any(state=>state.HasInvalidInput);
    public string Label {get;}
    private static DependencyProperty ValueProperty(int i)=>i==0?Numbers.SliderArrangeBase.StartValueProperty:Numbers.SliderArrangeBase.EndValueProperty;
    private static DependencyProperty ModeProperty(int i)=>i==0?Numbers.SliderArrangeBase.IsSubscribeLeftProperty:Numbers.SliderArrangeBase.IsSubscribeRightProperty;

    public NativeRangePresenter(DeclaredFieldState left,DeclaredFieldState right,FieldPresentation lower,FieldPresentation upper,
        Action<IReadOnlyDictionary<string,ParameterSetting>> write,ReferenceFieldOptions? references,Func<OutputReference,string>? format,
        Func<IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)>,Action,bool>? acceptPrepared=null,Func<IDisposable?>? beginGroup=null)
    {
        _expansion=new(this,references?.WindowOwner);
        _states=new[]{left,right};_presentations=new[]{lower,upper};_write=write;_validate=references?.ValidateProposal;
        for(var i=0;i<2;i++){_observedAcceptance[i]=_states[i].AcceptanceVersion;_observedReference[i]=_states[i].IsReferenceMode;}
        _acceptPrepared=acceptPrepared;_beginGroup=beginGroup;
        _allow=_presentations.Select(p=>references is not null&&p.Layout?.AllowReference!=false).ToArray();
        Language=XmlLanguage.GetLanguage("en-US");Margin=new Thickness(0);MinHeight=32;
        Label=lower.DisplayName.Replace("下限","范围");
        var bounds=_states.Select((s,i)=>Bounds(s.Port,_presentations[i].Layout!)).ToArray();
        NativeView=left.Port.Type.Scalar==ScalarKind.Int64?new Numbers.SliderArrange():new Numbers.SliderArrangeFloat{DecimalDigits=lower.Layout!.DecimalPlaces??throw new ArgumentException("原范围滑条必须声明固定小数位。")};
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
                _references[i]=new(_states[i],references??ReferenceFieldOptions.Empty,format,proposal=>TryAccept(endpoint,proposal),()=>TryCommitEndpoint(endpoint),_expansion.Open,_expansion.Preference,selectVariable:_expansion.OpenVariable,isExpanded:_expansion.IsExpanded);
                _scopes[i]=new(this,_references[i]!,()=>!_disposed&&_states[endpoint].IsReferenceMode);
            }
            _states[i].Changed+=OnStateChanged;
        }
        NativeView.SubscribeDataLeft=_references[0];NativeView.SubscribeDataRight=_references[1];
        var row=new SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTab.LeftRightControl();
        row.LeftContent=new TextBlock{Text=Label,ToolTip=Label,FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
        row.RightContent=NativeView;Content=row;
        NativeView.OnStartValueChange+=OnLeftValue;NativeView.OnEndValueChange+=OnRightValue;
        NativeView.OnLeftTypeChange+=OnLeftMode;NativeView.OnRightTypeChange+=OnRightMode;
        Loaded+=OnLoaded;Unloaded+=OnUnloaded;IsVisibleChanged+=OnVisibility;IsEnabledChanged+=OnEnabled;
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
        var generation=++_attachGeneration;var old=_commits.ToArray();var pending=old.Select(commit=>commit?.Pending).ToArray();
        var edits=old.Select(commit=>commit?.EditVersion).ToArray();
        NativeView.ApplyTemplate();NativeView.UpdateLayout();_box!.ApplyTemplate();
        if(_disposed||generation!=_attachGeneration||old.Where((commit,i)=>!ReferenceEquals(commit,_commits[i])).Any())return;
        var pair=NativeView.EnsureTemplateInputs();var leaves=new[]{pair.Left,pair.Right};
        for(var i=0;i<2;i++)
        {
            if(_commits[i] is {} current&&ReferenceEquals(current.Text,leaves[i].FindName("ValueText")))continue;
            var raw=edits[i]==old[i]?.EditVersion?pending[i]:null;old[i]?.Dispose();
            var endpoint=i;
            var created=new NativeNumericCommit(_states[i],_presentations[i].Layout,leaves[i],NativeView,ValueProperty(i),paired:true,
                isCurrent:()=>!_disposed&&ReferenceEquals(_box.Template.FindName(endpoint==0?"T1":"T2",_box),leaves[endpoint]));
            if(_disposed||generation!=_attachGeneration||!ReferenceEquals(old[i],_commits[i])){created.Dispose();return;}
            _commits[i]=created;
            _leaves[i]=leaves[i];
            _commits[i]!.RestorePending(raw);_commits[i]!.ValidateInitial();
        }
    }
    private bool ProjectValues(ParameterSetting[] values,Action action)
    {
        bool Next(int i)
        {
            if(i==2){action();return true;}
            if(values[i] is not ConstantSetting number)return Next(i+1);
            var nested=false;return _commits[i]!.ProjectCandidate(number,()=>nested=Next(i+1))&&nested;
        }
        return Next(0);
    }
    private void OnStateChanged()
    {
        var accepted=false;for(var i=0;i<2;i++){accepted|=_observedAcceptance[i]!=_states[i].AcceptanceVersion||_observedReference[i]!=_states[i].IsReferenceMode;_observedAcceptance[i]=_states[i].AcceptanceVersion;_observedReference[i]=_states[i].IsReferenceMode;}
        if(_syncing&&accepted)_acceptedProjectionPending=true;
        if(!_committing)Project(false);
    }
    internal void ValidateReplay(IReadOnlyDictionary<string,AcceptedFieldValue> values)
    {
        if(values[_states[0].Port.Id].Setting is ConstantSetting low&&values[_states[1].Port.Id].Setting is ConstantSetting high
            &&Convert.ToDouble(low.Value,CultureInfo.InvariantCulture)>Convert.ToDouble(high.Value,CultureInfo.InvariantCulture))
            throw new ArgumentException("历史范围下限不能大于上限。");
    }
    internal void ReplayAccepted()
    {
        if(_disposed)return;
        ++_generation;_pending?.Abort();_pending=null;_resume?.Abort();_resume=null;
        _expansion.Close();foreach(var scope in _scopes)scope?.ClosePopups();Project(true);
    }
    private void Project(bool accepted,Func<int,bool>? mayReplace=null)
    {
        if(_disposed||_syncing)return;_syncing=true;
        try
        {
            for(var i=0;i<2;i++)NativeView.SetCurrentValue(ModeProperty(i),_states[i].IsReferenceMode);
            // A subscription active before Loaded can leave the original right
            // column collapsed. Once both endpoints are editable, reapply the
            // original layout; this does not accept or invent a constant value.
            if(!NativeView.IsSubscribeLeft&&!NativeView.IsSubscribeRight)NativeView.InitUI();
            NativeView.IsSubscribeVisible=_references.Any(session=>session is not null)?Visibility.Visible:Visibility.Collapsed;
            for(var i=0;i<2;i++)
            {
                if((!accepted||mayReplace?.Invoke(i)!=false)&&(accepted||_states[i].IsReferenceMode||_commits[i]?.IsDirty!=true))_commits[i]?.ProjectAccepted();
                if(accepted&&mayReplace?.Invoke(i)!=false)_references[i]?.ProjectAccepted();
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
        finally{_syncing=false;if(_acceptedProjectionPending&&!_committing){_acceptedProjectionPending=false;Project(false);}}
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
        if(endpoint is {} selected)return TryAcceptEndpoint(selected,explicitProposal??throw new ArgumentNullException(nameof(explicitProposal)));
        if(_disposed||_states.Any(state=>state.IsDisposed||state.SourceError.Length!=0))return false;
        if(_committing)return false;
        _committing=true;
        IDisposable? group=null;
        var raw=_commits.Select(commit=>commit?.Pending).ToArray();var candidateInstalled=false;var accepted=false;var projected=false;
        var versions=_states.Select(state=>state.AcceptanceVersion).ToArray();var modes=_states.Select(state=>state.IsReferenceMode).ToArray();var commits=_commits.ToArray();var edits=commits.Select(commit=>commit?.EditVersion).ToArray();
        bool Unchanged()=>!_disposed&&Enumerable.Range(0,2).All(i=>!_states[i].IsDisposed&&versions[i]==_states[i].AcceptanceVersion&&modes[i]==_states[i].IsReferenceMode&&ReferenceEquals(commits[i],_commits[i])&&edits[i]==commits[i]?.EditVersion);
        try
        {
            if(_beginGroup is not null){group=_beginGroup();if(group is null)return false;}
            var proposals=new ParameterSetting[2];
            for(var i=0;i<2;i++)
            {
                if(endpoint==i)proposals[i]=explicitProposal!;
                else if(!Prepare(i,out proposals[i]))return false;
            }
            for(var i=0;i<2;i++)
            {
                var error=_validate?.Invoke(_states[i].Port,proposals[i]);
                if(!Unchanged())return false;
                if(!string.IsNullOrEmpty(error)){_states[i].SetEditError(error);return false;}
            }
            if(proposals[0] is ConstantSetting low&&proposals[1] is ConstantSetting high&&
                Convert.ToDouble(low.Value,CultureInfo.InvariantCulture)>Convert.ToDouble(high.Value,CultureInfo.InvariantCulture))
            {foreach(var state in _states)state.SetEditError("范围下限不能大于上限。");return false;}
            // Prepare both first, then establish the complete candidate context for both original RangeRules.
            _syncing=true;candidateInstalled=true;
            try
            {
                if(!ProjectValues(proposals,()=>
                {
                    for(var i=0;i<2;i++)if(proposals[i] is ConstantSetting number)
                    {NativeView.SetCurrentValue(ValueProperty(i),Convert.ToDouble(number.Value,CultureInfo.InvariantCulture));if(!Unchanged())return;}
                }))return false;
                for(var i=0;i<2;i++)if(proposals[i] is ConstantSetting number&&!ValidateEndpointBinding(i,number,proposals[1-i]))return false;
            }
            finally{_syncing=false;}
            if(!Unchanged())return false;
            var prepared=new[]{(_states[0],proposals[0]),(_states[1],proposals[1])};
            accepted=_acceptPrepared is {} submit?submit(prepared,()=>accepted=true):DeclaredFieldState.TrySetGroup(prepared,_write,installed:()=>accepted=true);
            if(_disposed||_states.Any(state=>state.IsDisposed))return false;
            if(accepted){Project(true,i=>ReferenceEquals(commits[i],_commits[i])&&edits[i]==commits[i]?.EditVersion);projected=true;}return accepted&&!Invalid;
        }
        finally
        {
            try
            {
            if(!_disposed&&accepted&&!projected&&!_states.Any(state=>state.IsDisposed))Project(true,i=>ReferenceEquals(commits[i],_commits[i])&&edits[i]==commits[i]?.EditVersion);
            if(candidateInstalled&&!accepted&&Unchanged())
            {
                _syncing=true;
                try
                {
                    ProjectValues(_states.Select(state=>state.Accepted as ConstantSetting??state.ConstantBackup??state.Accepted).ToArray(),()=>
                    {
                        for(var i=0;i<2;i++)if((_states[i].Accepted as ConstantSetting??_states[i].ConstantBackup) is {} number)
                        {NativeView.SetCurrentValue(ValueProperty(i),Convert.ToDouble(number.Value,CultureInfo.InvariantCulture));if(!Unchanged())return;}
                        for(var i=0;i<2;i++){_commits[i]!.RestoreRejected(raw[i]);if(!Unchanged())return;}
                    });
                }
                finally{_syncing=false;}
            }
            _committing=false;if(!_disposed)Project(false);
            }
            finally{_committing=false;group?.Dispose();}
        }
    }
    private bool ValidateEndpointBinding(int endpoint,ConstantSetting value,ParameterSetting? other)
    {
        var commits=_commits.ToArray();var commit=commits[endpoint]??throw new InvalidOperationException("范围数值输入尚未初始化。");
        var leaf=_leaves[endpoint]??throw new InvalidOperationException("范围数值输入尚未初始化。");
        var minBinding=BindingOperations.GetBindingExpression(leaf,Numbers.NumericControl.MinimumProperty);
        var maxBinding=BindingOperations.GetBindingExpression(leaf,Numbers.NumericControl.MaximumProperty);
        var oldMin=leaf.Minimum;var oldMax=leaf.Maximum;var acceptance=_states[endpoint].AcceptanceVersion;
        var mode=_states[endpoint].IsReferenceMode;var edit=commit.EditVersion;
        var bounds=Bounds(_states[endpoint].Port,_presentations[endpoint].Layout!);
        var minimum=bounds.Min;var maximum=bounds.Max;
        if(other is ConstantSetting constant)
        {
            var opposite=Convert.ToDouble(constant.Value,CultureInfo.InvariantCulture);
            if(endpoint==0)maximum=Math.Min(maximum,opposite);else minimum=Math.Max(minimum,opposite);
        }
        bool OwnsLeaf()=>!_disposed&&ReferenceEquals(_leaves[endpoint],leaf)&&ReferenceEquals(_box?.Template.FindName(endpoint==0?"T1":"T2",_box),leaf);
        bool Current()=>OwnsLeaf()&&!_states[endpoint].IsDisposed&&acceptance==_states[endpoint].AcceptanceVersion
            &&mode==_states[endpoint].IsReferenceMode&&edit==commit.EditVersion&&ReferenceEquals(commits[endpoint],_commits[endpoint])
            &&ReferenceEquals(minBinding,BindingOperations.GetBindingExpression(leaf,Numbers.NumericControl.MinimumProperty))
            &&ReferenceEquals(maxBinding,BindingOperations.GetBindingExpression(leaf,Numbers.NumericControl.MaximumProperty));
        Exception? failure=null;var valid=false;
        try
        {
            if(Current())
            {
                leaf.SetCurrentValue(Numbers.NumericControl.MinimumProperty,minimum);
                if(Current())
                {
                    leaf.SetCurrentValue(Numbers.NumericControl.MaximumProperty,maximum);
                    if(Current())valid=commit.ValidatePreparedBindings(value);
                }
            }
        }
        catch(Exception error){failure=error;}
        void Restore(DependencyProperty property,BindingExpression? binding,double oldValue)
        {
            try
            {
                if(!OwnsLeaf()||!ReferenceEquals(binding,BindingOperations.GetBindingExpression(leaf,property)))return;
                if(binding is null)leaf.SetCurrentValue(property,oldValue);else binding.UpdateTarget();
            }
            catch(Exception cleanup){failure=failure is null?cleanup:new AggregateException(failure,cleanup);}
        }
        Restore(Numbers.NumericControl.MinimumProperty,minBinding,oldMin);
        Restore(Numbers.NumericControl.MaximumProperty,maxBinding,oldMax);
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return valid&&Current();
    }
    private bool TryCommitEndpoint(int endpoint)
    {
        if(_disposed||_committing)return false;
        if(!Prepare(endpoint,out var proposal))return false;
        return TryAcceptEndpoint(endpoint,proposal);
    }
    private bool TryAcceptEndpoint(int endpoint,ParameterSetting proposal)
    {
        if(_disposed||_committing||endpoint is <0 or >1||_states.Any(state=>state.IsDisposed))return false;
        var state=_states[endpoint];
        _committing=true;IDisposable? group=null;
        var commits=_commits.ToArray();var versions=_states.Select(item=>item.AcceptanceVersion).ToArray();
        var modes=_states.Select(item=>item.IsReferenceMode).ToArray();var edits=commits.Select(item=>item?.EditVersion).ToArray();
        var raw=commits[endpoint]?.Pending;var candidateInstalled=false;var accepted=false;var projected=false;
        bool Unchanged()=>!_disposed&&Enumerable.Range(0,2).All(i=>!_states[i].IsDisposed&&versions[i]==_states[i].AcceptanceVersion
            &&modes[i]==_states[i].IsReferenceMode&&ReferenceEquals(commits[i],_commits[i])&&edits[i]==commits[i]?.EditVersion);
        try
        {
            if(_beginGroup is not null){group=_beginGroup();if(group is null)return false;}
            var other=_states[1-endpoint].IsReferenceMode?null:_states[1-endpoint].Accepted;
            if(proposal is ConstantSetting number)
            {
                try{commits[endpoint]!.ValidateStored(number);}
                catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){state.SetEditError(error.Message);return false;}
                if(other is ConstantSetting opposite)
                {
                    var current=Convert.ToDouble(number.Value,CultureInfo.InvariantCulture);
                    var comparison=Convert.ToDouble(opposite.Value,CultureInfo.InvariantCulture);
                    if(endpoint==0?current>comparison:current<comparison){state.SetEditError("范围下限不能大于上限。");return false;}
                }
            }
            var validation=_validate?.Invoke(state.Port,proposal);
            if(!Unchanged())return false;
            if(!string.IsNullOrEmpty(validation)){state.SetEditError(validation);return false;}
            if(proposal is ConstantSetting candidate)
            {
                _syncing=true;candidateInstalled=true;
                try
                {
                    if(!commits[endpoint]!.ProjectCandidate(candidate,()=>NativeView.SetCurrentValue(ValueProperty(endpoint),Convert.ToDouble(candidate.Value,CultureInfo.InvariantCulture))))return false;
                    if(!Unchanged()||!ValidateEndpointBinding(endpoint,candidate,other))return false;
                }
                finally{_syncing=false;}
            }
            if(!Unchanged())return false;
            var prepared=new[]{(state,proposal)};
            accepted=_acceptPrepared is {} submit?submit(prepared,()=>accepted=true):DeclaredFieldState.TrySetGroup(prepared,_write,installed:()=>accepted=true);
            if(_disposed||state.IsDisposed)return false;
            if(accepted){Project(true,i=>i==endpoint&&ReferenceEquals(commits[i],_commits[i])&&edits[i]==commits[i]?.EditVersion);projected=true;}
            return accepted;
        }
        finally
        {
            try
            {
                if(!_disposed&&accepted&&!projected&&!state.IsDisposed)
                    Project(true,i=>i==endpoint&&ReferenceEquals(commits[i],_commits[i])&&edits[i]==commits[i]?.EditVersion);
                if(candidateInstalled&&!accepted&&Unchanged())
                {
                    _syncing=true;
                    try
                    {
                        if(state.Accepted is ConstantSetting saved)
                            commits[endpoint]!.ProjectCandidate(saved,()=>NativeView.SetCurrentValue(ValueProperty(endpoint),Convert.ToDouble(saved.Value,CultureInfo.InvariantCulture)));
                        if(Unchanged())commits[endpoint]!.RestoreRejected(raw);
                    }
                    finally{_syncing=false;}
                }
                _committing=false;if(!_disposed)Project(false);
            }
            finally{_committing=false;group?.Dispose();}
        }
    }
    public bool TryCommitPending(bool explicitRetry=false)
    {
        if(_disposed)return false;
        if(_committing)return !Invalid;
        if(!explicitRetry&&!_commits.Any(commit=>commit?.IsDirty==true)&&!_references.Any(session=>session?.HasPendingText==true))return !Invalid;
        return TryAccept(null,null);
    }
    private void OnLeftValue(object sender,RoutedPropertyChangedEventArgs<double> args)=>OnNativeValue(0);
    private void OnRightValue(object sender,RoutedPropertyChangedEventArgs<double> args)=>OnNativeValue(1);
    private void OnNativeValue(int endpoint)
    {if(_active&&!_disposed&&IsEnabled&&!_syncing&&!_closing&&!_presentationHidden)TryCommitEndpoint(endpoint);}
    private void OnLeftMode(object sender,RoutedPropertyChangedEventArgs<double> args)=>ChangeMode(0);
    private void OnRightMode(object sender,RoutedPropertyChangedEventArgs<double> args)=>ChangeMode(1);
    private void ChangeMode(int i)
    {
        if(_disposed||_syncing||_presentationHidden)return;
        if(!IsEnabled){Project(false);return;}
        try
        {
            if((bool)NativeView.GetValue(ModeProperty(i)))
            {
                if(!_allow[i]){_states[i].SetEditError("此字段不允许新增订阅。");return;}
                if(TryCommitEndpoint(i))_states[i].EnterReferenceMode();
            }
            else if(_states[i].ConstantBackup is {} backup)TrySetConstant(i,backup.Value!);
            else
            {
                var state=_states[i];var commit=_commits[i];var acceptance=state.AcceptanceVersion;var edit=commit?.EditVersion;
                _states[i].TryUseConstant();
                if(!_disposed&&!state.IsDisposed&&!state.IsReferenceMode&&state.ConstantBackup is null
                    &&acceptance==state.AcceptanceVersion&&ReferenceEquals(commit,_commits[i])&&edit==commit?.EditVersion)
                    commit?.BeginConstantEntry();
            }
        }
        finally{Project(false);foreach(var scope in _scopes)scope?.Refresh();}
    }
    private void OnLoaded(object sender,RoutedEventArgs args)
    {
        if(_disposed)return;_active=true;AttachInputs();
        if(_owner is not null)_owner.Deactivated-=OnDeactivated;_owner=Window.GetWindow(this);if(_owner is not null)_owner.Deactivated+=OnDeactivated;
        Project(false);foreach(var scope in _scopes)scope?.PrepareForVisibility(!_presentationHidden&&IsVisible&&IsEnabled);
        QueueResume();
    }
    private void QueueResume()
    {
        var generation=++_generation;_resume?.Abort();_resume=null;
        if(_disposed||!_active||!IsVisible||_presentationHidden||!IsEnabled)return;
        _resume=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {if(_disposed||!_active||!IsVisible||_presentationHidden||!IsEnabled||generation!=_generation)return;_resume=null;foreach(var commit in _commits)commit?.Resume();}));
    }
    private void OnEnabled(object sender,DependencyPropertyChangedEventArgs args)
    {
        if(_disposed)return;
        if(!IsEnabled)
        {
            _generation++;_resume?.Abort();_resume=null;
            ParameterCleanup.Run(_commits.Where(commit=>commit is not null).Select(commit=>(Action)commit!.Suspend)
                .Concat(_scopes.Where(scope=>scope is not null).Select(scope=>(Action)(()=>scope!.PrepareForVisibility(false))))
                .Concat(new Action[]{_expansion.Close,ClosePopups}));
        }
        else
        {
            foreach(var scope in _scopes)scope?.PrepareForVisibility(!_presentationHidden&&IsVisible);
            QueueResume();
        }
    }

    private void OnKey(object sender,KeyEventArgs args)
    {
        if(_disposed||!IsEnabled||_presentationHidden)return;
        var i=Array.FindIndex(_commits,commit=>commit?.Text.IsKeyboardFocusWithin==true);
        if(i<0||_states[i].IsReferenceMode)return;
        if(args.Key==Key.Escape){ClosePopups();args.Handled=true;return;}
        if(args.Key!=Key.Enter)return;args.Handled=true;
        if(TryCommitEndpoint(i)){ClosePopups();FocusManager.SetFocusedElement(FocusManager.GetFocusScope(Input(i)!),null);Keyboard.ClearFocus();}
    }
    private void Queue(Action action)
    {
        _pending?.Abort();var generation=_generation;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{if(_disposed||!_active||!IsVisible||!IsEnabled||_presentationHidden||generation!=_generation)return;_pending=null;action();}));
    }
    private void OnWheel(object sender,MouseWheelEventArgs args)
    {var endpoint=Array.FindIndex(_commits,commit=>commit?.Text.IsKeyboardFocused==true);if(endpoint>=0)Queue(()=>TryCommitEndpoint(endpoint));}
    private void OnLostFocus(object sender,KeyboardFocusChangedEventArgs args)
    {
        if(_disposed||!IsEnabled||_presentationHidden)return;
        var endpoint=Array.FindIndex(_commits,commit=>ReferenceEquals(commit?.Text,args.OldFocus));
        Queue(()=>{if(endpoint>=0)TryCommitEndpoint(endpoint);if(!IsKeyboardFocusWithin&&_box!.Template.FindName("PART_Popup",_box) is not Popup {Child:{IsKeyboardFocusWithin:true}})ClosePopups();});
    }
    private void ClosePopups()
    {
        _pending?.Abort();_pending=null;_closing=true;
        try{ParameterCleanup.Run(new Action[]{()=>_box?.SetCurrentValue(ComboBox.IsDropDownOpenProperty,false)}
            .Concat(_scopes.Where(scope=>scope is not null).Select(scope=>(Action)scope!.ClosePopups)));}
        finally{_closing=false;}
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        _active=false;_generation++;_resume?.Abort();_resume=null;var owner=_owner;_owner=null;
        ParameterCleanup.Run(new Action[]{()=>{if(owner is not null)owner.Deactivated-=OnDeactivated;}}
            .Concat(_commits.Where(commit=>commit is not null).Select(commit=>(Action)commit!.Suspend))
            .Concat(new Action[]{ClosePopups}));
    }
    void IParameterVisibility.PrepareForVisibility(bool visible)=>PrepareForVisibility(visible);
    private void PrepareForVisibility(bool visible)
    {
        if(_disposed)return;
        if(!visible)
        {
            _presentationHidden=true;_generation++;_resume?.Abort();_resume=null;
            ParameterCleanup.Run(_scopes.Where(scope=>scope is not null).Select(scope=>(Action)(()=>scope!.PrepareForVisibility(false)))
                .Concat(_commits.Where(commit=>commit is not null).Select(commit=>(Action)commit!.Suspend))
                .Concat(new Action[]{ClosePopups}));
        }
        else
        {
            _presentationHidden=false;
            foreach(var scope in _scopes)scope?.PrepareForVisibility(IsEnabled);
            if(!_active||!IsVisible||!_commits.Any(commit=>commit?.IsSuspended==true))return;
            QueueResume();
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
        if(_disposed)return;_disposed=true;_active=false;_generation++;_resume?.Abort();_resume=null;
        NativeView.OnStartValueChange-=OnLeftValue;NativeView.OnEndValueChange-=OnRightValue;
        NativeView.OnLeftTypeChange-=OnLeftMode;NativeView.OnRightTypeChange-=OnRightMode;
        foreach(var state in _states)state.Changed-=OnStateChanged;
        var owner=_owner;_owner=null;
        Loaded-=OnLoaded;Unloaded-=OnUnloaded;IsVisibleChanged-=OnVisibility;IsEnabledChanged-=OnEnabled;PreviewKeyDown-=OnKey;PreviewMouseWheel-=OnWheel;LostKeyboardFocus-=OnLostFocus;
        ParameterCleanup.Run(new Action[]{()=>{if(owner is not null)owner.Deactivated-=OnDeactivated;},ClosePopups,
                ()=>NativeView.SubscribeDataLeft=null,()=>NativeView.SubscribeDataRight=null,_expansion.Dispose}
            .Concat(_commits.Where(commit=>commit is not null).Select(commit=>(Action)commit!.Dispose))
            .Concat(_scopes.Where(scope=>scope is not null).Select(scope=>(Action)scope!.Dispose))
            .Concat(_references.Where(reference=>reference is not null).Select(reference=>(Action)reference!.Dispose))
            .Concat(new Action[]{()=>Content=null}));
    }
}
