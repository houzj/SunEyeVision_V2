using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.IntensityMeasure.Runtime;
using Xunit;

namespace SunEyeVision.Tools.LogicData.Tests;

/// <summary>亮度测量的声明面：端口、属性列表、显示需求、字段布局、显示组合与参数校验。</summary>
public sealed class IntensityMeasureDeclarationTests
{
    private static readonly IntensityMeasureTool Tool = new();

    [Fact]
    public void Declares_image_roi_and_mask_inputs_without_branch_outputs()
    {
        var schema = Tool.Describe();
        Assert.Equal(new[] { IntensityMeasureTool.ImageInput, IntensityMeasureTool.RoiInput, IntensityMeasureTool.MaskInput },
            schema.Inputs.Select(port => port.Id));
        Assert.Equal(IntensityMeasureTool.ImageInput, schema.WorkflowImageInputId);
        Assert.Empty(schema.ControlOutputs);
        Assert.Equal(new[]
        {
            IntensityMeasureOutput.Status, IntensityMeasureOutput.RunTime, IntensityMeasureOutput.LumMin,
            IntensityMeasureOutput.LumMax, IntensityMeasureOutput.LumMean, IntensityMeasureOutput.LumStd,
            IntensityMeasureOutput.HistContrast, IntensityMeasureOutput.Histogram, IntensityMeasureOutput.Roi,
            IntensityMeasureOutput.Mask, IntensityMeasureOutput.Rows
        }, schema.Outputs.Select(port => port.Id));
    }

    [Fact]
    public void Roi_and_mask_are_optional_referenced_inputs()
    {
        var inputs = Tool.Describe().Inputs.ToDictionary(port => port.Id, StringComparer.Ordinal);
        Assert.True(inputs[IntensityMeasureTool.ImageInput].Required);
        foreach (var id in new[] { IntensityMeasureTool.RoiInput, IntensityMeasureTool.MaskInput })
        {
            Assert.False(inputs[id].Required);
            Assert.True(inputs[id].Nullable);
            Assert.Equal(FieldEditModes.Reference, inputs[id].EditModes);
        }
    }

    [Fact]
    public void Output_types_follow_the_original_channel_declarations()
    {
        var outputs = Tool.ResultSchema.Outputs.ToDictionary(port => port.Id, StringComparer.Ordinal);
        // 原件 LumMin/LumMax 为 int、LumMean/LumStd/HistContrast 为 float、LumHistogram 为 int 数组。
        Assert.Equal(NumericStorage.Int32, outputs[IntensityMeasureOutput.LumMin].Storage);
        Assert.Equal(NumericStorage.Int32, outputs[IntensityMeasureOutput.LumMax].Storage);
        Assert.Equal(DataType.Number, outputs[IntensityMeasureOutput.LumMean].Type);
        Assert.Equal(DataType.Number, outputs[IntensityMeasureOutput.LumStd].Type);
        Assert.Equal(DataType.Number, outputs[IntensityMeasureOutput.HistContrast].Type);
        var histogram = outputs[IntensityMeasureOutput.Histogram].Type;
        Assert.Equal(ValueShape.Sequence, histogram.Shape);
        Assert.Equal(TypeRef.ScalarOf(ScalarKind.Int64), histogram.Element);
        Assert.Equal(IntensityMeasureOutput.HistogramBinCount, histogram.Count!.Minimum);
        Assert.Equal(IntensityMeasureOutput.HistogramBinCount, histogram.Count!.Maximum);
    }

    [Fact]
    public void Result_projection_and_display_requirements_are_declared_consistently()
    {
        var schema = Tool.ResultSchema;
        Assert.Empty(Tool.ResultProjection.Validate(schema));
        Tool.ResultDisplay.Validate(schema);
        Assert.Equal(ResultProjectionKind.PropertyList, Tool.ResultProjection.Kind);
        Assert.Equal(ResultCardinality.One, Tool.ResultProjection.Cardinality);
        Assert.Equal(new[]
        {
            "module-status", "module-time", "lum-min", "lum-max", "lum-mean", "lum-std", "hist-contrast",
            "lum-histogram", "roi", "mask"
        }, Tool.ResultProjection.Columns.Select(column => column.Key));
        Assert.Equal(IntensityMeasureOutput.Rows, Tool.ResultDisplay.Rows.PortId);
        Assert.Equal(IntensityMeasureOutput.Rows, Tool.ResultDisplay.GeometrySource.PortId);
        Assert.Equal(IntensityMeasureOutput.Region, Tool.ResultDisplay.GeometrySource.MemberFieldPath.Single());
        Assert.Equal(IntensityMeasureOutput.Rectangle, Tool.ResultDisplay.Rectangle.MemberFieldPath.Single());
        Assert.Equal(IntensityMeasureOutput.LumMean, Tool.ResultDisplay.TextValue.MemberFieldPath.Single());
        Assert.Equal(IntensityMeasureOutput.RowIndex, Tool.ResultDisplay.Identity.MemberFieldPath.Single());
        Assert.Equal(IntensityMeasureOutput.JudgmentPassed, Tool.ResultDisplay.Passed.MemberFieldPath.Single());
    }

