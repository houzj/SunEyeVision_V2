using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests.Manifest;

/// <summary>
/// ToolManifest 声明契约的准入校验：身份唯一、槽位可解析、扩展点必须登记。
/// 这些约束是后续 ManifestCompiler 生成 Schema 的前提，失败必须显式抛错。
/// </summary>
public sealed class ToolManifestContractTests
{
    private static ToolResultDeclaration Result() => new(
        "demo.rows",
        new[]
        {
            new ToolResultField("element-id", "序号", DataType.Integer),
            new ToolResultField("box", "匹配框", TypeRef.RecordOf("demo.box", new[]
            {
                new Port("center", FixtureDeclarations.Point),
                new Port("width", DataType.Number),
                new Port("height", DataType.Number),
                new Port("angle-degrees", DataType.Number)
            })),
            new ToolResultField("region", "区域", TypeRef.ResourceOf(ResourceKind.Region)),
            new ToolResultField("score", "分数", DataType.Number),
            new ToolResultField("judgment-passed", "判定", DataType.Boolean)
        },
        new ToolResultSlots("element-id", "box", "region", "score", "judgment-passed"),
        new[] { new ToolResultColumn("element-id", "序号", 0), new ToolResultColumn("score", "分数") });

    private static ToolDisplayDeclaration Display() => new(new[]
    {
        new ToolDisplayItem(ToolDisplayRole.Graphic, "匹配结果"),
        new ToolDisplayItem(ToolDisplayRole.Text, "文本")
    });

    private static ToolManifest Manifest(params ToolParameterDeclaration[] parameters) => new(
        "Demo", "示例", "声明契约用例", "demo", "定位", 10,
        parameters,
        Result(),
        Display());

    [Fact]
    public void Valid_manifest_exposes_declarations_and_defaults()
    {
        var manifest = Manifest(new ToolParameterDeclaration("threshold", "阈值", "检测", DataType.Number, 100d,
            minimum: 0, maximum: 255, step: 1, bindable: ParameterBindingKinds.Constant | ParameterBindingKinds.NodeOutput));
        var parameter = Assert.Single(manifest.Parameters);
        Assert.Equal("threshold", parameter.PropertyName);
        Assert.Equal(FieldDefaultPresence.UseDeclaredDefault, parameter.DefaultPresence);
        Assert.True(parameter.Bindable.HasFlag(ParameterBindingKinds.NodeOutput));
        Assert.Equal("demo.rows", manifest.Result.RowsPortId);
        Assert.Equal(2, manifest.Display.Items.Count);
    }

    [Fact]
    public void Duplicate_parameter_identity_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Manifest(
            new ToolParameterDeclaration("value", "一", "组", DataType.Number, 1d),
            new ToolParameterDeclaration("value", "二", "组", DataType.Number, 2d)));
    }

    [Fact]
    public void Result_slot_referencing_undeclared_field_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ToolResultDeclaration(
            "demo.rows",
            new[] { new ToolResultField("element-id", "序号", DataType.Integer) },
            new ToolResultSlots("element-id", "box", "region", "score", "judgment-passed")));
    }

    [Fact]
    public void Duplicate_display_role_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ToolDisplayDeclaration(new[]
        {
            new ToolDisplayItem(ToolDisplayRole.Graphic, "匹配结果"),
            new ToolDisplayItem(ToolDisplayRole.Graphic, "重复")
        }));
    }

    [Fact]
    public void Panel_override_requires_a_registered_reason()
    {
        Assert.Throws<ArgumentException>(() => new ToolPanelOverride("ThresholdAreaList", " "));
        var over = new ToolPanelOverride("ThresholdAreaList", "原厂复合控件，行级替换");
        Assert.Equal("ThresholdAreaList", over.DisplayTypeKey);
    }

    [Fact]
    public void Parameter_range_and_step_are_validated()
    {
        Assert.Throws<ArgumentException>(() => new ToolParameterDeclaration("value", "值", "组", DataType.Number, 5d,
            minimum: 10, maximum: 1));
        Assert.Throws<ArgumentException>(() => new ToolParameterDeclaration("value", "值", "组", DataType.Number, 5d,
            step: 0));
    }
}
