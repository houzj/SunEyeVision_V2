using System.Text.Json;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.WorkflowGraph.Tests;

public sealed class WorkflowFormatTests
{
    [Theory]
    [InlineData("Parameters")]
    [InlineData("ParamSettings")]
    [InlineData("parameters")]
    public void Legacy_node_fields_are_rejected_before_deserialization(string field)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new WorkflowJsonConverter());
        var json = "{\"Id\":\"flow\",\"Name\":\"flow\",\"Nodes\":[{\"Id\":\"n\",\"" + field + "\":{}}]}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SunEyeVision.Workflow.Workflow>(json, options));
    }
}
