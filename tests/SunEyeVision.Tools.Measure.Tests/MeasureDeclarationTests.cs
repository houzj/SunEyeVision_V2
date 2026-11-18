using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.CircleCircleMeasure.Runtime;
using SunEyeVision.Tool.PointPointMeasure.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Measure.Tests;

/// <summary>
/// 圆圆测量 / 点点测量的声明面：字段数量、端口、页签分组、结果投影、显示需求与组合消费。
/// 证据：docs/07-工具逆向/tools/imvsc2cmeasuremodu|imvsp2pmeasuremodu/ 的 parameters.json、ui-preview.json。
/// </summary>
public sealed class MeasureDeclarationTests
{
    private static readonly CircleCircleMeasureTool C2C = new();
    private static readonly PointPointMeasureTool P2P = new();

    [Fact]
    public void CircleCircle_declares_all_46_recovered_parameters_across_three_pages()
    {
        var fields = C2C.SettingsSchema.Presentation!.Fields;
        Assert.Equal(46, C2C.SettingsSchema.Inputs.Count);
        Assert.Equal(46, fields.Count);
        var pages = fields.Select(field => field.Layout!.Page).Distinct(StringComparer.Ordinal).ToHashSet();
        Assert.Equal(new[] { CircleCircleMeasureFields.BasicPage, CircleCircleMeasureFields.RunPage, CircleCircleMeasureFields.ResultPage }.ToHashSet(), pages);
    }

    [Fact]
    public void CircleCircle_defaults_match_original_ParamItem_values()
    {
        Assert.Equal(1L, CircleCircleMeasureFields.Defaults["settings.choose-coordinate"]);
        Assert.Equal(15L, CircleCircleMeasureFields.Defaults["settings.edge-threshold-1"]);
        Assert.Equal(-180d, CircleCircleMeasureFields.Defaults["settings.angle-limit-low"]);
        Assert.Equal(99999d, CircleCircleMeasureFields.Defaults["settings.dist-limit-high"]);
        Assert.Equal(false, CircleCircleMeasureFields.Defaults["settings.inter2-y-limit-enabled"]);
    }

    [Fact]
    public void CircleCircle_declares_original_inputs_and_fifteen_outputs()
    {
        var schema = C2C.Describe();
        Assert.Equal(new[]
        {
            CircleCircleMeasureTool.ImageInput, CircleCircleMeasureTool.CircleAnnulus1Input,
            CircleCircleMeasureTool.CircleAnnulus2Input, CircleCircleMeasureTool.FixtureInput,
            CircleCircleMeasureTool.CalibMatrixInput
        }, schema.Inputs.Select(port => port.Id));
        Assert.Equal(CircleCircleMeasureTool.ImageInput, schema.WorkflowImageInputId);
        Assert.Empty(schema.ControlOutputs);
        Assert.Equal(16, schema.Outputs.Count); // 15 原厂输出 + 结果页 Rows
    }

    [Fact]
    public void PointPoint_declares_all_14_recovered_parameters_and_eight_outputs()
    {
        Assert.Equal(14, P2P.SettingsSchema.Inputs.Count);
        Assert.Equal(14, P2P.SettingsSchema.Presentation!.Fields.Count);
        Assert.Equal(1L, PointPointMeasureFields.Defaults["settings.choose-coordinate"]);
        Assert.Equal(-180d, PointPointMeasureFields.Defaults["settings.angle-limit-low"]);
        Assert.Equal(99999d, PointPointMeasureFields.Defaults["settings.dist-limit-high"]);
        var schema = P2P.Describe();
        Assert.Equal(4, schema.Inputs.Count);
        Assert.Equal(9, schema.Outputs.Count); // 8 原厂输出 + Rows
    }

    [Fact]
    public void Both_result_projections_and_display_requirements_validate()
    {
        Assert.Empty(C2C.ResultProjection.Validate(C2C.ResultSchema));
        C2C.ResultDisplay.Validate(C2C.ResultSchema);
        Assert.Empty(P2P.ResultProjection.Validate(P2P.ResultSchema));
        P2P.ResultDisplay.Validate(P2P.ResultSchema);
        Assert.Equal(ResultProjectionKind.PropertyList, C2C.ResultProjection.Kind);
        Assert.Equal(ResultProjectionKind.PropertyList, P2P.ResultProjection.Kind);
    }

    [Fact]
    public void Display_settings_are_consumed_completely_by_the_presentation_layer()
    {
        // 九组 C2C 图形 + 七行文本；三组 P2P 图形 + 七行文本，分组字段必须被组合声明完整消费。
        Assert.Equal(9, CircleCircleMeasureResultDisplay.DisplayGroups.Count);
        Assert.Equal(9 * 5 + 8, C2C.DisplaySettingsSchema.Inputs.Count);
        Assert.Equal(3, PointPointMeasureResultDisplay.DisplayGroups.Count);
        Assert.Equal(3 * 5 + 8, P2P.DisplaySettingsSchema.Inputs.Count);
        Assert.Equal(ResultDisplayFields.EditorSchema(C2C).Inputs.Count,
            C2C.SettingsSchema.Inputs.Count + C2C.DisplaySettingsSchema.Inputs.Count);
        Assert.Equal(ResultDisplayFields.EditorSchema(P2P).Inputs.Count,
            P2P.SettingsSchema.Inputs.Count + P2P.DisplaySettingsSchema.Inputs.Count);
    }

    [Fact]
    public void Invalid_limit_ranges_are_rejected_instead_of_clamped()
    {
        var c2c = new CircleCircleMeasureSettings(new Dictionary<string, object?>(CircleCircleMeasureFields.Defaults, StringComparer.Ordinal)
        {
            ["settings.angle-limit-enabled"] = true,
            ["settings.angle-limit-low"] = 10d,
            ["settings.angle-limit-high"] = -10d
        });
        Assert.Contains(c2c.Validate(), diagnostic => diagnostic.Code == "CIRCLECIRCLEMEASURE_LIMIT_RANGE");

        var p2p = new PointPointMeasureSettings(new Dictionary<string, object?>(PointPointMeasureFields.Defaults, StringComparer.Ordinal)
        {
            ["settings.dist-limit-enabled"] = true,
            ["settings.dist-limit-low"] = 99999d,
            ["settings.dist-limit-high"] = 1d
        });
        Assert.Contains(p2p.Validate(), diagnostic => diagnostic.Code == "POINTMEASURE_LIMIT_RANGE");
    }
}
