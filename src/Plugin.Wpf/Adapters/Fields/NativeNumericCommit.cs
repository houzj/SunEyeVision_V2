using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.Contracts;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

internal readonly record struct NumericPendingEdit(string Raw,long Acceptance);

/// <summary>Raw-edit and binding commit over the original numeric leaf; accepted values belong only to the borrowed state.</summary>
internal sealed class NativeNumericCommit : IDisposable
{
    private readonly DeclaredFieldState _state;
    private readonly FieldLayout? _layout;
    private readonly Numbers.NumericControl _leaf;
    private readonly Numbers.SliderArrangeBase? _owner;
    private readonly DependencyProperty? _valueProperty;
    private readonly bool _paired;
    private readonly bool _requireOwnerBinding;
    private readonly Func<bool>? _isCurrent;
    private readonly Action<double>? _projectBoundValue;
    private readonly TextBox _text;
    private bool _syncing,_disposed,_suspended;
    private string? _pendingRaw;
    private string _rawError="";
    private bool _dirty,_validateRaw;
    private long _rawAcceptance,_externalMutation;
    private long _textRevision;
    private sealed class TextWrite(string expected){internal readonly string Expected=expected;internal bool Seen;}
    private TextWrite? _textWrite;
    public bool IsDirty {get=>_dirty&&_rawAcceptance==_state.AcceptanceVersion;private set{_dirty=value;if(value)_rawAcceptance=_state.AcceptanceVersion;}}
    public long EditVersion {get;private set;}
    public bool IsSuspended=>_suspended;
    public TextBox Text=>_text;
    public NumericPendingEdit? Pending=>IsDirty?new(_suspended?_pendingRaw??_text.Text:_text.Text,_rawAcceptance):null;
    private (long Acceptance,long Mutation,bool Reference) Stamp()=> (_state.AcceptanceVersion,_externalMutation,_state.IsReferenceMode);
    private bool Matches((long Acceptance,long Mutation,bool Reference) stamp)=>Current()&&stamp==Stamp();
    private bool WriteText(string expected,Action action,(long Acceptance,long Mutation,bool Reference) stamp)
    {
        if(!Matches(stamp))return false;
        var revision=_textRevision;
        var inner=_text.GetBindingExpression(TextBox.TextProperty);var outer=_leaf.GetBindingExpression(Numbers.NumericControl.TextProperty);
        var previous=_textWrite;_textWrite=new(expected);
        try{IgnoreChanges(action);}finally{_textWrite=previous;}
        var valid=Matches(stamp)&&_textRevision-revision<=1&&ReferenceEquals(inner,_text.GetBindingExpression(TextBox.TextProperty))
            &&ReferenceEquals(outer,_leaf.GetBindingExpression(Numbers.NumericControl.TextProperty));
        if(!valid)_externalMutation++;
        return valid;
    }
    internal bool ProjectCandidate(ConstantSetting value,Action action)=>WriteText(Format(value.Value!),action,Stamp());
    internal static void EnsureRepresentable(ConstantSetting setting)
    {
        if(setting.Value is long value&&(value < -9007199254740992L||value > 9007199254740992L))
            throw new ArgumentException("当前原数值控件不能保证此整数的精确步进。");
    }

