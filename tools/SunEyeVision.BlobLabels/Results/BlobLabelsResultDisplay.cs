using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// 原件「结果显示」页签的 8 个显示项（Blob、检测区域、轮廓点、基准点、运行点、文本、屏蔽区多边形、扇环检测区域）。
/// 这些字段只随工具持久化，不是算法参数，也不进入执行输入。
/// </summary>
public static class BlobLabelsResultDisplay
{
    public const string Graphic = "display.blob";
    public const string Inspection = "display.inspection";
    public const string Outline = "display.outline";
    public const string ReferencePoint = "display.reference-point";
    public const string RunPoint = "display.run-point";
    public const string Text = "display.text";
    public const string Mask = "display.mask";
    public const string Annulus = "display.annulus";
    public static string Field(string group, string role) => group + "." + role;

    private sealed record Definition(Port Port, FieldPresentation Presentation, object Value);
    private static readonly IReadOnlyList<Definition> Definitions = Build();

    public static ToolSchema Schema { get; } = new(Definitions.Select(item => item.Port), Array.Empty<Port>(),
        new ToolPresentation(Definitions.Select(item => item.Presentation)));

    /// <summary>
    /// 结果页显示需求：行集合、行身份、矩形、区域、文本值、判定、轮廓点、质心。
    /// 不写进这里的结果资源，结果页不会叠加（Blob 工具踩过的坑）。
    /// </summary>
    public static ResultDisplayDescriptor Descriptor { get; } = new(
        new(BlobLabelsSchema.Rows),
        new(BlobLabelsSchema.Rows, new[] { "element-id" }),
        new(BlobLabelsSchema.Rows, new[] { "oriented-bounds" }),
        new(BlobLabelsSchema.Rows, new[] { "region" }),
        new(BlobLabelsSchema.Rows, new[] { "area" }),
        new(BlobLabelsSchema.Rows, new[] { "judgment-passed" }),
        // 轮廓点由系统类型（IToolResultOutlineOutput）承载，显示合同不再声明通道。
        selectedRectangleColor: new(255, 255, 0),
        centroid: new(BlobLabelsSchema.Rows, new[] { "centroid" }));

    public static InputValues Defaults()
        => new(Schema.Inputs, Definitions.ToDictionary(item => item.Port.Id, item => (object?)item.Value, StringComparer.Ordinal));

