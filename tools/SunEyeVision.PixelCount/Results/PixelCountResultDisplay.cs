using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 原厂「结果显示」页的图像显示项（<c>IMVSPixelCountModuVAAlgorithmTab.xml:176-249</c> 的
/// <c>CheckZone</c>/<c>Fixtured Point</c>/<c>Unfixtured Point</c>/<c>ResutlText</c>，颜色取
/// <c>IMVSPixelCountModuVADisplay.xml</c> 的 <c>OK/NG</c> 色与线宽）。
/// 这些字段只随工具持久化，不是算法参数，也不进入执行输入。
/// 位置修正两点（Fixtured/Unfixtured Point）随位置修正能力一起未实现，见交接单未实现项。
/// </summary>
public static class PixelCountResultDisplay
{
    public const string Roi = "display.roi";
    public const string ReferencePoint = "display.reference-point";
    public const string RunPoint = "display.run-point";
    public const string Text = "display.text";
    public static string Field(string group, string role) => group + "." + role;

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    /// <summary>
    /// 结果页显示需求：行集合、行身份、矩形、区域、文本值、判定。
    /// 不写进这里的结果资源，结果页不会叠加（Blob 工具踩过的坑）。
    /// </summary>
    public static ResultDisplayDescriptor Descriptor { get; } = new(
        new(PixelCountOutput.Rows),
        new(PixelCountOutput.Rows, new[] { PixelCountOutput.RangeIndex }),
        new(PixelCountOutput.Rows, new[] { PixelCountOutput.Rectangle }),
        new(PixelCountOutput.Rows, new[] { PixelCountOutput.Region }),
        new(PixelCountOutput.Rows, new[] { PixelCountOutput.RangeRatio }),
        new(PixelCountOutput.Rows, new[] { PixelCountOutput.JudgmentPassed }),
        selectedRectangleColor: new(0, 0, 255));

    public static InputValues Defaults()
        => new(Schema.Inputs, Definitions.ToDictionary(item => item.Port.Id, item => (object?)item.Value, StringComparer.Ordinal));

    /// <summary>
    /// 原厂 Display.xml 模板 `ModuleStatus:{0}PixelNum:{1} ,Rate{2}` 的中文资源值：
    /// `docs/07-工具逆向/decompiled/Lang/zh-cn.xaml` 中该 key 对应 `模块状态:{0}像素数量:{1} ,比率{2}`。
    /// </summary>
    public const string DefaultTextContent = "模块状态:{0}像素数量:{1} ,比率{2}";

    private static IReadOnlyList<Definition> Build()
    {
        var result = new List<Definition>();
        // VM places display declarations after the tool's result-judgment
        // fields on the same result page.  Starting at zero would move
        // 图像显示 ahead of 结果判定.
        var order = (PixelCountParameterDefinition.Instance.SettingsSchema.Presentation?.Fields
            .Select(field => field.Order)
            .DefaultIfEmpty(-1)
            .Max() ?? -1) + 1;
        void Add(string id, string label, string section, TypeRef type, object value, FieldEditModes modes,
            double? minimum = null, double? maximum = null, double? increment = null, int? decimals = 2,
            string? dialogGroup = null, string? dialogTitle = null, bool dialogPrimary = false)
        {
            var port = new Port(id, type, EditModes: modes);
            result.Add(new(port, new(id, label, section, order++,
                type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar)
            {
                Layout = new FieldLayout("结果显示", section, Minimum: minimum, Maximum: maximum,
                    Increment: increment ?? 1, DecimalPlaces: type == DataType.Number ? decimals : 0,
                    AllowReference: modes.HasFlag(FieldEditModes.Reference),
                    DialogGroup: dialogGroup,
                    DialogTitle: dialogTitle,
                    DialogPrimary: dialogPrimary)
            }, value));
        }
        var ok = PresentationDeclarations.Color(102, 255, 0);
        var ng = PresentationDeclarations.Color(255, 0, 0);
        void GraphicFields(string group, string label, RecordValue color, double width, bool dialog = false)
        {
            // 原厂 ImageViewConfig 的显示名就是行标题（ROI / Fixtured Point / Unfixtured Point），
            // 不能在样式对话框上写死成同一标题。
            var title = dialog ? label : null;
            Add(Field(group, "visible"), label, "图像显示", DataType.Boolean, true, FieldEditModes.Constant,
                dialogGroup: dialog ? group : null, dialogTitle: title, dialogPrimary: dialog);
            Add(Field(group, "ok"), "OK颜色", "图像显示", PresentationDeclarations.Rgba, color, FieldEditModes.Constant,
                dialogGroup: dialog ? group : null, dialogTitle: title);
            Add(Field(group, "ng"), "NG颜色", "图像显示", PresentationDeclarations.Rgba, ng, FieldEditModes.Constant,
                dialogGroup: dialog ? group : null, dialogTitle: title);
            Add(Field(group, "opacity"), "透明度", "图像显示", DataType.Number, 1d, FieldEditModes.Constant, 0, 1, .01,
                dialogGroup: dialog ? group : null, dialogTitle: title);
            Add(Field(group, "width"), "线宽", "图像显示", DataType.Number, width, FieldEditModes.Constant, 1, 10, 1,
                dialogGroup: dialog ? group : null, dialogTitle: title);
        }
        // 原厂 ImageViewConfig 顺序是 CheckZone、FixtureInitPoint、FixtureRunPoint、ResutlText。
        // CustomVisible=False 只控制叠加对象的初始可见性，结果页仍保留两行配置。
        GraphicFields(Roi, "检测区域", PresentationDeclarations.Color(0, 0, 255), 2, dialog: true);
        GraphicFields(ReferencePoint, "基准点", ok, 1, dialog: true);
        GraphicFields(RunPoint, "运行点", ng, 1, dialog: true);
        // 原厂 AlgorithmTab 的 ResutlText 属于 Tab_Image Display，是第 4 个 ImageViewConfig：
        // 只有眼睛、没有样式笔（PanVisible=False）；文本显示组只放内容/颜色/字号等 7 行。
        Add(Field(Text, "visible"), "文本", "图像显示", DataType.Boolean, true, FieldEditModes.Constant);
        Add(Field(Text, "content"), "内容", "文本显示", DataType.Text, DefaultTextContent,
            FieldEditModes.Constant | FieldEditModes.DisplayExpression);
        Add(Field(Text, "ok"), "OK颜色", "文本显示", PresentationDeclarations.Rgba, ok, FieldEditModes.Constant);
        Add(Field(Text, "ng"), "NG颜色", "文本显示", PresentationDeclarations.Rgba, ng, FieldEditModes.Constant);
        Add(Field(Text, "size"), "字号", "文本显示", DataType.Number, 10d, FieldEditModes.Constant, 6, 72, 1);
        Add(Field(Text, "opacity"), "透明度", "文本显示", DataType.Number, 1d, FieldEditModes.Constant, 0, 1, .1);
        // 原厂文本位置可被前序变量引用（PositionX/PositionY 独立取值）。
        Add(Field(Text, "x"), "位置X", "文本显示", DataType.Number, 0d,
            FieldEditModes.Constant | FieldEditModes.Reference, decimals: 2);
        Add(Field(Text, "y"), "位置Y", "文本显示", DataType.Number, 0d,
            FieldEditModes.Constant | FieldEditModes.Reference, decimals: 2);
        return result.AsReadOnly();
    }

