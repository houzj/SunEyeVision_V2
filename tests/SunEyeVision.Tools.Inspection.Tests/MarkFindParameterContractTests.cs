using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.MarkFind.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>参数契约：原件 47 项运行参数 + 12 项模板参数都必须有声明、默认值与绑定名。</summary>
public sealed class MarkFindParameterContractTests
{
    private static readonly string[] RunParameters =
    {
        "AngleLimitLow", "MatchPointXLimitEnable", "ScoreLimitLow", "BoxPointYLimitHigh", "ScaleStart", "ScaleEnd",
        "BoxPointYLimitLow", "BoxPointXLimitHigh", "MaxMatchNum", "BoxPointYLimitEnable", "MatchPointXLimitHigh",
        "MaxOverlap", "MinScore", "AngleEnd", "MatchPointYLimitHigh", "NumLimitHigh", "MatchPointYLimitLow",
        "Polarity", "AngleStart", "TimeOut", "MatchPointYLimitEnable", "ScoreLimitHigh", "NumLimitEnable",
        "MatchPointXLimitLow", "ScoreLimitEnable", "SortType", "AngleLimitHigh", "BoxPointXLimitLow",
        "AngleLimitEnable", "NumLimitLow", "BoxPointXLimitEnable", "MatchThresholdFlag", "MatchThresholdHigh",
        "MatchExtentRate", "SpotterFlag", "ScaleXStart", "ScaleXEnd", "ScaleYStart", "ScaleYEnd",
        "ScaleXLimitEnable", "ScaleXLimitLow", "ScaleXLimitHigh", "ScaleYLimitEnable", "ScaleYLimitLow",
        "ScaleYLimitHigh", "OutLineEnable", "MarkLocationType"
    };

    private static readonly string[] TemplateParameters =
    {
        "ModelData", "PyramidscaleMode", "PyramidScale", "FinePyramidscale", "ThresholdMode", "HighThreshold",
        "FeatureMatchType", "RotationAngle", "RotationStep", "ProjectionInterval", "MarkType", "MaskType"
    };

    [Fact]
    public void Every_original_parameter_is_declared_with_its_original_name()
    {
        var names = MarkFindFields.All.Select(field => field.VirtualMachineName).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(47, RunParameters.Length);
        foreach (var name in RunParameters.Concat(TemplateParameters)) Assert.Contains(name, names);
        Assert.Equal(RunParameters.Length + TemplateParameters.Length, MarkFindFields.All.Count);
        Assert.Equal(59, MarkFindFields.Schema.Inputs.Count);
    }

    [Fact]
    public void Field_defaults_match_the_original_parameter_file()
    {
        Assert.Equal(-180d, MarkFindFields.SpecByVmName("AngleLimitLow").Default);
        Assert.Equal(45, MarkFindFields.SpecByVmName("AngleEnd").Default);
        Assert.Equal(-45, MarkFindFields.SpecByVmName("AngleStart").Default);
        Assert.Equal(0.5, MarkFindFields.SpecByVmName("MinScore").Default);
        Assert.Equal(1, MarkFindFields.SpecByVmName("MaxMatchNum").Default);
        Assert.Equal(80, MarkFindFields.SpecByVmName("MaxOverlap").Default);
        Assert.Equal(MarkFindSortType.Descend, MarkFindFields.SpecByVmName("SortType").Default);
        Assert.Equal(1, MarkFindFields.SpecByVmName("Polarity").Default);
        Assert.Equal(99_999, MarkFindFields.SpecByVmName("NumLimitHigh").Default);
        Assert.Equal(true, MarkFindFields.SpecByVmName("OutLineEnable").Default);
        Assert.Equal(3, MarkFindFields.SpecByVmName("PyramidScale").Default);
        Assert.Equal(20, MarkFindFields.SpecByVmName("HighThreshold").Default);
        Assert.Equal(10, MarkFindFields.SpecByVmName("RotationAngle").Default);
        Assert.Equal(0.5, MarkFindFields.SpecByVmName("RotationStep").Default);
        Assert.Equal(2, MarkFindFields.SpecByVmName("MarkType").Default is MarkFindMarkType type ? (int)type : -1);
    }

