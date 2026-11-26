using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests.Manifest;

/// <summary>
/// 六项原厂写法在 manifest 里的对应：字段改名、条件显隐、成对编辑、布尔提示、分组、ROI 形状选择。
/// 每项都要能被编译器消费（不是只声明不用）。
/// </summary>
public sealed class ToolDeclarationExtensionsTests
{
    private static ToolResultDeclaration Result() => new(
        "demo.rows",
        new[]
        {
            new ToolResultField("element-id", "序号", DataType.Integer),
            new ToolResultField("box", "匹配框", TypeRef.RecordOf("demo.box", new[] { new Port("width", DataType.Number) })),
            new ToolResultField("region", "区域", TypeRef.ResourceOf(ResourceKind.Region)),
            new ToolResultField("score", "分数", DataType.Number),
            new ToolResultField("judgment-passed", "判定", DataType.Boolean)
        },
        new ToolResultSlots("element-id", "box", "region", "score", "judgment-passed"));

    private static ToolDisplayDeclaration Display() => new(new[]
    {
        new ToolDisplayItem(ToolDisplayRole.Graphic, "匹配结果"),
        new ToolDisplayItem(ToolDisplayRole.Text, "文本")
    });

    private static ToolManifest Build(
        IReadOnlyList<ToolParameterDeclaration> parameters,
        IReadOnlyList<ToolSectionDeclaration>? sections = null,
        IReadOnlyList<FieldVisibilityRule>? visibility = null,
        IReadOnlyList<ToolPairGroupDeclaration>? pairs = null)
        => new("Demo", "示例", "六项扩展用例", "demo", "检测", 10, parameters, Result(), Display(),
            sections: sections, visibility: visibility, pairs: pairs);

    [Fact]
    public void Field_identity_uses_storage_key_property_and_label_only()
    {
        // 原厂的 Name / DisplayName / AlgorithmIndex 三项职责，在本方分别由
        // Port.Id / Label /（无原生下发通道）承担：不再声明第三个名字，避免只声明不消费。
        var parameter = new ToolParameterDeclaration("roi.area-type", "ROI区域类型", "ROI区域",
            DataType.Integer, 0L, propertyName: "RoiAreaType");
        Assert.Equal("roi.area-type", parameter.Id);        // 存储键（原厂 Name）
        Assert.Equal("ROI区域类型", parameter.Label);        // 界面文案（原厂 DisplayName）
        Assert.Equal("RoiAreaType", parameter.PropertyName); // C# 属性名
    }

    [Fact]
    public void Visibility_rule_compiles_into_a_single_value_condition()
    {
        var manifest = Build(
            new[]
            {
                new ToolParameterDeclaration("threshold-type", "阈值类型", "检测", DataType.Integer, 2L),
                new ToolParameterDeclaration("polarity", "极性", "检测", DataType.Integer, 0L)
            },
            visibility: new[]
            {
                new FieldVisibilityRule("threshold-type", new[] { "2" }, new[] { "polarity" })
            });
        var layout = ManifestCompiler.SettingsSchema(manifest).Presentation!.Fields
            .Single(field => field.FieldId == "polarity").Layout!;
        Assert.Equal(new FieldCondition("threshold-type", 2L), layout.VisibleWhen);
    }

