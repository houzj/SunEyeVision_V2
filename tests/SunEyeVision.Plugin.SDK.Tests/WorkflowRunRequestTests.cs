using System.Collections.Generic;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class WorkflowRunRequestTests
{
    [Fact]
    public void Constructor_CopiesMutableHostDictionaries()
    {
        var runtimeParameters = new Dictionary<string, object> { ["ImageIndex"] = 1 };
        var globals = new Dictionary<string, object?> { ["Threshold"] = 50d };
        var overrides = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["image-load"] = new Dictionary<string, object?> { ["FilePath"] = "first.png" }
        };

        var request = new WorkflowRunRequest("workflow", null, globals, runtimeParameters,
            nodeParameterOverrides: overrides);

        runtimeParameters["ImageIndex"] = 2;
        globals["Threshold"] = 60d;
        ((Dictionary<string, object?>)overrides["image-load"])["FilePath"] = "second.png";

        Assert.Equal(1, request.RuntimeParameters["ImageIndex"]);
        Assert.Equal(50d, request.GlobalVariables["Threshold"]);
        Assert.Equal("first.png", request.NodeParameterOverrides["image-load"]["FilePath"]);
    }
}
