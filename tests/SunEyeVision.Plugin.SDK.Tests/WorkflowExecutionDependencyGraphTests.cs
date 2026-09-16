using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class WorkflowExecutionDependencyGraphTests
{
    [Fact]
    public void Compile_AddsParameterBindingAsDependency_WhenCanvasHasNoConnection()
    {
        var workflow = CreateWorkflow();
        workflow.Nodes[1].ParamSettings.SetDynamicSetting("Threshold", "source", "Value");

        var graph = WorkflowExecutionDependencyGraph.Compile(workflow);

        Assert.Contains(graph.Dependencies, dependency =>
            dependency.SourceNodeId == "source" &&
            dependency.TargetNodeId == "target" &&
            dependency.Kind == WorkflowDependencyKind.ParameterBinding);
        Assert.Equal(new[] { "source" }, graph.GetTopologicalLayers(workflow.Nodes.Select(node => node.Id))[0]);
    }

    [Fact]
    public void Compile_RejectsBindingToMissingNode()
    {
        var workflow = CreateWorkflow();
        workflow.Nodes[1].ParamSettings.SetDynamicSetting("Threshold", "missing", "Value");

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowExecutionDependencyGraph.Compile(workflow));

        Assert.Contains("不存在的节点", exception.Message);
    }

    private static SunEyeVision.Workflow.Workflow CreateWorkflow()
    {
        var workflow = new SunEyeVision.Workflow.Workflow("workflow", "workflow");
        workflow.AddNode(new WorkflowNodeBase("source", "source", "source", "tool"));
        workflow.AddNode(new WorkflowNodeBase("target", "target", "target", "tool"));
        return workflow;
    }
}
