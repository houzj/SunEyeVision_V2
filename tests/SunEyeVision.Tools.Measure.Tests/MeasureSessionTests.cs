using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Tool.CircleCircleMeasure.Runtime;
using SunEyeVision.Tool.PointPointMeasure.Runtime;
using Xunit;

namespace SunEyeVision.Tools.Measure.Tests;

/// <summary>
/// 算法取证为 not-established 的两个测量工具：执行必须给出明确"待补证"诊断，
/// 不得静默产出全零/默认值（rule-015 禁止兜底回退）。
/// </summary>
public sealed class MeasureSessionTests
{
    private sealed class Context : IToolExecutionContext
    {
        public RunIdentity Identity { get; } =
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "measure-node", Guid.NewGuid());
        public CancellationToken Cancellation => CancellationToken.None;
        public IOutputResources Resources { get; } = new FrozenOutputResources();
        public void Log(string message) { }
    }

    private static InputValues Inputs(ToolSchema schema, params (string Id, object Value)[] values)
        => new(schema.Inputs, values.ToDictionary(pair => pair.Id, pair => (object?)pair.Value, StringComparer.Ordinal));

    [Fact]
    public async Task CircleCircle_reports_pending_algorithm_without_silent_output()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(2, 2, new byte[] { 1, 2, 3, 4 });
        var inputs = Inputs(CircleCircleMeasureTool.Schema, (CircleCircleMeasureTool.ImageInput, image));
        await using var session = new CircleCircleMeasureTool().CreateSession();
        var outcome = await session.ExecuteAsync(new(new CircleCircleMeasureSettings(), inputs, new Context()));
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        Assert.Contains(outcome.Diagnostics, diagnostic => diagnostic.Code == "CIRCLECIRCLEMEASURE_ALGORITHM_PENDING");
    }

    [Fact]
    public async Task PointPoint_reports_pending_algorithm_without_silent_output()
    {
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(2, 2, new byte[] { 1, 2, 3, 4 });
        var schema = PointPointMeasureTool.Schema;
        RecordValue Point(string id) => new(schema.Inputs.Single(port => port.Id == id).Type,
            new Dictionary<string, object?> { ["x"] = 0d, ["y"] = 0d });
        var inputs = Inputs(schema,
            (PointPointMeasureTool.ImageInput, image),
            (PointPointMeasureTool.Point1Input, Point(PointPointMeasureTool.Point1Input)),
            (PointPointMeasureTool.Point2Input, Point(PointPointMeasureTool.Point2Input)));
        await using var session = new PointPointMeasureTool().CreateSession();
        var outcome = await session.ExecuteAsync(new(new PointPointMeasureSettings(), inputs, new Context()));
        Assert.False(outcome.Success);
        Assert.Null(outcome.Output);
        Assert.Contains(outcome.Diagnostics, diagnostic => diagnostic.Code == "POINTMEASURE_ALGORITHM_PENDING");
    }

    [Fact]
    public async Task Invalid_settings_fail_before_algorithm_pending()
    {
        var values = new Dictionary<string, object?>(CircleCircleMeasureFields.Defaults, StringComparer.Ordinal)
        {
            ["settings.angle-limit-enabled"] = true,
            ["settings.angle-limit-low"] = 100d,
            ["settings.angle-limit-high"] = -100d
        };
        using var resources = new FrozenOutputResources();
        var image = resources.CreateGray8(2, 2, new byte[] { 1, 2, 3, 4 });
        var inputs = Inputs(CircleCircleMeasureTool.Schema, (CircleCircleMeasureTool.ImageInput, image));
        await using var session = new CircleCircleMeasureTool().CreateSession();
        var outcome = await session.ExecuteAsync(new(new CircleCircleMeasureSettings(values), inputs, new Context()));
        Assert.False(outcome.Success);
        Assert.Contains(outcome.Diagnostics, diagnostic => diagnostic.Code == "CIRCLECIRCLEMEASURE_SETTINGS_INVALID");
    }

    [Fact]
    public void Image_input_is_a_required_port_in_both_tools()
    {
        var c2c = CircleCircleMeasureTool.Schema.Inputs.ToDictionary(port => port.Id, StringComparer.Ordinal);
        var p2p = PointPointMeasureTool.Schema.Inputs.ToDictionary(port => port.Id, StringComparer.Ordinal);
        Assert.True(c2c[CircleCircleMeasureTool.ImageInput].Required);
        Assert.True(p2p[PointPointMeasureTool.ImageInput].Required);
    }
}
