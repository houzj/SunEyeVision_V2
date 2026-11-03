using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>One editor session's accepted setting and errors; no visuals, defaults or execution access.</summary>
internal sealed class DeclaredFieldState : IDisposable
{
    private Action<ParameterSetting>? _write;
    private Func<ParameterSetting,string?>? _validate;
    private Func<ParameterSetting,bool>? _propose;
    private ParameterSetting _accepted;
    private ConstantSetting? _constantBackup;
    private Func<ConstantSetting?>? _readCurrentValue;
    private readonly bool _allowUnboundReference;
    private bool _writing;
    private bool _referenceCleared;
    internal NativeNumericModel? NumericModel {get;private set;}
    private long _numericRead;
    internal int NumericEndpoint {get;set;}=-1;
    internal DeclaredFieldState? NumericPeer {get;set;}
    internal Action<object>? NumericCheck {get;set;}
    internal string ReadNumericComparisonString()
    {
        ObjectDisposedException.ThrowIf(IsDisposed,this);
        var value=_readCurrentValue?.Invoke()??throw new InvalidOperationException("普通参数不可用。");
        // Only the original Check bridge consumes a raw ordinary string. Explicit
        // model reads still validate the formal CLR type and storage separately.
        if(value.Value is string raw)return raw;
        var scalar=NativeNumericSequenceProjection.Single(Port,ValueRules.Freeze(Port,value.Value)!);
        return Convert.ToString(scalar,System.Globalization.CultureInfo.InvariantCulture)??throw new InvalidOperationException("普通参数没有字符串值。");
    }
    internal void EnableNumericModel(FieldLayout? layout)=>NumericModel??=new(Port,layout,_accepted);
    internal NumericModelReadResult ReadNumericModel(Func<bool>? current=null)
    {
        var peer=NumericPeer;var peerAcceptance=peer?.AcceptanceVersion;var peerVersion=peer?.NumericModel?.Version;
        var numericVersion=NumericModel?.Version;
        bool Current()=>!IsDisposed&&(current?.Invoke()??true)&&!IsDisposed
            &&(peer is null||!peer.IsDisposed&&peer.AcceptanceVersion==peerAcceptance&&peer.NumericModel?.Version==peerVersion)
            &&NumericModel?.Version==numericVersion;
        if(!Current())return new(Port.Id,NumericModelReadStatus.Retired);
        if(NumericModel is not {} model)return new(Port.Id,NumericModelReadStatus.NotApplicable);
        var request=++_numericRead;var acceptance=AcceptanceVersion;
        try
        {
            var read=_readCurrentValue;var value=read?.Invoke();
            if(!Current()||request!=_numericRead||acceptance!=AcceptanceVersion)return new(Port.Id,NumericModelReadStatus.Retired);
            if(value is null)return new(Port.Id,NumericModelReadStatus.Unavailable);
            var prepared=model.ReadProjection(value.Value!);NumericCheck?.Invoke(value.Value!);
            if(!Current()||request!=_numericRead||acceptance!=AcceptanceVersion)return new(Port.Id,NumericModelReadStatus.Retired);
            var scalar=NativeNumericSequenceProjection.Single(Port,value.Value!);
            var changed=!Equals(model.CurValue,scalar)||!model.HasValue;
            numericVersion=model.Version+(changed?1:0);
            var status=model.Install(scalar);
            return !Current()||request!=_numericRead||acceptance!=AcceptanceVersion?new(Port.Id,NumericModelReadStatus.Retired):new(Port.Id,status);
        }
        catch(Exception error)
        {return new(Port.Id,Current()&&request==_numericRead&&acceptance==AcceptanceVersion?NumericModelReadStatus.Failed:NumericModelReadStatus.Retired,error.Message);}
    }
    public Port Port { get; }
    internal ParameterReferenceBehavior ReferenceBehavior {get;}
    public ParameterSetting Accepted => CopySetting(_accepted);
    public ConstantSetting? ConstantBackup
    {
        get
        {
            if(IsDisposed)return null;
            // A production host owns current values. Missing host data must not silently
            // resurrect a pre-subscription backup or an XML default.
            if(NumericModel is null&&_readCurrentValue is {} read&&_accepted is ReferenceSetting or VariableSetting)
            {
                var value=read();
                return IsDisposed||value is null?null:(ConstantSetting)CopySetting(value);
            }
            return _constantBackup is null?null:(ConstantSetting)CopySetting(_constantBackup);
        }
    }
    public bool IsReferenceMode { get; private set; }
    public string EditError { get; private set; } = "";
    public string SourceError { get; private set; } = "";
    public bool IsDisposed { get; private set; }
    internal long AcceptanceVersion {get;private set;}
    public bool ReferenceCleared => _referenceCleared;
    public bool MissingReference => IsReferenceMode && (_referenceCleared || _accepted is not (ReferenceSetting or VariableSetting))
        && !((_allowUnboundReference || !Port.Required) && _accepted is UnsetSetting);
    public bool HasInvalidInput => MissingReference || (_accepted is UnsetSetting && Port.Required && !_allowUnboundReference)
        || (IsReferenceMode && (_accepted is ReferenceSetting {RebindingReason:{Length:>0}} or VariableSetting {RebindingReason:{Length:>0}}))
        || EditError.Length != 0 || SourceError.Length != 0;
    public string Error => EditError.Length!=0?EditError:SourceError.Length!=0?SourceError
        : IsReferenceMode && _accepted is ReferenceSetting {RebindingReason:{Length:>0} reason} ? reason
        : IsReferenceMode && _accepted is VariableSetting {RebindingReason:{Length:>0} variableReason} ? variableReason
        : MissingReference ? "请选择订阅来源，或切回数值模式。"
        : _accepted is UnsetSetting && Port.Required && !_allowUnboundReference ? "必填项尚未设置。" : "";
    public event Action? Changed;
    internal event Action? ExplicitlyAccepted;

