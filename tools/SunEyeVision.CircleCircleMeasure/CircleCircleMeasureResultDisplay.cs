using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.CircleCircleMeasure.Runtime;

/// <summary>
/// 原厂 ResultShow 页的图像显示项（ui-preview.json 的 ImageViewConfig）：LineResult、
/// IntersectionPoint1/2、CircleInput1/2、CircleCenterPoint1/2、DetectCaliperBox1/2、
/// FixtureInitPoint/FixtureRunPoint、ResutlText。本批实现图形分组与文本；
/// FixtureInitPoint/FixtureRunPoint 属位置修正能力，未取证不实现，列在交接单未实现项。
/// 这些字段只随工具持久化，不是算法参数。
/// </summary>
public static class CircleCircleMeasureResultDisplay
{
    public const string LineResult = "display.line-result";
    public const string Intersection1 = "display.intersection-1";
    public const string Intersection2 = "display.intersection-2";
    public const string Circle1 = "display.circle-1";
    public const string Circle2 = "display.circle-2";
    public const string Caliper1 = "display.caliper-1";
    public const string Caliper2 = "display.caliper-2";
    public const string Center1 = "display.center-1";
    public const string Center2 = "display.center-2";
    public const string Text = "display.text";

    public static string Field(string group, string role) => group + "." + role;

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    private static readonly string[] Groups =
    {
        LineResult, Intersection1, Intersection2, Circle1, Circle2, Center1, Center2, Caliper1, Caliper2
    };
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    public static ResultDisplayDescriptor Descriptor { get; } = new(
        new(CircleCircleMeasureOutput.Rows),
        new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.RowIndex }),
        new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.Caliper1 }),
        new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.Region }),
        new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.Angle }),
        new(CircleCircleMeasureOutput.Rows, new[] { CircleCircleMeasureOutput.JudgmentPassed }),
        selectedRectangleColor: new(0, 0, 255));

    public static InputValues Defaults()
        => new(Schema.Inputs, Definitions.ToDictionary(item => item.Port.Id, item => (object?)item.Value, StringComparer.Ordinal));

    public static ResultDisplayAppearance Capture(FieldSettings fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        T Read<T>(string id) => fields.Get<T>(id);
        ResultDisplayColor Color(string group, string role)
        {
            var value = Read<RecordValue>(Field(group, role));
            return new(checked((byte)value.Get<long>("r")), checked((byte)value.Get<long>("g")),
                checked((byte)value.Get<long>("b")), checked((byte)value.Get<long>("a")));
        }
        ResultGraphicAppearance Style(string group) => new(Read<bool>(Field(group, "visible")),
            Color(group, "ok"), Color(group, "ng"), Read<double>(Field(group, "opacity")),
            Read<double>(Field(group, "width")));
        var content = fields.Read(Field(Text, "content")) switch
        {
            DisplayExpressionSetting expression => expression.Text,
            ConstantSetting { Value: string raw } => raw,
            _ => throw new ArgumentException("文本内容设置无效。")
        };
        var size = Read<double>(Field(Text, "size"));
        return new(Style(Circle1),
            new(Read<bool>(Field(Text, "visible")), content, Color(Text, "ok"), Color(Text, "ng"),
                size, Read<double>(Field(Text, "opacity")), Read<double>(Field(Text, "x")), Read<double>(Field(Text, "y"))),
            Inspection: Style(Caliper1), Outline: new(Style(LineResult).Visible, Style(LineResult).Ok, Style(LineResult).Ng),
            ReferencePoint: Style(Center1), RunPoint: Style(Center2), Centroids: Style(Intersection1));
    }

    private static IReadOnlyList<Definition> Build()
    {
        var result = new List<Definition>();
        var order = 0;
        var ok = PresentationDeclarations.Color(102, 255, 0);
        var ng = PresentationDeclarations.Color(255, 0, 0);
        void Add(string id, string label, string section, TypeRef type, object value, FieldEditModes modes,
            double? minimum = null, double? maximum = null, double? increment = null, int? decimals = 2,
            string? dialog = null, string? dialogTitle = null, bool primary = false, bool textCoordinate = false)
            => result.Add(new(new Port(id, type, EditModes: modes),
                new(id, label, section, order++,
                    type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.FromPort(type, type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar))
                {
                    Layout = new FieldLayout("结果显示", section, Minimum: minimum, Maximum: maximum,
                        Increment: increment ?? 1, DecimalPlaces: textCoordinate ? null : decimals,
                        AllowReference: modes.HasFlag(FieldEditModes.Reference),
                        DialogGroup: dialog, DialogTitle: dialogTitle, DialogPrimary: primary)
                }, value));

        foreach (var group in Groups)
        {
            var title = group switch
            {
                var g when g == LineResult => "测量线",
                var g when g == Intersection1 => "交点1",
                var g when g == Intersection2 => "交点2",
                var g when g == Circle1 => "圆1",
                var g when g == Circle2 => "圆2",
                var g when g == Center1 => "圆1圆心",
                var g when g == Center2 => "圆2圆心",
                var g when g == Caliper1 => "卡尺框1",
                _ => "卡尺框2"
            };
            Add(Field(group, "visible"), title, "图像显示", DataType.Boolean, true, FieldEditModes.Constant,
                dialog: group, dialogTitle: title, primary: true);
            Add(Field(group, "ok"), "OK颜色", "图像显示", PresentationDeclarations.Rgba, ok, FieldEditModes.Constant,
                dialog: group, dialogTitle: title);
            Add(Field(group, "ng"), "NG颜色", "图像显示", PresentationDeclarations.Rgba, ng, FieldEditModes.Constant,
                dialog: group, dialogTitle: title);
            Add(Field(group, "opacity"), "透明度", "图像显示", DataType.Number, 1d, FieldEditModes.Constant,
                0, 1, .01, dialog: group, dialogTitle: title);
            Add(Field(group, "width"), "线宽", "图像显示", DataType.Number, 2d, FieldEditModes.Constant,
                1, 10, 1, dialog: group, dialogTitle: title);
        }
        Add(Field(Text, "visible"), "文本", "文本显示", DataType.Boolean, true, FieldEditModes.Constant);
        Add(Field(Text, "content"), "内容", "文本显示", DataType.Text, "圆圆测量",
            FieldEditModes.Constant | FieldEditModes.DisplayExpression);
        Add(Field(Text, "ok"), "OK颜色", "文本显示", PresentationDeclarations.Rgba, ok, FieldEditModes.Constant);
        Add(Field(Text, "ng"), "NG颜色", "文本显示", PresentationDeclarations.Rgba, ng, FieldEditModes.Constant);
        Add(Field(Text, "size"), "字号", "文本显示", DataType.Number, 10d, FieldEditModes.Constant, 6, 72, 1, decimals: 0);
        Add(Field(Text, "opacity"), "透明度", "文本显示", DataType.Number, 1d, FieldEditModes.Constant, 0, 1, .1);
        Add(Field(Text, "x"), "位置X", "文本显示", DataType.Number, 0d,
            FieldEditModes.Constant | FieldEditModes.Reference, decimals: null, textCoordinate: true);
        Add(Field(Text, "y"), "位置Y", "文本显示", DataType.Number, 0d,
            FieldEditModes.Constant | FieldEditModes.Reference, decimals: null, textCoordinate: true);
        return result.AsReadOnly();
    }

    public static IReadOnlyList<string> DisplayGroups => Groups;
}
