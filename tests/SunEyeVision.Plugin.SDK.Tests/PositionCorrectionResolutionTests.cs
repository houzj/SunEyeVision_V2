using NSubstitute;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class PositionCorrectionResolutionTests
{
    private static MeasureGuideParameters Parameters(object? value) => new()
    {
        IsPositionCorrectionEnabled = true,
        PositionCorrectionBindingSource = new AvailableDataSource { SourceNodeId = "source", PropertyName = "Matrices", CurrentValue = value }
    };
    [Fact]
    public void Snapshot_is_detached_and_live_query_never_falls_back_to_cached_value()
    {
        var cached = new AffineMatrix(1, 0, 0, 1, 100, 0);
        var live = new AffineMatrix(1, 0, 0, 1, -50, 20);
        var p = Parameters(cached);
        var query = Substitute.For<IDataSourceQueryService>();
        query.GetPropertyValue("source", "Matrices").Returns(new[] { live });
        var context = PositionCorrectionResolution.Capture(p, query);
        Assert.Equal(PositionCorrectionState.Valid, context.State); Assert.Equal(-50, context.Matrix!.OffsetX);
        live.OffsetX = 900; context.Matrix!.OffsetX = 800;
        Assert.Equal(-50, context.Matrix!.OffsetX); Assert.Same(cached, p.PositionCorrectionBindingSource!.CurrentValue);
        query.GetPropertyValue("source", "Matrices").Returns((object?)null);
        Assert.Equal(PositionCorrectionState.WaitingForSource, PositionCorrectionResolution.Capture(p, query).State);
    }

    [Fact]
    public void Reference_only_resolution_does_not_write_parameters()
    {
        var p = new MeasureGuideParameters { IsPositionCorrectionEnabled = true,
            PositionCorrectionSource = new ImageSourceReference("source", "Matrices", "", "") };
        var query = Substitute.For<IDataSourceQueryService>();
        query.GetPropertyValue("source", "Matrices").Returns(new[] { AffineMatrix.Identity });
        Assert.Equal(PositionCorrectionState.Valid, PositionCorrectionResolution.Capture(p, query).State);
        Assert.Null(p.PositionCorrectionBindingSource);
    }
    [Theory]
    [InlineData(1, 0, 0, 1, PositionCorrectionState.Valid)]
    [InlineData(0, 2, -2, 0, PositionCorrectionState.Valid)]
    [InlineData(1, 0, 0, 2, PositionCorrectionState.UnsupportedTransform)]
    [InlineData(1, 0, 0.3, 1, PositionCorrectionState.UnsupportedTransform)]
    [InlineData(-1, 0, 0, 1, PositionCorrectionState.UnsupportedTransform)]
    [InlineData(0, 0, 0, 0, PositionCorrectionState.InvalidMatrix)]
    [InlineData(double.NaN, 0, 0, 1, PositionCorrectionState.InvalidMatrix)]
    public void Matrix_validation_is_explicit(double a, double b, double c, double d, PositionCorrectionState state) =>
        Assert.Equal(state, PositionCorrectionResolution.Capture(Parameters(new AffineMatrix(a, b, c, d, 0, 0))).State);
    [Fact]
    public void Missing_empty_wrong_unbound_and_disabled_are_distinct()
    {
        Assert.Equal(PositionCorrectionState.WaitingForSource, PositionCorrectionResolution.Capture(Parameters(null)).State);
        Assert.Equal(PositionCorrectionState.EmptyMatch, PositionCorrectionResolution.Capture(Parameters(Array.Empty<AffineMatrix>())).State);
        Assert.Equal(PositionCorrectionState.InvalidMatrix, PositionCorrectionResolution.Capture(Parameters("bad")).State);
        Assert.Equal(PositionCorrectionState.Unbound, PositionCorrectionResolution.Capture(new MeasureGuideParameters { IsPositionCorrectionEnabled = true }).State);
        var p = Parameters(null); p.IsPositionCorrectionEnabled = false;
        Assert.Equal(PositionCorrectionState.Disabled, PositionCorrectionResolution.Capture(p).State);
    }
}
