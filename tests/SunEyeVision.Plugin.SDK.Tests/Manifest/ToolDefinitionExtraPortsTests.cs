using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests.Manifest;

/// <summary>
/// 基类的附加端口能力：组合体输入、行端口之外的输出、手工引用来源。
/// 这三项让基类能承载 PixelCount 这类“多端口输出 + 组合输入”的工具。
/// </summary>
public sealed class ToolDefinitionExtraPortsTests
{
    public sealed record Settings : IToolSettings<Settings>
    {
        public long Value { get; init; }
        public Settings Freeze() => this;
    }

    private sealed class Output : IToolOutput
    {
        public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?>();
    }

    private sealed class DemoTool : ToolDefinition<Settings, Output>
    {
        private readonly ToolManifest _manifest;
        public DemoTool(ToolManifest manifest) => _manifest = manifest;
        protected override ToolManifest Manifest => _manifest;
        public override IToolSession<Settings, Output> CreateSession() => throw new NotSupportedException();
    }

    private static ToolManifest Manifest(IReadOnlyList<string>? referenceSources = null) => new(
        "Demo", "示例", "附加端口用例", "demo", "检测", 10,
        new[]
        {
            new ToolParameterDeclaration("value", "值", "检测", DataType.Integer, 0L,
                propertyName: nameof(Settings.Value))
        },
        new ToolResultDeclaration(null,
            new[] { new ToolResultField("value", "值", DataType.Integer) },
            slots: null,
            shape: ToolResultShape.PropertyList),
        new ToolDisplayDeclaration(new[] { new ToolDisplayItem(ToolDisplayRole.Text, "文本") }),
        extraInputs: new[]
        {
            new Port("input.roi-document", TypeRef.ResourceOf(ResourceKind.Region),
                Required: false, Nullable: true)
        },
        extraOutputs: new[]
        {
            new Port("output.mask", TypeRef.ResourceOf(ResourceKind.Image), Nullable: true)
        },
        manualReferenceSources: referenceSources);

    [Fact]
    public void Extra_inputs_and_outputs_are_composed_into_the_schemas()
    {
        var tool = new DemoTool(Manifest(new[] { "output.mask" }));
        // 组合体输入进执行 Schema，但不出现在设置 Schema（设置只针对参数）。
        Assert.Contains("input.roi-document", tool.Describe().Inputs.Select(port => port.Id));
        Assert.DoesNotContain("input.roi-document", tool.SettingsSchema.Inputs.Select(port => port.Id));
        // 附加输出与属性列字段并列。
        Assert.Contains("output.mask", tool.ResultSchema.Outputs.Select(port => port.Id));
        Assert.Single(tool.ManualReferenceSources);
    }

    [Fact]
    public void Manual_reference_source_outside_declared_outputs_is_rejected()
        => Assert.Throws<ArgumentException>(() => Manifest(new[] { "output.missing" }));

    [Fact]
    public void Extra_output_cannot_reuse_the_rows_port_identity()
    {
        Assert.Throws<ArgumentException>(() => new ToolManifest("Demo", "示例", "冲突用例", "demo", "检测", 10,
            new[] { new ToolParameterDeclaration("value", "值", "检测", DataType.Integer, 0L) },
            new ToolResultDeclaration("demo.rows",
                new[] { new ToolResultField("element-id", "序号", DataType.Integer) },
                new ToolResultSlots("element-id", "element-id", "element-id", "element-id", "element-id")),
            new ToolDisplayDeclaration(new[] { new ToolDisplayItem(ToolDisplayRole.Text, "文本") }),
            extraOutputs: new[] { new Port("demo.rows", DataType.Text) }));
    }
}