    [Fact]
    public void Visibility_rule_referencing_undeclared_target_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Build(
            new[] { new ToolParameterDeclaration("threshold-type", "阈值类型", "检测", DataType.Integer, 2L) },
            visibility: new[] { new FieldVisibilityRule("threshold-type", new[] { "2" }, new[] { "missing" }) }));
        // 目标不能是源自身。
        Assert.Throws<ArgumentException>(() => Build(
            new[] { new ToolParameterDeclaration("threshold-type", "阈值类型", "检测", DataType.Integer, 2L) },
            visibility: new[] { new FieldVisibilityRule("threshold-type", new[] { "2" }, new[] { "threshold-type" }) }));
    }

    [Fact]
    public void Pair_group_compiles_into_pair_with_and_rejects_bad_identity()
    {
        var manifest = Build(
            new[]
            {
                new ToolParameterDeclaration("low", "下限", "阈值范围", DataType.Integer, 100L),
                new ToolParameterDeclaration("high", "上限", "阈值范围", DataType.Integer, 200L)
            },
            pairs: new[] { new ToolPairGroupDeclaration("range", "low", "high") });
        var layout = ManifestCompiler.SettingsSchema(manifest).Presentation!.Fields
            .Single(field => field.FieldId == "low").Layout!;
        Assert.Equal("high", layout.PairWith);
        // 容器与上下限同名、上下限相同都直接拒绝。
        Assert.Throws<ArgumentException>(() => new ToolPairGroupDeclaration("low", "low", "high"));
        Assert.Throws<ArgumentException>(() => new ToolPairGroupDeclaration("range", "low", "low"));
        // 引用未声明字段由 manifest 校验拒绝。
        Assert.Throws<ArgumentException>(() => Build(
            new[] { new ToolParameterDeclaration("low", "下限", "阈值范围", DataType.Integer, 0L) },
            pairs: new[] { new ToolPairGroupDeclaration("range", "low", "missing") }));
    }

    [Fact]
    public void Boolean_tips_compile_into_state_tips()
    {
        var manifest = Build(new[]
        {
            new ToolParameterDeclaration("enabled", "启用", "检测", DataType.Boolean, false,
                tipsOn: "最大保存120张图片", tipsOff: "不保存")
        });
        var layout = ManifestCompiler.SettingsSchema(manifest).Presentation!.Fields
            .Single(field => field.FieldId == "enabled").Layout!;
        Assert.Equal(new BooleanStateTips("最大保存120张图片", "不保存"), layout.BooleanTips);
    }

    [Fact]
    public void Section_declaration_supplies_the_page_name()
    {
        var manifest = Build(
            new[] { new ToolParameterDeclaration("area", "区域", "ROI区域", DataType.Integer, 0L) },
            sections: new[] { new ToolSectionDeclaration("基本参数", "ROI区域", vmCategory: "Tab_ROI Area") });
        var layout = ManifestCompiler.SettingsSchema(manifest).Presentation!.Fields
            .Single(field => field.FieldId == "area").Layout!;
        Assert.Equal("基本参数", layout.Page);
        Assert.Equal("ROI区域", layout.Section);
    }

    [Fact]
    public void Roi_select_pairs_modes_with_shapes_like_the_original()
    {
        var declaration = new RoiSelectDeclaration("Mul", new[] { "Box", "Polygon" },
            maximumCount: 16, fullScreenEnable: true, avoidSelectType: "Avoid", avoidShapes: new[] { "Polygon" });
        Assert.Equal(2, declaration.Shapes.Count);
        Assert.Equal(16, declaration.MaximumCount);
        Assert.Equal(new[] { "Polygon" }, declaration.AvoidShapes);
        // 单模式适用于全部形状（原厂 Blob：Mul + 4 形状）；多模式才逐个配对。
        var singleMode = new RoiSelectDeclaration("Mul", new[] { "Box", "SectorRing", "Polygon" });
        Assert.Equal(3, singleMode.Shapes.Count);
        Assert.Throws<ArgumentException>(() =>
            new RoiSelectDeclaration("Mul Fix", new[] { "Box" }));
        Assert.Throws<ArgumentException>(() =>
            new RoiSelectDeclaration("Mul", new[] { "Box" }, avoidSelectType: "Avoid"));
        var group = new RoiGroupDeclaration("ROI区域", "mode", "Local", new[] { "Box" },
            "rect", "ring", Array.Empty<RoiParameterRowDeclaration>(), Array.Empty<RoiParameterRowDeclaration>(),
            "area", "区域类型", Array.Empty<RoiChoiceDeclaration>(), "mask", "掩膜",
            "outMask", "输出掩膜", "avoid", "屏蔽区", "maskInherited", declaration);
        Assert.Same(declaration, group.Selection);
    }
}
