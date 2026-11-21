using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.PixelCount.Runtime;
using Xunit;

namespace SunEyeVision.Tools.LogicData.Tests;

/// <summary>像素统计的声明面：端口、结果表、显示需求、字段布局与参数校验。</summary>
public sealed class PixelCountDeclarationTests
{
    private static readonly PixelCountTool Tool = new();

    [Fact]
    public void Declares_image_roi_and_mask_inputs_without_branch_outputs()
    {
        var schema = Tool.Describe();
        // 图像、ROI、掩膜是前三个主输入；ROI 组还会追加本地几何/继承等明细端口。
        Assert.Equal(new[] { PixelCountTool.ImageInput, PixelCountTool.RoiInput, PixelCountTool.MaskInput },
            schema.Inputs.Take(3).Select(port => port.Id));
        Assert.Contains(schema.Inputs, port => port.Id == PixelCountTool.RoiInput);
        Assert.Equal(PixelCountTool.ImageInput, schema.WorkflowImageInputId);
        Assert.Empty(schema.ControlOutputs);
        Assert.Equal(new[]
        {
            PixelCountOutput.Status, PixelCountOutput.RunTime, PixelCountOutput.Count, PixelCountOutput.Ratio,
            PixelCountOutput.Roi, PixelCountOutput.Mask, PixelCountOutput.Rows
        }, schema.Outputs.Select(port => port.Id));
    }

    [Fact]
    public void Roi_and_mask_are_optional_referenced_inputs()
    {
        var inputs = Tool.Describe().Inputs.ToDictionary(port => port.Id, StringComparer.Ordinal);
        Assert.True(inputs[PixelCountTool.ImageInput].Required);
        foreach (var id in new[] { PixelCountTool.RoiInput, PixelCountTool.MaskInput })
        {
            Assert.False(inputs[id].Required);
            Assert.True(inputs[id].Nullable);
            Assert.Equal(FieldEditModes.Reference, inputs[id].EditModes);
        }
    }

    [Fact]
    public void Result_projection_and_display_requirements_are_declared_consistently()
    {
        var schema = Tool.ResultSchema;
        Assert.Empty(Tool.ResultProjection.Validate(schema));
        Tool.ResultDisplay.Validate(schema);
        Assert.Equal(ResultProjectionKind.StandardTable, Tool.ResultProjection.Kind);
        Assert.Equal(PixelCountOutput.Rows, Tool.ResultProjection.Rows!.PortId);
        Assert.Equal(PixelCountOutput.RangeIndex, Tool.ResultProjection.ElementIdentityFieldId);
        Assert.Equal(new[] { "range-index", "threshold-min", "threshold-max", "count", "ratio", "judgment" },
            Tool.ResultProjection.Columns.Select(column => column.Key));
        Assert.Equal(PixelCountOutput.Rows, Tool.ResultDisplay.Rows.PortId);
        Assert.Equal(PixelCountOutput.Rows, Tool.ResultDisplay.GeometrySource.PortId);
        Assert.Equal(PixelCountOutput.Region, Tool.ResultDisplay.GeometrySource.MemberFieldPath.Single());
        Assert.Equal(PixelCountOutput.Rectangle, Tool.ResultDisplay.Rectangle.MemberFieldPath.Single());
        Assert.Equal(PixelCountOutput.RangeRatio, Tool.ResultDisplay.TextValue.MemberFieldPath.Single());
        Assert.Equal(PixelCountOutput.RangeIndex, Tool.ResultDisplay.Identity.MemberFieldPath.Single());
        Assert.Equal(PixelCountOutput.JudgmentPassed, Tool.ResultDisplay.Passed.MemberFieldPath.Single());
    }

