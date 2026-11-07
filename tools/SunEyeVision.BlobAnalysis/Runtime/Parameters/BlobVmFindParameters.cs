namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>VM IMVSBlobFindModu 的参数闭包。类型和字段来自 BlobFindParam.cs，宿主资源和 ROI 不在此状态中。</summary>
public enum BlobVmThresholdType { BinaryImage = 1, SingleThreshold, DoubleThreshold, AutoThreshold, FixedSoftThreshold, RelativeSoftThreshold }
public enum BlobVmPolarity { DarkOnBright, BrightOnDark }
public enum BlobVmContourType { NoneContour, NoneSequentialContour, SequentialContour }
public enum BlobVmSortFeature { SortFeatureArea = 1, SortFeaturePerimeter, SortFeatureCircularity, SortFeatureRect, SortFeatureCentroidX, SortFeatureCentroidY, SortFeatureBoxAngle, SortFeatureBoxWidth, SortFeatureBoxHeight, SortFeatureRectX, SortFeatureRectY, SortFeatureAxisAngle, SortFeatureAxisRatio }
public enum BlobVmSortMode { SortModeAscend = 1, SortModeDecend, SortModeNotSort }
public enum BlobVmConnectivity { Connected_8 = 8, Connected_4 = 4 }

public sealed record BlobVmFindParameters
{
    public BlobVmThresholdType ThresholdType { get; init; } = BlobVmThresholdType.SingleThreshold;
    public BlobVmPolarity Polarity { get; init; } = BlobVmPolarity.DarkOnBright;
    public int LowThreshold { get; init; } = 0;
    public int HightThreshold { get; init; } = 0;
    public int LowSoftThreshold { get; init; } = 0;
    public int HightSoftThreshold { get; init; } = 0;
    public int Softness { get; init; } = 0;
    public int SoftLeftRatio { get; init; } = 0;
    public int SoftRightRatio { get; init; } = 0;
    public int SoftLowRatio { get; init; } = 0;
    public int SoftHighRatio { get; init; } = 0;
    public int FindNum { get; init; } = 0;
    public int HoleMinArea { get; init; } = 0;
    public bool SelectByArea { get; init; } = false;
    public int MinArea { get; init; } = 0;
    public int MaxArea { get; init; } = 0;
    public bool BolbOutLineEnable { get; init; } = false;
    public BlobVmContourType BlobContourType { get; init; } = BlobVmContourType.NoneContour;
    public bool BolbImageEnable { get; init; } = false;
    public bool BinaryImageEnable { get; init; } = false;
    public bool SelectByHistInfo { get; init; } = false;
    public bool SelectByBoxAngle { get; init; } = false;
    public int MinBoxAngle { get; init; } = -9000;
    public int MaxBoxAngle { get; init; } = 9000;
    public bool SelectByRectWidth { get; init; } = false;
    public int MinRectWidth { get; init; } = 1;
    public int MaxRectWidth { get; init; } = 1000000;
    public bool SelectByRectHeight { get; init; } = false;
    public int MinRectHeight { get; init; } = 1;
    public int MaxRectHeight { get; init; } = 1000000;
    public bool SelectByPerimeter { get; init; } = false;
    public int MinPerimeter { get; init; } = 1;
    public int MaxPerimeter { get; init; } = 999999999;
    public bool SelectByShortAxis { get; init; } = false;
    public int MinShortAxis { get; init; } = 1;
    public int MaxShortAxis { get; init; } = 999999999;
    public bool SelectByLongAxis { get; init; } = false;
    public int MinLongAxis { get; init; } = 1;
    public int MaxLongAxis { get; init; } = 999999999;
    public bool SelectByCircularuty { get; init; } = false;
    public double MinCircularity { get; init; } = 0d;
    public double MaxCircularity { get; init; } = 0d;
    public bool SelectByRectangularity { get; init; } = false;
    public double MinRectangularity { get; init; } = 0d;
    public double MaxRectangularity { get; init; } = 0d;
    public bool SelectByCentraBias { get; init; } = false;
    public double MinCenterBias { get; init; } = 0d;
    public double MaxCenterBias { get; init; } = 9999999.99d;
    public bool AxisRatioEnable { get; init; } = false;
    public double MinAxisRatio { get; init; } = 0.01d;
    public double MaxAxisRatio { get; init; } = 1d;
    public BlobVmSortFeature SortFeature { get; init; } = BlobVmSortFeature.SortFeatureArea;
    public BlobVmSortMode SortMode { get; init; } = BlobVmSortMode.SortModeDecend;
    public BlobVmConnectivity Connectivity { get; init; } = BlobVmConnectivity.Connected_8;
    public int MaxAreaInnerRectNum { get; init; } = 0;
    public int OverlapRatio { get; init; } = 0;
    public bool MaxOutPixelNumEnable { get; init; } = false;
    public int MaxOutPixelNum { get; init; } = 0;
    public bool OKWhenNumIsZero { get; init; } = false;
    public bool BlobNumLimitEnable { get; init; } = false;
    public int BlobNumLimitLow { get; init; } = 1;
    public int BlobNumLimitHigh { get; init; } = 99999;
    public bool BlobAreaLimitEnable { get; init; } = false;
    public double BlobAreaLimitLow { get; init; } = 1d;
    public double BlobAreaLimitHigh { get; init; } = 999999999d;
    public bool BlobTotalAreaLimitEnable { get; init; } = false;
    public double BlobTotalAreaLimitLow { get; init; } = 1d;
    public double BlobTotalAreaLimitHigh { get; init; } = 999999999d;
    public bool CentroidXLimitEnable { get; init; } = false;
    public double CentroidXLimitLow { get; init; } = -99999;
    public double CentroidXLimitHigh { get; init; } = 99999d;
    public bool CentroidYLimitEnable { get; init; } = false;
    public double CentroidYLimitLow { get; init; } = -99999;
    public double CentroidYLimitHigh { get; init; } = 99999d;
    public bool AngleLimitEnable { get; init; } = false;
    public double AngleLimitLow { get; init; } = -180;
    public double AngleLimitHigh { get; init; } = 180d;
    public bool CenterXLimitEnable { get; init; } = false;
    public double CenterXLimitLow { get; init; } = -99999;
    public double CenterXLimitHigh { get; init; } = 99999d;
    public bool CenterYLimitEnable { get; init; } = false;
    public double CenterYLimitLow { get; init; } = -99999;
    public double CenterYLimitHigh { get; init; } = 99999d;
    public bool BoxHeightLimitEnable { get; init; } = false;
    public double BoxHeightLimitLow { get; init; } = 1d;
    public double BoxHeightLimitHigh { get; init; } = 99999d;
    public bool BoxWidthLimitEnable { get; init; } = false;
    public double BoxWidthLimitLow { get; init; } = 1d;
    public double BoxWidthLimitHigh { get; init; } = 99999d;
    public bool PerimeterLimitEnable { get; init; } = false;
    public double PerimeterLimitLow { get; init; } = 1d;
    public double PerimeterLimitHigh { get; init; } = 999999999d;
    public bool LongAxisLimitEnable { get; init; } = false;
    public double LongAxisLimitLow { get; init; } = 1d;
    public double LongAxisLimitHigh { get; init; } = 999999999d;
    public bool ShortAxisLimitEnable { get; init; } = false;
    public double ShortAxisLimitLow { get; init; } = 1d;
    public double ShortAxisLimitHigh { get; init; } = 999999999d;
    public bool CircularityLimitEnable { get; init; } = false;
    public double CircularityLimitLow { get; init; } = 0d;
    public double CircularityLimitHigh { get; init; } = 1d;
    public bool RectangularityLimitEnable { get; init; } = false;
    public double RectangularityLimitLow { get; init; } = 0d;
    public double RectangularityLimitHigh { get; init; } = 1d;

