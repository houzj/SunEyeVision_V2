using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.CircleFind.Runtime;

/// <summary>
/// 圆查找「结果显示」页显示项，来自 <c>resources.json:173-216</c> 的 ImageViewConfig
/// （CircleFiled/CenterPoint/OutlinePoint/CheckZone/CaliperBox/DetectCaliperBox/
/// FixtureInitPoint/FixtureRunPoint/ResutlText/BlindZone/CheckZoneAnnular）。
/// 这些字段只随工具持久化，不是算法参数，也不进入执行输入。
/// </summary>
public static class CircleFindResultDisplay
{
    public const string Circle = "display.circle";
    public const string CentrePoint = "display.centre-point";
    public const string Outline = "display.outline";
    public const string CheckZone = "display.check-zone";
    public const string BlindZone = "display.blind-zone";
    public const string Text = "display.text";
    public static string Field(string group, string role) => group + "." + role;

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    public static ResultDisplayDescriptor Descriptor { get; } = new(
        new(CircleFindSchema.Rows),
        new(CircleFindSchema.Rows, new[] { "element-id" }),
        new(CircleFindSchema.Rows, new[] { "rectangle" }),
        new(CircleFindSchema.Rows, new[] { "region" }),
        new(CircleFindSchema.Rows, new[] { "fit-error" }),
        new(CircleFindSchema.Rows, new[] { "judgment-passed" }),
        selectedRectangleColor: new(255, 255, 0),
        centroid: new(CircleFindSchema.Rows, new[] { "centre-point" }));

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
        GraphicFields(Circle, "输出圆", ok);
        GraphicFields(CentrePoint, "圆心点", ng, width: 1);
        GraphicFields(Outline, "轮廓点", PresentationDeclarations.Color(255, 255, 0));
        GraphicFields(CheckZone, "检测区域", PresentationDeclarations.Color(0, 0, 255));
        GraphicFields(BlindZone, "盲区", PresentationDeclarations.Color(255, 84, 0), .5);
        Add(Field(Text, "visible"), "文本", "图像显示", DataType.Boolean, true);
        Add(Field(Text, "content"), "内容", "文本显示", DataType.Text, "圆心:({圆心X},{圆心Y}) 半径:{半径}");
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
        return new(Style(Circle),
            new(Read<bool>(Field(Text, "visible")), content, Color(Text, "ok"), Color(Text, "ng"),
                size == Math.Truncate(size) ? size : 10d, Number(Text, "opacity"),
                x is ConstantSetting { Value: double xv } ? xv : 0, y is ConstantSetting { Value: double yv } ? yv : 0,
                PositionX: x, PositionY: y),
            Style(CheckZone) with { ShowMeasurements = true },
            Style(BlindZone) with { Fill = new(255, 84, 0) },
            null,
            new(Read<bool>(Field(Outline, "visible")), new(255, 255, 0), new(255, 0, 0)),
            null,
            Style(CentrePoint) with { FixedJudgment = true, PointLabel = "CentrePoint" });
    }
}
