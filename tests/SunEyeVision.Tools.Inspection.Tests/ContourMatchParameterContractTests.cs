using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.ContourMatch.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>参数契约：原件 56 项运行/判断参数 + 9 项模板参数都必须有声明、默认值与绑定名。</summary>
public sealed class ContourMatchParameterContractTests
{
    /// <summary>原件 parameters.json 的 56 项 ParamItem（名称与顺序无关，只校验集合）。</summary>
    private static readonly string[] RunParameters =
    {
        "AngleLimitLow", "MatchPointXLimitEnable", "ScoreLimitLow", "BoxPointYLimitHigh", "ScaleStart", "ScaleEnd",
        "BoxPointYLimitLow", "BoxPointXLimitHigh", "MaxMatchNum", "BoxPointYLimitEnable", "MatchPointXLimitHigh",
        "MaxOverlap", "MultiModelMaxOverlap", "MinScore", "AngleEnd", "MatchPointYLimitHigh", "NumLimitHigh",
        "MatchPointYLimitLow", "Polarity", "AngleStart", "TimeOut", "MatchPointYLimitEnable", "ScoreLimitHigh",
        "NumLimitEnable", "MatchPointXLimitLow", "ScoreLimitEnable", "SortType", "AngleLimitHigh",
        "BoxPointXLimitLow", "AngleLimitEnable", "NumLimitLow", "BoxPointXLimitEnable", "MatchThresholdFlag",
        "MatchThresholdHigh", "MatchExtentRate", "SpotterFlag", "ScaleXStart", "ScaleXEnd", "ScaleYStart", "ScaleYEnd",
        "ScaleLimitEnable", "ScaleLimitLow", "ScaleLimitHigh", "ScaleXLimitEnable", "ScaleXLimitLow", "ScaleXLimitHigh",
        "ScaleYLimitEnable", "ScaleYLimitLow", "ScaleYLimitHigh", "OutLineEnable", "OutLinePointEnable",
        "UseMatchAllMode", "RangeScaleType", "OKWhenNumIsZero", "SkewXStart", "SkewXEnd"
    };

    /// <summary>原件训练对话框「匹配参数」组（hppyramidfeaturepatmatchdialoguistyle.xaml:879-906）。</summary>
    private static readonly string[] TemplateParameters =
    {
        "ModelData", "PyramidScaleFlag", "PyramidScaleLevel", "PyramidScaleRLevel", "EdgeThresholdFlag",
        "EdgeThreshold", "ChainFlag", "MinChainLen", "MaskModelFlag"
    };

    [Fact]
    public void Every_original_parameter_is_declared_with_its_original_name()
    {
        var names = ContourMatchFields.All.Select(field => field.VirtualMachineName).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(56, RunParameters.Length);
        Assert.Equal(9, TemplateParameters.Length);
        foreach (var name in RunParameters.Concat(TemplateParameters)) Assert.Contains(name, names);
        // TrainingRois 是本方为对齐原厂 SignList 训练样本新增的区域文档字段（非原厂标量参数），
        // 因此声明总数 = 原厂 65 项 + 1 项区域文档。
        Assert.Equal(RunParameters.Length + TemplateParameters.Length + 1, ContourMatchFields.All.Count);
        Assert.Equal(66, ContourMatchFields.Schema.Inputs.Count);
        Assert.Contains("TrainingRois", ContourMatchFields.All.Select(field => field.Name));
        Assert.Equal(SunEyeVision.Plugin.Contracts.RoiDocumentValue.Declaration,
            ContourMatchFields.Schema.Inputs.Single(port => port.Id == ContourMatchFields.TrainingRois).Type);
    }

