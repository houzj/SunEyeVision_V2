using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class RuntimeParameterSnapshotTests
{
    [Fact]
    public void Resolve_ProducesReadOnlyValuesWithoutChangingDesignParameters()
    {
        var bindings = new ParameterBindingSet(new[]
        {
            new ParameterBindingDefinition
            {
                ParameterName = "Threshold",
                BindingType = BindingType.Constant,
                ConstantValue = 128,
                TargetType = typeof(int)
            }
        });

        var snapshot = new RuntimeParameterSnapshotResolver(new ParameterResolver())
            .Resolve(bindings, new Dictionary<string, ToolResults>());

        Assert.True(snapshot.IsValid);
        Assert.True(snapshot.TryGet<int>("Threshold", out var threshold));
        Assert.Equal(128, threshold);
        Assert.Throws<InvalidOperationException>(() => snapshot.GetRequired<double>("Threshold"));
    }

    [Fact]
    public void ParameterBindingSet_RejectsDuplicateParameterNames()
    {
        var definitions = new[]
        {
            new ParameterBindingDefinition { ParameterName = "Threshold" },
            new ParameterBindingDefinition { ParameterName = "Threshold" }
        };

        Assert.Throws<ArgumentException>(() => new ParameterBindingSet(definitions));
    }

    [Fact]
    public void ToolExecutor_CanAttachRuntimeSnapshotToRequest()
    {
        var request = new ToolExecutionRequest(new EmptyParameters())
        {
            ParameterBindings = new ParameterBindingSet(new[]
            {
                new ParameterBindingDefinition
                {
                    ParameterName = "Threshold",
                    BindingType = BindingType.Constant,
                    ConstantValue = 64,
                    TargetType = typeof(int)
                }
            })
        };

        var snapshot = new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(request);

        Assert.Same(snapshot, request.RuntimeParameters);
        Assert.Equal(64, snapshot.GetRequired<int>("Threshold"));
        Assert.Null(request.Parameters.Context);
        Assert.Null(request.Parameters.NodeResults);
    }

    [Fact]
    public void ToolExecutor_AppliesSnapshotToRuntimeCopy()
    {
        var runtimeParameters = new NumericParameters();
        var snapshot = new RuntimeParameterSnapshot(
            new Dictionary<string, object?> { [nameof(NumericParameters.Threshold)] = 64 });

        ToolExecutor.ApplyRuntimeSnapshot(runtimeParameters, snapshot);

        Assert.Equal(64, runtimeParameters.Threshold);
    }

    private sealed class EmptyParameters : ToolParameters
    {
    }

    private sealed class NumericParameters : ToolParameters
    {
        public int Threshold { get; set; }
    }
}
