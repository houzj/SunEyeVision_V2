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
        // 轮廓点是系统类型：工具只声明出口端口，不再在显示合同里声明通道。
        var outline = Assert.IsAssignableFrom<IToolResultOutlineDeclaration>(tool);
        Assert.Equal(MarkFindSchema.OutlinePoints, outline.OutlinePortId);
        Assert.NotNull(descriptor.Centroid);
        Assert.Equal(new[] { "match-point" }, descriptor.Centroid!.MemberFieldPath);
        Assert.NotNull(descriptor.SelectedRectangleColor);
        descriptor.Validate(tool.ResultSchema);
    }

    [Fact]
    public void Outline_points_are_requested_from_the_host_so_geometry_is_published()
    {
        // 系统按 IToolResultOutlineDeclaration.OutlinePortId 统一请求轮廓点输出。
        var outline = Assert.IsAssignableFrom<IToolResultOutlineDeclaration>(new MarkFindTool());
        Assert.Equal(MarkFindSchema.OutlinePoints, outline.OutlinePortId);
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

    /// <summary>
    /// 原厂 IMVSMarkFindModuAlgorithmTab.xml 对 RangScale/RangScaleX/RangScaleY/
    /// ScaleXLimitEnable/RangeScaleXLimit/ScaleYLimitEnable/RangeScaleYLimit 标了
    /// IsAbandon="True"：参数留在配置里，界面不渲染。
    /// </summary>
    [Fact]
    public void Abandoned_parameters_keep_ports_but_render_no_row()
    {
        var tool = new MarkFindTool();
        var abandoned = new[]
        {
            nameof(MarkFindSettings.ScaleStart), nameof(MarkFindSettings.ScaleEnd),
            nameof(MarkFindSettings.ScaleXStart), nameof(MarkFindSettings.ScaleXEnd),
            nameof(MarkFindSettings.ScaleYStart), nameof(MarkFindSettings.ScaleYEnd),
            nameof(MarkFindSettings.ScaleXLimitEnabled), nameof(MarkFindSettings.ScaleXLimitLow),
            nameof(MarkFindSettings.ScaleXLimitHigh),
            nameof(MarkFindSettings.ScaleYLimitEnabled), nameof(MarkFindSettings.ScaleYLimitLow),
            nameof(MarkFindSettings.ScaleYLimitHigh)
        }.Select(MarkFindFields.IdOf).ToArray();
        var ports = tool.SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var rows = tool.SettingsSchema.Presentation!.Fields.Select(field => field.FieldId).ToHashSet(StringComparer.Ordinal);
        // 端口与取值保留：算法与已保存方案不受影响。
        Assert.All(abandoned, id => Assert.Contains(id, ports));
        // 参数页不渲染这些行。
        Assert.All(abandoned, id => Assert.DoesNotContain(id, rows));
    }

    /// <summary>
    /// 原厂 AlgorithmTab.xml 两个 Expand 收起时显式隐藏 8 + 5 个控件。
    /// 本方等价机制：标 Advanced 的字段按 (Section, IsAdvanced) 独立成折叠区，
    /// 收起时隐藏该区全部字段。因此 advanced 集合必须与那 13 个逐项一致。
    /// </summary>
    [Fact]
    public void Advanced_fields_match_the_original_expand_groups()
    {
        var expected = new[]
        {
            // Expand #1（Tab_Run Params 页）：原厂 MarkLocationType / MaxOverlap / SortType /
            // TimeOut / OutLineEnable / MatchThresholdFlag / MatchExtentRate / SpotterFlag
            nameof(MarkFindSettings.MarkLocationType), nameof(MarkFindSettings.MaxOverlap),
            nameof(MarkFindSettings.SortType), nameof(MarkFindSettings.TimeOut),
            nameof(MarkFindSettings.OutlineEnabled), nameof(MarkFindSettings.MatchThresholdMode),
            nameof(MarkFindSettings.MatchExtentRate), nameof(MarkFindSettings.SpotterEnabled),
            // Expand #2（ResultShow 页）：原厂 ScoreLimitEnable / MatchPointX/YLimitEnable /
            // BoxPointX/YLimitEnable
            nameof(MarkFindSettings.ScoreLimitEnabled),
            nameof(MarkFindSettings.MatchPointXLimitEnabled), nameof(MarkFindSettings.MatchPointYLimitEnabled),
            nameof(MarkFindSettings.BoxPointXLimitEnabled), nameof(MarkFindSettings.BoxPointYLimitEnabled)
        }.Select(MarkFindFields.IdOf).ToHashSet(StringComparer.Ordinal);
        // 只比较有 XML 证据的两页（Tab_Run Params / ResultShow）；模板页参数由
        // MarkFindConfigPanal 代码控件承载，无 AlgorithmTab.xml 证据，不在本断言范围。
        var actual = new MarkFindTool().SettingsSchema.Presentation!.Fields
            .Where(field => field.Layout?.Advanced == true)
            .Where(field => field.Layout!.Section == MarkFindFields.RunSection
                || field.Layout.Section == MarkFindFields.JudgeSection)
            .Select(field => field.FieldId)
            .ToHashSet(StringComparer.Ordinal);
        // 逐项断言：缺项与多项都能直接报出字段身份。
        Assert.All(expected, id => Assert.Contains(id, actual));
        Assert.All(actual, id => Assert.Contains(id, expected));
    }
}
