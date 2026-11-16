using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.BlobLabels.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>
/// 参数清单对照原件 <c>IMVSBlobFindLabelsModu.xml</c> 的 78 个 ParamItem：
/// 名称与默认值逐条核对，页签/分类/联动按 AlgorithmTab 证据核对。
/// </summary>
public sealed class BlobLabelsParameterContractTests
{
    private static readonly (string Name, string Default)[] OriginalParameters =
    {
        ("FindNum", "100"), ("SelectByArea", "False"), ("MinArea", "10"), ("MaxArea", "999999999"),
        ("BolbOutLineEnable", "False"), ("BolbImageEnable", "True"),
        ("SelectByPerimeter", "False"), ("MinPerimeter", "10"), ("MaxPerimeter", "999999999"),
        ("SelectByShortAxis", "False"), ("MinShortAxis", "1"), ("MaxShortAxis", "999999999"),
        ("SelectByLongAxis", "False"), ("MinLongAxis", "10"), ("MaxLongAxis", "999999999"),
        ("SelectByCircularuty", "False"), ("MinCircularity", "0.1"), ("MaxCircularity", "1"),
        ("SelectByRectangularity", "False"), ("MinRectangularity", "0.1"), ("MaxRectangularity", "1"),
        ("SelectByCentraBias", "False"), ("MinCenterBias", "0"), ("MaxCenterBias", "9999999.99"),
        ("AxisRatioEnable", "False"), ("MinAxisRatio", "0.01"), ("MaxAxisRatio", "1.0"),
        ("SortFeature", "1"), ("SortMode", "2"), ("connectivity", "8"),
        ("OverlapRatio", "0"), ("MaxOutPixelNumEnable", "False"), ("MaxOutPixelNum", "0"),
        ("BlobNumLimitEnable", "False"), ("BlobNumLimitLow", "1"), ("BlobNumLimitHigh", "99999"),
        ("BlobAreaLimitEnable", "False"), ("BlobAreaLimitLow", "1"), ("BlobAreaLimitHigh", "999999999"),
        ("BlobTotalAreaLimitEnable", "False"), ("BlobTotalAreaLimitLow", "1"), ("BlobTotalAreaLimitHigh", "999999999"),
        ("CentroidXLimitEnable", "False"), ("CentroidXLimitLow", "-99999"), ("CentroidXLimitHigh", "99999"),
        ("CentroidYLimitEnable", "False"), ("CentroidYLimitLow", "-99999"), ("CentroidYLimitHigh", "99999"),
        ("AngleLimitEnable", "False"), ("AngleLimitLow", "-180"), ("AngleLimitHigh", "180"),
        ("CenterXLimitEnable", "False"), ("CenterXLimitLow", "-99999"), ("CenterXLimitHigh", "99999"),
        ("CenterYLimitEnable", "False"), ("CenterYLimitLow", "-99999"), ("CenterYLimitHigh", "99999"),
        ("BoxHeightLimitEnable", "False"), ("BoxHeightLimitLow", "1"), ("BoxHeightLimitHigh", "99999"),
        ("BoxWidthLimitEnable", "False"), ("BoxWidthLimitLow", "1"), ("BoxWidthLimitHigh", "99999"),
        ("PerimeterLimitEnable", "False"), ("PerimeterLimitLow", "1"), ("PerimeterLimitHigh", "999999999"),
        ("LongAxisLimitEnable", "False"), ("LongAxisLimitLow", "1"), ("LongAxisLimitHigh", "999999999"),
        ("ShortAxisLimitEnable", "False"), ("ShortAxisLimitLow", "1"), ("ShortAxisLimitHigh", "999999999"),
        ("CircularityLimitEnable", "False"), ("CircularityLimitLow", "0"), ("CircularityLimitHigh", "1"),
        ("RectangularityLimitEnable", "False"), ("RectangularityLimitLow", "0"), ("RectangularityLimitHigh", "1")
    };

