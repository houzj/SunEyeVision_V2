using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 原件「结果显示」页的图像/文本显示项。原件该页由 5 个 <c>ImageViewConfig</c> 组成
/// （<c>SensorFiled</c>、<c>FixtureInitPoint</c>、<c>FixtureRunPoint</c>、<c>ResutlText</c>、
/// <c>CheckZoneAnnular</c>，见 docs/07-工具逆向/tools/imvsintensitymeasuremodu/ui-preview.json:67-86）：
/// 本方实现「亮度区域」「检测区域」两个图形分组与「文本」七行；位置修正的基准点/运行点
/// （<c>FixtureInitPoint</c>/<c>FixtureRunPoint</c>）与 <c>SensorFiled</c> 随位置修正能力一起未实现，
/// 见交接单未实现项。这些字段只随工具持久化，不是算法参数，也不进入执行输入。
/// </summary>
public static class IntensityMeasureResultDisplay
{
    public const string Region = "display.lum-region";
    public const string Roi = "display.roi";
    public const string Text = "display.text";
    public const string ReferencePoint = "display.reference-point";
    public const string RunPoint = "display.run-point";
    public const string Annulus = "display.annulus";
    /// <summary>原厂「图像显示 → 文本」行：只控制文字叠加可见性。</summary>
    public const string TextOverlayVisible = "display.text-overlay.visible";
    public static string Field(string group, string role) => group + "." + role;

    public const string RegionTitle = "亮度区域";
    public const string RoiTitle = "检测区域";

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    /// <summary>
    /// 结果页显示需求：行集合、行身份、矩形、区域、文本数值、判定。
    /// 不写进这里的结果资源，结果页不会叠加（Blob 工具踩过的坑）。
    /// </summary>
    public static ResultDisplayDescriptor Descriptor { get; } = new(
        new(IntensityMeasureOutput.Rows),
        new(IntensityMeasureOutput.Rows, new[] { IntensityMeasureOutput.RowIndex }),
        new(IntensityMeasureOutput.Rows, new[] { IntensityMeasureOutput.Rectangle }),
        new(IntensityMeasureOutput.Rows, new[] { IntensityMeasureOutput.Region }),
        new(IntensityMeasureOutput.Rows, new[] { IntensityMeasureOutput.LumMean }),
        new(IntensityMeasureOutput.Rows, new[] { IntensityMeasureOutput.JudgmentPassed }),
        selectedRectangleColor: new(0, 0, 255));

    public static InputValues Defaults()
        => new(Schema.Inputs, Definitions.ToDictionary(item => item.Port.Id, item => (object?)item.Value, StringComparer.Ordinal));

    /// <summary>原厂文本项默认模板：模块状态与五项统计量，占位符由结果文本表达式解析。</summary>
    public const string DefaultTextContent = "模块状态:{模块状态}最小值:{最小值} 最大值:{最大值} 均值:{均值} 标准差:{标准差} 对比度:{对比度}";

