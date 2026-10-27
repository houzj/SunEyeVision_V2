using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Presentation choices for the original two-slot or five-slot integer radio template.</summary>
public sealed record NativeIntegerRadioChoice(int Value,string Label);

public sealed class NativeIntegerRadioOptions
{
    public IReadOnlyList<NativeIntegerRadioChoice> Choices {get;}
    public bool UseFiveSlotTemplate {get;}
    public NativeIntegerRadioOptions(IEnumerable<NativeIntegerRadioChoice> choices,bool useFiveSlotTemplate=false)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var values=choices.ToArray();
        if(values.Length==0||values.Length>(useFiveSlotTemplate?5:2))throw new ArgumentException("原整数单选模板的选项数量超出槽位。",nameof(choices));
        if(values.Any(x=>x is null||string.IsNullOrWhiteSpace(x.Label))||values.Select(x=>x.Value).Distinct().Count()!=values.Length)
            throw new ArgumentException("整数单选需要非空标签和唯一值。",nameof(choices));
        Choices=Array.AsReadOnly(values);UseFiveSlotTemplate=useFiveSlotTemplate;
    }
    internal void ValidatePort(Port port)
    {
        if(port.Type.Shape!=ValueShape.Scalar||port.Type.Scalar!=ScalarKind.Int64||!port.Required
            ||port.EditModes is not (FieldEditModes.None or FieldEditModes.Constant))
            throw new ArgumentException("原整数单选要求必填、常量编辑的Int64字段。");
    }
    internal string? Validate(ParameterSetting setting)=>setting is ConstantSetting{Value:long value}
        &&value>=int.MinValue&&value<=int.MaxValue&&Choices.Any(x=>x.Value==value)
            ?null:"当前整数值不在声明的单选项中，请重新选择。";
}