    [Fact]
    public void Original_parameter_list_is_declared_with_the_original_defaults()
    {
        Assert.Equal(78, OriginalParameters.Length);
        foreach (var (name, text) in OriginalParameters)
        {
            var spec = BlobLabelsFields.SpecByVmName(name);
            var expected = spec.Type == typeof(bool) ? bool.Parse(text)
                : spec.Type == typeof(int) ? int.Parse(text, System.Globalization.CultureInfo.InvariantCulture)
                : spec.Type == typeof(double) ? double.Parse(text, System.Globalization.CultureInfo.InvariantCulture)
                : spec.Type.IsEnum ? Enum.ToObject(spec.Type, int.Parse(text, System.Globalization.CultureInfo.InvariantCulture))
                : text;
            Assert.Equal(expected, spec.Default);
        }
    }

    [Fact]
    public void Declared_fields_have_stable_unique_identities()
    {
        var ids = BlobLabelsFields.All.Select(field => field.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^field:[0-9a-f]{32}$", id));
        Assert.Equal(BlobLabelsFields.IdOf(nameof(BlobLabelsSettings.FindCount)), BlobLabelsFields.IdOf(nameof(BlobLabelsSettings.FindCount)));
    }

    [Fact]
    public void Editor_schema_exposes_the_three_original_tabs()
    {
        var tool = new BlobLabelsTool();
        var schema = ResultDisplayFields.EditorSchema(tool);
        var pages = schema.Presentation!.Fields.Select(field => field.Layout!.Page).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "基本参数", "运行参数", "结果显示" }, pages);
    }

    [Fact]
    public void Original_categories_are_preserved()
    {
        var tool = new BlobLabelsTool();
        var schema = tool.SettingsSchema;
        var categories = schema.Presentation!.Fields
            .Select(field => (Page: field.Layout!.Page, Section: field.Layout.Section))
            .Distinct()
            .ToArray();
        Assert.Contains(("基本参数", "类别信息"), categories);
        Assert.Contains(("运行参数", "运行参数"), categories);
        Assert.Contains(("运行参数", "结果判断"), categories);
    }

    [Fact]
    public void Range_filters_pair_and_follow_their_enable_switch()
    {
        var tool = new BlobLabelsTool();
        var fields = tool.SettingsSchema.Presentation!.Fields.ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        var minimum = fields[BlobLabelsFields.AreaFilterMin];
        var maximum = fields[BlobLabelsFields.AreaFilterMax];
        Assert.Equal(BlobLabelsFields.AreaFilterMax, minimum.Layout!.PairWith);
        Assert.Equal("全局面积范围", minimum.Layout.PairDisplayName);
        Assert.Equal(BlobLabelsFields.AreaFilterEnabled, maximum.Layout!.VisibleWhen!.FieldId);
        Assert.Equal(true, maximum.Layout.VisibleWhen.Value);
        Assert.True(minimum.Layout.PairMinimum is not null && minimum.Layout.PairMaximum is not null);
    }

    [Fact]
    public void Advanced_groups_follow_the_original_expand_controls()
    {
        var tool = new BlobLabelsTool();
        var fields = tool.SettingsSchema.Presentation!.Fields.ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        Assert.True(fields[BlobLabelsFields.IdOf(nameof(BlobLabelsSettings.PerimeterFilterEnabled))].Layout!.Advanced);
        Assert.False(fields[BlobLabelsFields.AreaFilterEnabled].Layout!.Advanced);
        Assert.True(fields[BlobLabelsFields.IdOf(nameof(BlobLabelsSettings.CircularityJudgeEnabled))].Layout!.Advanced);
        Assert.False(fields[BlobLabelsFields.IdOf(nameof(BlobLabelsSettings.BlobCountJudgeEnabled))].Layout!.Advanced);
    }

    [Fact]
    public void Binding_names_cover_every_reference_enabled_field()
    {
        var tool = new BlobLabelsTool();
        var referenceFields = tool.SettingsSchema.Inputs
            .Where(port => (port.EditModes & FieldEditModes.Reference) != 0)
            .Select(port => port.Id)
            .ToArray();
        Assert.NotEmpty(referenceFields);
        Assert.All(referenceFields, id => Assert.True(tool.ParameterBindingNames.ContainsKey(id), id));
        foreach (var (fieldId, bindingName) in tool.ParameterBindingNames)
        {
            var spec = BlobLabelsFields.All.Single(item => item.Id == fieldId);
            Assert.Equal(spec.VirtualMachineName, bindingName);
        }
    }
}