    private static IReadOnlyList<Definition> Build()
    {
        var result = new List<Definition>();
        // 结果显示页分区顺序跟随字段 Order：原厂是「结果判断 → 图像显示 → 文本显示」。
        // 判据字段在本工具设置声明中为 10..50，显示字段必须排在其后。
        var order = 100;
        void Add(string id, string label, string section, TypeRef type, object value, FieldEditModes modes,
            double? minimum = null, double? maximum = null, double? increment = null, int? decimals = 2,
            string? dialog = null, string? dialogTitle = null, bool primary = false)
        {
            var port = new Port(id, type, EditModes: modes);
            result.Add(new(port, new(id, label, section, order++,
                type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar)
            {
                Layout = new FieldLayout("结果显示", section, Minimum: minimum, Maximum: maximum,
                    Increment: increment ?? 1, DecimalPlaces: type == DataType.Number ? decimals : 0,
                    AllowReference: modes.HasFlag(FieldEditModes.Reference),
                    DialogGroup: dialog, DialogTitle: dialogTitle, DialogPrimary: primary)
            }, value));
        }
        var ok = PresentationDeclarations.Color(102, 255, 0);
        var ng = PresentationDeclarations.Color(255, 0, 0);
        void GraphicFields(string group, string label, RecordValue color, double opacity = 1, double width = 2)
        {
            // 分组四行样式与可见性开关必须同时给出 DialogGroup/DialogTitle，并把开关立为主字段：
            // 缺任一项，参数页组合会在运行时拒绝该分组。
            Add(Field(group, "visible"), label, "图像显示", DataType.Boolean, true, FieldEditModes.Constant,
                dialog: group, dialogTitle: label, primary: true);
            Add(Field(group, "ok"), "OK颜色", "图像显示", PresentationDeclarations.Rgba, color, FieldEditModes.Constant,
                dialog: group, dialogTitle: label);
            Add(Field(group, "ng"), "NG颜色", "图像显示", PresentationDeclarations.Rgba, ng, FieldEditModes.Constant,
                dialog: group, dialogTitle: label);
            Add(Field(group, "opacity"), "透明度", "图像显示", DataType.Number, opacity, FieldEditModes.Constant,
                0, 1, .01, dialog: group, dialogTitle: label);
            Add(Field(group, "width"), "线宽", "图像显示", DataType.Number, width, FieldEditModes.Constant,
                1, 10, 1, dialog: group, dialogTitle: label);
        }
        GraphicFields(Region, RegionTitle, ok);
        // 原件 CheckZoneAnnular 的检测区域用蓝色叠加，与像素统计工具的检测区域一致。
        GraphicFields(Roi, RoiTitle, PresentationDeclarations.Color(0, 0, 255));
        GraphicFields(ReferencePoint, "基准点", ok);
        GraphicFields(RunPoint, "运行点", ng);
        // 原厂「图像显示 → 文本」：只负责文字叠加的可见性，样式仍在文本显示分区。
        Add(TextOverlayVisible, "文本", "图像显示", DataType.Boolean, true, FieldEditModes.Constant);
        GraphicFields(Annulus, "扇环检测区域", PresentationDeclarations.Color(0, 0, 255));
        Add(Field(Text, "content"), "内容", "文本显示", DataType.Text, DefaultTextContent,
            FieldEditModes.Constant | FieldEditModes.DisplayExpression);
        Add(Field(Text, "ok"), "OK颜色", "文本显示", PresentationDeclarations.Rgba, ok, FieldEditModes.Constant);
        Add(Field(Text, "ng"), "NG颜色", "文本显示", PresentationDeclarations.Rgba, ng, FieldEditModes.Constant);
        Add(Field(Text, "size"), "字号", "文本显示", DataType.Number, 10d, FieldEditModes.Constant, 6, 72, 1);
        Add(Field(Text, "opacity"), "透明度", "文本显示", DataType.Number, 1d, FieldEditModes.Constant, 0, 1, .1);
        // 文本位置可被前序变量引用（PositionX/PositionY 独立取值），因此不定固定小数位；
        // 该写法必须在参数页用 TextDisplays 注册为文本坐标，否则参数页拒绝组装。
        Add(Field(Text, "x"), "位置X", "文本显示", DataType.Number, 0d,
            FieldEditModes.Constant | FieldEditModes.Reference, decimals: null);
        Add(Field(Text, "y"), "位置Y", "文本显示", DataType.Number, 0d,
            FieldEditModes.Constant | FieldEditModes.Reference, decimals: null);
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
        return new(Style(Region),
            new(Read<bool>(TextOverlayVisible), content, Color(Text, "ok"), Color(Text, "ng"),
                size == Math.Truncate(size) ? size : 10d, Number(Text, "opacity"),
                x is ConstantSetting { Value: double xv } ? xv : 0, y is ConstantSetting { Value: double yv } ? yv : 0,
                PositionX: x, PositionY: y),
            Style(Roi) with { ShowMeasurements = true },
            Annulus: Style(Annulus),
            ReferencePoint: Style(ReferencePoint),
            RunPoint: Style(RunPoint));
    }
}
