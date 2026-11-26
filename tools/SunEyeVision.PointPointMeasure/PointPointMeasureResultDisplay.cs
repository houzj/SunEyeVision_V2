using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.PointPointMeasure.Runtime;

/// <summary>
/// 原厂「结果显示」页的 ImageViewConfig（ui-preview.json:80-94）：LineResult、OriginPointInput、
/// EndPointInput、ResutlText。本批实现三条图形分组与文本，字段只随工具持久化。
/// </summary>
public static class PointPointMeasureResultDisplay
{
    public const string LineResult = "display.line-result";
    public const string OriginPoint = "display.origin-point";
    public const string EndPoint = "display.end-point";
    public const string Text = "display.text";

    public static string Field(string group, string role) => group + "." + role;

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    public static IReadOnlyList<string> DisplayGroups { get; } = new[] { LineResult, OriginPoint, EndPoint };
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    public static ResultDisplayDescriptor Descriptor { get; } = new(
        new(PointPointMeasureOutput.Rows),
        new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.RowIndex }),
        new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.Line }),
        new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.Region }),
        new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.Angle }),
        new(PointPointMeasureOutput.Rows, new[] { PointPointMeasureOutput.JudgmentPassed }),
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
        return new(Style(LineResult),
            new(Read<bool>(Field(Text, "visible")), content, Color(Text, "ok"), Color(Text, "ng"),
                Read<double>(Field(Text, "size")), Read<double>(Field(Text, "opacity")),
                Read<double>(Field(Text, "x")), Read<double>(Field(Text, "y"))),
            Outline: new(Style(LineResult).Visible, Style(LineResult).Ok, Style(LineResult).Ng),
            ReferencePoint: Style(OriginPoint), RunPoint: Style(EndPoint));
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

        foreach (var group in DisplayGroups)
        {
            var title = group == LineResult ? "测量线" : group == OriginPoint ? "起点" : "终点";
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
        Add(Field(Text, "content"), "内容", "文本显示", DataType.Text, "点点测量",
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
}
