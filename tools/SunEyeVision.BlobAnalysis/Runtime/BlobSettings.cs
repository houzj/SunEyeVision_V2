using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public enum BlobThresholdMode { Binary, BinaryInv }
public enum BlobSort { None, AreaAsc, AreaDesc, CircularityAsc, CircularityDesc, RectangularityAsc, RectangularityDesc, XAsc, XDesc, YAsc, YDesc }
public enum BlobPreprocess { None, Mean, Gaussian, Median, HistogramEqualization }
public enum BlobMorphology { Erosion, Dilation, Opening, Closing, TopHat, BottomHat }
public enum BlobElementShape { Rectangle, Circle }
public enum BlobJudgmentKind { Range, Empty, Boolean }
public sealed record BlobJudgment(string Key, bool PerItem, bool Enabled, double Minimum, double Maximum, BlobJudgmentKind Kind = BlobJudgmentKind.Range);

/// <summary>Business settings only: no bindings, brushes, view state, backend handles or writable globals.</summary>
public sealed record BlobSettings : IToolSettings<BlobSettings>
{
    public int Threshold { get; init; } = 128;
    public int MaxValue { get; init; } = 255;
    public BlobThresholdMode Type { get; init; }
    public bool IsPositionCorrectionEnabled { get; init; }
    public bool IsPreprocessingEnabled { get; init; }
    public BlobPreprocess PreprocessMethod { get; init; }
    public int PreprocessKernelSize { get; init; } = 5;
    public bool IsMorphologyEnabled { get; init; }
    public BlobMorphology MorphOperation { get; init; }
    public BlobElementShape MorphElementShape { get; init; }
    public int MorphElementWidth { get; init; } = 5;
    public int MorphElementHeight { get; init; } = 5;
    public int MorphIterations { get; init; } = 1;
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
        Check(Threshold >= 0 && Threshold <= MaxValue && MaxValue <= 255, "阈值必须满足0≤Threshold≤MaxValue≤255。");
        Check(Enum.IsDefined(Type) && Enum.IsDefined(SortMode) && Enum.IsDefined(PreprocessMethod) && Enum.IsDefined(MorphOperation) && Enum.IsDefined(MorphElementShape), "不支持的Blob模式。");
        Check(PreprocessKernelSize > 0 && PreprocessKernelSize % 2 == 1, "预处理核必须为正奇数。");
        Check(MorphElementWidth > 0 && MorphElementHeight > 0 && MorphIterations > 0, "形态学尺寸和次数必须为正数。");
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
