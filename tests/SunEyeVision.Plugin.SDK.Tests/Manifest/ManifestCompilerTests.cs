using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests.Manifest;

/// <summary>
/// ManifestCompiler 的等价性基础：端口展开、Schema、Descriptor 全部由声明推导，
/// 且同一 manifest 两次编译结果一致（迁移期快照比对的前提）。
/// </summary>
public sealed class ManifestCompilerTests
{
    private static ToolManifest Manifest(ToolInputTemplate inputs = ToolInputTemplate.ImageRoiFixture) => new(
        "Demo", "示例", "编译器用例", "demo", "定位", 10,
        new[]
        {
            new ToolParameterDeclaration("threshold", "阈值", "检测", DataType.Number, 100d,
                minimum: 0, maximum: 255, step: 1),
            new ToolParameterDeclaration("invert", "反色", "检测", DataType.Boolean, false),
            new ToolParameterDeclaration("model", "模板", "模板", TypeRef.RecordOf("demo.model", new[]
            {
                new Port("score", DataType.Number)
            }), new RecordValue(TypeRef.RecordOf("demo.model", new[] { new Port("score", DataType.Number) }),
                new Dictionary<string, object?> { ["score"] = 0.5d }))
        },
        new ToolResultDeclaration(
            "demo.rows",
            new[]
            {
                new ToolResultField("element-id", "序号", DataType.Integer),
                new ToolResultField("box", "匹配框", TypeRef.RecordOf("demo.box", new[]
                {
                    new Port("center", FixtureDeclarations.Point),
                    new Port("width", DataType.Number)
                })),
                new ToolResultField("region", "区域", TypeRef.ResourceOf(ResourceKind.Region)),
                new ToolResultField("score", "分数", DataType.Number),
                new ToolResultField("judgment-passed", "判定", DataType.Boolean)
            },
            new ToolResultSlots("element-id", "box", "region", "score", "judgment-passed",
                centroid: "element-id"),
            selectedRectangleColor: new ResultDisplayColor(0, 0, 255)),
        new ToolDisplayDeclaration(new[]
        {
            new ToolDisplayItem(ToolDisplayRole.Inspection, "检测区域"),
            new ToolDisplayItem(ToolDisplayRole.Graphic, "匹配结果"),
            new ToolDisplayItem(ToolDisplayRole.Text, "文本")
        }),
        inputs);

