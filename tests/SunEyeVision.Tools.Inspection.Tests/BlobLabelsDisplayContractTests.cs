using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.BlobLabels.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>结果显示需求必须完整：缺少 region/outline 等声明时结果页会缺图。</summary>
public sealed class BlobLabelsDisplayContractTests
{
    private static readonly string[] OriginalDisplayItems =
    {
        "Blob", "检测区域", "轮廓点", "基准点", "运行点", "文本", "屏蔽区多边形", "扇环检测区域"
    };

    [Fact]
    public void Descriptor_declares_rows_identity_rectangle_region_outline_and_centroid()
    {
        var tool = new BlobLabelsTool();
        var descriptor = tool.ResultDisplay;
        Assert.Equal(BlobLabelsSchema.Rows, descriptor.Rows.PortId);
        Assert.Equal(new[] { "element-id" }, descriptor.Identity.MemberFieldPath);
        Assert.Equal(new[] { "oriented-bounds" }, descriptor.Rectangle.MemberFieldPath);
        Assert.Equal(new[] { "region" }, descriptor.GeometrySource.MemberFieldPath);
        Assert.Equal(new[] { "area" }, descriptor.TextValue.MemberFieldPath);
        Assert.Equal(new[] { "judgment-passed" }, descriptor.Passed.MemberFieldPath);
        Assert.NotNull(descriptor.Outline);
        Assert.Equal(new[] { "contour" }, descriptor.Outline!.MemberFieldPath);
        Assert.NotNull(descriptor.Centroid);
        Assert.NotNull(descriptor.SelectedRectangleColor);
        descriptor.Validate(tool.ResultSchema);
    }

    [Fact]
    public void Outline_is_requested_from_the_host_so_geometry_is_published()
    {
        var descriptor = new BlobLabelsTool().ResultDisplay;
        var request = Assert.Single(descriptor.RequiredOutputs);
        Assert.Equal(BlobLabelsSchema.Rows, request.PortId);
        Assert.Equal(new[] { "contour" }, request.FieldPath);
    }

    [Fact]
    public void All_eight_original_display_items_exist()
    {
        var tool = new BlobLabelsTool();
        var labels = tool.DisplaySettingsSchema.Presentation!.Fields.Select(field => field.DisplayName).ToHashSet(StringComparer.Ordinal);
        foreach (var item in OriginalDisplayItems) Assert.Contains(item, labels);
        var groups = tool.DisplaySettingsSchema.Presentation!.Fields.Select(field => field.Group).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Contains("图像显示", groups);
        Assert.Contains("文本显示", groups);
    }

    [Fact]
    public void Display_settings_are_separate_from_algorithm_settings()
    {
        var tool = new BlobLabelsTool();
        var algorithm = tool.SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var display = tool.DisplaySettingsSchema.Inputs.Select(port => port.Id).ToArray();
        Assert.NotEmpty(display);
        Assert.All(display, id => Assert.DoesNotContain(id, algorithm));
        var values = tool.CreateDisplaySettings();
        Assert.Equal(display.Length, values.CopyValues().Count);
        Assert.Equal(ResultDisplayFields.EditorSchema(tool).Inputs.Count, algorithm.Count + display.Length);
    }

    private static Dictionary<string, ParameterSetting> DisplayFields(BlobLabelsTool tool)
        => tool.CreateDisplaySettings().CopyValues()
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);

    [Fact]
    public void Appearance_capture_rejects_out_of_range_values()
    {
        var tool = new BlobLabelsTool();
        var schema = tool.DisplaySettingsSchema;
        var values = DisplayFields(tool);
        values[BlobLabelsResultDisplay.Field(BlobLabelsResultDisplay.Graphic, "opacity")] = new ConstantSetting(2d);
        Assert.ThrowsAny<ArgumentException>(() => tool.CaptureResultDisplay(new FieldSettings(schema.Inputs, values)));
    }

    [Fact]
    public void Default_appearance_is_fully_captured()
    {
        var tool = new BlobLabelsTool();
        var appearance = tool.CaptureResultDisplay(new FieldSettings(tool.DisplaySettingsSchema.Inputs, DisplayFields(tool)));
        Assert.True(appearance.Graphic.Visible);
        Assert.True(appearance.Text.Visible);
        Assert.NotNull(appearance.Inspection);
        Assert.NotNull(appearance.Mask);
        Assert.NotNull(appearance.Annulus);
        Assert.NotNull(appearance.Outline);
        Assert.NotNull(appearance.ReferencePoint);
        Assert.NotNull(appearance.RunPoint);
    }
}