    public NativeNumericCommit(DeclaredFieldState state,FieldLayout? layout,Numbers.NumericControl leaf,Numbers.SliderArrangeBase? owner,DependencyProperty? valueProperty,bool paired=false,
        bool requireOwnerBinding=false,Func<bool>? isCurrent=null,Action<double>? projectBoundValue=null)
    {
        _state=state;_layout=layout;_leaf=leaf;_owner=owner;_valueProperty=valueProperty;_paired=paired;
        _requireOwnerBinding=requireOwnerBinding;_isCurrent=isCurrent;_projectBoundValue=projectBoundValue;
        if(owner is not null&&valueProperty is null)throw new ArgumentException("原数值组合必须明确端点。");
        _text=(TextBox)leaf.FindName("ValueText");
        _text.TextChanged+=OnTextChanged;
        try{ProjectAccepted();}
        catch{_disposed=true;_text.TextChanged-=OnTextChanged;throw;}
    }
    private void OnTextChanged(object sender,TextChangedEventArgs args)
    {
        _textRevision++;
        if(_textWrite is {Seen:false} write&&_text.Text==write.Expected){write.Seen=true;return;}
        _externalMutation++;
        var revision=_textRevision;var acceptance=_state.AcceptanceVersion;
        if(_suspended||!Current()||_state.IsReferenceMode||revision!=_textRevision||acceptance!=_state.AcceptanceVersion)return;
        IsDirty=true;
        EditVersion++;
        _validateRaw=true;
        if(!_syncing)ValidateRaw();
    }
    private void ValidateRaw()
    {
        _validateRaw=false;
        if(!Current()||!IsDirty||_state.IsReferenceMode)return;
        var stamp=Stamp();
        var previous=_rawError;
        _rawError=ReadRaw(out _,out var error)?"":error;
        if(!Matches(stamp))return;
        if(_rawError.Length!=0)_state.SetEditError(_rawError);
        else if(previous.Length!=0&&_state.EditError==previous)_state.SetEditError("");
    }
    private bool ReadRaw(out object value,out string error)
    {
        value=null!;error="";
        try
        {
            var raw=_suspended?(IsDirty?_pendingRaw??_text.Text:Format((_state.Accepted as ConstantSetting??_state.ConstantBackup??throw new ArgumentException("没有可提交的常量。")).Value!)):_text.Text;
            if(string.IsNullOrWhiteSpace(raw))throw new ArgumentException("请输入数值。");
            if(_state.Port.Type.Scalar==ScalarKind.Int64)
            {
                var integer=long.Parse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture);
                EnsureRepresentable(new ConstantSetting(integer));
                value=integer;
            }
            else
            {
                var number=double.Parse(raw,NumberStyles.Float,CultureInfo.InvariantCulture);
                if(!double.IsFinite(number))throw new ArgumentException("请输入有限数值。");
                value=number;
            }
            // Typed raw input must satisfy absolute bounds before display precision is applied.
            // Paired endpoint ordering is validated later against the complete candidate pair.
            ValidateBounds(value);
            value=PrepareValue(value).Value!;
            return true;
        }
        catch(Exception failure)when(failure is ArgumentException or FormatException or OverflowException)
        {error=failure.Message;return false;}
    }
    public ConstantSetting PrepareValue(object value)
    {
        EnsureRepresentable(new ConstantSetting(value));
        if(value is double number)
        {
            if(!double.IsFinite(number))throw new ArgumentException("请输入有限数值。");
            if(_layout?.DecimalPlaces is {} digits)value=Math.Round(number,digits,MidpointRounding.ToEven);
        }
        var frozen=ValueRules.Freeze(_state.Port,value)!;
        ValidateBounds(frozen);
        return new(frozen);
    }
    private void ValidateBounds(object value)
    {
        var numeric=Convert.ToDouble(value,CultureInfo.InvariantCulture);
        if((_layout?.Minimum is {} minimum&&numeric<minimum)||(_layout?.Maximum is {} maximum&&numeric>maximum)
            ||(!_paired&&(numeric<_leaf.Minimum||numeric>_leaf.Maximum))
            ||(_paired&&_owner is not null&&(numeric<_owner.Minimum||numeric>_owner.Maximum))
            ||_leaf.IsNeedCheckExtremumByXML&&(numeric<_leaf.MinValue||numeric>_leaf.MaxValue))
            throw new ArgumentException("数值超出允许范围。");
    }
    public void ValidateStored(ConstantSetting setting)
    {
        var prepared=PrepareValue(setting.Value!);
        if(!Equals(prepared.Value,setting.Value))throw new ArgumentException("当前数值超出声明的编辑精度，请明确修改后提交。");
    }
    public bool TryPrepare(out ConstantSetting proposal)
    {
        proposal=null!;
        if(_disposed||_state.IsDisposed)return false;
        if(!ReadRaw(out var value,out var parseError)){_state.SetEditError(parseError);return false;}
        proposal=new(value);
        var prepared=proposal;var raw=_suspended?_pendingRaw??_text.Text:_text.Text;var editVersion=EditVersion;var acceptance=_state.AcceptanceVersion;var stamp=Stamp();
        var valid=false;Exception? failure=null;string? bindingError=null;
        try{IgnoreChanges(()=>valid=ValidatePreparedBindings(prepared,error=>bindingError=error));}
        catch(Exception error){failure=error;}
        try
        {
            // Only successful, unchanged binding transfers may restore their captured raw text.
            // A rejected transfer may contain a validator's newer edit; leave its actual text intact.
            if(valid&&Current()&&editVersion==EditVersion&&acceptance==_state.AcceptanceVersion)
            {if(_suspended)_pendingRaw=raw;else if(!WriteText(raw,()=>_text.SetCurrentValue(TextBox.TextProperty,raw),stamp))valid=false;}
        }
        catch(Exception cleanup){failure=failure is null?cleanup:new AggregateException(failure,cleanup);}
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        if(bindingError is not null&&Matches(stamp)&&editVersion==EditVersion&&acceptance==_state.AcceptanceVersion)
            _state.SetEditError(bindingError);
        return valid&&Matches(stamp)&&editVersion==EditVersion&&acceptance==_state.AcceptanceVersion&&!_state.IsReferenceMode;
    }
    public bool TryReadProposal(out ConstantSetting proposal)
    {
        proposal=null!;
        if(_disposed||_state.IsDisposed)return false;
        if(!ReadRaw(out var value,out var error)){_state.SetEditError(error);return false;}
        proposal=new(value);return true;
    }
    public void IgnoreChanges(Action action)
    {var previous=_syncing;_syncing=true;try{action();}finally{_syncing=previous;if(!_syncing&&_validateRaw)ValidateRaw();}}
    public bool ValidatePreparedBindings(ConstantSetting proposal,Action<string>? reportError=null)
    {
        void Reject(string error){if(reportError is not null)reportError(error);else _state.SetEditError(error);}
        if(_disposed||_state.IsDisposed)return false;
        // An unloaded capture consumes its saved edit, without touching the detached visual.
        if(_suspended)return true;
        var stamp=Stamp();var expected=Format(proposal.Value!);
        if(_paired&&!WriteText(expected,()=>_text.SetCurrentValue(TextBox.TextProperty,expected),stamp))return false;
        // Preserve both original bindings, their validators, trigger and floating format.
        var inner=_text.GetBindingExpression(TextBox.TextProperty);
        if(!WriteText(expected,()=>inner?.UpdateSource(),stamp))return false;
        if(inner?.HasValidationError==true){Reject("原数值输入校验未通过。");return false;}
        var outer=_leaf.GetBindingExpression(Numbers.NumericControl.TextProperty);
        if(!WriteText(expected,()=>outer?.UpdateSource(),stamp))return false;
        if(outer?.HasValidationError==true){Reject("无法提交原数值绑定。");return false;}
        if(_requireOwnerBinding)
        {
            // The original style template adds a third binding: Arrange.StartValue -> staged bridge.
            // Its setter never writes the field. Every binding must be live before the host proposal.
            if(inner is null||outer is null||inner.Status!=BindingStatus.Active||outer.Status!=BindingStatus.Active)
            {Reject("原数值输入绑定已失效。");return false;}
            var bridge=BindingOperations.GetBindingExpressionBase(_owner!,_valueProperty!);
            if(bridge is not BindingExpression binding||binding.Status!=BindingStatus.Active)
            {Reject("原数值桥绑定缺失或已失效。");return false;}
            if(!WriteText(expected,binding.UpdateSource,stamp))return false;
            if(!ReferenceEquals(bridge,BindingOperations.GetBindingExpressionBase(_owner!,_valueProperty!))
                ||binding.Status!=BindingStatus.Active||binding.HasValidationError||Validation.GetHasError(_owner!))
            {Reject("无法提交原数值桥绑定。");return false;}
        }
        return true;
    }
    private bool Current()
    {
        if(_disposed||_state.IsDisposed)return false;
        var current=_isCurrent?.Invoke()??true;
        return current&&!_disposed&&!_state.IsDisposed;
    }
    public void ProjectAccepted()
    {
        if(!Current())return;
        if(_state.IsReferenceMode){IsDirty=false;_rawError="";_pendingRaw=null;return;}
        var constant=_state.Accepted as ConstantSetting??_state.ConstantBackup;
        if(constant is null)return;
        EnsureRepresentable(constant);
        if(_suspended){IsDirty=false;_rawError="";_pendingRaw=null;return;}
        var stamp=Stamp();var expected=Format(constant.Value!);var syncing=_syncing;_syncing=true;
        try
        {
            if(_owner is not null)
            {
                var value=Convert.ToDouble(constant.Value,CultureInfo.InvariantCulture);
                if(!WriteText(expected,()=>_projectBoundValue?.Invoke(value),stamp))return;
                if(!WriteText(expected,()=>_owner.SetCurrentValue(_valueProperty!,value),stamp))return;
                if(_requireOwnerBinding&&!WriteText(expected,()=>_owner.GetBindingExpression(_valueProperty!)?.UpdateTarget(),stamp))return;
            }
            else if(!WriteText(expected,()=>_leaf.SetCurrentValue(Numbers.NumericControl.TextProperty,expected),stamp))return;
            var outer=_leaf.GetBindingExpression(Numbers.NumericControl.TextProperty);
            if(outer is not null&&!WriteText(expected,()=>{Validation.ClearInvalid(outer);outer.UpdateTarget();},stamp))return;
            var inner=_text.GetBindingExpression(TextBox.TextProperty);
            if(inner is not null&&!WriteText(expected,()=>{Validation.ClearInvalid(inner);inner.UpdateTarget();},stamp))return;
            // A restored raw SetCurrentValue can remain the effective target override even
            // after UpdateTarget. Install this accepted text without replacing either binding.
            if(!WriteText(expected,()=>_text.SetCurrentValue(TextBox.TextProperty,expected),stamp))return;
            IsDirty=false;_rawError="";_pendingRaw=null;
        }
        finally{_syncing=syncing;if(!_syncing&&_validateRaw)ValidateRaw();}
    }
    public void BeginConstantEntry()
    {
        if(!Current()||_state.IsReferenceMode)return;
        var stamp=Stamp();
        if(!WriteText(string.Empty,()=>_text.SetCurrentValue(TextBox.TextProperty,string.Empty),stamp))return;
        IsDirty=true;_pendingRaw=string.Empty;
        _state.SetEditError("请输入新的常量值。");
    }
    private string Format(object value)=>value is long integer?integer.ToString(CultureInfo.InvariantCulture)
        :Convert.ToDouble(value,CultureInfo.InvariantCulture).ToString(_layout is null?"G17":"F"+_layout.DecimalPlaces,CultureInfo.InvariantCulture);
    public void ValidateInitial()
    {
        if(_state.IsReferenceMode||_state.Accepted is not ConstantSetting initial)return;
        try{if(IsDirty){if(!ReadRaw(out _,out var error))_state.SetEditError(error);}else ValidateStored(initial);}
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){_state.SetEditError(error.Message);}
    }
    public void RestoreRejected(NumericPendingEdit? raw)
    {
        var suspended=_suspended;var stamp=Stamp();ProjectAccepted();
        if(raw is null||!Matches(stamp))return;
        RestorePending(raw);if(!suspended)Resume();
    }
    public void Suspend(){if(_suspended)return;_pendingRaw=Pending?.Raw;_suspended=true;}
    public void RestorePending(NumericPendingEdit? raw)
    {if(raw is null||raw.Value.Acceptance!=_state.AcceptanceVersion||IsDirty)return;_pendingRaw=raw.Value.Raw;_suspended=true;IsDirty=true;}
    public void Resume()
    {
        if(!_suspended||!Current())return;
        _suspended=false;
        if(!IsDirty||_pendingRaw is not {} raw){ProjectAccepted();return;}
        var stamp=Stamp();
        if(!WriteText(raw,()=>_text.SetCurrentValue(TextBox.TextProperty,raw),stamp))return;
        IsDirty=true;_pendingRaw=null;
        _rawError=ReadRaw(out _,out var error)?"":error;
        if(_rawError.Length!=0)_state.SetEditError(_rawError);
    }
    public void Dispose()
    {if(_disposed)return;_disposed=true;_text.TextChanged-=OnTextChanged;}
}
