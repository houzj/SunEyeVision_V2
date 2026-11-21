using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>
/// 原件「结果显示」页的显示项（<c>ui-preview.json</c> 的 ImageViewConfig：SensorFiled/MatchResults/
/// MatchLocation/LineFiled/MatchOutlinePoints/FixtureInitPoint/FixtureRunPoint/ResutlText/BlindZone）。
/// 这些字段只随工具持久化，不是算法参数，也不进入执行输入。
/// </summary>
/// <remarks>
/// 原件把「匹配结果」与「直线域」分成两项，但两者画的是同一组匹配框线
/// （<c>MarkFindDialogUI.xaml.cs:903 DrawLinesAndPoints</c>）；本框架的外观槽位只有一个匹配框组，
/// 故合并为「匹配结果」一项，差异记录在交接单。
/// </remarks>
public static class MarkFindResultDisplay
{
    public const string Match = "display.match";
    public const string Location = "display.location";
    public const string Inspection = "display.inspection";
    public const string Outline = "display.outline";
    public const string ReferencePoint = "display.reference-point";
    public const string RunPoint = "display.run-point";
    public const string Mask = "display.mask";
    public const string Text = "display.text";
    public static string Field(string group, string role) => group + "." + role;

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    /// <summary>
    /// 结果页显示需求：行集合、行身份、匹配框、区域、分数、判定、轮廓点、匹配点。
    /// 不写进这里的结果资源，结果页不会叠加。
    /// </summary>
    public static ResultDisplayDescriptor Descriptor { get; } = new(
        new(MarkFindSchema.Rows),
        new(MarkFindSchema.Rows, new[] { "element-id" }),
        new(MarkFindSchema.Rows, new[] { "box" }),
        new(MarkFindSchema.Rows, new[] { "region" }),
        new(MarkFindSchema.Rows, new[] { "score" }),
        new(MarkFindSchema.Rows, new[] { "judgment-passed" }),
        outline: new(MarkFindSchema.Rows, new[] { "outline" }),
        selectedRectangleColor: new(255, 255, 0),
        centroid: new(MarkFindSchema.Rows, new[] { "match-point" }));

    public static InputValues Defaults()
        => new(Schema.Inputs, Definitions.ToDictionary(item => item.Port.Id, item => (object?)item.Value, StringComparer.Ordinal));

    private static IReadOnlyList<Definition> Build()
    {
        var result = new List<Definition>();
        var order = 0;
        void Add(string id, string label, string section, TypeRef type, object value,
            double? minimum = null, double? maximum = null, double? increment = null, int? decimals = 2)
        {
            var modes = id == Field(Text, "content") ? FieldEditModes.Constant | FieldEditModes.DisplayExpression
                : id == Field(Text, "x") || id == Field(Text, "y") ? FieldEditModes.Constant | FieldEditModes.Reference
                : FieldEditModes.Constant;
            var port = new Port(id, type, EditModes: modes);
            result.Add(new(port, new(id, label, section, order++,
                type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar)
            {
                Layout = new FieldLayout("结果显示", section, Minimum: minimum, Maximum: maximum,
                    Increment: increment ?? 1, DecimalPlaces: type == DataType.Number ? decimals : 0)
            }, value));
        }
        var ok = PresentationDeclarations.Color(102, 255, 0);
        var ng = PresentationDeclarations.Color(255, 0, 0);
        void GraphicFields(string group, string label, RecordValue color, double opacity = 1, double width = 2)
        {
            Add(Field(group, "visible"), label, "图像显示", DataType.Boolean, true);
            Add(Field(group, "ok"), "OK颜色", "图像显示", PresentationDeclarations.Rgba, color);
            Add(Field(group, "ng"), "NG颜色", "图像显示", PresentationDeclarations.Rgba, ng);
            Add(Field(group, "opacity"), "透明度", "图像显示", DataType.Number, opacity, 0, 1, .01);
            Add(Field(group, "width"), "线宽", "图像显示", DataType.Number, width, 1, 10, 1);
        }
        GraphicFields(Match, "匹配结果", ok);
        GraphicFields(Location, "匹配位置", ng, width: 1);
        GraphicFields(Inspection, "传感器区域", PresentationDeclarations.Color(0, 0, 255));
        Add(Field(Outline, "visible"), "匹配轮廓点", "图像显示", DataType.Boolean, true);
        GraphicFields(ReferencePoint, "基准点", ok, width: 1);
        GraphicFields(RunPoint, "运行点", ng, width: 1);
        GraphicFields(Mask, "盲区", PresentationDeclarations.Color(255, 84, 0), .5);
        Add(Field(Text, "visible"), "文本", "图像显示", DataType.Boolean, true);
        Add(Field(Text, "content"), "内容", "文本显示", DataType.Text, "匹配分数:{分数}");
        Add(Field(Text, "ok"), "OK颜色", "文本显示", PresentationDeclarations.Rgba, ok);
        Add(Field(Text, "ng"), "NG颜色", "文本显示", PresentationDeclarations.Rgba, ng);
        Add(Field(Text, "size"), "字号", "文本显示", DataType.Number, 10d, 6, 72, 1);
        Add(Field(Text, "opacity"), "透明度", "文本显示", DataType.Number, 1d, 0, 1, .1);
        Add(Field(Text, "x"), "位置X", "文本显示", DataType.Number, 0d, decimals: 2);
        Add(Field(Text, "y"), "位置Y", "文本显示", DataType.Number, 0d, decimals: 2);
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
            => new(Read<bool>(Field(group, "visible")), Color(group, "ok"), Color(group, "ng"), Number(group, "opacity"), Number(group, "width"));
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
        var renderedSize = size == Math.Truncate(size) ? size : 10d;
        return new(Style(Match),
            new(Read<bool>(Field(Text, "visible")), content, Color(Text, "ok"), Color(Text, "ng"), renderedSize,
                Number(Text, "opacity"), x is ConstantSetting { Value: double xv } ? xv : 0, y is ConstantSetting { Value: double yv } ? yv : 0,
                PositionX: x, PositionY: y),
            Style(Inspection) with { ShowMeasurements = true },
            Style(Mask) with { Fill = new(255, 84, 0) },
            null,
            new(Read<bool>(Field(Outline, "visible")), new(102, 255, 0), new(255, 0, 0)),
            null,
            Style(ReferencePoint) with { FixedJudgment = true, PointLabel = "Fixture BasePoint" },
            Style(RunPoint) with { FixedJudgment = true, PointLabel = "Fixture RunPoint" },
            Style(Location) with { FixedJudgment = true, PointLabel = "MatchLocation" });
    }
}
