using System;
using System.Linq;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public class SolutionExecutionDependencyGraphTests
{
    [Fact]
    public void Compile_AddsCrossWorkflowDependency_ForGlobalProducerAndConsumer()
    {
        var solution = new Solution();
        var sourceWorkflow = CreateWorkflow("source-workflow", "采集流程", "source");
        var targetWorkflow = CreateWorkflow("target-workflow", "检测流程", "target");
        solution.Workflows.Add(sourceWorkflow);
        solution.Workflows.Add(targetWorkflow);
        solution.GlobalVariables.Add(new GlobalVariable("共享阈值", 0d, "Double")
        {
            ProducerBinding = new ProducerBinding
            {
                WorkflowId = sourceWorkflow.Id,
                WorkflowName = sourceWorkflow.Name,
                NodeId = "source",
                NodeName = "源节点",
                ResultName = "Value",
                ResultDisplayName = "值"
            },
            ConsumerBindings =
            {
                new ConsumerBinding
                {
                    WorkflowId = targetWorkflow.Id,
                    WorkflowName = targetWorkflow.Name,
                    NodeId = "target",
                    NodeName = "目标节点",
                    ParameterName = "Threshold",
                    ParameterDisplayName = "阈值"
                }
            }
        });

        var graph = SolutionExecutionDependencyGraph.Compile(solution);

        Assert.Contains(graph.Dependencies, dependency =>
            dependency.Kind == SolutionDependencyKind.GlobalVariable &&
            dependency.GlobalVariableName == "共享阈值" &&
            dependency.SourceScopedNodeId == "source-workflow/source" &&
            dependency.TargetScopedNodeId == "target-workflow/target");
    }

    [Fact]
    public void Compile_RejectsAmbiguousLegacyConsumer_WhenNodeIdsRepeatAcrossWorkflows()
    {
        var solution = new Solution();
        var sourceWorkflow = CreateWorkflow("source-workflow", "来源", "source");
        var firstTarget = CreateWorkflow("target-a", "目标A", "target");
        var secondTarget = CreateWorkflow("target-b", "目标B", "target");
        solution.Workflows.Add(sourceWorkflow);
        solution.Workflows.Add(firstTarget);
        solution.Workflows.Add(secondTarget);
        solution.GlobalVariables.Add(new GlobalVariable("共享值", 0, "Integer")
        {
            ProducerBinding = new ProducerBinding { WorkflowId = sourceWorkflow.Id, NodeId = "source", ResultName = "Value" },
            ConsumerBindings = { new ConsumerBinding { NodeId = "target", ParameterName = "Value" } }
        });

        var exception = Assert.Throws<InvalidOperationException>(() => SolutionExecutionDependencyGraph.Compile(solution));

        Assert.Contains("多个工作流中重复", exception.Message);
    }

    private static SunEyeVision.Workflow.Workflow CreateWorkflow(string id, string name, string nodeId)
    {
        var workflow = new SunEyeVision.Workflow.Workflow(id, name);
        workflow.AddNode(new WorkflowNodeBase(nodeId, nodeId, nodeId, "tool"));
        return workflow;
    }
}
