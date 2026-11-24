using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.ContourMatch.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>参数契约：原件 56 项运行/判断参数 + 9 项模板参数都必须有声明、默认值与绑定名。</summary>
public sealed class ContourMatchParameterContractTests
{
    /// <summary>
    /// 参数集以 HALCON 形状模板能力为准：保留 HALCON 有原生对应的参数（含本方多模板/判定扩展），
    /// 删除 HALCON 无对应语义的原厂参数，避免"UI 有开关、算法无实现"。
    /// </summary>
    private static readonly string[] HalconParameters =
    {
        // 匹配
        "MinScore", "MaxMatchNum", "Polarity", "AngleStart", "AngleEnd",
        "ScaleStart", "ScaleEnd", "ScaleXStart", "ScaleXEnd", "ScaleYStart", "ScaleYEnd",
        "MaxOverlap", "MultiModelMaxOverlap", "SortType", "RangeScaleType",
        "Greediness", "SubPixel", "TimeOut", "OutLineEnable", "OutLinePointEnable", "UseMatchAllMode",
        // 创建
        "ModelData", "PyramidScaleFlag", "PyramidScaleLevel", "PyramidScaleRLevel",
        "EdgeThresholdFlag", "EdgeThreshold", "MinContrast", "Optimization",
        // 本方扩展
        "TrainingRois", "MatchExtentRate",
        // 判定
        "NumLimitEnable", "NumLimitLow", "NumLimitHigh", "AngleLimitEnable", "AngleLimitLow", "AngleLimitHigh",
        "ScoreLimitEnable", "ScoreLimitLow", "ScoreLimitHigh", "ScaleLimitEnable", "ScaleLimitLow", "ScaleLimitHigh",
        "ScaleXLimitEnable", "ScaleXLimitLow", "ScaleXLimitHigh", "ScaleYLimitEnable", "ScaleYLimitLow", "ScaleYLimitHigh",
        "MatchPointXLimitEnable", "MatchPointXLimitLow", "MatchPointXLimitHigh",
        "MatchPointYLimitEnable", "MatchPointYLimitLow", "MatchPointYLimitHigh",
        "BoxPointXLimitEnable", "BoxPointXLimitLow", "BoxPointXLimitHigh",
        "BoxPointYLimitEnable", "BoxPointYLimitLow", "BoxPointYLimitHigh", "OKWhenNumIsZero"
    };

    /// <summary>已删除的原厂参数：HALCON 无对应语义（链长/建模类型/横切角度/运行期阈值/噪点开关）。</summary>
    private static readonly string[] RemovedOriginalParameters =
    {
        "ChainFlag", "MinChainLen", "MaskModelFlag", "MatchThresholdFlag", "MatchThresholdHigh",
        "SpotterFlag", "SkewXStart", "SkewXEnd"
    };

    [Fact]
    public void Parameters_follow_halcon_capabilities()
    {
        var names = ContourMatchFields.All.Select(field => field.VirtualMachineName).ToHashSet(StringComparer.Ordinal);
        foreach (var name in HalconParameters) Assert.Contains(name, names);
        foreach (var name in RemovedOriginalParameters) Assert.DoesNotContain(name, names);
        Assert.Contains("TrainingRois", ContourMatchFields.All.Select(field => field.Name));
        Assert.Equal(SunEyeVision.Plugin.Contracts.RoiDocumentValue.Declaration,
            ContourMatchFields.Schema.Inputs.Single(port => port.Id == ContourMatchFields.TrainingRois).Type);
    }

    [Fact]
    public void Field_defaults_match_the_original_sources()
    {
        // 原厂 AlgorithmTab.xml 原件默认值（2026-11-22 补证：MinScore=0.8、RangeAngleLimit=0、Range*PointLimit=0）
        Assert.Equal(-45, ContourMatchFields.SpecByVmName("AngleStart").Default);
        Assert.Equal(45, ContourMatchFields.SpecByVmName("AngleEnd").Default);
        Assert.Equal(0.8, ContourMatchFields.SpecByVmName("MinScore").Default);
        Assert.Equal(1, ContourMatchFields.SpecByVmName("MaxMatchNum").Default);
        Assert.Equal(50, ContourMatchFields.SpecByVmName("MaxOverlap").Default);
        Assert.Equal(50, ContourMatchFields.SpecByVmName("MultiModelMaxOverlap").Default);
        Assert.Equal(1, ContourMatchFields.SpecByVmName("Polarity").Default);
        Assert.Equal(2, ContourMatchFields.SpecByVmName("SortType").Default is ContourMatchSortType sort ? (int)sort : -1);
        Assert.Equal(99_999, ContourMatchFields.SpecByVmName("NumLimitHigh").Default);
        Assert.Equal(0d, ContourMatchFields.SpecByVmName("AngleLimitLow").Default);
        Assert.Equal(0d, ContourMatchFields.SpecByVmName("MatchPointXLimitLow").Default);
        Assert.Equal(0d, ContourMatchFields.SpecByVmName("BoxPointXLimitLow").Default);
        Assert.Equal(true, ContourMatchFields.SpecByVmName("OutLineEnable").Default);
        Assert.Equal(false, ContourMatchFields.SpecByVmName("OutLinePointEnable").Default);
        Assert.Equal(ContourMatchRangeScaleType.Range, ContourMatchFields.SpecByVmName("RangeScaleType").Default);

        // HALCON 创建/匹配参数默认值
        Assert.Equal(ContourMatchPyramidMode.Auto, ContourMatchFields.SpecByVmName("PyramidScaleFlag").Default);
        Assert.Equal(5, ContourMatchFields.SpecByVmName("PyramidScaleLevel").Default);
        Assert.Equal(1, ContourMatchFields.SpecByVmName("PyramidScaleRLevel").Default);
        Assert.Equal(ContourMatchThresholdMode.Auto, ContourMatchFields.SpecByVmName("EdgeThresholdFlag").Default);
        Assert.Equal(15, ContourMatchFields.SpecByVmName("EdgeThreshold").Default);
        Assert.Equal(5, ContourMatchFields.SpecByVmName("MinContrast").Default);
        Assert.Equal(ContourMatchOptimization.Auto, ContourMatchFields.SpecByVmName("Optimization").Default);
        Assert.Equal(0.9, ContourMatchFields.SpecByVmName("Greediness").Default);
        Assert.Equal(ContourMatchSubPixel.LeastSquares, ContourMatchFields.SpecByVmName("SubPixel").Default);
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

        // HALCON 原生参数：最小对比度 1~255、贪婪度 0~1。
        var minContrast = ContourMatchFields.Spec(nameof(ContourMatchSettings.MinContrast));
        Assert.Equal(1d, minContrast.Minimum);
        Assert.Equal(255d, minContrast.Maximum);
        var greediness = ContourMatchFields.Spec(nameof(ContourMatchSettings.Greediness));
        Assert.Equal(0d, greediness.Minimum);
        Assert.Equal(1d, greediness.Maximum);

        Assert.Equal(new[] { "手动", "自动" }, ContourMatchFields.Spec(nameof(ContourMatchSettings.PyramidMode)).ChoiceLabels.Values);
        Assert.Equal(5, ContourMatchFields.Spec(nameof(ContourMatchSettings.Optimization)).Choices.Count);
        Assert.Equal(3, ContourMatchFields.Spec(nameof(ContourMatchSettings.SubPixel)).Choices.Count);
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