    [Fact]
    public void Field_defaults_match_the_original_sources()
    {
        // parameters.json 默认值
        Assert.Equal(-45, ContourMatchFields.SpecByVmName("AngleStart").Default);
        Assert.Equal(45, ContourMatchFields.SpecByVmName("AngleEnd").Default);
        Assert.Equal(0.5, ContourMatchFields.SpecByVmName("MinScore").Default);
        Assert.Equal(1, ContourMatchFields.SpecByVmName("MaxMatchNum").Default);
        Assert.Equal(50, ContourMatchFields.SpecByVmName("MaxOverlap").Default);
        Assert.Equal(50, ContourMatchFields.SpecByVmName("MultiModelMaxOverlap").Default);
        Assert.Equal(1, ContourMatchFields.SpecByVmName("Polarity").Default);
        Assert.Equal(2, ContourMatchFields.SpecByVmName("SortType").Default is ContourMatchSortType sort ? (int)sort : -1);
        Assert.Equal(99_999, ContourMatchFields.SpecByVmName("NumLimitHigh").Default);
        Assert.Equal(-180d, ContourMatchFields.SpecByVmName("AngleLimitLow").Default);
        Assert.Equal(true, ContourMatchFields.SpecByVmName("OutLineEnable").Default);
        Assert.Equal(false, ContourMatchFields.SpecByVmName("OutLinePointEnable").Default);
        Assert.Equal(40, ContourMatchFields.SpecByVmName("MatchThresholdHigh").Default);
        Assert.Equal(0, ContourMatchFields.SpecByVmName("RangeScaleType").Default);

        // HPPyramidFeaturepatMatchDialogData 字段默认值
        Assert.Equal(ContourMatchPyramidMode.Auto, ContourMatchFields.SpecByVmName("PyramidScaleFlag").Default);
        Assert.Equal(5, ContourMatchFields.SpecByVmName("PyramidScaleLevel").Default);
        Assert.Equal(1, ContourMatchFields.SpecByVmName("PyramidScaleRLevel").Default);
        Assert.Equal(ContourMatchThresholdMode.Auto, ContourMatchFields.SpecByVmName("EdgeThresholdFlag").Default);
        Assert.Equal(15, ContourMatchFields.SpecByVmName("EdgeThreshold").Default);
        Assert.Equal(4, ContourMatchFields.SpecByVmName("MinChainLen").Default);
        Assert.Equal(ContourMatchMaskModel.FullOutline, ContourMatchFields.SpecByVmName("MaskModelFlag").Default);
    }

    [Fact]
    public void Template_parameters_follow_the_original_dialog_ranges_and_visibility()
    {
        var level = ContourMatchFields.Spec(nameof(ContourMatchSettings.PyramidScaleLevel));
        Assert.Equal(1d, level.Minimum);
        Assert.Equal(10d, level.Maximum);
        Assert.Equal(nameof(ContourMatchSettings.PyramidMode), level.VisibleWhen);
        Assert.Equal(1d, ContourMatchFields.Spec(nameof(ContourMatchSettings.PyramidScaleRLevel)).Minimum);
        Assert.Equal(10d, ContourMatchFields.Spec(nameof(ContourMatchSettings.PyramidScaleRLevel)).Maximum);

        var threshold = ContourMatchFields.Spec(nameof(ContourMatchSettings.EdgeThreshold));
        Assert.Equal(1d, threshold.Minimum);
        Assert.Equal(255d, threshold.Maximum);
        Assert.Equal(nameof(ContourMatchSettings.EdgeThresholdMode), threshold.VisibleWhen);

        var chain = ContourMatchFields.Spec(nameof(ContourMatchSettings.MinChainLen));
        Assert.Equal(2d, chain.Minimum);
        Assert.Equal(1000d, chain.Maximum);
        Assert.Equal(nameof(ContourMatchSettings.ChainMode), chain.VisibleWhen);

        Assert.Equal(new[] { "手动", "自动" }, ContourMatchFields.Spec(nameof(ContourMatchSettings.PyramidMode)).ChoiceLabels.Values);
        Assert.Equal(new[] { "全轮廓", "外轮廓" }, ContourMatchFields.Spec(nameof(ContourMatchSettings.MaskModel)).ChoiceLabels.Values);
        Assert.Equal(6, ContourMatchFields.Spec(nameof(ContourMatchSettings.SortType)).Choices.Count);
    }