    public static BlobVmFindParameters FromSettings(BlobSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var vm = settings.VmParameters ?? new BlobVmFindParameters();
        return vm with
        {
            ThresholdType = settings.SegmentationMode switch { BlobSegmentationMode.None => BlobVmThresholdType.BinaryImage, BlobSegmentationMode.DoubleThreshold => BlobVmThresholdType.DoubleThreshold, BlobSegmentationMode.AutoThreshold => BlobVmThresholdType.AutoThreshold, BlobSegmentationMode.FixedSoftThreshold => BlobVmThresholdType.FixedSoftThreshold, BlobSegmentationMode.RelativeSoftThreshold => BlobVmThresholdType.RelativeSoftThreshold, _ => BlobVmThresholdType.SingleThreshold },
            Polarity = settings.Type == BlobThresholdMode.BinaryInv ? BlobVmPolarity.BrightOnDark : BlobVmPolarity.DarkOnBright,
            LowThreshold = settings.Threshold, HightThreshold = settings.UpperThreshold,
            LowSoftThreshold = settings.FixedSoftLowerThreshold, HightSoftThreshold = settings.FixedSoftUpperThreshold, Softness = settings.ThresholdSoftness,
            SoftLeftRatio = settings.RelativeLeftRatio, SoftRightRatio = settings.RelativeRightRatio, SoftLowRatio = settings.RelativeLowerRatio, SoftHighRatio = settings.RelativeUpperRatio,
            FindNum = settings.BlobCountLimit, SelectByArea = settings.IsAreaFilterEnabled, MinArea = settings.FilterAreaMin, MaxArea = settings.FilterAreaMax,
            BolbOutLineEnable = settings.OutputBoundary, BlobContourType = settings.OutputBoundary ? BlobVmContourType.SequentialContour : BlobVmContourType.NoneContour,
            BolbImageEnable = settings.OutputBlobImage, BinaryImageEnable = settings.OutputBinaryImage,
            SelectByCircularuty = settings.IsCircularityFilterEnabled, MinCircularity = settings.FilterCircularityMin, MaxCircularity = settings.FilterCircularityMax,
            SelectByRectangularity = settings.IsRectangularityFilterEnabled, MinRectangularity = settings.FilterRectangularityMin, MaxRectangularity = settings.FilterRectangularityMax,
            SortFeature = (BlobVmSortFeature)(int)settings.SortFeature, SortMode = (BlobVmSortMode)(int)settings.SortMode, Connectivity = (BlobVmConnectivity)(int)settings.Connectivity
        };
    }

