using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>One field owner's nonpersistent CurValue. Neither a binding identity nor an execution value.</summary>
internal sealed class NativeNumericModel
{
    private readonly Port _port;
    private readonly FieldLayout? _layout;
    internal bool HasValue {get;private set;}
    internal object? Value {get;private set;}
    internal long Version {get;private set;}
    internal event Action? Changed;
    internal NativeNumericModel(Port port,FieldLayout? layout,ParameterSetting accepted)
    {_port=port;_layout=layout;Accept(accepted);}
    internal ConstantSetting Prepare(object value,bool checkBounds=true)
    {
        var frozen=NativeNumericSequenceProjection.Applies(_port)?NativeNumericSequenceProjection.FreezeNumber(_port,NativeNumericSequenceProjection.Single(_port,value)):ValueRules.Freeze(_port,value);
        var scalar=NativeNumericSequenceProjection.Single(_port,frozen!);
        NativeNumericCommit.EnsureRepresentable(new(frozen));
        var number=Convert.ToDouble(scalar,System.Globalization.CultureInfo.InvariantCulture);
        if(!double.IsFinite(number))throw new ArgumentException("请输入有限数值。");
        var minimum=_layout?.Minimum;var maximum=_layout?.Maximum;
        if(checkBounds&&(minimum is {} min&&number<min||maximum is {} max&&number>max))throw new ArgumentException("数值超出允许范围。");
        if(_port.Type.Scalar==ScalarKind.Int64&&NumericStorageRules.Effective(_port.Type,_port.Storage)==NumericStorage.Int32)
        {
            var low=minimum??int.MinValue;var high=maximum??int.MaxValue;var increment=_layout?.Increment??1;
            if(!double.IsFinite(low)||low!=Math.Truncate(low)||low<int.MinValue||low>int.MaxValue
                ||!double.IsFinite(high)||high!=Math.Truncate(high)||high<int.MinValue||high>int.MaxValue
                ||!double.IsFinite(increment)||increment<=0||increment!=Math.Truncate(increment)||increment>int.MaxValue)
                throw new ArgumentException("整数最小值、最大值及正步长必须是有效Int32格点。");
            var integer=checked((int)(long)scalar);var start=checked((int)low);var step=checked((int)increment);
            if(integer>=low&&integer<=high)
            {var offset=checked(integer-start);integer=checked(checked(offset/step*step)+start);}
            frozen=(long)integer;
        }
        else if(_port.Type.Scalar==ScalarKind.Float64||NativeNumericSequenceProjection.Applies(_port))
        {
            if((_layout?.DecimalPlaces??2) is var digits)
            {if(digits is <0 or >9)throw new ArgumentException("原浮点控件仅支持0至9位编辑精度。");number=Math.Round(number,digits,MidpointRounding.ToEven);}
            frozen=NativeNumericSequenceProjection.FreezeNumber(_port,number);
        }
        return new(frozen);
    }
    internal void Accept(ParameterSetting setting)
    {
        if(setting is not ConstantSetting constant)return;
        try{Install(NativeNumericSequenceProjection.Single(_port,Prepare(constant.Value!,false).Value!),false);}catch(Exception error)when(error is ArgumentException or OverflowException or FormatException){HasValue=false;Value=null;Version++;}
    }
    internal NumericModelReadStatus Install(object value,bool publish=true)
    {
        if(HasValue&&Equals(Value,value))return NumericModelReadStatus.Unchanged;
        HasValue=true;Value=value;Version++;
        if(publish)ParameterCleanup.Run((Changed?.GetInvocationList().Cast<Action>()??Enumerable.Empty<Action>()).Select(handler=>(Action)(()=>handler())));
        return NumericModelReadStatus.Updated;
    }
    internal void Retire()=>Changed=null;
}
