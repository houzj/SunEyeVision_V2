using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.UI.Services.Editing;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>引用文本按原件规则：{模块编号} {模块名}.{IO名}，编号 0 起，成员取叶子名，资源本体折叠为端口名。</summary>
public sealed class ReferenceTextTests
{
    private static ToolSchema ImageSourceSchema() => new(Array.Empty<Port>(),
        new[]
        {
            new Port("image", TypeRef.RecordOf("image", new[]
            {
                new Port("data", TypeRef.ResourceOf(ResourceKind.Image)),
                new Port("width", DataType.Integer, Storage: NumericStorage.Int32)
            }))
        },
        outputDisplay: new[]
        {
            new OutputDisplayNode("image", "图像", new[]
            {
                new OutputDisplayNode("data", "图像数据"),
                new OutputDisplayNode("width", "图像宽度")
            })
        });

    [Fact]
    public void Module_number_starts_at_zero_like_original()
        => Assert.Equal(0, ReferenceText.ModuleNumber(1));

    [Fact]
    public void Resource_member_folds_to_port_caption()
        => Assert.Equal("0 图像源1.图像",
            ReferenceText.Format(0, "图像源1", "图像", new[] { "data" }, new[] { "图像数据" }));

    [Fact]
    public void Deep_member_uses_leaf_caption()
        => Assert.Equal("0 图像源1.图像宽度",
            ReferenceText.Format(0, "图像源1", "图像", new[] { "width" }, new[] { "图像宽度" }));

    [Fact]
    public void Image_reference_has_no_index_marker()
        => Assert.Equal(string.Empty, ReferenceText.IndexSuffix(
            TypeRef.ResourceOf(ResourceKind.Image), BindingSelection.All, true, _ => "", _ => ""));

    [Fact]
    public void Global_variable_reference_has_no_index_marker()
        => Assert.Equal(string.Empty, ReferenceText.IndexSuffix(
            TypeRef.ScalarOf(ScalarKind.Int64), BindingSelection.All, false, _ => "", _ => ""));

    [Fact]
    public void Scalar_reference_defaults_to_all_index_marker()
        => Assert.Equal("[]", ReferenceText.IndexSuffix(
            TypeRef.ScalarOf(ScalarKind.Int64), BindingSelection.All, true, _ => "", _ => ""));

    [Fact]
    public void Constant_index_marker_is_numeric()
        => Assert.Equal("[2]", ReferenceText.IndexSuffix(
            TypeRef.ScalarOf(ScalarKind.Int64), new BindingSelection.AtConstant(2), true, _ => "", _ => ""));

    [Fact]
    public void Numeric_field_text_matches_original()
        => Assert.Equal("0 图像源1.图像宽度[]",
            ReferenceText.Format(0, "图像源1", "图像宽度") + ReferenceText.IndexSuffix(
                TypeRef.ScalarOf(ScalarKind.Int64), BindingSelection.All, true, _ => "", _ => ""));

    [Fact]
    public void Candidate_labels_carry_module_number_and_dots_only()
    {
        var schema = ImageSourceSchema();
        var port = schema.Outputs[0];
        var labels = DeclaredReferenceCandidateBuilder.Build(schema, port, "n1", "w1", "图像源1", 0)
            .Select(candidate => candidate.Label).ToArray();
        Assert.Contains("0 图像源1.图像", labels);
        Assert.Contains("0 图像源1.图像宽度", labels);
        Assert.DoesNotContain(labels, label => label.Contains(" · ") || label.Contains(" / "));
    }
}