    [Fact]
    public void Template_parameters_hide_the_dependent_rows_like_the_original_dialog()
    {
        var pyramid = MarkFindFields.Spec(nameof(MarkFindSettings.RoughPyramidScale));
        Assert.Equal(nameof(MarkFindSettings.PyramidScaleMode), pyramid.VisibleWhen);
        Assert.Equal(nameof(MarkFindSettings.ThresholdMode), MarkFindFields.Spec(nameof(MarkFindSettings.HighThreshold)).VisibleWhen);
        Assert.True(MarkFindFields.Spec(nameof(MarkFindSettings.RotationAngle)).Advanced);
        Assert.True(MarkFindFields.Spec(nameof(MarkFindSettings.MarkType)).Advanced);
        Assert.True(MarkFindFields.Spec(nameof(MarkFindSettings.ProjectionInterval)).Advanced);
    }

    [Fact]
    public void Range_pairs_are_declared_for_every_limit_group()
    {
        foreach (var name in new[] { nameof(MarkFindSettings.AngleEnd), nameof(MarkFindSettings.ScoreLimitHigh),
                     nameof(MarkFindSettings.CountLimitHigh), nameof(MarkFindSettings.MatchPointXLimitHigh),
                     nameof(MarkFindSettings.BoxPointYLimitHigh) })
        {
            var layout = MarkFindFields.Schema.Inputs.Single(port => port.Id == MarkFindFields.IdOf(name));
            Assert.NotEqual(FieldEditModes.None, layout.EditModes);
        }
        Assert.Equal(nameof(MarkFindSettings.AngleEnd), MarkFindFields.Spec(nameof(MarkFindSettings.AngleStart)).PairWith);
        Assert.Equal(nameof(MarkFindSettings.ScoreLimitHigh), MarkFindFields.Spec(nameof(MarkFindSettings.ScoreLimitLow)).PairWith);
    }

    [Fact]
    public void Binding_names_cover_reference_capable_fields_only()
    {
        var tool = new MarkFindTool();
        var names = tool.ParameterBindingNames;
        Assert.NotEmpty(names);
        Assert.All(names, pair => Assert.Contains(pair.Key, MarkFindFields.Schema.Inputs.Select(port => port.Id)));
        Assert.DoesNotContain(MarkFindFields.ModelData, names.Keys);
        Assert.DoesNotContain(MarkFindFields.MaskType, names.Keys);
        Assert.Equal("MaxMatchNum", names[MarkFindFields.MaxMatchCount]);
        Assert.Equal("MinScore", names[MarkFindFields.MinScore]);
    }

    [Fact]
    public void Execution_schema_declares_image_roi_and_fixture_without_setting_collisions()
    {
        var tool = new MarkFindTool();
        var schema = tool.Describe();
        Assert.Equal(MarkFindSchema.ImageInput, schema.WorkflowImageInputId);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), schema.Inputs.Single(port => port.Id == MarkFindSchema.ImageInput).Type);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Region), schema.Inputs.Single(port => port.Id == MarkFindSchema.RoiInput).Type);
        Assert.Equal(FixtureDeclarations.Info, schema.Inputs.Single(port => port.Id == MarkFindSchema.FixtureInput).Type);
        Assert.Empty(tool.ResourceFields);
        Assert.Contains("halcon", tool.Capabilities().ResourceKeys);
        Assert.Equal("MarkFind", tool.ToolId);
        Assert.Equal("图形定位", tool.Catalog.DisplayName);
    }

    [Fact]
    public void Known_settings_validation_reports_out_of_range_constants()
    {
        var tool = new MarkFindTool();
        var schema = tool.SettingsSchema;
        var settings = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
        foreach (var port in schema.Inputs)
        {
            var spec = MarkFindFields.All.Single(field => field.Id == port.Id);
            settings[port.Id] = new ConstantSetting(spec.Type == typeof(int) ? (long)(int)spec.Default
                : spec.Type == typeof(double) ? (double)spec.Default
                : spec.Type.IsEnum ? spec.Default.ToString()!
                : spec.Default);
        }
        settings[MarkFindFields.MaxMatchCount] = new ConstantSetting(0L);
        var errors = tool.ValidateKnownSettings(new FieldSettings(schema.Inputs, settings));
        Assert.Contains(errors, item => item.FieldId == MarkFindFields.MaxMatchCount);
    }
}
