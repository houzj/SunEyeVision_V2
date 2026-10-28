using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.Contracts;
using Numbers=SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

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
    public bool IsDirty {get;private set;}
    public long EditVersion {get;private set;}
    public bool IsSuspended=>_suspended;
    public TextBox Text=>_text;
    public string? PendingRaw=>IsDirty?(_suspended?_pendingRaw:_text.Text):null;
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
        if(_syncing||_suspended||!Current()||_state.IsReferenceMode)return;
        IsDirty=true;
        EditVersion++;
        var previous=_rawError;
        _rawError=ReadRaw(out _,out var error)?"":error;
        if(_rawError.Length!=0)_state.SetEditError(_rawError);
        else if(previous.Length!=0&&_state.EditError==previous)_state.SetEditError("");
    }
    private bool ReadRaw(out object value,out string error)
    {
        value=null!;error="";
        try
        {
            var raw=_suspended?_pendingRaw??_text.Text:_text.Text;
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
        var numeric=Convert.ToDouble(frozen,CultureInfo.InvariantCulture);
        if((_layout?.Minimum is {} minimum&&numeric<minimum)||(_layout?.Maximum is {} maximum&&numeric>maximum)
            ||(!_paired&&(numeric<_leaf.Minimum||numeric>_leaf.Maximum))
            ||(_paired&&_owner is not null&&(numeric<_owner.Minimum||numeric>_owner.Maximum))
            ||_leaf.IsNeedCheckExtremumByXML&&(numeric<_leaf.MinValue||numeric>_leaf.MaxValue))
            throw new ArgumentException("数值超出允许范围。");
        return new(frozen);
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
        var prepared=proposal;var raw=_suspended?_pendingRaw??_text.Text:_text.Text;var editVersion=EditVersion;var acceptance=_state.AcceptanceVersion;
        var valid=false;Exception? failure=null;string? bindingError=null;
        try{IgnoreChanges(()=>valid=ValidatePreparedBindings(prepared,error=>bindingError=error));}
        catch(Exception error){failure=error;}
        try
        {
            // Binding validation may format a Float target. Only owner acceptance may replace the edit's raw text.
            if(Current()&&editVersion==EditVersion&&acceptance==_state.AcceptanceVersion)
                IgnoreChanges(()=>{if(_suspended)_pendingRaw=raw;else _text.SetCurrentValue(TextBox.TextProperty,raw);});
        }
        catch(Exception cleanup){failure=failure is null?cleanup:new AggregateException(failure,cleanup);}
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        if(bindingError is not null&&Current()&&editVersion==EditVersion&&acceptance==_state.AcceptanceVersion)
            _state.SetEditError(bindingError);
        return valid&&Current()&&editVersion==EditVersion&&acceptance==_state.AcceptanceVersion&&!_state.IsReferenceMode;
    }
    public bool TryReadProposal(out ConstantSetting proposal)
    {
        proposal=null!;
        if(_disposed||_state.IsDisposed)return false;
        if(!ReadRaw(out var value,out var error)){_state.SetEditError(error);return false;}
        proposal=new(value);return true;
    }
    public void IgnoreChanges(Action action)
    {var previous=_syncing;_syncing=true;try{action();}finally{_syncing=previous;}}
    public bool ValidatePreparedBindings(ConstantSetting proposal,Action<string>? reportError=null)
    {
        void Reject(string error){if(reportError is not null)reportError(error);else _state.SetEditError(error);}
        if(_disposed||_state.IsDisposed)return false;
        // An unloaded capture consumes its saved edit, without touching the detached visual.
        if(_suspended)return true;
        if(_paired)IgnoreChanges(()=>_text.SetCurrentValue(TextBox.TextProperty,Format(proposal.Value!)));
        // Preserve both original bindings, their validators, trigger and floating format.
        var inner=_text.GetBindingExpression(TextBox.TextProperty);
        inner?.UpdateSource();
        if(!Current())return false;
        if(inner?.HasValidationError==true){Reject("原数值输入校验未通过。");return false;}
        var outer=_leaf.GetBindingExpression(Numbers.NumericControl.TextProperty);
        outer?.UpdateSource();
        if(!Current())return false;
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
            binding.UpdateSource();
            if(!Current())return false;
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
        _syncing=true;
        try
        {
            if(_owner is not null)
            {
                var value=Convert.ToDouble(constant.Value,CultureInfo.InvariantCulture);
                _projectBoundValue?.Invoke(value);
                if(!Current())return;
                _owner.SetCurrentValue(_valueProperty!,value);
                if(!Current())return;
                if(_requireOwnerBinding)_owner.GetBindingExpression(_valueProperty!)?.UpdateTarget();
                if(!Current())return;
            }
            else _leaf.SetCurrentValue(Numbers.NumericControl.TextProperty,Format(constant.Value!));
            if(!Current())return;
            var outer=_leaf.GetBindingExpression(Numbers.NumericControl.TextProperty);
            if(outer is not null){Validation.ClearInvalid(outer);outer.UpdateTarget();}
            if(!Current())return;
            var inner=_text.GetBindingExpression(TextBox.TextProperty);
            if(inner is not null){Validation.ClearInvalid(inner);inner.UpdateTarget();}
            if(!Current())return;
            IsDirty=false;_rawError="";_pendingRaw=null;
        }
        finally{_syncing=false;}
    }
    private string Format(object value)=>value is long integer?integer.ToString(CultureInfo.InvariantCulture)
        :Convert.ToDouble(value,CultureInfo.InvariantCulture).ToString(_layout is null?"G17":"F"+_layout.DecimalPlaces,CultureInfo.InvariantCulture);
    public void ValidateInitial()
    {
        if(_state.IsReferenceMode||_state.Accepted is not ConstantSetting initial)return;
        try{if(IsDirty){if(!ReadRaw(out _,out var error))_state.SetEditError(error);}else ValidateStored(initial);}
        catch(Exception error)when(error is ArgumentException or FormatException or OverflowException){_state.SetEditError(error.Message);}
    }
    public void RestoreRejected(string? raw)
    {
        var suspended=_suspended;ProjectAccepted();
        if(raw is null)return;
        RestorePending(raw);if(!suspended)Resume();
    }
    public void Suspend(){if(_suspended)return;_pendingRaw=PendingRaw;_suspended=true;}
    public void RestorePending(string? raw)
    {if(raw is null)return;_pendingRaw=raw;_suspended=true;IsDirty=true;}
    public void Resume()
    {
        if(!_suspended||!Current())return;
        _suspended=false;
        if(_pendingRaw is not {} raw){ProjectAccepted();return;}
        _syncing=true;
        try{_text.SetCurrentValue(TextBox.TextProperty,raw);if(!Current())return;IsDirty=true;_pendingRaw=null;}
        finally{_syncing=false;}
        _rawError=ReadRaw(out _,out var error)?"":error;
        if(_rawError.Length!=0)_state.SetEditError(_rawError);
    }
    public void Dispose()
    {if(_disposed)return;_disposed=true;_text.TextChanged-=OnTextChanged;}
}
