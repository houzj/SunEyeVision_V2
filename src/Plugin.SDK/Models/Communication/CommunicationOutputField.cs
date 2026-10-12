using System;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

/// <summary>The field identity is persisted; the label and ordering may change.</summary>
public sealed record CommunicationOutputField(Guid FieldId, string Name, string VariableType)
{
    public string BindingKey => "Field_" + FieldId.ToString("N");

    public OutputVariable CreateContract(object? value = null)
    {
        if (FieldId == Guid.Empty || string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("输出字段身份和名称不能为空。");
        var type = SdkTypeVocabulary.ToSystemType(VariableType)
            ?? throw new NotSupportedException($"不支持的输出类型：{VariableType}");
        return new OutputVariable
        {
            Name = BindingKey, DisplayName = Name, VariableType = VariableType,
            Kind = OutputVariableKind.Data,
            RuntimeValue = OutputVariableArrayPromoter.Promote(value, VariableType, type)
        };
    }
}
