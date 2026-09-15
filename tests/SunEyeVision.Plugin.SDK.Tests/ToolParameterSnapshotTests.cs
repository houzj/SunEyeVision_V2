using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class ToolParameterSnapshotTests
{
    [Fact]
    public void Create_DeepCopiesEditableConfiguration_AndDropsRuntimeState()
    {
        var source = new SnapshotParameters
        {
            Threshold = 80,
            Samples = new List<int> { 1, 2, 3 },
            NodeResults = new Dictionary<string, SunEyeVision.Plugin.SDK.Execution.Results.ToolResults>()
        };

        var snapshot = Assert.IsType<SnapshotParameters>(ToolParameterSnapshot.Create(source));

        source.Threshold = 120;
        source.Samples[0] = 99;

        Assert.Equal(80, snapshot.Threshold);
        Assert.Equal(new[] { 1, 2, 3 }, snapshot.Samples);
        Assert.Null(snapshot.Context);
        Assert.Null(snapshot.NodeResults);
        Assert.Null(snapshot.ResolvedInputImage);
    }

    public sealed class SnapshotParameters : ToolParameters
    {
        public int Threshold { get; set; }
        public List<int> Samples { get; set; } = new();
    }
}
