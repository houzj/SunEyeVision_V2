using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.MarkFind.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>结果显示需求必须完整：缺少 region/outline/point 等声明时结果页会缺图。</summary>
public sealed class MarkFindDisplayContractTests
{
    /// <summary>
    /// 原件结果显示项的显示名（ui-preview.json 的 ImageViewConfig）。
    /// 「直线域」与「匹配结果」在原件画同一组匹配框线，本框架合并为一项，见交接单差异记录。
    /// </summary>
    private static readonly string[] OriginalDisplayItems =
    {
        "匹配结果", "匹配位置", "传感器区域", "匹配轮廓点", "基准点", "运行点", "文本", "盲区"
    };

    [Fact]
    public void Descriptor_declares_rows_identity_rectangle_region_score_outline_and_centroid()
    {
        var tool = new MarkFindTool();
        var descriptor = tool.ResultDisplay;
        Assert.Equal(MarkFindSchema.Rows, descriptor.Rows.PortId);
        Assert.Equal(new[] { "element-id" }, descriptor.Identity.MemberFieldPath);
        Assert.Equal(new[] { "box" }, descriptor.Rectangle.MemberFieldPath);
        Assert.Equal(new[] { "region" }, descriptor.GeometrySource.MemberFieldPath);
        Assert.Equal(new[] { "score" }, descriptor.TextValue.MemberFieldPath);
        Assert.Equal(new[] { "judgment-passed" }, descriptor.Passed.MemberFieldPath);
        Assert.NotNull(descriptor.Outline);
        Assert.Equal(new[] { "outline" }, descriptor.Outline!.MemberFieldPath);
        Assert.NotNull(descriptor.Centroid);
        Assert.Equal(new[] { "match-point" }, descriptor.Centroid!.MemberFieldPath);
        Assert.NotNull(descriptor.SelectedRectangleColor);
        descriptor.Validate(tool.ResultSchema);
    }

    [Fact]
    public void Outline_points_are_requested_from_the_host_so_geometry_is_published()
    {
        var descriptor = new MarkFindTool().ResultDisplay;
        var request = Assert.Single(descriptor.RequiredOutputs);
        Assert.Equal(MarkFindSchema.Rows, request.PortId);
        Assert.Equal(new[] { "outline" }, request.FieldPath);
    }

    [Fact]
    public void All_original_display_items_exist()
    {
        var tool = new MarkFindTool();
        var labels = tool.DisplaySettingsSchema.Presentation!.Fields.Select(field => field.DisplayName).ToHashSet(StringComparer.Ordinal);
        foreach (var item in OriginalDisplayItems) Assert.Contains(item, labels);
        var groups = tool.DisplaySettingsSchema.Presentation!.Fields.Select(field => field.Group).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Contains("图像显示", groups);
        Assert.Contains("文本显示", groups);
    }

    [Fact]
    public void Display_settings_are_separate_from_algorithm_settings()
    {
        var tool = new MarkFindTool();
        var algorithm = tool.SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var display = tool.DisplaySettingsSchema.Inputs.Select(port => port.Id).ToArray();
        Assert.NotEmpty(display);
        Assert.All(display, id => Assert.DoesNotContain(id, algorithm));
        var values = tool.CreateDisplaySettings();
        Assert.Equal(display.Length, values.CopyValues().Count);
        Assert.Equal(ResultDisplayFields.EditorSchema(tool).Inputs.Count, algorithm.Count + display.Length);
    }

    private static Dictionary<string, ParameterSetting> DisplayFields(MarkFindTool tool)
        => tool.CreateDisplaySettings().CopyValues()
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);

    [Fact]
    public void Appearance_capture_rejects_out_of_range_values()
    {
        var tool = new MarkFindTool();
        var schema = tool.DisplaySettingsSchema;
        var values = DisplayFields(tool);
        values[MarkFindResultDisplay.Field(MarkFindResultDisplay.Match, "opacity")] = new ConstantSetting(2d);
        Assert.ThrowsAny<ArgumentException>(() => tool.CaptureResultDisplay(new FieldSettings(schema.Inputs, values)));
    }

    [Fact]
    public void Default_appearance_is_fully_captured()
    {
        var tool = new MarkFindTool();
        var appearance = tool.CaptureResultDisplay(new FieldSettings(tool.DisplaySettingsSchema.Inputs, DisplayFields(tool)));
        Assert.True(appearance.Graphic.Visible);
        Assert.True(appearance.Text.Visible);
        Assert.NotNull(appearance.Inspection);
        Assert.NotNull(appearance.Mask);
        Assert.NotNull(appearance.Outline);
        Assert.NotNull(appearance.ReferencePoint);
        Assert.NotNull(appearance.RunPoint);
        Assert.NotNull(appearance.Centroids);
    }

    [Fact]
    public void Result_projection_declares_row_cells_with_region_geometry_source()
    {
        var tool = new MarkFindTool();
        var projection = tool.ResultProjection;
        Assert.Equal(ResultProjectionKind.StandardTable, projection.Kind);
        Assert.Equal(ResultCardinality.Many, projection.Cardinality);
        Assert.Equal("element-id", projection.ElementIdentityFieldId);
        Assert.Empty(projection.Validate(tool.ResultSchema));
        var score = projection.Columns.Single(column => column.Key == "score");
        Assert.Equal(new[] { "score" }, score.Value!.MemberFieldPath);
        Assert.Equal(new[] { "region" }, score.GeometrySource!.MemberFieldPath);
        Assert.Equal(new[] { "match-point" }, score.Geometry!.MemberFieldPath);
    }
}
