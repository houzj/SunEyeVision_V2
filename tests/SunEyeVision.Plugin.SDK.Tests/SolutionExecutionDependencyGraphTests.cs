using System;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
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
        sourceWorkflow.Nodes[0].Parameters=new GraphSourceParameters();
        solution.Workflows.Add(sourceWorkflow);
        solution.Workflows.Add(targetWorkflow);
        solution.GlobalVariables.Add(new GlobalVariable("共享阈值", 0d, "Double")
        {
            ProducerBinding = new ProducerBinding
            {
                ReferenceExpression = Reference(sourceWorkflow.Id,"source",ScalarKind.Float64),
                WorkflowName = sourceWorkflow.Name,
                NodeName = "源节点",
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Compile_DoesNotGuessAmbiguousConsumer_AndPreservesExplicitScopedConsumer(bool checkReadiness)
    {
        var solution = new Solution();
        var sourceWorkflow = CreateWorkflow("source-workflow", "来源", "source");
        var firstTarget = CreateWorkflow("target-a", "目标A", "target");
        var secondTarget = CreateWorkflow("target-b", "目标B", "target");
        sourceWorkflow.Nodes[0].Parameters=new GraphSourceParameters{Kind=ScalarKind.Int64};
        solution.Workflows.Add(sourceWorkflow);
        solution.Workflows.Add(firstTarget);
        solution.Workflows.Add(secondTarget);
        solution.GlobalVariables.Add(new GlobalVariable("共享值", 0, "Integer")
        {
            ProducerBinding = new ProducerBinding { ReferenceExpression=Reference(sourceWorkflow.Id,"source",ScalarKind.Int64) },
            ConsumerBindings = { new ConsumerBinding { NodeId = "target", ParameterName = "Value" } }
        });

        // ConsumerBindings is reverse-index metadata, not the executable input expression.
        // A missing scope must never fan out to both same-ID nodes or poison unrelated graphs.
        var graph = SolutionExecutionDependencyGraph.Compile(solution,checkReadiness);
        Assert.Empty(graph.Dependencies);
        Assert.Empty(graph.GetPredecessors("target-a/target"));
        Assert.Empty(graph.GetPredecessors("target-b/target"));
        solution.GlobalVariables[0].ConsumerBindings.Add(new ConsumerBinding { WorkflowId=firstTarget.Id,NodeId="target",ParameterName="Value" });
        graph=SolutionExecutionDependencyGraph.Compile(solution,checkReadiness);
        var edge=Assert.Single(graph.Dependencies);
        Assert.Equal("source-workflow/source",edge.SourceScopedNodeId);
        Assert.Equal("target-a/target",edge.TargetScopedNodeId);
        Assert.Empty(graph.GetPredecessors("target-b/target"));
        Assert.Equal(new[]{"source-workflow/source"},graph.GetPredecessors("target-a/target"));
    }

    private sealed class GraphSourceParameters:ToolParameters,IOutputContractProvider
    {
        public ScalarKind Kind{get;set;}=ScalarKind.Float64;
        public System.Collections.Generic.IReadOnlyList<OutputVariable> GetOutputContract()=>new[]{new OutputVariable{
            PortId="output.value",Name="Value",ValueDeclaration=TypeRef.ScalarOf(Kind),Kind=OutputVariableKind.Data}};
    }
    private static string Reference(string scope,string node,ScalarKind kind)=>BindingReferenceSerializer.Serialize(
        new OutputReference(node,"output.value",TypeRef.ScalarOf(kind),scopeId:scope));
    private static SunEyeVision.Workflow.Workflow CreateWorkflow(string id, string name, string nodeId)
    {
        var workflow = new SunEyeVision.Workflow.Workflow(id, name);
        workflow.AddNode(new WorkflowNodeBase(nodeId, nodeId, nodeId, "tool"));
        return workflow;
    }
}
