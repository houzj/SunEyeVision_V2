namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Blob运行参数的中性快照。宿主资源和ROI不在此状态中。</summary>
public enum BlobThresholdType { BinaryImage = 1, SingleThreshold, DoubleThreshold, AutoThreshold, FixedSoftThreshold, RelativeSoftThreshold }
public enum BlobPolarity { DarkOnBright, BrightOnDark }
public enum BlobContourType { NoneContour, NoneSequentialContour, SequentialContour }
public sealed record BlobFindParameters
{
    public BlobThresholdType ThresholdType { get; init; } = BlobThresholdType.SingleThreshold;
    public BlobPolarity Polarity { get; init; } = BlobPolarity.DarkOnBright;
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
    public BlobContourType BlobContourType { get; init; } = BlobContourType.NoneContour;
    public bool BolbImageEnable { get; init; } = false;
    public bool BinaryImageEnable { get; init; } = false;
    public bool SelectByHistInfo { get; init; } = false;
    public bool SelectByBoxAngle { get; init; } = false;
    public int MinBoxAngle { get; init; } = -9000;
    public int MaxBoxAngle { get; init; } = 9000;
    public bool SelectByRectWidth { get; init; } = false;
    public int MinRectWidth { get; init; } = 100;
    public int MaxRectWidth { get; init; } = 200;
    public bool SelectByRectHeight { get; init; } = false;
    public int MinRectHeight { get; init; } = 100;
    public int MaxRectHeight { get; init; } = 200;
    public bool SelectByPerimeter { get; init; } = false;
    public int MinPerimeter { get; init; } = 10;
    public int MaxPerimeter { get; init; } = 999999999;
    public bool SelectByShortAxis { get; init; } = false;
    public int MinShortAxis { get; init; } = 1;
    public int MaxShortAxis { get; init; } = 999999999;
    public bool SelectByLongAxis { get; init; } = false;
    public int MinLongAxis { get; init; } = 10;
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
    public BlobSortFeature SortFeature { get; init; } = BlobSortFeature.SortFeatureArea;
    public BlobSortMode SortMode { get; init; } = BlobSortMode.SortModeDecend;
    public BlobConnectivity Connectivity { get; init; } = BlobConnectivity.Connected_8;
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

    public static BlobFindParameters FromSettings(BlobSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var vm = settings.FindParameters ?? new BlobFindParameters();
        // The active original window declares only the three-valued contour mode.
        // Keep that mode unchanged through host projection and Freeze.
        return vm with
        {
            ThresholdType = settings.SegmentationMode switch { BlobSegmentationMode.None => BlobThresholdType.BinaryImage, BlobSegmentationMode.DoubleThreshold => BlobThresholdType.DoubleThreshold, BlobSegmentationMode.AutoThreshold => BlobThresholdType.AutoThreshold, BlobSegmentationMode.FixedSoftThreshold => BlobThresholdType.FixedSoftThreshold, BlobSegmentationMode.RelativeSoftThreshold => BlobThresholdType.RelativeSoftThreshold, _ => BlobThresholdType.SingleThreshold },
            Polarity = settings.Type == BlobThresholdMode.BinaryInv ? BlobPolarity.BrightOnDark : BlobPolarity.DarkOnBright,
            LowThreshold = settings.Threshold, HightThreshold = settings.UpperThreshold,
            LowSoftThreshold = settings.FixedSoftLowerThreshold, HightSoftThreshold = settings.FixedSoftUpperThreshold, Softness = settings.ThresholdSoftness,
            SoftLeftRatio = settings.RelativeLeftRatio, SoftRightRatio = settings.RelativeRightRatio, SoftLowRatio = settings.RelativeLowerRatio, SoftHighRatio = settings.RelativeUpperRatio,
            FindNum = settings.BlobCountLimit, SelectByArea = settings.IsAreaFilterEnabled, MinArea = settings.FilterAreaMin, MaxArea = settings.FilterAreaMax,
            BolbImageEnable = settings.OutputBlobImage, BinaryImageEnable = settings.OutputBinaryImage,
            SelectByCircularuty = settings.IsCircularityFilterEnabled, MinCircularity = settings.FilterCircularityMin, MaxCircularity = settings.FilterCircularityMax,
            SelectByRectangularity = settings.IsRectangularityFilterEnabled, MinRectangularity = settings.FilterRectangularityMin, MaxRectangularity = settings.FilterRectangularityMax,
            SortFeature = (BlobSortFeature)(int)settings.SortFeature, SortMode = (BlobSortMode)(int)settings.SortMode, Connectivity = (BlobConnectivity)(int)settings.Connectivity
        };
    }

    public BlobSettings ApplyTo(BlobSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var result = settings with
        {
            SegmentationMode = ThresholdType switch { BlobThresholdType.BinaryImage => BlobSegmentationMode.None, BlobThresholdType.DoubleThreshold => BlobSegmentationMode.DoubleThreshold, BlobThresholdType.AutoThreshold => BlobSegmentationMode.AutoThreshold, BlobThresholdType.FixedSoftThreshold => BlobSegmentationMode.FixedSoftThreshold, BlobThresholdType.RelativeSoftThreshold => BlobSegmentationMode.RelativeSoftThreshold, _ => BlobSegmentationMode.SingleThreshold },
            Type = Polarity == BlobPolarity.BrightOnDark ? BlobThresholdMode.BinaryInv : BlobThresholdMode.Binary,
            Threshold = LowThreshold, UpperThreshold = HightThreshold, FixedSoftLowerThreshold = LowSoftThreshold, FixedSoftUpperThreshold = HightSoftThreshold, ThresholdSoftness = Softness,
            RelativeLeftRatio = SoftLeftRatio, RelativeRightRatio = SoftRightRatio, RelativeLowerRatio = SoftLowRatio, RelativeUpperRatio = SoftHighRatio, BlobCountLimit = FindNum,
            IsAreaFilterEnabled = SelectByArea, FilterAreaMin = MinArea, FilterAreaMax = MaxArea, OutputBlobImage = BolbImageEnable, OutputBinaryImage = BinaryImageEnable,
            IsCircularityFilterEnabled = SelectByCircularuty, FilterCircularityMin = MinCircularity, FilterCircularityMax = MaxCircularity, IsRectangularityFilterEnabled = SelectByRectangularity, FilterRectangularityMin = MinRectangularity, FilterRectangularityMax = MaxRectangularity,
            SortFeature = (BlobSortFeature)(int)SortFeature, SortMode = (BlobSortMode)(int)SortMode, Connectivity = (BlobConnectivity)(int)Connectivity, FindParameters = this
        };
        return result;
    }

    internal static bool InRange(double value, double low, double high) => value >= low && value <= high;
}

