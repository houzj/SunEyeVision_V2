using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.GrayMatch.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>
/// 参数契约：原件 VA 版 33 项运行/判断参数 + 6 项模板参数都必须有声明、默认值与绑定名。
/// 默认值只取 VA 清单（`tools/imvsgraymatchmoduva/parameters.json`），不套用旧版 31 项。
/// </summary>
public sealed class GrayMatchParameterContractTests
{
    private static readonly string[] RunParameters =
    {
        "AngleLimitLow", "MatchPointXLimitEnable", "ScoreLimitLow", "BoxPointYLimitHigh", "BoxPointYLimitLow",
        "BoxPointXLimitHigh", "MaxMatchNum", "BoxPointYLimitEnable", "MatchPointXLimitHigh", "MaxOverlap",
        "MinScore", "AngleEnd", "MatchPointYLimitHigh", "NumLimitHigh", "MatchPointYLimitLow", "Polarity",
        "MatchingPolarity", "AngleStart", "TimeOut", "MatchPointYLimitEnable", "ScoreLimitHigh", "NumLimitEnable",
        "MatchPointXLimitLow", "ScoreLimitEnable", "SortType", "AngleLimitHigh", "BoxPointXLimitLow",
        "AngleLimitEnable", "NumLimitLow", "BoxPointXLimitEnable", "OKWhenNumIsZero", "EndPyramidLevel",
        "UseMatchAllMode"
    };

    /// <summary>原件训练对话框「配置参数」组（graymatchvadialoguistyle.xaml:860-872）。</summary>
    private static readonly string[] TemplateParameters =
    {
        "ModelData", "PyramidNumLevelFlag", "PyramidNumLevel", "ModelAngleStep", "ModelAngleStart", "ModelAngleEnd"
    };

    [Fact]
    public void Every_original_parameter_is_declared_with_its_original_name()
    {
        var names = GrayMatchFields.All.Select(field => field.VirtualMachineName).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(33, RunParameters.Length);
        Assert.Equal(6, TemplateParameters.Length);
        foreach (var name in RunParameters.Concat(TemplateParameters)) Assert.Contains(name, names);
        Assert.Equal(39, GrayMatchFields.All.Count);
        Assert.Equal(39, GrayMatchFields.Schema.Inputs.Count);
        // VA 版没有 AngleStep（那是旧版 31 项里的运行参数）。
        Assert.DoesNotContain("AngleStep", names);
    }

    [Fact]
    public void Field_defaults_match_the_va_parameter_file()
    {
        Assert.Equal(0.5, GrayMatchFields.SpecByVmName("MinScore").Default);
        Assert.Equal(1, GrayMatchFields.SpecByVmName("MaxMatchNum").Default);
        Assert.Equal(40, GrayMatchFields.SpecByVmName("MaxOverlap").Default);
        Assert.Equal(1, GrayMatchFields.SpecByVmName("Polarity").Default);
        Assert.Equal(1, GrayMatchFields.SpecByVmName("MatchingPolarity").Default);
        Assert.Equal(-45, GrayMatchFields.SpecByVmName("AngleStart").Default);
        Assert.Equal(45, GrayMatchFields.SpecByVmName("AngleEnd").Default);
        Assert.Equal(0, GrayMatchFields.SpecByVmName("EndPyramidLevel").Default);
        Assert.Equal(0, GrayMatchFields.SpecByVmName("TimeOut").Default);
        Assert.Equal(false, GrayMatchFields.SpecByVmName("UseMatchAllMode").Default);
        Assert.Equal(false, GrayMatchFields.SpecByVmName("OKWhenNumIsZero").Default);
        Assert.Equal(2, GrayMatchFields.SpecByVmName("SortType").Default is GrayMatchSortType sort ? (int)sort : -1);
        Assert.Equal(99_999, GrayMatchFields.SpecByVmName("NumLimitHigh").Default);
        Assert.Equal(-180d, GrayMatchFields.SpecByVmName("AngleLimitLow").Default);
    }

    [Fact]
    public void Template_parameters_follow_the_original_dialog_ranges_and_visibility()
    {
        Assert.Equal(GrayMatchPyramidMode.Auto, GrayMatchFields.SpecByVmName("PyramidNumLevelFlag").Default);
        var level = GrayMatchFields.Spec(nameof(GrayMatchSettings.PyramidLevel));
        Assert.Equal(5, level.Default);
        Assert.Equal(1d, level.Minimum);
        Assert.Equal(8d, level.Maximum);
        Assert.Equal(nameof(GrayMatchSettings.PyramidMode), level.VisibleWhen);

        var step = GrayMatchFields.Spec(nameof(GrayMatchSettings.ModelAngleStep));
        Assert.Equal(2, step.Default);
        Assert.Equal(1d, step.Minimum);
        Assert.Equal(50d, step.Maximum);

        var start = GrayMatchFields.Spec(nameof(GrayMatchSettings.ModelAngleStart));
        Assert.Equal(-45, start.Default);
        Assert.Equal(-180d, start.Minimum);
        Assert.Equal(180d, start.Maximum);
        Assert.Equal(nameof(GrayMatchSettings.ModelAngleEnd), start.PairWith);
        Assert.Equal(45, GrayMatchFields.Spec(nameof(GrayMatchSettings.ModelAngleEnd)).Default);

        Assert.Equal(new[] { "手动", "自动" }, GrayMatchFields.Spec(nameof(GrayMatchSettings.PyramidMode)).ChoiceLabels.Values);
        Assert.Equal(6, GrayMatchFields.Spec(nameof(GrayMatchSettings.SortType)).Choices.Count);
    }