    private static IReadOnlyList<Definition> Build()
    {
        var result = new List<Definition>();
        var order = 0;
        void Add(string id, string label, string section, TypeRef type, object value,
            double? minimum = null, double? maximum = null, double? increment = null, int? decimals = 2,
            string? dialog = null, string? dialogTitle = null, bool primary = false)
        {
            var modes = id == Field(Text, "content") ? FieldEditModes.Constant | FieldEditModes.DisplayExpression
                : id == Field(Text, "x") || id == Field(Text, "y") ? FieldEditModes.Constant | FieldEditModes.Reference
                : FieldEditModes.Constant;
            var port = new Port(id, type, EditModes: modes);
            result.Add(new(port, new(id, label, section, order++,
                type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar, null, null, DeclaredFieldTemplateKey.FromPort(type, type.Shape == ValueShape.Record ? FieldEditorKind.Record : FieldEditorKind.Scalar))
            {
                Layout = new FieldLayout("结果显示", section, Minimum: minimum, Maximum: maximum,
                    Increment: increment ?? 1, DecimalPlaces: type == DataType.Number ? decimals : 0,
                    DialogGroup: dialog, DialogTitle: dialogTitle, DialogPrimary: primary)
            }, value));
        }
        var ok = PresentationDeclarations.Color(102, 255, 0);
        var ng = PresentationDeclarations.Color(255, 0, 0);
        void GraphicFields(string group, string label, RecordValue color, double opacity = 1, double width = 2)
        {
            Add(Field(group, "visible"), label, "图像显示", DataType.Boolean, true, dialog: group, dialogTitle: label, primary: true);
            Add(Field(group, "ok"), "OK颜色", "图像显示", PresentationDeclarations.Rgba, color, dialog: group, dialogTitle: label);
            Add(Field(group, "ng"), "NG颜色", "图像显示", PresentationDeclarations.Rgba, ng, dialog: group, dialogTitle: label);
            Add(Field(group, "opacity"), "透明度", "图像显示", DataType.Number, opacity, 0, 1, .01, dialog: group, dialogTitle: label);
            Add(Field(group, "width"), "线宽", "图像显示", DataType.Number, width, 1, 10, 1, dialog: group, dialogTitle: label);
        }
        GraphicFields(Graphic, "Blob", ok);
        GraphicFields(Inspection, "检测区域", PresentationDeclarations.Color(0, 0, 255));
        Add(Field(Outline, "visible"), "轮廓点", "图像显示", DataType.Boolean, true);
        GraphicFields(ReferencePoint, "基准点", ok, width: 1);
        GraphicFields(RunPoint, "运行点", ng, width: 1);
        GraphicFields(Mask, "屏蔽区多边形", PresentationDeclarations.Color(0, 0, 255), .5);
        GraphicFields(Annulus, "扇环检测区域", PresentationDeclarations.Color(0, 0, 255));
        Add(Field(Text, "visible"), "文本", "图像显示", DataType.Boolean, true);
        Add(Field(Text, "content"), "内容", "文本显示", DataType.Text, "Blob面积:{面积}");
        Add(Field(Text, "ok"), "OK颜色", "文本显示", PresentationDeclarations.Rgba, ok);
        Add(Field(Text, "ng"), "NG颜色", "文本显示", PresentationDeclarations.Rgba, ng);
        Add(Field(Text, "size"), "字号", "文本显示", DataType.Number, 10d, 6, 72, 1);
        Add(Field(Text, "opacity"), "透明度", "文本显示", DataType.Number, 1d, 0, 1, .1);
        Add(Field(Text, "x"), "位置X", "文本显示", DataType.Number, 0d, decimals: null);
        Add(Field(Text, "y"), "位置Y", "文本显示", DataType.Number, 0d, decimals: null);
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
        return new(Style(Graphic),
            new(Read<bool>(Field(Text, "visible")), content, Color(Text, "ok"), Color(Text, "ng"), renderedSize,
                Number(Text, "opacity"), x is ConstantSetting { Value: double xv } ? xv : 0, y is ConstantSetting { Value: double yv } ? yv : 0,
                PositionX: x, PositionY: y),
            Style(Inspection) with { ShowMeasurements = true },
            Style(Mask) with { Fill = new(255, 84, 0) },
            Style(Annulus) with { ShowMeasurements = true },
            new(Read<bool>(Field(Outline, "visible")), new(102, 255, 0), new(255, 0, 0)),
            null,
            Style(ReferencePoint) with { FixedJudgment = true, PointLabel = "Fixture BasePoint" },
            Style(RunPoint) with { FixedJudgment = true, PointLabel = "Fixture RunPoint" },
            null);
    }

    /// <summary>解析「文本显示-内容」的占位表达式；未知占位直接报错，不做兜底。</summary>
    public static DisplayExpressionSetting ParseContent(string content, IEnumerable<DisplayExpressionSlot>? available = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var slots = new List<DisplayExpressionSlot>();
        var known = (available ?? Array.Empty<DisplayExpressionSlot>()).ToArray();
        var position = 0;
        while (position < content.Length)
        {
            var start = content.IndexOfAny(new[] { '{', '}' }, position);
            if (start < 0) break;
            if (content[start] != '{') throw new ArgumentException("文本表达式括号不完整。");
            var end = content.IndexOf('}', start + 1);
            if (end < 0 || content.AsSpan(start + 1, end - start - 1).Contains('{')) throw new ArgumentException("文本表达式括号不完整。");
            var token = content.Substring(start + 1, end - start - 1);
            var matches = known.Where(slot => slot.Token == token).ToArray();
            if (matches.Length > 1 && !matches.Skip(1).All(slot => new DisplayExpressionSetting("", new[] { slot }).SameExpression(new("", new[] { matches[0] }))))
                throw new ArgumentException("文本来源名称不唯一，请重新选择。");
            if (matches.Length != 0) slots.Add(matches[0]);
            else if (System.Text.RegularExpressions.Regex.Match(token, @"^(0|面积|Area)(?::([FfNnGgRr][0-9]?))?$") is { Success: true } area)
                slots.Add(new(token, new ResultFieldPath(BlobLabelsSchema.Rows, new[] { "area" }), area.Groups[2].Success ? area.Groups[2].Value : null));
            else throw new ArgumentException("文本来源尚未选择或已失效：" + token);
            position = end + 1;
        }
        return new(content, slots);
    }

    public static void ValidateContent(string content) => _ = ParseContent(content);
}
