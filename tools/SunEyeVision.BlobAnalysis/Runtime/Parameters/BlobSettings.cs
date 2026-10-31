using System.Text.Json.Serialization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public enum BlobThresholdMode { Binary, BinaryInv }
public enum BlobSort { None, AreaAsc, AreaDesc, CircularityAsc, CircularityDesc, RectangularityAsc, RectangularityDesc, XAsc, XDesc, YAsc, YDesc }
public enum BlobSegmentationMode { SingleThreshold, DoubleThreshold, None }
public enum BlobJudgmentKind { Range, Empty, Boolean }
public sealed record BlobJudgment(
    [property: JsonRequired] string Key,
    [property: JsonRequired] bool PerItem,
    [property: JsonRequired] bool Enabled,
    [property: JsonRequired] double Minimum,
    [property: JsonRequired] double Maximum,
    [property: JsonRequired] BlobJudgmentKind Kind = BlobJudgmentKind.Range);

/// <summary>Business settings only: no bindings, brushes, view state, backend handles or writable globals.</summary>
public sealed record BlobSettings : IToolSettings<BlobSettings>
{
    public BlobSettings() : this(true) { }
    internal BlobSettings(bool initializeDefaults)
    {
        if (!initializeDefaults) { Judgments = Array.Empty<BlobJudgment>(); return; }
        var definition=BlobParameterDefinition.Instance;
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
        OutputGrayImage=definition.Default<bool>(nameof(OutputGrayImage));
        OutputBinaryImage=definition.Default<bool>(nameof(OutputBinaryImage));
        OutputBoundary=definition.Default<bool>(nameof(OutputBoundary));
        SortMode=definition.Default<BlobSort>(nameof(SortMode));
        Judgments=definition.CreateJudgments();
    }

    [JsonRequired]
    public int Threshold { get; init; }
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
    public bool OutputGrayImage { get; init; }
    [JsonRequired]
    public bool OutputBinaryImage { get; init; }
    [JsonRequired]
    public bool OutputBoundary { get; init; }
    [JsonRequired]
    public BlobSort SortMode { get; init; }
    [JsonRequired]
    public IReadOnlyList<BlobJudgment> Judgments { get; init; }
    public BlobSettings Freeze()
    {
        if (Judgments is null || Judgments.Any(x => x is null))
            throw new ArgumentException("Blob结果判定集合及条目不能为空。", nameof(Judgments));
        return this with { Judgments = Array.AsReadOnly(Judgments.Select(x => x with { }).ToArray()) };
    }

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
        Check(Enum.IsDefined(SegmentationMode) && Enum.IsDefined(Type) && Enum.IsDefined(SortMode), "不支持的Blob分割、极性或排序模式。");
        if (SegmentationMode != BlobSegmentationMode.None) Number(nameof(Threshold), Threshold);
        if (SegmentationMode == BlobSegmentationMode.DoubleThreshold)
        {
            Number(nameof(UpperThreshold), UpperThreshold);
            Check(UpperThreshold >= Threshold, "双阈值下限不能超过上限。");
        }
        Number(nameof(BlobCountLimit), BlobCountLimit);
        void Range(bool enabled, string minField, double min, string maxField, double max)
        {
            Number(minField, min); Number(maxField, max);
            if(enabled)Check(min <= max, "筛选下限不能超过上限。");
        }
        Range(IsAreaFilterEnabled, nameof(FilterAreaMin), FilterAreaMin, nameof(FilterAreaMax), FilterAreaMax);
        Range(IsCircularityFilterEnabled, nameof(FilterCircularityMin), FilterCircularityMin, nameof(FilterCircularityMax), FilterCircularityMax);
        Range(IsRectangularityFilterEnabled, nameof(FilterRectangularityMin), FilterRectangularityMin, nameof(FilterRectangularityMax), FilterRectangularityMax);
        if (Judgments is null || Judgments.Any(x => x is null))
        {
            errors.Add(new("BLOB_SETTINGS_INVALID", "Blob结果判定集合及条目不能为空。", nameof(Judgments)));
            return errors;
        }
        Check(Judgments.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == Judgments.Count, "结果判定键不得重复。");
        var declared = definition.CreateJudgments().ToDictionary(x => x.Key, StringComparer.Ordinal);
        Check(Judgments.Select(x => x.Key).ToHashSet(StringComparer.Ordinal).SetEquals(declared.Keys), "结果判定集合必须完整对应XML声明，不能缺项或增加未知项。");
        var metadata = definition.JudgmentMetadata.ToDictionary(x => (string)x.Attribute("key")!, StringComparer.Ordinal);
        foreach (var rule in Judgments)
        {
            if (rule.Key is null || !declared.TryGetValue(rule.Key, out var expected))
            {
                Check(false, "未知结果判定键。");
                continue;
            }
            Check(rule.Kind == expected.Kind && rule.PerItem == expected.PerItem, "结果判定类型或作用范围与XML声明不符。");
            if (rule.Enabled && rule.Kind == BlobJudgmentKind.Range)
            {
                var item = metadata[rule.Key];
                Check(double.IsFinite(rule.Minimum) && double.IsFinite(rule.Maximum) && rule.Minimum <= rule.Maximum
                    && rule.Minimum >= BlobParameterDefinition.Finite(item, "allowedMinimum")
                    && rule.Maximum <= BlobParameterDefinition.Finite(item, "allowedMaximum"), "结果判定范围与XML声明不符。");
            }
        }
        return errors;
    }
}