    [Fact]
    public void Range_pairs_are_declared_for_every_limit_group()
    {
        Assert.Equal(nameof(GrayMatchSettings.AngleEnd), GrayMatchFields.Spec(nameof(GrayMatchSettings.AngleStart)).PairWith);
        Assert.Equal(nameof(GrayMatchSettings.CountLimitHigh), GrayMatchFields.Spec(nameof(GrayMatchSettings.CountLimitLow)).PairWith);
        Assert.Equal(nameof(GrayMatchSettings.ScoreLimitHigh), GrayMatchFields.Spec(nameof(GrayMatchSettings.ScoreLimitLow)).PairWith);
        Assert.Equal(nameof(GrayMatchSettings.MatchPointXLimitHigh), GrayMatchFields.Spec(nameof(GrayMatchSettings.MatchPointXLimitLow)).PairWith);
        Assert.Equal(nameof(GrayMatchSettings.MatchPointYLimitHigh), GrayMatchFields.Spec(nameof(GrayMatchSettings.MatchPointYLimitLow)).PairWith);
        Assert.Equal(nameof(GrayMatchSettings.BoxPointXLimitHigh), GrayMatchFields.Spec(nameof(GrayMatchSettings.BoxPointXLimitLow)).PairWith);
        Assert.Equal(nameof(GrayMatchSettings.BoxPointYLimitHigh), GrayMatchFields.Spec(nameof(GrayMatchSettings.BoxPointYLimitLow)).PairWith);
    }

    [Fact]
    public void Binding_names_cover_reference_capable_fields_only()
    {
        var tool = new GrayMatchTool();
        var names = tool.ParameterBindingNames;
        Assert.NotEmpty(names);
        Assert.All(names, pair => Assert.Contains(pair.Key, GrayMatchFields.Schema.Inputs.Select(port => port.Id)));
        Assert.DoesNotContain(GrayMatchFields.ModelData, names.Keys);
        Assert.DoesNotContain(GrayMatchFields.PyramidLevel, names.Keys);
        Assert.DoesNotContain(GrayMatchFields.ModelAngleStep, names.Keys);
        Assert.Equal("MaxMatchNum", names[GrayMatchFields.MaxMatchCount]);
        Assert.Equal("MinScore", names[GrayMatchFields.MinScore]);
        Assert.Equal("MatchingPolarity", names[GrayMatchFields.MatchingPolarity]);
        Assert.Equal("EndPyramidLevel", names[GrayMatchFields.EndPyramidLevel]);
    }

    [Fact]
    public void Execution_schema_declares_image_roi_and_fixture_without_setting_collisions()
    {
        var tool = new GrayMatchTool();
        var schema = tool.Describe();
        Assert.Equal(GrayMatchSchema.ImageInput, schema.WorkflowImageInputId);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), schema.Inputs.Single(port => port.Id == GrayMatchSchema.ImageInput).Type);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Region), schema.Inputs.Single(port => port.Id == GrayMatchSchema.RoiInput).Type);
        Assert.Equal(FixtureDeclarations.Info, schema.Inputs.Single(port => port.Id == GrayMatchSchema.FixtureInput).Type);
        Assert.Empty(tool.ResourceFields);
        Assert.Contains("halcon", tool.Capabilities().ResourceKeys);
        Assert.Equal("GrayMatch", tool.ToolId);
        Assert.Equal("灰度匹配", tool.Catalog.DisplayName);
        Assert.Equal("定位", tool.Catalog.Category);
    }

    [Fact]
    public void Known_settings_validation_reports_out_of_range_constants()
    {
        var tool = new GrayMatchTool();
        var schema = tool.SettingsSchema;
        var settings = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
        foreach (var port in schema.Inputs)
        {
            var spec = GrayMatchFields.All.Single(field => field.Id == port.Id);
            settings[port.Id] = new ConstantSetting(spec.Type == typeof(int) ? (long)(int)spec.Default
                : spec.Type == typeof(double) ? (double)spec.Default
                : spec.Type.IsEnum ? spec.Default.ToString()!
                : spec.Default);
        }
        settings[GrayMatchFields.MaxMatchCount] = new ConstantSetting(0L);
        var errors = tool.ValidateKnownSettings(new FieldSettings(schema.Inputs, settings));
        Assert.Contains(errors, item => item.FieldId == GrayMatchFields.MaxMatchCount);
    }
}
