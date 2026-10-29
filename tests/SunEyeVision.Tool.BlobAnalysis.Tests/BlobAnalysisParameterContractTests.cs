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
        Assert.Equal(new[]{BlobTool.ImageInput,BlobTool.InspectionInput,BlobTool.MaskInput,BlobTool.PoseInput},tool.Describe().Inputs.Select(port=>port.Id));
        Assert.Equal(54,tool.SettingsSchema.Inputs.Count);
        Assert.Contains(tool.SettingsSchema.Presentation!.Fields,field=>field.DisplayName==BlobParameterDefinition.Instance.Label(nameof(BlobSettings.FilterAreaMin)));
        var references=typeof(BlobTool).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
        Assert.DoesNotContain("SunEyeVision.Plugin.SDK",references);
        Assert.DoesNotContain("PresentationFramework",references);
        Assert.DoesNotContain("SunEyeVision.UI",references);
    }
    [Fact]
    public void Validation_rejects_reversed_threshold_range()
    {
        var settings=new BlobSettings{Threshold=100,SegmentationMode=BlobSegmentationMode.DoubleThreshold,UpperThreshold=30};
        Assert.NotEmpty(settings.Validate());
    }
}