    [Fact]
    public void Display_settings_declare_one_row_per_field_and_capture_defaults()
    {
        var schema = Tool.DisplaySettingsSchema;
        Assert.Equal(23, schema.Inputs.Count);
        Assert.Equal(schema.Inputs.Count, schema.Presentation!.Fields.Count);
        Assert.All(schema.Inputs, port => Assert.Single(schema.Presentation!.Fields, field => field.FieldId == port.Id));
        var displayIdentities = schema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(PixelCountFields.Schema.Inputs, port => displayIdentities.Contains(port.Id));
        Assert.Equal(ResultDisplayFields.EditorSchema(Tool).Inputs.Count,
            PixelCountFields.Schema.Inputs.Count + schema.Inputs.Count);
        var defaults = Tool.CreateDisplaySettings();
        var appearance = Tool.CaptureResultDisplay(new FieldSettings(schema.Inputs, defaults.CopyValues()
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal)));
        Assert.True(appearance.Graphic.Visible);
        Assert.Equal(new ResultDisplayColor(0, 0, 255), appearance.Inspection!.Ok);
        Assert.Equal(PixelCountResultDisplay.DefaultTextContent, appearance.Text.Content);
        Assert.Equal(10d, appearance.Text.FontSize);
    }

    [Fact]
    public void Default_settings_pass_admission_with_one_full_scale_range()
    {
        var definition = (IToolDefinition)Tool;
        Assert.Empty(definition.Validate(definition.CreateSettings()));
        var settings = PixelCountFields.Materialize(PixelCountFields.Read(new PixelCountSettings()));
        var range = Assert.Single(settings.ReadRanges());
        Assert.Equal(0, range.Minimum);
        Assert.Equal(255, range.Maximum);
        Assert.False(settings.CountLimitEnabled);
        Assert.Equal(0L, settings.CountLimitLow);
        Assert.Equal(PixelCountFields.MaximumCountLimit, settings.CountLimitHigh);
        Assert.False(settings.RatioLimitEnabled);
        Assert.Equal(0d, settings.RatioLimitLow);
        Assert.Equal(PixelCountFields.MaximumRatioLimit, settings.RatioLimitHigh);
    }

    [Fact]
    public void Every_settings_field_has_one_presentation_row()
    {
        var schema = PixelCountFields.Schema;
        var presentation = schema.Presentation ?? throw new InvalidOperationException("像素统计必须声明参数页布局。");
        Assert.Equal(schema.Inputs.Count, presentation.Fields.Count);
        Assert.All(schema.Inputs, port => Assert.Single(presentation.Fields, field => field.FieldId == port.Id));
    }

    [Fact]
    public void Limit_rows_follow_their_enable_switch_and_pair_into_a_range()
    {
        var fields = PixelCountFields.Schema.Presentation!.Fields.ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        Assert.Null(fields[PixelCountFields.RangeCount].Layout?.VisibleWhen);
        foreach (var (enabled, low, high, label) in new[]
        {
            (PixelCountFields.CountLimitEnabled, PixelCountFields.CountLimitLow, PixelCountFields.CountLimitHigh, "数量范围"),
            (PixelCountFields.RatioLimitEnabled, PixelCountFields.RatioLimitLow, PixelCountFields.RatioLimitHigh, "比率范围")
        })
        {
            foreach (var id in new[] { low, high })
            {
                var condition = fields[id].Layout?.VisibleWhen;
                Assert.NotNull(condition);
                Assert.Equal(enabled, condition!.FieldId);
                Assert.Equal(true, condition.Value);
            }
            Assert.Equal(high, fields[low].Layout?.PairWith);
            Assert.Equal(label, fields[low].Layout?.PairDisplayName);
        }
    }

    [Theory]
    [InlineData("0-255")]
    [InlineData("0-10;11-255")]
    [InlineData(" 1-1 , 2-2 ")]
    public void Threshold_ranges_accept_one_to_thirty_two_closed_intervals(string encoded)
    {
        var ranges = PixelCountRanges.Parse(encoded);
        Assert.NotEmpty(ranges);
        Assert.All(ranges, range => Assert.True(range.Minimum <= range.Maximum));
        Assert.Equal(encoded.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length,
            ranges.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("100")]
    [InlineData("10-")]
    [InlineData("-1-10")]
    [InlineData("0-256")]
    [InlineData("200-100")]
    [InlineData("a-b")]
    public void Invalid_threshold_text_is_rejected_without_fallback(string encoded)
        => Assert.Throws<ArgumentException>(() => PixelCountRanges.Parse(encoded));

    [Fact]
    public void More_than_thirty_two_ranges_are_rejected()
    {
        var encoded = string.Join(';', Enumerable.Range(0, PixelCountRanges.MaximumCount + 1).Select(index => $"{index}-{index}"));
        var error = Assert.Throws<ArgumentException>(() => PixelCountRanges.Parse(encoded));
        Assert.Contains("32", error.Message);
    }

    [Fact]
    public void Threshold_slots_round_trip_through_the_settings_model()
    {
        var ranges = new[] { new PixelCountRange(0, 127), new PixelCountRange(128, 255) };
        var slots = PixelCountRanges.CreateSlots(2).ToArray();
        slots[0] = ranges[0];
        slots[1] = ranges[1];
        var settings = new PixelCountSettings { RangeCount = 2, Ranges = slots };
        Assert.Equal(ranges, settings.ReadRanges());
        var materialized = PixelCountFields.Materialize(PixelCountFields.Read(settings));
        Assert.Equal(ranges, materialized.ReadRanges());
    }

    [Fact]
    public void Settings_validation_reports_threshold_and_limit_problems()
    {
        var invalid = PixelCountRanges.CreateSlots(1).ToArray();
        invalid[0] = new PixelCountRange(300, 400);
        Assert.Equal("PIXELCOUNT_THRESHOLD",
            Assert.Single(new PixelCountSettings { RangeCount = 1, Ranges = invalid }.Validate()).Code);
        var count = Assert.Single(new PixelCountSettings
        {
            CountLimitEnabled = true, CountLimitLow = 10, CountLimitHigh = 5
        }.Validate());
        Assert.Equal("PIXELCOUNT_COUNT_LIMIT", count.Code);
        Assert.Equal(PixelCountFields.CountLimitLow, count.FieldId);
        var ratio = Assert.Single(new PixelCountSettings
        {
            RatioLimitEnabled = true, RatioLimitHigh = 2
        }.Validate());
        Assert.Equal("PIXELCOUNT_RATIO_LIMIT", ratio.Code);
    }

    [Fact]
    public void Materialize_rejects_an_invalid_limit_pair_instead_of_clamping_it()
    {
        var values = new Dictionary<string, object?>(PixelCountFields.Read(new PixelCountSettings
        {
            CountLimitEnabled = true, CountLimitLow = 10, CountLimitHigh = 5
        }).CopyValues(), StringComparer.Ordinal);
        Assert.Throws<ArgumentException>(() => PixelCountFields.Materialize(new InputValues(PixelCountFields.Schema.Inputs, values)));
    }

    [Fact]
    public void A_non_numeric_field_value_is_rejected_by_its_declared_type()
    {
        var values = new Dictionary<string, object?>(PixelCountFields.Read(new PixelCountSettings()).CopyValues(), StringComparer.Ordinal)
        {
            [PixelCountFields.CountLimitLow] = "不是整数"
        };
        // 声明类型是 Int32 整数：文本来源被数值范围校验直接拒绝，不会当作 0 或默认值。
        Assert.ThrowsAny<ArithmeticException>(() => new InputValues(PixelCountFields.Schema.Inputs, values));
    }

    [Fact]
    public void Host_declaration_rules_accept_the_tool_and_produce_a_stable_fingerprint()
    {
        var first = ToolHostDeclarationRules.ValidateAndFingerprint(Tool);
        var second = ToolHostDeclarationRules.ValidateAndFingerprint(new PixelCountTool());
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Tool_reports_the_original_catalog_identity()
    {
        Assert.Equal("PixelCount", Tool.ToolId);
        Assert.Equal("像素统计", Tool.Catalog.DisplayName);
        Assert.Equal("测量", Tool.Catalog.Category);
        Assert.Equal("halcon", Assert.Single(Tool.Capabilities().ResourceKeys));
    }
}
