using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.DivideImage.Runtime;

/// <summary>唯一持久化来源；每次调用使用一份冻结值。</summary>
public sealed record DivideImageSettings : IToolSettings<DivideImageSettings>
{
    /// <summary>原厂 <c>NumX</c> 默认值 1（IMVSDivideImageModuAlgorithm.xml DefaultValue 证据）。</summary>
    public long Columns { get; init; } = 1;
    /// <summary>原厂 <c>NumY</c> 默认值 1。</summary>
    public long Rows { get; init; } = 1;
    /// <summary>原厂 <c>OverlaprateX</c> 默认值 0。</summary>
    public long OverlapX { get; init; }
    /// <summary>原厂 <c>OverlaprateY</c> 默认值 0。</summary>
    public long OverlapY { get; init; }

    public DivideImageSettings Freeze() => this;

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        if (Columns < 1)
            errors.Add(new("DIVIDE_IMAGE_COLUMNS", "X方向划片数量必须≥1。", DivideImageFields.Columns));
        if (Rows < 1)
            errors.Add(new("DIVIDE_IMAGE_ROWS", "Y方向划片数量必须≥1。", DivideImageFields.Rows));
        if (OverlapX is < DivideImageFields.MinimumOverlapPercent or > DivideImageFields.MaximumOverlapPercent)
            errors.Add(new("DIVIDE_IMAGE_OVERLAP_X",
                $"X方向重叠率必须在 {DivideImageFields.MinimumOverlapPercent}..{DivideImageFields.MaximumOverlapPercent} 之间（%）。",
                DivideImageFields.OverlapX));
        if (OverlapY is < DivideImageFields.MinimumOverlapPercent or > DivideImageFields.MaximumOverlapPercent)
            errors.Add(new("DIVIDE_IMAGE_OVERLAP_Y",
                $"Y方向重叠率必须在 {DivideImageFields.MinimumOverlapPercent}..{DivideImageFields.MaximumOverlapPercent} 之间（%）。",
                DivideImageFields.OverlapY));
        if (Columns >= 1 && Rows >= 1 && Columns > int.MaxValue / Rows)
            errors.Add(new("DIVIDE_IMAGE_TILE_COUNT", "分片总数超出可表示范围。", DivideImageFields.Columns));
        return errors.AsReadOnly();
    }
}