    public BlobSettings ApplyTo(BlobSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var result = settings with
        {
            SegmentationMode = ThresholdType switch { BlobVmThresholdType.BinaryImage => BlobSegmentationMode.None, BlobVmThresholdType.DoubleThreshold => BlobSegmentationMode.DoubleThreshold, BlobVmThresholdType.AutoThreshold => BlobSegmentationMode.AutoThreshold, BlobVmThresholdType.FixedSoftThreshold => BlobSegmentationMode.FixedSoftThreshold, BlobVmThresholdType.RelativeSoftThreshold => BlobSegmentationMode.RelativeSoftThreshold, _ => BlobSegmentationMode.SingleThreshold },
            Type = Polarity == BlobVmPolarity.BrightOnDark ? BlobThresholdMode.BinaryInv : BlobThresholdMode.Binary,
            Threshold = LowThreshold, UpperThreshold = HightThreshold, FixedSoftLowerThreshold = LowSoftThreshold, FixedSoftUpperThreshold = HightSoftThreshold, ThresholdSoftness = Softness,
            RelativeLeftRatio = SoftLeftRatio, RelativeRightRatio = SoftRightRatio, RelativeLowerRatio = SoftLowRatio, RelativeUpperRatio = SoftHighRatio, BlobCountLimit = FindNum,
            IsAreaFilterEnabled = SelectByArea, FilterAreaMin = MinArea, FilterAreaMax = MaxArea, OutputBoundary = BolbOutLineEnable || BlobContourType != BlobVmContourType.NoneContour, OutputBlobImage = BolbImageEnable, OutputBinaryImage = BinaryImageEnable,
            IsCircularityFilterEnabled = SelectByCircularuty, FilterCircularityMin = MinCircularity, FilterCircularityMax = MaxCircularity, IsRectangularityFilterEnabled = SelectByRectangularity, FilterRectangularityMin = MinRectangularity, FilterRectangularityMax = MaxRectangularity,
            SortFeature = (BlobSortFeature)(int)SortFeature, SortMode = (BlobSortMode)(int)SortMode, Connectivity = (BlobConnectivity)(int)Connectivity, VmParameters = this
        };
        return result;
    }

    internal static bool InRange(double value, double low, double high) => value >= low && value <= high;
}

