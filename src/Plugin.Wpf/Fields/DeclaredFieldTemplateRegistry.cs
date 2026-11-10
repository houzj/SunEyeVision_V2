using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// VM 参数树到本方视觉模板的唯一分类入口。
///
/// 这个注册表只决定"用哪一种原参数行模板"，不保存参数值，也不执行工具逻辑。
/// 键与原厂 <c>paramusercontroldatatemplate.xaml</c> 的 DataTemplate 键同名（Default/Integer/
/// Float/String/Boolean/Enumeration/IntegerBettween/FloatBettween/IntegerRadio/Reference），
/// 由 <see cref="ParameterRowTemplateSelector"/> 直接按键取模板；未登记的键会直接报错，
/// 不允许静默渲染成空行。
/// </summary>
public static class DeclaredFieldTemplateRegistry
{
    public const string NumericPlain = "Default";
    public const string Integer = "Integer";
    public const string Float = "Float";
    public const string String = "String";
    public const string Boolean = "Boolean";
    public const string BooleanEye = "BooleanEye";
    public const string Enumeration = "Enumeration";
    public const string IntegerBettween = "IntegerBettween";
    public const string FloatBettween = "FloatBettween";
    public const string IntegerRadio = "IntegerRadio";
    public const string Reference = "Reference";
    /// <summary>组合槽占位：图像来源等由消费者在构造后挂接显式组合控件的字段（UseImageSourceSelector 等）。</summary>
    public const string ComposedSlot = "Slot";

    /// <summary>
    /// Resolves one declared field to the original VM-aligned template key.
    /// The port and presentation are validated together so an unknown or mismatched
    /// field cannot silently render as an empty row.
    /// </summary>
    /// <param name="numericSequence">序列数值投影（走原厂滑条模式）。</param>
    /// <param name="hasIntegerRadio">整数单选选项（按信息/按点等模式切换）。</param>
    /// <param name="inlineConstantReference">常量|订阅内联切换（同一槽位）。</param>
    /// <param name="isDialogPrimary">对话组主字段（眼睛开关）。</param>
    public static string ResolveTemplateKey(
        Port port,
        FieldPresentation presentation,
        bool isNumericPair = false,
        bool numericSequence = false,
        bool hasIntegerRadio = false,
        bool inlineConstantReference = false,
        bool isDialogPrimary = false)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(presentation);
        if (!StringComparer.Ordinal.Equals(port.Id, presentation.FieldId))
            throw new ArgumentException("字段身份与参数展示声明不一致。", nameof(presentation));

        if (isNumericPair)
        {
            return port.Type == TypeRef.ScalarOf(ScalarKind.Int64) ? IntegerBettween : FloatBettween;
        }

        if (hasIntegerRadio)
            return IntegerRadio;

        if (inlineConstantReference)
            return Reference;

        // 仅引用字段（区域/图像等任意形态，对齐原编辑器引用模式分支）：订阅链接行。
        if (port.EditModes == FieldEditModes.Reference)
            return Reference;

        return presentation.Editor switch
        {
            FieldEditorKind.Scalar => ResolveScalar(port, presentation, numericSequence, isDialogPrimary),
            FieldEditorKind.Choice => Enumeration,
            // 对齐原编辑器：记录字段由行宿主展开子行（组合槽自渲染），不走行内模板。
            FieldEditorKind.Record => ComposedSlot,
            FieldEditorKind.Sequence => throw new NotSupportedException($"序列字段不支持行内模板：{port.Id}（{presentation.Editor}）。"),
            FieldEditorKind.Geometry => throw new NotSupportedException($"几何字段不支持行内模板：{port.Id}（{presentation.Editor}）。"),
            FieldEditorKind.Region => throw new NotSupportedException($"区域字段不支持行内模板：{port.Id}（{presentation.Editor}）。"),
            FieldEditorKind.Image when port.Type == TypeRef.ResourceOf(ResourceKind.Image) && port.EditModes == FieldEditModes.Reference => ComposedSlot,
            FieldEditorKind.Image => throw new NotSupportedException($"图像字段不支持行内模板：{port.Id}（{presentation.Editor}）。"),
            _ => throw new NotSupportedException($"未注册的参数模板类型：{presentation.Editor}")
        };
    }

    /// <summary>数值是否走原厂滑条模式（对齐原 NativeNumberPresenter.UsesOriginalModes 判定）。</summary>
    public static bool UsesOriginalModes(Port port, FieldPresentation? presentation, bool numericSequence)
        => numericSequence && NativeNumericSequenceProjection.Applies(port)
           || port.Type == TypeRef.ScalarOf(ScalarKind.Int64)
           || (port.Type == TypeRef.ScalarOf(ScalarKind.Float64) && presentation?.Layout is {Minimum: not null, Maximum: not null});

    private static string ResolveScalar(Port port, FieldPresentation presentation, bool numericSequence, bool isDialogPrimary)
    {
        var type = port.Type;
        if (type == TypeRef.ScalarOf(ScalarKind.Boolean))
            return isDialogPrimary ? BooleanEye : Boolean;

        if (type == TypeRef.ScalarOf(ScalarKind.Int64))
            return Integer;

        if (type == TypeRef.ScalarOf(ScalarKind.Float64))
        {
            // 对齐 NativeNumberPresenter.UsesOriginalModes：float64 有显式上下界（或序列投影）走原厂滑条，否则走数值框。
            return presentation.Layout is {Minimum: not null, Maximum: not null} || numericSequence
                ? Float
                : NumericPlain;
        }

        if (type == TypeRef.ScalarOf(ScalarKind.Text))
            return String;

        throw new NotSupportedException($"未模板化的标量类型不支持行内模板：{port.Id}（{type}）");
    }
}
