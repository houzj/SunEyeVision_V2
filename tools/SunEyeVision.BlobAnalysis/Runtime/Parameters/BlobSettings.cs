using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public enum BlobConnectivity { Connected_8 = 8, Connected_4 = 4 }
public enum BlobThresholdMode { Binary, BinaryInv }
public enum BlobSortFeature { SortFeatureArea = 1, SortFeaturePerimeter = 2, SortFeatureCircularity = 3, SortFeatureRect = 4, SortFeatureCentroidX = 5, SortFeatureCentroidY = 6, SortFeatureRectX = 10, SortFeatureRectY = 11 }
public enum BlobSortMode { SortModeAscend = 1, SortModeDecend = 2, SortModeNotSort = 3 }
public enum BlobSegmentationMode { SingleThreshold, DoubleThreshold, None, AutoThreshold, FixedSoftThreshold, RelativeSoftThreshold }

/// <summary>Business settings only: no bindings, brushes, view state, backend handles or writable globals.</summary>
public sealed record BlobSettings : IToolSettings<BlobSettings>
{
    public BlobSettings() : this(true) { }
    internal BlobSettings(bool initializeDefaults)
    {
        if (!initializeDefaults) return;
        var definition=BlobParameterDefinition.Instance;
        FixedSoftLowerThreshold=definition.Default<int>(nameof(FixedSoftLowerThreshold));
        FixedSoftUpperThreshold=definition.Default<int>(nameof(FixedSoftUpperThreshold));
        ThresholdSoftness=definition.Default<int>(nameof(ThresholdSoftness));
        RelativeLeftRatio=definition.Default<int>(nameof(RelativeLeftRatio));
        RelativeRightRatio=definition.Default<int>(nameof(RelativeRightRatio));
        RelativeLowerRatio=definition.Default<int>(nameof(RelativeLowerRatio));
        RelativeUpperRatio=definition.Default<int>(nameof(RelativeUpperRatio));
        Threshold=definition.Default<int>(nameof(Threshold));
        UpperThreshold=definition.Default<int>(nameof(UpperThreshold));
        SegmentationMode=definition.Default<BlobSegmentationMode>(nameof(SegmentationMode));
        Type=definition.Default<BlobThresholdMode>(nameof(Type));
        IsPositionCorrectionEnabled=definition.Default<bool>(nameof(IsPositionCorrectionEnabled));
        IsAreaFilterEnabled=definition.Default<bool>(nameof(IsAreaFilterEnabled));
        FilterAreaMin=definition.Default<int>(nameof(FilterAreaMin));
        FilterAreaMax=definition.Default<int>(nameof(FilterAreaMax));
        IsCircularityFilterEnabled=definition.Default<bool>(nameof(IsCircularityFilterEnabled));
        FilterCircularityMin=definition.Default<double>(nameof(FilterCircularityMin));
        FilterCircularityMax=definition.Default<double>(nameof(FilterCircularityMax));
        IsRectangularityFilterEnabled=definition.Default<bool>(nameof(IsRectangularityFilterEnabled));
        FilterRectangularityMin=definition.Default<double>(nameof(FilterRectangularityMin));
        FilterRectangularityMax=definition.Default<double>(nameof(FilterRectangularityMax));
        BlobCountLimit=definition.Default<int>(nameof(BlobCountLimit));
        OutputBlobImage=definition.Default<bool>(nameof(OutputBlobImage));
        OutputBinaryImage=definition.Default<bool>(nameof(OutputBinaryImage));
        OutputBoundary=definition.Default<bool>(nameof(OutputBoundary));
        SortMode=definition.Default<BlobSortMode>(nameof(SortMode));
        SortFeature=definition.Default<BlobSortFeature>(nameof(SortFeature));
        Connectivity=definition.Default<BlobConnectivity>(nameof(Connectivity));
    }

    [JsonRequired]
    public int Threshold { get; init; }
    [JsonRequired]
    public int FixedSoftLowerThreshold { get; init; }
    [JsonRequired]
    public int FixedSoftUpperThreshold { get; init; }
    [JsonRequired]
    public int ThresholdSoftness { get; init; }
    [JsonRequired]
    public int RelativeLeftRatio { get; init; }
    [JsonRequired]
    public int RelativeRightRatio { get; init; }
    [JsonRequired]
    public int RelativeLowerRatio { get; init; }
    [JsonRequired]
    public int RelativeUpperRatio { get; init; }
    [JsonRequired]
    public int UpperThreshold { get; init; }
    [JsonRequired]
    public BlobSegmentationMode SegmentationMode { get; init; }
    [JsonRequired]
    public BlobThresholdMode Type { get; init; }
    [JsonRequired]
    public bool IsPositionCorrectionEnabled { get; init; }
    [JsonRequired]
    public bool IsAreaFilterEnabled { get; init; }
    [JsonRequired]
    public int FilterAreaMin { get; init; }
    [JsonRequired]
    public int FilterAreaMax { get; init; }
    [JsonRequired]
    public bool IsCircularityFilterEnabled { get; init; }
    [JsonRequired]
    public double FilterCircularityMin { get; init; }
    [JsonRequired]
    public double FilterCircularityMax { get; init; }
    [JsonRequired]
    public bool IsRectangularityFilterEnabled { get; init; }
    [JsonRequired]
    public double FilterRectangularityMin { get; init; }
    [JsonRequired]
    public double FilterRectangularityMax { get; init; }

