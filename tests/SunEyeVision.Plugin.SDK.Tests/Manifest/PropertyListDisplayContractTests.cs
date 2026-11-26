using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests.Manifest;

/// <summary>
/// 原厂 Output 段与 Display.xml 是两份独立声明：属性列结果（多端口输出）同样可以带
/// 行显示合同——显示引用某个行端口，与结果形态无关。
/// </summary>
public sealed class PropertyListDisplayContractTests
{
    private const string Rows = "output.rows";
    private const string Area = "output.total-area";
    private const string Histogram = "output.histogram";

    private static TypeRef RectangleType => TypeRef.RecordOf("probe.rectangle", new[]
    {
        new Port("center", TypeRef.GeometryOf(GeometryKind.Point, RoiDeclarations.ImageFrame)),
        new Port("width", DataType.Number, Storage: NumericStorage.Float32),
        new Port("height", DataType.Number, Storage: NumericStorage.Float32),
        new Port("angle-degrees", DataType.Number, Storage: NumericStorage.Float32)
    });

    private static TypeRef RowsType => TypeRef.SequenceOf(TypeRef.RecordOf("probe.rows", new[]
    {
        new Port("row-index", DataType.Integer, Storage: NumericStorage.Int32),
        new Port("ratio", DataType.Number, Storage: NumericStorage.Float32),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region)),
        new Port("rectangle", RectangleType),
        new Port("judgment", DataType.Boolean)
    }));

    private static ToolManifest Probe() => new(
        "Probe", "探针", "属性列结果 + 行显示合同", "probe", "测量", 10,
        Array.Empty<ToolParameterDeclaration>(),
        new ToolResultDeclaration(Rows,
            new[]
            {
                new ToolResultField(Area, "总面积", DataType.Number, storage: NumericStorage.Float32),
                new ToolResultField(Histogram, "灰度直方图", DataType.NumberArray)
            },
            // 显示合同指向额外的行端口，而不是属性列自身。
            slots: new ToolResultSlots("row-index", "rectangle", "region", "ratio", "judgment"),
            columns: new[]
            {
                new ToolResultColumn(Area, "总面积", 3),
                new ToolResultColumn(Histogram, "灰度直方图")
            },
            kind: ResultProjectionKind.PropertyList,
            shape: ToolResultShape.PropertyList),
        new ToolDisplayDeclaration(Array.Empty<ToolDisplayItem>()),
        ToolInputTemplate.Image,
        extraOutputs: new[] { new Port(Rows, RowsType) });

    [Fact]
    public void Property_list_result_keeps_both_its_own_ports_and_the_row_port()
    {
        var schema = ManifestCompiler.ResultSchema(Probe());
        Assert.Equal(new[] { Area, Histogram, Rows }, schema.Outputs.Select(port => port.Id));
        // 行端口是记录序列，满足显示合同对行形态的要求。
        Assert.Equal(ValueShape.Sequence, schema.Outputs.Single(port => port.Id == Rows).Type.Shape);
    }

    [Fact]
    public void Display_contract_resolves_against_the_declared_row_port()
    {
        var descriptor = ManifestCompiler.Descriptor(Probe());
        Assert.Equal(Rows, descriptor.Rows.PortId);
        Assert.Equal("row-index", descriptor.Identity.MemberFieldPath.Single());
        Assert.Equal("ratio", descriptor.TextValue.MemberFieldPath.Single());
    }

    [Fact]
    public void Display_contract_without_a_row_port_is_rejected()
    {
        var manifest = Probe();
        // 只有行端口没有槽位：声明期就应该被拒绝，而不是渲染时才失败。
        Assert.Throws<ArgumentException>(() => new ToolResultDeclaration(Rows,
            new[] { new ToolResultField(Area, "总面积", DataType.Number) },
            slots: null,
            shape: ToolResultShape.PropertyList));
        // 没有行端口时，工具必须自己声明显示字段（不再隐式推断）。
        Assert.NotNull(manifest.Result.RowsPortId);
    }
}
