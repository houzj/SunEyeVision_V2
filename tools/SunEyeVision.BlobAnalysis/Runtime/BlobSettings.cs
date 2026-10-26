using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public enum BlobThresholdMode { Binary, BinaryInv }
public enum BlobSort { None, AreaAsc, AreaDesc, CircularityAsc, CircularityDesc, RectangularityAsc, RectangularityDesc, XAsc, XDesc, YAsc, YDesc }
public enum BlobSegmentationMode { SingleThreshold, DoubleThreshold, None }
public enum BlobJudgmentKind { Range, Empty, Boolean }
public sealed record BlobJudgment(string Key, bool PerItem, bool Enabled, double Minimum, double Maximum, BlobJudgmentKind Kind = BlobJudgmentKind.Range);

/// <summary>Business settings only: no bindings, brushes, view state, backend handles or writable globals.</summary>
public sealed record BlobSettings : IToolSettings<BlobSettings>
{
    public int Threshold { get; init; } = 128;
    public int UpperThreshold { get; init; } = 255;
    public BlobSegmentationMode SegmentationMode { get; init; }
    public BlobThresholdMode Type { get; init; }
    public bool IsPositionCorrectionEnabled { get; init; }
    public bool IsAreaFilterEnabled { get; init; } = true;
    public double FilterAreaMin { get; init; } = 10;
    public double FilterAreaMax { get; init; } = 9999999;
    public bool IsCircularityFilterEnabled { get; init; }
    public double FilterCircularityMin { get; init; }
    public double FilterCircularityMax { get; init; } = 1;
    public bool IsRectangularityFilterEnabled { get; init; }
    public double FilterRectangularityMin { get; init; }
    public double FilterRectangularityMax { get; init; } = 1;
    public bool IsBlobCountFilterEnabled { get; init; } = true;
    public int BlobCountLimit { get; init; } = 1000;
    public bool OutputGrayImage { get; init; } = true;
    public bool OutputBinaryImage { get; init; }
    public bool OutputBoundary { get; init; }
    public BlobSort SortMode { get; init; } = BlobSort.AreaDesc;
    public IReadOnlyList<BlobJudgment> Judgments { get; init; } = Array.Empty<BlobJudgment>();
    public BlobSettings Freeze() => this with { Judgments = Array.AsReadOnly(Judgments.Select(x => x with { }).ToArray()) };

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Check(bool condition, string message) { if (!condition) errors.Add(new("BLOB_SETTINGS_INVALID", message)); }
        Check(Enum.IsDefined(SegmentationMode) && Enum.IsDefined(Type) && Enum.IsDefined(SortMode), "不支持的Blob分割、极性或排序模式。");
        Check(SegmentationMode == BlobSegmentationMode.None || Threshold is >= 0 and <= 255, "阈值必须在0至255之间。");
        Check(SegmentationMode != BlobSegmentationMode.DoubleThreshold || UpperThreshold <= 255 && UpperThreshold >= Threshold, "双阈值必须满足0≤下限≤上限≤255。");
        Check(!IsBlobCountFilterEnabled || BlobCountLimit > 0, "数量限制必须为正数。");
        void Range(bool enabled, double min, double max, double? limit = null) => Check(!enabled || double.IsFinite(min) && double.IsFinite(max) && min >= 0 && min <= max && (limit is null || max <= limit), "筛选范围必须有限、有序且处于有效范围。");
        Range(IsAreaFilterEnabled, FilterAreaMin, FilterAreaMax);
        Range(IsCircularityFilterEnabled, FilterCircularityMin, FilterCircularityMax, 1);
        Range(IsRectangularityFilterEnabled, FilterRectangularityMin, FilterRectangularityMax, 1);
        Check(Judgments.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == Judgments.Count, "结果判定键不得重复。");
        foreach (var rule in Judgments)
        {
            Check(Enum.IsDefined(rule.Kind), "结果判定类型无效。");
            Check(!rule.Enabled || rule.Kind != BlobJudgmentKind.Range || double.IsFinite(rule.Minimum) && double.IsFinite(rule.Maximum) && rule.Minimum <= rule.Maximum, "结果判定范围无效。");
            Check(rule.PerItem ? rule.Key is "per-blob-area" or "per-blob-circularity" or "per-blob-rectangularity"
                : rule.Key is "output-not-empty" or "white-ratio" or "blob-count" or "total-area" or "average-area" or "max-area" or "min-area" or "average-circularity" or "average-rectangularity", "结果判定键与作用范围不匹配。");
        }
        return errors;
    }
}
