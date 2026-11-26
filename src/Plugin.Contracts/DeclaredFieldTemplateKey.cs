namespace SunEyeVision.Plugin.Contracts;

/// <summary>
/// 参数行模板键（原厂 <c>DisplayType</c> 的合法取值）。
/// 这些字符串与原厂 <c>paramusercontroldatatemplate.xaml</c> 的 <c>DataTemplate x:Key</c> 同名，
/// 契约层只持有键名（不依赖 WPF），由平台按名字查模板。
/// </summary>
/// <remarks>
/// 原厂的分派规则是“精确字符串 → 字典 key”，未命中落 <c>Default</c>；
/// 本方保留 <see cref="Default"/> 作为显式选择，但**不做静默回退**：
/// 未登记的键在渲染时直接报错。
/// </remarks>
public static class DeclaredFieldTemplateKey
{
    /// <summary>纯数值框/文本兜底（原厂未命中时的落点，这里只作显式选择）。</summary>
    public const string Default = "Default";
    public const string Integer = "Integer";
    public const string Float = "Float";
    public const string String = "String";
    public const string Boolean = "Boolean";
    /// <summary>带开关状态提示文案的布尔（原厂 <c>SelecetedTips/UnSelecetedTips</c>）。</summary>
    public const string BooleanTips = "BooleanTips";
    /// <summary>对话框主字段的眼睛开关。</summary>
    public const string BooleanEye = "BooleanEye";
    public const string Enumeration = "Enumeration";
    public const string IntegerBettween = "IntegerBettween";
    public const string FloatBettween = "FloatBettween";
    public const string IntegerRadio = "IntegerRadio";
    /// <summary>引用/订阅行（原厂订阅链接形态）。</summary>
    public const string Reference = "Reference";
    /// <summary>图像来源选择（原厂 <c>EnumerationG</c>）。</summary>
    public const string ImageSource = "EnumerationG";
    /// <summary>组合槽：由消费者在构造后挂接显式组合控件（原厂 <c>Slot</c>）。</summary>
    public const string ComposedSlot = "Slot";
    public const string FolderPath = "OpenFileDialog";
    public const string CameraSelecter = "CameraSelecter";

    /// <summary>
    /// 声明期把“编辑器形态 + 端口类型”折算成模板键。
    /// 只在**读取声明**时调用一次（XML 加载、回退目录、编译器输入行），
    /// 渲染期一律读 <see cref="FieldPresentation.DisplayType"/>，不再推导。
    /// </summary>
    public static string FromPort(TypeRef type, FieldEditorKind editor)
    {
        ArgumentNullException.ThrowIfNull(type);
        return editor switch
        {
            FieldEditorKind.Choice => Enumeration,
            FieldEditorKind.Reference => Reference,
            FieldEditorKind.Image or FieldEditorKind.Region or FieldEditorKind.Record
                or FieldEditorKind.Sequence or FieldEditorKind.Geometry => ComposedSlot,
            FieldEditorKind.FolderPath => FolderPath,
            FieldEditorKind.Camera => CameraSelecter,
            // 序列/记录型（NumberArray、TextArray、记录序列）不是标量数值，走组合槽。
            _ => type.Shape is ValueShape.Sequence or ValueShape.Record ? ComposedSlot
                : type == DataType.Boolean ? Boolean
                : type == DataType.Text ? String
                : type == DataType.Number ? Float
                : Integer
        };
    }
}
