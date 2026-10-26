using SunEyeVision.Tool.BlobAnalysis;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

public sealed class BlobAnalysisParameterContractTests
{
    [Fact]
    public void Contract_DeclaresRoiInputsAndRuntimeParameters()
    {
        var parameters = new BlobAnalysisParameters();

        var violations = parameters.ValidateParameterContract();

        Assert.Empty(violations);
        Assert.Equal(
            new[] { "InspectionRois", "MaskRois", "InspectionRegionInput", "MaskRegionInput" },
            ((IInputSlotContract)parameters).GetInputSlots().Select(slot => slot.PropertyName));
        Assert.Contains(
            ((IRuntimeParameterContract)parameters).GetRuntimeParameters(),
            parameter => parameter.PropertyName == nameof(BlobAnalysisParameters.FilterAreaMin)
                && parameter.TypeVocabulary == "Double");
    }

    [Fact]
    public void Validate_RejectsReversedThresholdRange()
    {
        var parameters = new BlobAnalysisParameters
        {
            Threshold = 100,
            SegmentationMode = BlobSegmentationMode.DoubleThreshold,
            UpperThreshold = 30
        };

        var validation = parameters.Validate();

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("双阈值"));
    }
}