    [JsonRequired]
    public int BlobCountLimit { get; init; }
    [JsonRequired]
    public bool OutputBlobImage { get; init; }
    [JsonRequired]
    public bool OutputBinaryImage { get; init; }
    [JsonRequired]
    public bool OutputBoundary { get; init; }
    [JsonRequired]
    public BlobSortMode SortMode { get; init; }
    [JsonRequired]
    public BlobSortFeature SortFeature { get; init; }
    [JsonRequired]
    public BlobConnectivity Connectivity { get; init; }

    /// <summary>
    /// 完整 VM 参数闭包。现有 XML 字段是宿主的稳定字段合同，闭包保存 VM 已确认但尚未全部开放到公共字段树的成员。
    /// Freeze 会用当前宿主字段覆盖对应成员，避免两个模型产生漂移。
    /// </summary>
    [BlobNonField]
    public BlobVmFindParameters VmParameters { get; init; } = new();

    public BlobVmFindParameters ToVmParameters() => BlobVmFindParameters.FromSettings(this);
    public BlobSettings Freeze() => this with { VmParameters = ToVmParameters() };

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Check(bool condition, string message) { if (!condition) errors.Add(new("BLOB_SETTINGS_INVALID", message)); }
        var definition = BlobParameterDefinition.Instance;
        bool Number(string field, double value)
        {
            var valid = double.IsFinite(value) && value >= definition.Minimum(field) && value <= definition.Maximum(field);
            if (!valid) errors.Add(new("BLOB_SETTINGS_INVALID", $"{definition.Label(field)}必须在{definition.Minimum(field)}至{definition.Maximum(field)}之间。", field));
            return valid;
        }
        Check(Enum.IsDefined(SegmentationMode) && Enum.IsDefined(Type) && Enum.IsDefined(SortMode) && Enum.IsDefined(SortFeature) && Enum.IsDefined(Connectivity), "不支持的Blob分割、极性或排序模式。");
        if (SegmentationMode != BlobSegmentationMode.None) Number(nameof(Threshold), Threshold);
        if (SegmentationMode == BlobSegmentationMode.DoubleThreshold)
        {
            Number(nameof(UpperThreshold), UpperThreshold);
        }
        Number(nameof(FixedSoftLowerThreshold),FixedSoftLowerThreshold);
        Number(nameof(FixedSoftUpperThreshold),FixedSoftUpperThreshold);
        Number(nameof(ThresholdSoftness),ThresholdSoftness);
        Number(nameof(RelativeLeftRatio),RelativeLeftRatio);
        Number(nameof(RelativeRightRatio),RelativeRightRatio);
        Number(nameof(RelativeLowerRatio),RelativeLowerRatio);
        Number(nameof(RelativeUpperRatio),RelativeUpperRatio);
        Number(nameof(BlobCountLimit), BlobCountLimit);
        // 仅存在于VM闭包的四项参数（填充面积阈值、最大内接矩形数量、最小重叠率、最大超界像素）同样受XML上下限约束。
        var closure = VmParameters ?? new BlobVmFindParameters();
        Number("HoleMinArea", closure.HoleMinArea);
        Number("MaxAreaInnerRectNum", closure.MaxAreaInnerRectNum);
        Number("OverlapRatio", closure.OverlapRatio);
        Number("MaxOutPixelNum", closure.MaxOutPixelNum);
        void Range(bool enabled, string minField, double min, string maxField, double max)
        {
            Number(minField, min); Number(maxField, max);
            if(enabled&&minField!=nameof(FilterAreaMin))Check(min <= max, "筛选下限不能超过上限。");
        }
        Range(IsAreaFilterEnabled, nameof(FilterAreaMin), FilterAreaMin, nameof(FilterAreaMax), FilterAreaMax);
        Range(IsCircularityFilterEnabled, nameof(FilterCircularityMin), FilterCircularityMin, nameof(FilterCircularityMax), FilterCircularityMax);
        Range(IsRectangularityFilterEnabled, nameof(FilterRectangularityMin), FilterRectangularityMin, nameof(FilterRectangularityMax), FilterRectangularityMax);
        return errors;
    }
}
