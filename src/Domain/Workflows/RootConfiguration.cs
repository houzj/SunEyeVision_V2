using System.Collections.Immutable;

namespace SunEyeVision.Domain.Workflows;

public sealed record RootParameterProxy(Guid Id, string NodeId, string ToolType, string FieldId, string StoragePath, string DisplayName);

public sealed record RootWorkflowConfiguration
{
    public ImmutableArray<RootParameterProxy> RunParameters { get; init; } = ImmutableArray<RootParameterProxy>.Empty;
    public void Validate()
    {
        if (RunParameters.IsDefault) throw new ArgumentException("根运行参数目录不能为空对象。");
        if (RunParameters.Any(p => p is null || p.Id == Guid.Empty || string.IsNullOrWhiteSpace(p.NodeId) ||
            string.IsNullOrWhiteSpace(p.ToolType) || string.IsNullOrWhiteSpace(p.FieldId) ||
            string.IsNullOrWhiteSpace(p.StoragePath) || string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName.Length > 128))
            throw new ArgumentException("根运行参数身份、来源及名称必须完整。");
        if (RunParameters.Select(p => p.Id).Distinct().Count() != RunParameters.Length ||
            RunParameters.Select(p => (p.NodeId, p.FieldId)).Distinct().Count() != RunParameters.Length)
            throw new ArgumentException("根运行参数身份或曝光来源不能重复。");
    }
    public RootWorkflowConfiguration Remap(IReadOnlyDictionary<string, string> nodeIds) => this with
    {
        RunParameters = RunParameters.Select(p => p with { Id = Guid.NewGuid(), NodeId = nodeIds.GetValueOrDefault(p.NodeId) ?? p.NodeId }).ToImmutableArray()
    };
}

public sealed record RootWorkspaceSettings
{
    public int RunIntervalMs { get; init; } = 80;
    public bool StopOnNg { get; init; }
    public double? OverviewX { get; init; }
    public double? OverviewY { get; init; }
    public void Validate()
    {
        if (RunIntervalMs is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(RunIntervalMs));
        if (OverviewX.HasValue != OverviewY.HasValue || OverviewX is { } x && (!double.IsFinite(x) || x < 0 || x > 4860) ||
            OverviewY is { } y && (!double.IsFinite(y) || y < 0 || y > 4880))
            throw new ArgumentException("流程总览位置必须成对位于5000画布内。");
    }
}
