using System.Globalization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>Validates the original editable subscription text without inventing a numeric slider format.</summary>
internal static class InlineConstantRules
{
    public static void Validate(Port port,FieldPresentation presentation,ParameterSetting setting)
    {
        var layout=presentation.Layout;
        if(layout is not null&&(layout.DecimalPlaces is <0 or >15
            ||layout.Minimum is {} min&&!double.IsFinite(min)||layout.Maximum is {} max&&!double.IsFinite(max)
            ||layout.Minimum>layout.Maximum))throw new ArgumentException("内联常量的范围或小数位声明无效。");
        if(setting is not ConstantSetting proposed)return;
        var constant=new ConstantSetting(ValueRules.Freeze(port,proposed.Value));
        var value=Convert.ToDouble(constant.Value,CultureInfo.InvariantCulture);
        if(layout is not null&&(value<layout.Minimum||value>layout.Maximum))throw new ArgumentException("数值超出声明的编辑范围。");
        if(constant.Value is double real&&layout?.DecimalPlaces is {} digits)
        {
            var rounded=Math.Round(real,digits,MidpointRounding.ToEven);
            if(NumericStorageRules.Effective(port.Type,port.Storage)==NumericStorage.Float32)rounded=(double)(float)rounded;
            if(rounded!=real)throw new ArgumentException("数值超出声明的编辑精度，请提供规范值。");
        }
    }
}