    [Fact]
    public void Display_settings_declare_one_row_per_field_and_capture_defaults()
    {
        var schema = Tool.DisplaySettingsSchema;
        Assert.Equal(18, schema.Inputs.Count);
        Assert.Equal(schema.Inputs.Count, schema.Presentation!.Fields.Count);
        Assert.All(schema.Inputs, port => Assert.Single(schema.Presentation!.Fields, field => field.FieldId == port.Id));
        var displayIdentities = schema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(IntensityMeasureFields.Schema.Inputs, port => displayIdentities.Contains(port.Id));
        Assert.Equal(ResultDisplayFields.EditorSchema(Tool).Inputs.Count,
            IntensityMeasureFields.Schema.Inputs.Count + schema.Inputs.Count);
        var defaults = Tool.CreateDisplaySettings();
        var appearance = Tool.CaptureResultDisplay(new FieldSettings(schema.Inputs, defaults.CopyValues()
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal)));
        Assert.True(appearance.Graphic.Visible);
        Assert.Equal(new ResultDisplayColor(0, 0, 255), appearance.Inspection!.Ok);
        Assert.Equal(IntensityMeasureResultDisplay.DefaultTextContent, appearance.Text.Content);
        Assert.Equal(10d, appearance.Text.FontSize);
    }

    [Fact]
    public void Graphic_groups_declare_a_primary_switch_and_a_title()
    {
        var groups = Tool.DisplaySettingsSchema.Presentation!.Fields
            .Where(field => field.Layout?.DialogGroup is not null)
            .GroupBy(field => field.Layout!.DialogGroup!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        Assert.Equal(new[] { IntensityMeasureResultDisplay.Region, IntensityMeasureResultDisplay.Roi }, groups.Keys);
        foreach (var (id, fields) in groups)
        {
            Assert.All(fields, field =>
            {
                Assert.False(string.IsNullOrWhiteSpace(field.Layout!.DialogTitle));
                Assert.Equal("结果显示", field.Layout.Page);
                Assert.Equal("图像显示", field.Layout.Section);
            });
            var primary = Assert.Single(fields, field => field.Layout!.DialogPrimary);
            Assert.Equal(id + ".visible", primary.FieldId);
            Assert.Equal(5, fields.Length);
        }
    }

    /// <summary>
    /// P4/P5 的声明前提：每个图形分组恰好五条角色行（visible/ok/ng/opacity/width），
    /// 且「不限固定小数位」的坐标只有文本位置两行——参数页的组合声明按同一组角色名对接。
    /// 真实组装（含图形角色完整消费校验）由 Release 冒烟覆盖。
    /// </summary>
    [Fact]
    public void Grouped_fields_declare_exactly_the_five_graphic_roles_per_group()
    {
        var fields = Tool.DisplaySettingsSchema.Presentation!.Fields;
        var groups = fields.Where(field => field.Layout?.DialogGroup is not null)
            .GroupBy(field => field.Layout!.DialogGroup!, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, groups.Length);
        foreach (var group in groups)
        {
            var roles = group.Select(field => field.FieldId[(field.FieldId.LastIndexOf('.') + 1)..])
                .OrderBy(role => role, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "ng", "ok", "opacity", "visible", "width" }, roles);
        }
        var unformatted = fields.Where(field => field.Layout?.DecimalPlaces is null)
            .Select(field => field.FieldId).ToArray();
        Assert.Equal(new[]
        {
            IntensityMeasureResultDisplay.Field(IntensityMeasureResultDisplay.Text, "x"),
            IntensityMeasureResultDisplay.Field(IntensityMeasureResultDisplay.Text, "y")
        }, unformatted);
    }

    [Fact]
    public void Every_settings_field_has_one_presentation_row()
    {
        var schema = IntensityMeasureFields.Schema;
        var presentation = schema.Presentation ?? throw new InvalidOperationException("亮度测量必须声明参数页布局。");
        Assert.Equal(schema.Inputs.Count, presentation.Fields.Count);
        Assert.Equal(15, presentation.Fields.Count);
        Assert.All(schema.Inputs, port => Assert.Single(presentation.Fields, field => field.FieldId == port.Id));
    }

    [Fact]
    public void Limit_rows_follow_their_enable_switch_and_pair_into_a_range()
    {
        var fields = IntensityMeasureFields.Schema.Presentation!.Fields.ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        foreach (var (enabled, low, high, label) in new[]
        {
            (IntensityMeasureFields.MaxValueLimitEnabled, IntensityMeasureFields.MaxValueLimitLow, IntensityMeasureFields.MaxValueLimitHigh, "最大值范围"),
            (IntensityMeasureFields.MinValueLimitEnabled, IntensityMeasureFields.MinValueLimitLow, IntensityMeasureFields.MinValueLimitHigh, "最小值范围"),
            (IntensityMeasureFields.MeanLimitEnabled, IntensityMeasureFields.MeanLimitLow, IntensityMeasureFields.MeanLimitHigh, "均值范围"),
            (IntensityMeasureFields.StdLimitEnabled, IntensityMeasureFields.StdLimitLow, IntensityMeasureFields.StdLimitHigh, "标准差范围"),
            (IntensityMeasureFields.ContrastLimitEnabled, IntensityMeasureFields.ContrastLimitLow, IntensityMeasureFields.ContrastLimitHigh, "对比度范围")
        })
        {
            Assert.Null(fields[enabled].Layout?.VisibleWhen);
            Assert.Equal(new BooleanStateTips("已启用", "未启用"), fields[enabled].Layout?.BooleanTips);
            foreach (var id in new[] { low, high })
            {
                var condition = fields[id].Layout?.VisibleWhen;
                Assert.NotNull(condition);
                Assert.Equal(enabled, condition!.FieldId);
                Assert.Equal(true, condition.Value);
                Assert.Equal("结果显示", fields[id].Layout!.Page);
                Assert.Equal("结果判断", fields[id].Layout.Section);
            }
            Assert.Equal(high, fields[low].Layout?.PairWith);
            Assert.Equal(label, fields[low].Layout?.PairDisplayName);
        }
    }

    [Fact]
    public void Default_settings_pass_admission_with_disabled_judges_and_full_scale_limits()
    {
        var definition = (IToolDefinition)Tool;
        Assert.Empty(definition.Validate(definition.CreateSettings()));
        var settings = IntensityMeasureFields.Materialize(IntensityMeasureFields.Read(new IntensityMeasureSettings()));
        Assert.False(settings.MaxValueLimitEnabled);
        Assert.Equal(0L, settings.MaxValueLimitLow);
        Assert.Equal(255L, settings.MaxValueLimitHigh);
        Assert.False(settings.MinValueLimitEnabled);
        Assert.False(settings.MeanLimitEnabled);
        Assert.Equal(IntensityMeasureFields.MaximumGray, settings.MeanLimitHigh);
        Assert.False(settings.StdLimitEnabled);
        Assert.Equal(IntensityMeasureFields.MaximumGray, settings.StdLimitHigh);
        Assert.False(settings.ContrastLimitEnabled);
        Assert.Equal(0d, settings.ContrastLimitLow);
    }

    [Fact]
    public void Settings_validation_reports_out_of_range_and_inverted_limits()
    {
        var inverted = Assert.Single(new IntensityMeasureSettings
        {
            MaxValueLimitEnabled = true, MaxValueLimitLow = 200, MaxValueLimitHigh = 10
        }.Validate());
        Assert.Equal("INTENSITYMEASURE_LIMIT_RANGE", inverted.Code);
        Assert.Equal(IntensityMeasureFields.MaxValueLimitLow, inverted.FieldId);
        var beyond = Assert.Single(new IntensityMeasureSettings
        {
            StdLimitEnabled = true, StdLimitLow = -1
        }.Validate());
        Assert.Equal(IntensityMeasureFields.StdLimitLow, beyond.FieldId);
        var notFinite = Assert.Single(new IntensityMeasureSettings
        {
            ContrastLimitHigh = double.NaN
        }.Validate());
        Assert.Equal(IntensityMeasureFields.ContrastLimitLow, notFinite.FieldId);
    }

    [Fact]
    public void Materialize_rejects_an_invalid_limit_pair_instead_of_clamping_it()
    {
        var values = new Dictionary<string, object?>(IntensityMeasureFields.Read(new IntensityMeasureSettings
        {
            MinValueLimitEnabled = true, MinValueLimitLow = 200, MinValueLimitHigh = 10
        }).CopyValues(), StringComparer.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            IntensityMeasureFields.Materialize(new InputValues(IntensityMeasureFields.Schema.Inputs, values)));
    }

    [Fact]
    public void A_non_numeric_field_value_is_rejected_by_its_declared_type()
    {
        var values = new Dictionary<string, object?>(IntensityMeasureFields.Read(new IntensityMeasureSettings()).CopyValues(), StringComparer.Ordinal)
        {
            [IntensityMeasureFields.MaxValueLimitLow] = "不是整数"
        };
        Assert.ThrowsAny<ArithmeticException>(() => new InputValues(IntensityMeasureFields.Schema.Inputs, values));
    }

    [Fact]
    public void Host_declaration_rules_accept_the_tool_and_produce_a_stable_fingerprint()
    {
        var first = ToolHostDeclarationRules.ValidateAndFingerprint(Tool);
        var second = ToolHostDeclarationRules.ValidateAndFingerprint(new IntensityMeasureTool());
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Tool_reports_the_original_catalog_identity()
    {
        Assert.Equal("IntensityMeasure", Tool.ToolId);
        Assert.Equal("亮度测量", Tool.Catalog.DisplayName);
        Assert.Equal("测量", Tool.Catalog.Category);
        Assert.Equal("DEFAULT", Tool.Catalog.NodeDisplayType);
        Assert.Equal("halcon", Assert.Single(Tool.Capabilities().ResourceKeys));
    }
}