    public static ResultDisplayAppearance Capture(FieldSettings fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        T Read<T>(string id) => fields.Get<T>(id);
        double Number(string group, string role)
        {
            var id = Field(group, role);
            var value = Read<double>(id);
            var layout = Definitions.Single(item => item.Port.Id == id).Presentation.Layout!;
            if (!double.IsFinite(value) || value < layout.Minimum || value > layout.Maximum
                || layout.DecimalPlaces is { } digits && Math.Round(value, digits, MidpointRounding.ToEven) != value)
                throw new ArgumentException("显示数值超出范围或精度：" + id);
            return value;
        }
        ResultDisplayColor Color(string group, string role)
        {
            var value = Read<RecordValue>(Field(group, role));
            return new(checked((byte)value.Get<long>("r")), checked((byte)value.Get<long>("g")),
                checked((byte)value.Get<long>("b")), checked((byte)value.Get<long>("a")));
        }
        ResultGraphicAppearance Style(string group)
            => new(Read<bool>(Field(group, "visible")), Color(group, "ok"), Color(group, "ng"),
                Number(group, "opacity"), Number(group, "width"));
        var content = fields.Read(Field(Text, "content")) switch
        {
            DisplayExpressionSetting configured => configured.Text,
            ConstantSetting { Value: string raw } => raw,
            _ => throw new ArgumentException("文本内容设置无效。")
        };
        ParameterSetting Position(string role)
        {
            var setting = fields.Read(Field(Text, role));
            if (setting is ReferenceSetting) return setting;
            if (setting is ConstantSetting { Value: double number } && double.IsFinite(number)) return setting;
            throw new ArgumentException("文本位置必须是有限数或已声明数字来源。");
        }
        var x = Position("x");
        var y = Position("y");
        var size = Number(Text, "size");
        var rangeStyle = new ResultGraphicAppearance(true, new ResultDisplayColor(102, 255, 0), new ResultDisplayColor(255, 0, 0), 1d, 2d);
        return new(rangeStyle,
            new(Read<bool>(Field(Text, "visible")), content, Color(Text, "ok"), Color(Text, "ng"),
                size == Math.Truncate(size) ? size : 10d, Number(Text, "opacity"),
                x is ConstantSetting { Value: double xv } ? xv : 0, y is ConstantSetting { Value: double yv } ? yv : 0,
                PositionX: x, PositionY: y),
            Style(Roi) with { ShowMeasurements = true },
            ReferencePoint: Style(ReferencePoint) with { FixedJudgment = true, PointLabel = "Fixture BasePoint" },
            RunPoint: Style(RunPoint) with { FixedJudgment = true, PointLabel = "Fixture RunPoint" });
    }
}