    [Fact]
    public void Range_pairs_are_declared_for_every_limit_group()
    {
        Assert.Equal(nameof(ContourMatchSettings.AngleEnd), ContourMatchFields.Spec(nameof(ContourMatchSettings.AngleStart)).PairWith);
        Assert.Equal(nameof(ContourMatchSettings.CountLimitHigh), ContourMatchFields.Spec(nameof(ContourMatchSettings.CountLimitLow)).PairWith);
        Assert.Equal(nameof(ContourMatchSettings.ScoreLimitHigh), ContourMatchFields.Spec(nameof(ContourMatchSettings.ScoreLimitLow)).PairWith);
        Assert.Equal(nameof(ContourMatchSettings.ScaleLimitHigh), ContourMatchFields.Spec(nameof(ContourMatchSettings.ScaleLimitLow)).PairWith);
        Assert.Equal(nameof(ContourMatchSettings.ScaleXLimitHigh), ContourMatchFields.Spec(nameof(ContourMatchSettings.ScaleXLimitLow)).PairWith);
        Assert.Equal(nameof(ContourMatchSettings.ScaleYLimitHigh), ContourMatchFields.Spec(nameof(ContourMatchSettings.ScaleYLimitLow)).PairWith);
        Assert.Equal(nameof(ContourMatchSettings.MatchPointXLimitHigh), ContourMatchFields.Spec(nameof(ContourMatchSettings.MatchPointXLimitLow)).PairWith);
        Assert.Equal(nameof(ContourMatchSettings.BoxPointYLimitHigh), ContourMatchFields.Spec(nameof(ContourMatchSettings.BoxPointYLimitLow)).PairWith);
    }

    [Fact]
    public void Binding_names_cover_reference_capable_fields_only()
    {
        var tool = new ContourMatchTool();
        var names = tool.ParameterBindingNames;
        Assert.NotEmpty(names);
        Assert.All(names, pair => Assert.Contains(pair.Key, ContourMatchFields.Schema.Inputs.Select(port => port.Id)));
        Assert.DoesNotContain(ContourMatchFields.ModelData, names.Keys);
        Assert.DoesNotContain(ContourMatchFields.PyramidScaleLevel, names.Keys);
        Assert.DoesNotContain(ContourMatchFields.EdgeThreshold, names.Keys);
        Assert.Equal("MaxMatchNum", names[ContourMatchFields.MaxMatchCount]);
        Assert.Equal("MinScore", names[ContourMatchFields.MinScore]);
        Assert.Equal("NumLimitHigh", names[ContourMatchFields.CountLimitHigh]);
    }

    [Fact]
    public void Execution_schema_declares_image_roi_and_fixture_without_setting_collisions()
    {
        var tool = new ContourMatchTool();
        var schema = tool.Describe();
        Assert.Equal(ContourMatchSchema.ImageInput, schema.WorkflowImageInputId);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image), schema.Inputs.Single(port => port.Id == ContourMatchSchema.ImageInput).Type);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Region), schema.Inputs.Single(port => port.Id == ContourMatchSchema.RoiInput).Type);
        Assert.Equal(FixtureDeclarations.Info, schema.Inputs.Single(port => port.Id == ContourMatchSchema.FixtureInput).Type);
        Assert.Empty(tool.ResourceFields);
        Assert.Contains("halcon", tool.Capabilities().ResourceKeys);
        Assert.Equal("ContourMatch", tool.ToolId);
        Assert.Equal("轮廓匹配", tool.Catalog.DisplayName);
        Assert.Equal("定位", tool.Catalog.Category);
    }

    [Fact]
    public void Known_settings_validation_reports_out_of_range_constants()
    {
        var tool = new ContourMatchTool();
        var schema = tool.SettingsSchema;
        var settings = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
        foreach (var port in schema.Inputs)
        {
            var spec = ContourMatchFields.All.Single(field => field.Id == port.Id);
            settings[port.Id] = new ConstantSetting(spec.Type == typeof(int) ? (long)(int)spec.Default
                : spec.Type == typeof(double) ? (double)spec.Default
                : spec.Type.IsEnum ? spec.Default.ToString()!
                : spec.Default);
        }
        settings[ContourMatchFields.MaxMatchCount] = new ConstantSetting(0L);
        var errors = tool.ValidateKnownSettings(new FieldSettings(schema.Inputs, settings));
        Assert.Contains(errors, item => item.FieldId == ContourMatchFields.MaxMatchCount);
    }
}
