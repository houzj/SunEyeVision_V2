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
    // 键名统一由契约层给出，避免两处各写一份产生漂移。
    public const string NumericPlain = DeclaredFieldTemplateKey.Default;
    public const string Integer = DeclaredFieldTemplateKey.Integer;
    public const string Float = DeclaredFieldTemplateKey.Float;
    public const string String = DeclaredFieldTemplateKey.String;
    public const string FolderPath = DeclaredFieldTemplateKey.FolderPath;
    public const string CameraSelecter = DeclaredFieldTemplateKey.CameraSelecter;
    public const string Boolean = DeclaredFieldTemplateKey.Boolean;
    public const string BooleanTips = DeclaredFieldTemplateKey.BooleanTips;
    public const string BooleanEye = DeclaredFieldTemplateKey.BooleanEye;
    public const string Enumeration = DeclaredFieldTemplateKey.Enumeration;
    public const string IntegerBettween = DeclaredFieldTemplateKey.IntegerBettween;
    public const string FloatBettween = DeclaredFieldTemplateKey.FloatBettween;
    public const string IntegerRadio = DeclaredFieldTemplateKey.IntegerRadio;
    public const string Reference = DeclaredFieldTemplateKey.Reference;
    public const string ImageSource = DeclaredFieldTemplateKey.ImageSource;
    /// <summary>组合槽占位：图像来源等由消费者在构造后挂接显式组合控件的字段（UseImageSourceSelector 等）。</summary>
    public const string ComposedSlot = DeclaredFieldTemplateKey.ComposedSlot;

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
        if(presentation.Layout?.BooleanTips is {} tips && (port.Type!=TypeRef.ScalarOf(ScalarKind.Boolean)||!port.Required||port.Nullable
            ||port.EditModes!=FieldEditModes.Constant||tips.Selected is null||tips.Unselected is null||presentation.Editor!=FieldEditorKind.Scalar
            ||isNumericPair||hasIntegerRadio||inlineConstantReference||isDialogPrimary||presentation.Layout.DialogGroup is not null))
            throw new ArgumentException("State tips require an ordinary constant Boolean template.",nameof(presentation));

        // 原厂 ExternDisplayType：宿主在运行期把该行绑定成“常量|订阅”内联控件时，
        // 用运行时键覆盖声明键（原件 ParamUserControl 的同名依赖属性就是这么做的）。
        if (inlineConstantReference)
            return Reference;

        // 原厂 IntegerRadio 是独立节点类型：本方由宿主登记的单选选项声明该行，
        // 因此这里取登记的键，而不是按端口类型推。
        if (hasIntegerRadio)
            return IntegerRadio;

        // 原厂语义：声明直给模板键（XML 的 DisplayType）优先，平台不再推导。
        // 键是否真的存在由 ParameterRowTemplateSelector 查资源树校验（找不到直接报错）。
        if (presentation.DisplayType is { } declared)
            return declared;

        // 原厂 UserTempConverter 的语义：键只能由声明给出（DisplayType / ExternDisplayType）。
        // 声明缺键属于声明错误，不是运行时可猜的情形——直接失败，不落 Default、不推导。
        throw new ArgumentException(
            $"字段 {presentation.FieldId} 未声明参数行模板键（原厂 DisplayType）。", nameof(presentation));
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
        {
            if(presentation.Layout?.BooleanTips is not null)
                return !isDialogPrimary?BooleanTips:throw new ArgumentException("State tips cannot replace an eye-dialog control.");
            return isDialogPrimary ? BooleanEye : Boolean;
        }

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
