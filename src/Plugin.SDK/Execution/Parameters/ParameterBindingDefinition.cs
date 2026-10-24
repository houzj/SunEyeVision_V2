using System;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 参数绑定定义。只描述值的来源，不承载当前运行值，也不依赖 UI。
/// </summary>
public sealed record ParameterBindingDefinition
{
    public required string ParameterName { get; init; }
    public BindingType BindingType { get; init; } = BindingType.Constant;
    public object? ConstantValue { get; init; }
    public string? SourceNodeId { get; init; }
    public string? SourceWorkflowId { get; init; }
    public Guid? SourceFieldId { get; init; }
    public string? ReferenceExpression { get; init; }
    public SunEyeVision.Plugin.Contracts.BindingConversion Conversion { get; init; }
    public string? SourceProperty { get; init; }
    public string? SourceNodeName { get; init; }
    public string? SourceDisplayName { get; init; }
    public Type? TargetType { get; init; }

    public static ParameterBindingDefinition FromSetting(ParamSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        return new ParameterBindingDefinition
        {
            ParameterName = setting.ParameterName,
            BindingType = setting.BindingType,
            ConstantValue = setting.ConstantValue,
            SourceNodeId = setting.SourceNodeId,
            SourceWorkflowId = setting.SourceWorkflowId, SourceFieldId = setting.SourceFieldId,
            ReferenceExpression = setting.ReferenceExpression, Conversion = setting.Conversion,
            SourceProperty = setting.SourceProperty,
            SourceNodeName = setting.SourceNodeName,
            SourceDisplayName = setting.SourceDisplayName,
            TargetType = setting.TargetType
        };
    }

    public ParamSetting ToSetting()
        => new()
        {
            ParameterName = ParameterName,
            BindingType = BindingType,
            ConstantValue = ConstantValue,
            SourceNodeId = SourceNodeId,
            SourceWorkflowId = SourceWorkflowId, SourceFieldId = SourceFieldId,
            ReferenceExpression = ReferenceExpression, Conversion = Conversion,
            SourceProperty = SourceProperty,
            SourceNodeName = SourceNodeName,
            SourceDisplayName = SourceDisplayName,
            TargetType = TargetType
        };
}
