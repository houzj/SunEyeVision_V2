using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class BlobAnalysisParameterContractTests
{
    [Fact]
    public void Contract_declares_neutral_inputs_and_XML_settings()
    {
        var tool=new BlobTool();var settings=new BlobSettings();
        Assert.Empty(tool.Validate(settings));
        Assert.Equal(new[]{"input.image","input.inspection.region","input.mask.region","input.pose","input.roi.local",
            "input.roi.inheritance-mode","input.roi.rectangle","input.roi.annulus",
            "input.roi.rectangle.center-x","input.roi.rectangle.center-y","input.roi.rectangle.width","input.roi.rectangle.height","input.roi.rectangle.angle-degrees",
            "input.roi.annulus.center-x","input.roi.annulus.center-y","input.roi.annulus.inner-radius","input.roi.annulus.outer-radius","input.roi.annulus.start-degrees","input.roi.annulus.sweep-degrees"},tool.Describe().Inputs.Select(port=>port.Id));
        Assert.Equal(55,tool.SettingsSchema.Inputs.Count); // 21 settings + 12 judgment switches + 22 range bounds.
        Assert.Contains(tool.SettingsSchema.Presentation!.Fields,field=>field.DisplayName==BlobParameterDefinition.Instance.Label(nameof(BlobSettings.FilterAreaMin)));
        var references=typeof(BlobTool).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
        Assert.DoesNotContain("SunEyeVision.Plugin.SDK",references);
        Assert.DoesNotContain("PresentationFramework",references);
        Assert.DoesNotContain("SunEyeVision.UI",references);
    }
    [Theory]
    [InlineData((int)BlobSegmentationMode.None)]
    [InlineData((int)BlobSegmentationMode.DoubleThreshold)]
    public void Unsupported_modes_preserve_settings_but_are_not_admitted(int modeValue)
    {
        var mode=(BlobSegmentationMode)modeValue;
        var tool=new BlobTool();
        var fields=new FieldSettings(tool.SettingsSchema.Inputs,
            tool.ReadSettings(new BlobSettings { SegmentationMode=mode }).CopyValues()
                .ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value)));
        Assert.Contains(tool.ValidateKnownSettings(fields),issue=>issue.Code=="BLOB_MODE_NOT_READY");
        Assert.Equal(mode,((BlobSettings)tool.MaterializeSettings(fields.Materialize(new Dictionary<string,object?>()))).SegmentationMode);
    }
    [Fact]
    public void Validation_rejects_reversed_threshold_range()
    {
        var settings=new BlobSettings{Threshold=100,SegmentationMode=BlobSegmentationMode.DoubleThreshold,UpperThreshold=30};
        Assert.NotEmpty(settings.Validate());
    }
}
