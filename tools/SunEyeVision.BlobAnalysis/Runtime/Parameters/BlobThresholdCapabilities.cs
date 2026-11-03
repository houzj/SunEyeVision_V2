namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>One capability decision shared by formal editing and both execution entry points.</summary>
public static class BlobThresholdCapabilities
{
    public static bool IsSupported(BlobSegmentationMode mode)=>mode is BlobSegmentationMode.SingleThreshold or BlobSegmentationMode.DoubleThreshold
        or BlobSegmentationMode.AutoThreshold or BlobSegmentationMode.FixedSoftThreshold or BlobSegmentationMode.RelativeSoftThreshold or BlobSegmentationMode.None;
    public static bool IsSupported(string? mode)=>mode=="BinaryImage"||Enum.TryParse<BlobSegmentationMode>(mode,out var parsed)&&IsSupported(parsed);
    // Native mode 3 overwrites its local polarity from endpoint order; the saved Type is retained but ignored.
    internal static bool IsInverse(BlobSettings settings)=>settings.SegmentationMode==BlobSegmentationMode.DoubleThreshold
        ?settings.Threshold>settings.UpperThreshold:settings.Type==BlobThresholdMode.BinaryInv;
    public static bool IsThresholdFieldActive(BlobSegmentationMode mode,string property)=>property switch
    {
        nameof(BlobSettings.Threshold)=>mode is BlobSegmentationMode.SingleThreshold or BlobSegmentationMode.DoubleThreshold,
        nameof(BlobSettings.UpperThreshold)=>mode==BlobSegmentationMode.DoubleThreshold,
        nameof(BlobSettings.FixedSoftLowerThreshold) or nameof(BlobSettings.FixedSoftUpperThreshold)=>mode==BlobSegmentationMode.FixedSoftThreshold,
        nameof(BlobSettings.ThresholdSoftness)=>mode is BlobSegmentationMode.FixedSoftThreshold or BlobSegmentationMode.RelativeSoftThreshold,
        nameof(BlobSettings.RelativeLeftRatio) or nameof(BlobSettings.RelativeRightRatio) or nameof(BlobSettings.RelativeLowerRatio) or nameof(BlobSettings.RelativeUpperRatio)=>mode==BlobSegmentationMode.RelativeSoftThreshold,
        _=>false
    };
}
