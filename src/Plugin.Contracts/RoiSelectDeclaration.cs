namespace SunEyeVision.Plugin.Contracts;

/// <summary>
/// ROI 形状选择声明（原厂 <c>&lt;ROISelecter&gt;</c> 的 <c>SelectType</c>/<c>ROISelection</c>/
/// <c>RoiMaxNum</c>/<c>FullScreenEnable</c>）。它是 ROI 组内的一行，不是独立节点，
/// 因此挂在 <see cref="RoiGroupDeclaration.Selection"/> 上，而不是单独进 manifest。
/// </summary>
public sealed record RoiSelectDeclaration
{
    public RoiSelectDeclaration(string selectType, IReadOnlyList<string> shapes,
        int maximumCount = 8, bool fullScreenEnable = true,
        string? avoidSelectType = null, IReadOnlyList<string>? avoidShapes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectType);
        ArgumentNullException.ThrowIfNull(shapes);
        if (shapes.Count == 0) throw new ArgumentException("ROI 形状集合不能为空。", nameof(shapes));
        if (shapes.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("ROI 形状名不能为空。", nameof(shapes));
        if (maximumCount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCount));
        // 原厂 InitMap：单模式（如 Mul）适用于全部形状；多模式（如 "Mul Fix"）才按空格逐个配对，
        // 个数不等直接报错。
        var modes = selectType.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (modes.Length > 1 && modes.Length != shapes.Count)
            throw new ArgumentException($"ROI 选择模式与形状个数不一致（{modes.Length} vs {shapes.Count}）。", nameof(shapes));
        var avoidModes = avoidSelectType?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         ?? Array.Empty<string>();
        var avoided = avoidShapes ?? Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(avoidSelectType) && avoided.Count == 0)
            throw new ArgumentException("声明了屏蔽区选择模式就必须给出屏蔽区形状。", nameof(avoidShapes));
        if (avoidModes.Length > 1 && avoidModes.Length != avoided.Count)
            throw new ArgumentException("屏蔽区选择模式与形状个数不一致。", nameof(avoidShapes));
        SelectType = selectType;
        Shapes = Array.AsReadOnly(shapes.ToArray());
        MaximumCount = maximumCount;
        FullScreenEnable = fullScreenEnable;
        AvoidSelectType = avoidSelectType;
        AvoidShapes = Array.AsReadOnly(avoided.ToArray());
    }

    /// <summary>选择模式串（原厂 <c>SelectType</c>，如 <c>Mul</c> 或 <c>Mul Fix</c>）。</summary>
    public string SelectType { get; }
    /// <summary>形状名串（原厂 <c>ROISelection</c>，如 <c>Box SectorRing Polygon Sector</c>）。</summary>
    public IReadOnlyList<string> Shapes { get; }
    /// <summary>最多 ROI 个数（原厂 <c>RoiMaxNum</c>）。</summary>
    public int MaximumCount { get; }
    /// <summary>允许全屏（原厂 <c>FullScreenEnable</c>）。</summary>
    public bool FullScreenEnable { get; }
    /// <summary>屏蔽区选择模式（原厂 <c>ROISelecter_Extern/SelectType</c>，通常为 <c>Avoid</c>）；为空表示无屏蔽区。</summary>
    public string? AvoidSelectType { get; }
    /// <summary>屏蔽区形状（原厂通常只有 <c>Polygon</c>）。</summary>
    public IReadOnlyList<string> AvoidShapes { get; }
}
