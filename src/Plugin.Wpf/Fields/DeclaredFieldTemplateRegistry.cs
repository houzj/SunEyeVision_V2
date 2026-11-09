using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// VM 参数树到本方视觉模板的唯一分类入口。
///
/// 这个注册表只决定“用哪一种模板/外部槽”，不保存参数值，也不执行工具逻辑。
/// 普通字段由 Native 参数控件模板呈现，ROI、范围对、图形/文本组合等重型内容由
/// 工具通过显式槽挂接。这样可以沿 VM 的 ParamUserControl/DataTemplate 语义迁移，
/// 同时避免把 Blob 业务状态复制到公共控件库。
/// </summary>
public enum DeclaredFieldTemplateKind
{
    Scalar,
    Choice,
    Record,
    Sequence,
    Geometry,
    Region,
    ResultPresentation,
    Image,
    NumericPair,
    DialogGroup
}

public static class DeclaredFieldTemplateRegistry
{
    /// <summary>
    /// Resolves one declared field to the VM-aligned template slot.
    /// The port and presentation are validated together so an unknown or mismatched
    /// field cannot silently render as an empty row.
    /// </summary>
    public static DeclaredFieldTemplateKind Resolve(
        Port port,
        FieldPresentation presentation,
        bool isNumericPair = false,
        bool isDialogGroup = false)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(presentation);
        if (!StringComparer.Ordinal.Equals(port.Id, presentation.FieldId))
            throw new ArgumentException("字段身份与参数展示声明不一致。", nameof(presentation));

        var kind = presentation.Editor switch
        {
            FieldEditorKind.Scalar => DeclaredFieldTemplateKind.Scalar,
            FieldEditorKind.Choice => DeclaredFieldTemplateKind.Choice,
            FieldEditorKind.Record => DeclaredFieldTemplateKind.Record,
            FieldEditorKind.Sequence => DeclaredFieldTemplateKind.Sequence,
            FieldEditorKind.Geometry => DeclaredFieldTemplateKind.Geometry,
            FieldEditorKind.Region => DeclaredFieldTemplateKind.Region,
            FieldEditorKind.ResultPresentation => DeclaredFieldTemplateKind.ResultPresentation,
            FieldEditorKind.Image => DeclaredFieldTemplateKind.Image,
            _ => throw new NotSupportedException($"未注册的参数模板类型：{presentation.Editor}")
        };

        // Composite slots take precedence over the ordinary field template.  Their
        // child fields still retain their own stable identities and state models.
        if (isNumericPair)
            return DeclaredFieldTemplateKind.NumericPair;
        if (isDialogGroup)
            return DeclaredFieldTemplateKind.DialogGroup;
        return kind;
    }
}