    [Theory]
    [InlineData(ToolInputTemplate.Image, 1)]
    [InlineData(ToolInputTemplate.ImageRoi, 2)]
    [InlineData(ToolInputTemplate.ImageRoiFixture, 3)]
    [InlineData(ToolInputTemplate.ImageRoiMask, 3)]
    [InlineData(ToolInputTemplate.None, 0)]
    public void Input_template_expands_the_standard_port_set(ToolInputTemplate template, int expected)
    {
        var ports = ManifestCompiler.InputPorts(template);
        Assert.Equal(expected, ports.Count);
        if (expected > 0)
        {
            Assert.Equal(ManifestCompiler.ImageInput, ports[0].Id);
            Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), ports[0].Type);
        }
    }

    [Fact]
    public void Settings_schema_follows_the_parameter_declaration()
    {
        var schema = ManifestCompiler.SettingsSchema(Manifest(ToolInputTemplate.ImageRoi));
        // 设置 Schema 只含参数：混入输入端口会让设置读写要求 input.image。
        Assert.Equal(new[] { "threshold", "invert", "model" }, schema.Inputs.Select(port => port.Id));
        Assert.Empty(schema.Outputs);
        // 执行 Schema = 输入端口 + 设置参数。
        Assert.Equal(new[] { ManifestCompiler.ImageInput, ManifestCompiler.RoiInput, "threshold", "invert", "model" },
            ManifestCompiler.ExecutionSchema(Manifest(ToolInputTemplate.ImageRoi)).Inputs.Select(port => port.Id));
        var threshold = schema.Inputs.Single(port => port.Id == "threshold");
        Assert.Equal(DataType.Number, threshold.Type);
        Assert.True(threshold.EditModes.HasFlag(FieldEditModes.Constant));
        var presentation = schema.Presentation!.Fields.Single(field => field.FieldId == "threshold");
        Assert.Equal(0d, presentation.Layout!.Minimum);
        Assert.Equal(255d, presentation.Layout.Maximum);
        Assert.Equal(FieldEditorKind.Record, schema.Presentation.Fields.Single(field => field.FieldId == "model").Editor);
        Assert.Equal(FieldEditorKind.Scalar, presentation.Editor);
        Assert.Equal(new[] { 0, 1, 2 }, schema.Presentation.Fields.Select(field => field.Order));
    }

    [Fact]
    public void Result_schema_declares_a_single_rows_port()
    {
        var schema = ManifestCompiler.ResultSchema(Manifest());
        var rows = Assert.Single(schema.Outputs);
        Assert.Equal("demo.rows", rows.Id);
        Assert.Equal(ValueShape.Sequence, rows.Type.Shape);
        Assert.Equal(new[] { "element-id", "box", "region", "score", "judgment-passed" },
            rows.Type.Element!.Fields.Select(field => field.Id));
    }

    [Fact]
    public void Descriptor_points_only_at_declared_slots()
    {
        var descriptor = ManifestCompiler.Descriptor(Manifest());
        Assert.Equal("demo.rows", descriptor.Rows.PortId);
        Assert.Equal(new[] { "element-id" }, descriptor.Identity.MemberFieldPath);
        Assert.Equal(new[] { "box" }, descriptor.Rectangle.MemberFieldPath);
        Assert.Equal(new[] { "region" }, descriptor.GeometrySource.MemberFieldPath);
        Assert.Equal(new[] { "score" }, descriptor.TextValue.MemberFieldPath);
        Assert.Equal(new[] { "judgment-passed" }, descriptor.Passed.MemberFieldPath);
        Assert.Equal(new[] { "element-id" }, descriptor.Centroid!.MemberFieldPath);
        Assert.Null(descriptor.InnerRectangle);
        Assert.Equal(new ResultDisplayColor(0, 0, 255), descriptor.SelectedRectangleColor);
        // 轮廓点走系统类型（IToolResultOutlineDeclaration），Descriptor 不再携带通道声明。
    }

    [Fact]
    public void Compilation_is_deterministic()
    {
        var manifest = Manifest();
        var first = ManifestCompiler.SettingsSchema(manifest);
        var second = ManifestCompiler.SettingsSchema(manifest);
        Assert.Equal(first.Inputs.Select(port => port.Id), second.Inputs.Select(port => port.Id));
        Assert.Equal(first.Presentation!.Fields.Select(field => (field.FieldId, field.Order)),
            second.Presentation!.Fields.Select(field => (field.FieldId, field.Order)));
    }

    [Fact]
    public void Roi_declaration_is_passed_through_not_synthesized()
    {
        Assert.Null(ManifestCompiler.RoiEdit(Manifest()));
    }

    [Fact]
    public void Display_schema_generates_grouped_fields()
    {
        var schema = ManifestCompiler.DisplaySchema(Manifest());
        var ids = schema.Inputs.Select(port => port.Id).ToArray();
        // 图形项 5 项 + 检测区域 5 项 + 文本 8 项。
        Assert.Equal(18, ids.Length);
        Assert.Equal(new[]
        {
            "display.inspection.visible", "display.inspection.ok", "display.inspection.ng",
            "display.inspection.opacity", "display.inspection.width",
            "display.graphic.visible", "display.graphic.ok", "display.graphic.ng",
            "display.graphic.opacity", "display.graphic.width",
            "display.text.visible", "display.text.content", "display.text.ok", "display.text.ng",
            "display.text.size", "display.text.opacity", "display.text.x", "display.text.y"
        }, ids);
        Assert.Equal(ids.Length, schema.Presentation!.Fields.Count);
        Assert.Equal(Enumerable.Range(0, ids.Length), schema.Presentation.Fields.Select(field => field.Order));
        var size = schema.Presentation.Fields.Single(field => field.FieldId == "display.text.size");
        Assert.Equal(6d, size.Layout!.Minimum);
        Assert.Equal(72d, size.Layout.Maximum);
    }

    [Fact]
    public void Display_defaults_round_trip_into_appearance()
    {
        var manifest = Manifest();
        var schema = ManifestCompiler.DisplaySchema(manifest);
        var defaults = ManifestCompiler.DisplayDefaults(manifest);
        var fields = new FieldSettings(schema.Inputs,
            defaults.CopyValues().ToDictionary(pair => pair.Key,
                pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal));
        var appearance = ManifestCompiler.Appearance(manifest, fields);
        Assert.True(appearance.Graphic.Visible);
        Assert.Equal(new ResultDisplayColor(102, 255, 0), appearance.Graphic.Ok);
        Assert.Equal(new ResultDisplayColor(255, 0, 0), appearance.Graphic.Ng);
        Assert.Equal(2d, appearance.Graphic.Width);
        Assert.NotNull(appearance.Inspection);
        Assert.True(appearance.Text.Visible);
        Assert.Equal(10d, appearance.Text.FontSize);
        Assert.Equal(0d, appearance.Text.X);
        Assert.Null(appearance.Outline);
    }

    [Fact]
    public void Display_without_a_primary_graphic_uses_the_fixed_row_style()
    {
        var manifest = new ToolManifest("Demo", "示例", "编译器用例", "demo", "定位", 10,
            new[] { new ToolParameterDeclaration("threshold", "阈值", "检测", DataType.Number, 100d) },
            new ToolResultDeclaration(
                "demo.rows",
                new[]
                {
                    new ToolResultField("element-id", "序号", DataType.Integer),
                    new ToolResultField("box", "匹配框", TypeRef.RecordOf("demo.box", new[] { new Port("width", DataType.Number) })),
                    new ToolResultField("region", "区域", TypeRef.ResourceOf(ResourceKind.Region)),
                    new ToolResultField("score", "分数", DataType.Number),
                    new ToolResultField("judgment-passed", "判定", DataType.Boolean)
                },
                new ToolResultSlots("element-id", "box", "region", "score", "judgment-passed")),
            new ToolDisplayDeclaration(new[] { new ToolDisplayItem(ToolDisplayRole.Text, "文本") }));
        var schema = ManifestCompiler.DisplaySchema(manifest);
        var defaults = ManifestCompiler.DisplayDefaults(manifest);
        var fields = new FieldSettings(schema.Inputs,
            defaults.CopyValues().ToDictionary(pair => pair.Key,
                pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal));
        // 没有独立主图形项时用固定行样式（原件 PixelCount 的区间行：绿/红、线宽 2）。
        var appearance = ManifestCompiler.Appearance(manifest, fields);
        Assert.True(appearance.Graphic.Visible);
        Assert.Equal(2d, appearance.Graphic.Width);
        Assert.Equal(new ResultDisplayColor(102, 255, 0), appearance.Graphic.Ok);
        Assert.Null(appearance.Inspection);
    }
}
