using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests.Manifest;

/// <summary>
/// 工具基类契约：子类只给 Manifest 与 Session，其余契约全部由编译器推导，
/// 设置读写按 manifest 的属性名映射往返一致。
/// </summary>
public sealed class ToolDefinitionTests
{
    private sealed record DemoSettings : IToolSettings<DemoSettings>
    {
        public long Threshold { get; init; }
        public bool Invert { get; init; }
        public DemoSettings Freeze() => this;
    }

    private sealed class DemoOutput : IToolOutput
    {
        public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?>();
    }

    private sealed class DemoTool : ToolDefinition<DemoSettings, DemoOutput>
    {
        protected override ToolManifest Manifest { get; } = new(
            "Demo", "示例工具", "基类用例", "demo", "定位", 7,
            new[]
            {
                new ToolParameterDeclaration("threshold", "阈值", "检测", DataType.Integer, 100L,
                    minimum: 0, maximum: 255, propertyName: nameof(DemoSettings.Threshold)),
                new ToolParameterDeclaration("invert", "反色", "检测", DataType.Boolean, false,
                    propertyName: nameof(DemoSettings.Invert))
            },
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
                new ToolResultSlots("element-id", "box", "region", "score", "judgment-passed"),
                new[] { new ToolResultColumn("element-id", "序号", 0), new ToolResultColumn("score", "分数") }),
            new ToolDisplayDeclaration(new[]
            {
                new ToolDisplayItem(ToolDisplayRole.Graphic, "匹配结果"),
                new ToolDisplayItem(ToolDisplayRole.Text, "文本")
            }),
            ToolInputTemplate.ImageRoi,
            nodeDisplayType: "FUNCTION");

        public override IToolSession<DemoSettings, DemoOutput> CreateSession()
            => throw new NotSupportedException("契约测试不执行算法。");
    }

    [Fact]
    public void Catalog_and_schemas_are_derived_from_the_manifest()
    {
        var tool = new DemoTool();
        Assert.Equal("Demo", tool.ToolId);
        Assert.Equal("示例工具", tool.Catalog.DisplayName);
        Assert.Equal("FUNCTION", tool.Catalog.NodeDisplayType);
        Assert.Equal(7, tool.Catalog.Order);
        Assert.Equal(new[] { "threshold", "invert" },
            tool.SettingsSchema.Inputs.Select(port => port.Id));
        Assert.Equal(new[] { "input.image", "input.roi", "threshold", "invert" },
            tool.Describe().Inputs.Select(port => port.Id));
        Assert.Equal("demo.rows", Assert.Single(tool.ResultSchema.Outputs).Id);
        Assert.Equal(13, tool.DisplaySettingsSchema.Inputs.Count);
        Assert.Equal("demo.rows", tool.ResultDisplay.Rows.PortId);
        Assert.Null(tool.RoiEdit);
    }

    [Fact]
    public void Settings_round_trip_uses_the_declared_property_names()
    {
        var tool = new DemoTool();
        var settings = new DemoSettings { Threshold = 123, Invert = true };
        var values = tool.ReadSettings(settings);
        Assert.Equal(123L, values.Get<long>("threshold"));
        Assert.True(values.Get<bool>("invert"));
        var materialized = Assert.IsType<DemoSettings>(tool.MaterializeSettings(values));
        Assert.Equal(settings, materialized);
    }

    [Fact]
    public void Missing_setting_field_is_rejected_instead_of_defaulted()
    {
        var tool = new DemoTool();
        // 缺字段在 InputValues 构造期即被拒绝，不会静默补默认值。
        Assert.Throws<ArgumentException>(() => new InputValues(tool.SettingsSchema.Inputs,
            new Dictionary<string, object?> { ["threshold"] = 10L }));
    }

    [Fact]
    public void Result_projection_uses_declared_columns_and_identity()
    {
        var tool = new DemoTool();
        var projection = tool.ResultProjection;
        Assert.Equal(ResultProjectionKind.StandardTable, projection.Kind);
        Assert.Equal(ResultCardinality.Many, projection.Cardinality);
        Assert.Equal(new[] { "element-id", "score" }, projection.Columns.Select(column => column.Key));
        Assert.Equal("element-id", projection.ElementIdentityFieldId);
        Assert.Equal("demo.rows", projection.Rows!.PortId);
    }

    [Fact]
    public void Display_appearance_is_captured_from_node_values()
    {
        var tool = new DemoTool();
        var schema = tool.DisplaySettingsSchema;
        var defaults = tool.CreateDisplaySettings();
        var fields = new FieldSettings(schema.Inputs,
            defaults.CopyValues().ToDictionary(pair => pair.Key,
                pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal));
        var appearance = tool.CaptureResultDisplay(fields);
        Assert.True(appearance.Graphic.Visible);
        Assert.True(appearance.Text.Visible);
    }
}