    internal void NotifyEditSettled()
    {
        if(IsDisposed)return;
        var failures=new List<Exception>();
        foreach(Action listener in Changed?.GetInvocationList()??Array.Empty<Delegate>())
        {if(IsDisposed)break;try{listener();}catch(Exception error){failures.Add(error);}}
        if(failures.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if(failures.Count>1)throw new AggregateException("字段已接纳，编辑状态通知失败。",failures);
    }

    public DeclaredFieldState(Port port, ParameterSetting initial, Action<ParameterSetting> write,
        ConstantSetting? constantBackup = null, bool allowUnboundReference = false,Func<ParameterSetting,string?>? validate=null,
        Func<ParameterSetting,bool>? propose=null,Func<ConstantSetting?>? readCurrentValue=null,ParameterReferenceBehavior referenceBehavior=ParameterReferenceBehavior.StrictExactlyOne)
    {
        Port = port;ReferenceBehavior=referenceBehavior;ParameterReferenceRules.ValidateTarget(port,referenceBehavior); _write = write;_validate=validate;_propose=propose;_readCurrentValue=readCurrentValue;
        _accepted = PrepareInitial(initial);
        _constantBackup = initial is ConstantSetting ? (ConstantSetting)CopySetting(_accepted)
            : constantBackup is null ? null : (ConstantSetting)PrepareInitial(constantBackup);
        IsReferenceMode = initial is ReferenceSetting or VariableSetting || port.EditModes == FieldEditModes.Reference;
        _allowUnboundReference = allowUnboundReference;
    }

    public bool TrySet(ParameterSetting proposal)
    {
        if(IsDisposed||_writing)return false;
        if(_propose is {} propose)return propose(CopySetting(proposal));
        return TrySetGroup(new[]{(this,proposal)},values=>_write!(CopySetting(values[Port.Id])),values=>
        {
            var error=_validate?.Invoke(CopySetting(values[Port.Id]));
            if(!string.IsNullOrEmpty(error))throw new ArgumentException(error);
        });
    }

    internal AcceptedFieldValue CaptureAccepted()=>new(Accepted,IsReferenceMode,ConstantBackup);

    /// <summary>Owner history replay, deliberately separate from a draft proposal or its writer.</summary>
    internal static bool ReplayAccepted(IReadOnlyList<(DeclaredFieldState State,AcceptedFieldValue Value)> entries,
        Action<IReadOnlyDictionary<string,ParameterSetting>> validate,Func<bool> current,Func<bool> installOwner,out Action publish)
    {
        publish=()=>{};
        var states=entries.Select(entry=>entry.State).ToArray();
        if(states.Distinct().Count()!=states.Length)throw new ArgumentException("回放字段重复。");
        if(states.Any(state=>state.IsDisposed||state._writing)||!current())return false;
        foreach(var state in states)state._writing=true;
        try
        {
            var prepared=entries.Select(entry=>
            {
                var state=entry.State;var value=entry.Value;
                var setting=value.Setting is UnsetSetting?new UnsetSetting():state.PrepareProposal(value.Setting);
                var backup=value.ConstantBackup is null?null:(ConstantSetting)state.PrepareProposal(value.ConstantBackup);
                if(setting is (ReferenceSetting or VariableSetting)&&!value.ReferenceMode
                    ||value.ReferenceMode&&(state.Port.EditModes&FieldEditModes.Reference)==0)
                    throw new ArgumentException("回放的编辑模式与正式设置不符。");
                return (State:state,Value:new AcceptedFieldValue(CopySetting(setting),value.ReferenceMode,backup));
            }).ToArray();
            validate(new System.Collections.ObjectModel.ReadOnlyDictionary<string,ParameterSetting>(prepared.ToDictionary(
                entry=>entry.State.Port.Id,entry=>CopySetting(entry.Value.Setting),StringComparer.Ordinal)));
            if(states.Any(state=>state.IsDisposed)||!current()||states.Any(state=>state.IsDisposed))return false;
            // Contract: this callback only installs the already prepared owner root/cursor, without notifications.
            // There are no external callbacks between it and the complete accepted-state assignment below.
            if(!installOwner())return false;
            foreach(var (state,value) in prepared)
            {
                state._accepted=value.Setting;state._constantBackup=value.ConstantBackup;state.IsReferenceMode=value.ReferenceMode;
                state._referenceCleared=false;state.EditError="";state.SourceError="";state.AcceptanceVersion++;state.NumericModel?.Accept(value.Setting);
            }
        }
        finally{foreach(var state in states)state._writing=false;}
        publish=()=>PublishReplay(states);
        return true;
    }
    private static void PublishReplay(IReadOnlyList<DeclaredFieldState> states)
    {
        var failures=new List<Exception>();
        foreach(var state in states)
            foreach(Action listener in state.ExplicitlyAccepted?.GetInvocationList()??Array.Empty<Delegate>())
            {if(state.IsDisposed)break;try{listener();}catch(Exception error){failures.Add(error);}}
        foreach(var state in states)
            foreach(Action listener in state.Changed?.GetInvocationList()??Array.Empty<Delegate>())
            {if(state.IsDisposed)break;try{listener();}catch(Exception error){failures.Add(error);}}
        if(failures.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if(failures.Count>1)throw new AggregateException("历史已接纳，字段观察者发生异常。",failures);
    }

    /// <summary>Prepare every endpoint before one host write; install every accepted value before notifying.</summary>
    internal static bool TrySetGroup(IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)> proposals,
        Action<IReadOnlyDictionary<string,ParameterSetting>> writeGroup,
        Action<IReadOnlyDictionary<string,ParameterSetting>>? validatePrepared=null,Func<bool>? isCurrent=null,bool replacePending=false,Action? installed=null)
    {
        ArgumentNullException.ThrowIfNull(writeGroup);
        proposals=proposals.ToArray();
        if(proposals.Count==0)return isCurrent?.Invoke()!=false;
        var states=proposals.Select(item=>item.State).ToArray();
        bool Current()
        {
            if(states.Any(state=>state.IsDisposed))return false;
            var current=isCurrent?.Invoke()!=false;
            return current&&states.All(state=>!state.IsDisposed);
        }
        if(states.Distinct().Count()!=states.Length||states.Select(state=>state.Port.Id).Distinct(StringComparer.Ordinal).Count()!=states.Length)
            throw new ArgumentException("A field may occur only once in a proposal group.");
        if(!Current()||states.Any(state=>state._writing))return false;
        var notifications=new HashSet<DeclaredFieldState>();
        var accepted=false;
        foreach(var state in states)state._writing=true;
        try
        {
            var prepared=proposals.Select(item=>(item.State,Value:item.State.PrepareProposal(item.Proposal))).ToArray();
            IReadOnlyDictionary<string,ParameterSetting> CopyPrepared()=>new System.Collections.ObjectModel.ReadOnlyDictionary<string,ParameterSetting>(
                prepared.ToDictionary(item=>item.State.Port.Id,item=>CopySetting(item.Value),StringComparer.Ordinal));
            validatePrepared?.Invoke(CopyPrepared());
            if(!Current())return false;
            if(prepared.Any(item=>item.State._referenceCleared||!SameSetting(item.State._accepted,item.Value)))
                writeGroup(CopyPrepared());
            if(!Current())return false;
            foreach(var (state,value) in prepared)
            {
                var referenceMode=value is ReferenceSetting or VariableSetting||value is UnsetSetting&&state.IsReferenceMode;
                // Prepared proposals passed the current host validation and write. Retire the old source diagnostic atomically.
                var clearSourceError=state.SourceError.Length!=0;
                if(replacePending||!SameSetting(state._accepted,value)||state._referenceCleared||state.IsReferenceMode!=referenceMode||state.EditError.Length!=0||clearSourceError)
                    notifications.Add(state);
                state._accepted=value;
                if(value is ConstantSetting constant)state._constantBackup=constant;
                state.IsReferenceMode=referenceMode;state._referenceCleared=false;state.EditError="";
                if(clearSourceError)state.SourceError="";
                state.AcceptanceVersion++;state.NumericModel?.Accept(value);
            }
            accepted=true;
        }
        catch(Exception error)when(error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            if(!Current())return false;
            foreach(var state in states)
            {
                if(state.EditError!=error.Message)notifications.Add(state);
                state.EditError=error.Message;
            }
        }
        finally{foreach(var state in states)state._writing=false;}
        // Observers see both endpoints installed; their exceptions are never interpreted as host rejection.
        var failures=new List<Exception>();
        if(accepted&&installed is not null)try{installed();}catch(Exception error){failures.Add(error);}
        if(accepted&&replacePending)
            foreach(var state in states)
                foreach(Action listener in state.ExplicitlyAccepted?.GetInvocationList()??Array.Empty<Delegate>())
                {
                    if(state.IsDisposed)break;
                    try{listener();}catch(Exception error){failures.Add(error);}
                }
        foreach(var state in states)
        {
            if(state.IsDisposed||!notifications.Contains(state)||state.Changed is not {} changed)continue;
            foreach(Action listener in changed.GetInvocationList())
            {
                if(state.IsDisposed)break;
                try{listener();}catch(Exception error){failures.Add(error);}
            }
        }
        if(failures.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if(failures.Count>1)throw new AggregateException("Field observers failed after the group was accepted.",failures);
        return accepted;
    }

    private ParameterSetting PrepareProposal(ParameterSetting proposal)
    {
        if(proposal is DisplayExpressionSetting expression)return FieldSettings.Freeze(Port,expression);
        if(proposal is UnsetSetting&&Port.Required)throw new ArgumentException("必填项不能清除。");
        if(proposal is ConstantSetting constant)
        {
            if(Port.Type.Shape==ValueShape.Resource)throw new ArgumentException("图像和区域资源必须绑定本次执行来源。");
            if(Port.EditModes!=FieldEditModes.None&&(Port.EditModes&FieldEditModes.Constant)==0)
                throw new ArgumentException("此字段不允许常量。");
            return new ConstantSetting(ValueRules.Freeze(Port,constant.Value));
        }
        if(proposal is ReferenceSetting or VariableSetting)
        {
            if(proposal is ReferenceSetting {Reference:null} or VariableSetting {Reference:null})throw new ArgumentException("订阅设置缺少正式引用。");
            if((Port.EditModes&FieldEditModes.Reference)==0)throw new ArgumentException("此字段不允许订阅。");
            ParameterReferenceRules.ValidateIndexSelections(proposal is ReferenceSetting module?module.Reference.Selection:((VariableSetting)proposal).Reference.Selection,ReferenceBehavior);
            return proposal;
        }
        if(proposal is UnsetSetting)return proposal;
        throw new ArgumentException("不支持的字段设置类型。");
    }

    public void EnterReferenceMode()
    {
        if (IsDisposed || IsReferenceMode) return;
        if ((Port.EditModes & FieldEditModes.Reference) == 0)
        { SetEditError("此字段不允许订阅。"); return; }
        IsReferenceMode = true; Changed?.Invoke();
    }

    public bool TryUseConstant(Func<ConstantSetting,bool>? accept=null)
    {
        if (IsDisposed) return false;
        if(NumericModel is not null&&(Port.EditModes&FieldEditModes.Constant)==0){SetEditError("此字段不允许常量。");return false;}
        if(NumericModel is {} model&&_accepted is ReferenceSetting or VariableSetting)
        {
            var result=ReadNumericModel();
            if(result.Status is NumericModelReadStatus.Updated or NumericModelReadStatus.Unchanged)return accept is null?TrySet(model.Prepare(model.Value!)):accept(model.Prepare(model.Value!));
            if(IsDisposed)return false;
            IsReferenceMode=false;EditError=result.Error??"普通参数不可用，请输入新的常量值；正式引用在有效提交前保持。";Changed?.Invoke();return false;
        }
        if (ConstantBackup is { } value) return TrySet(value);
        if((Port.EditModes&FieldEditModes.Constant)==0){SetEditError("此字段不允许常量。");return false;}
        IsReferenceMode=false;
        EditError="请输入新的常量值；原订阅在有效提交前保持不变。";
        Changed?.Invoke();
        return false;
    }

    public void ClearReference()
    {
        if(IsDisposed)return;
        if(!Port.Required){TrySet(new UnsetSetting());return;}
        if(_referenceCleared)return;
        _referenceCleared=true;IsReferenceMode=true;
        EditError="请选择订阅来源，或切回数值模式。";
        Changed?.Invoke();
    }

    public void SetEditError(string error)
    {
        if (IsDisposed || EditError == error) return;
        EditError = error; Changed?.Invoke();
    }

    public void SetSourceError(string error)
    {
        if (IsDisposed || SourceError == error) return;
        SourceError = error; Changed?.Invoke();
    }

    internal static bool SameSetting(ParameterSetting left, ParameterSetting right) => (left, right) switch
    {
        (UnsetSetting, UnsetSetting) => true,
        (DisplayExpressionSetting a,DisplayExpressionSetting b)=>a.SameExpression(b),
        (ConstantSetting a, ConstantSetting b) => SameValue(a.Value, b.Value),
        (ReferenceSetting a, ReferenceSetting b) => a.Conversion == b.Conversion && a.OriginId == b.OriginId
            && a.RebindingReason == b.RebindingReason && SameReference(a.Reference, b.Reference),
        (VariableSetting a, VariableSetting b) => a.Conversion==b.Conversion&&a.RebindingReason==b.RebindingReason
            &&SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceSettingParts.SameSource(a.Reference,b.Reference)&&SameSelection(a.Reference.Selection,b.Reference.Selection),
        _ => false
    };

    private ParameterSetting PrepareInitial(ParameterSetting value)
    {
        if(value is DisplayExpressionSetting expression)return FieldSettings.Freeze(Port,expression);
        if (value is not ConstantSetting constant) return value;
        try { return new ConstantSetting(ValueRules.Freeze(Port, constant.Value)); }
        catch (ArgumentException) when (Port.Choices is not null && constant.Value is string)
        {
            // Retain a removed choice for explicit user repair, but still enforce its scalar type.
            EditError = "当前枚举值不在声明选项中，请重新选择。";
            return new ConstantSetting(ValueRules.Freeze(Port with { Choices = null }, constant.Value));
        }
    }

    internal static ParameterSetting CopySetting(ParameterSetting value) => value is DisplayExpressionSetting expression?expression.Copy():value is ConstantSetting { Value: double[] array }
        ? new ConstantSetting(array.ToArray()) : value;

    internal static bool SameReference(OutputReference a, OutputReference b) => SameSource(a,b) && SameSelection(a.Selection,b.Selection);
    internal static bool SameSource(OutputReference a,OutputReference b) => a.NodeId == b.NodeId
        && a.ScopeId == b.ScopeId && a.PortId == b.PortId && a.Type == b.Type && a.Storage == b.Storage
        && a.MemberFieldPath.SequenceEqual(b.MemberFieldPath);

    private static bool SameSelection(BindingSelection a, BindingSelection b) => (a, b) switch
    {
        (BindingSelection.AllItems, BindingSelection.AllItems) => true,
        (BindingSelection.AtConstant x, BindingSelection.AtConstant y) => x.Index == y.Index,
        (BindingSelection.AtField x, BindingSelection.AtField y) => SameReference(x.Reference, y.Reference),
        (BindingSelection.AtVariable x, BindingSelection.AtVariable y) => x.Reference.SolutionId==y.Reference.SolutionId
            &&x.Reference.VariableId==y.Reference.VariableId&&x.Reference.Type==y.Reference.Type&&x.Reference.Storage==y.Reference.Storage,
        _ => false
    };

    private static bool SameValue(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is double[] aa && right is double[] bb) return aa.SequenceEqual(bb);
        if (left is RecordValue a && right is RecordValue b)
        {
            if (a.Declaration != b.Declaration) return false;
            var fields = a.CopyFields(); var other = b.CopyFields();
            return fields.Count == other.Count && fields.All(field => other.TryGetValue(field.Key, out var value)
                && SameValue(field.Value, value));
        }
        if (left is SequenceValue x && right is SequenceValue y)
            return x.Declaration == y.Declaration && x.Count == y.Count
                && Enumerable.Range(0, x.Count).All(index => SameValue(x[index], y[index]));
        return Equals(left, right);
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true; _numericRead++;NumericModel?.Retire(); NumericPeer=null;NumericCheck=null;Changed = null;ExplicitlyAccepted=null; _write = null;_validate=null;_propose=null;_readCurrentValue=null;
    }
}
