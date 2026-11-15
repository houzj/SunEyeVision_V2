using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;

namespace SunEyeVision.Application;

/// <summary>
/// 方案级不可变执行计划。它只保存图结构和依赖关系；参数当前值、宿主输入和结果
/// 属于每一轮运行，不能进入这个计划，避免复用时冻结运行期状态。
/// </summary>
internal sealed class ExecutionPlan
{
    internal ExecutionPlan(Guid sessionId, string documentId, long generation,
        IReadOnlyList<ExecutionWorkflowPlan> workflows, GraphDependencySnapshot dependencies)
    {
        SessionId = sessionId;
        DocumentId = documentId;
        Generation = generation;
        Workflows = Array.AsReadOnly(workflows.ToArray());
        Dependencies = dependencies;
    }

    internal Guid SessionId { get; }
    internal string DocumentId { get; }
    internal long Generation { get; }
    internal IReadOnlyList<ExecutionWorkflowPlan> Workflows { get; }
    internal GraphDependencySnapshot Dependencies { get; }

    internal ExecutionWorkflowPlan GetWorkflow(string id) =>
        Workflows.SingleOrDefault(item => item.Workflow.Id == id && !item.Workflow.IsSubroutine)
        ?? throw new InvalidOperationException("运行流程不存在或不属于当前执行计划：" + id);

    internal bool Matches(SolutionDefinition document)
    {
        if (document.Id != DocumentId) return false;
        if (Workflows.Count != document.Workflows.Count) return false;
        foreach (var plan in Workflows)
        {
            var current = document.Workflows.SingleOrDefault(flow => ReferenceEquals(flow, plan.Workflow));
            if (current is null || current.Id != plan.Workflow.Id || current.Nodes.Count != plan.Nodes.Count ||
                current.Connections.Count != plan.Edges.Count) return false;
            for (var index = 0; index < plan.Nodes.Count; index++)
            {
                var node = current.Nodes[index];
                var planned = plan.Nodes[index];
                if (!ReferenceEquals(node, planned.Node) || node.Id != planned.Node.Id || node.IsEnabled != planned.Enabled)
                    return false;
            }
            for (var index = 0; index < plan.Edges.Count; index++)
            {
                var edge = current.Connections[index];
                var planned = plan.Edges[index];
                if (!ReferenceEquals(edge, planned.Edge) || edge.SourceNodeId != planned.Edge.SourceNodeId ||
                    edge.TargetNodeId != planned.Edge.TargetNodeId || edge.SourcePort != planned.Edge.SourcePort ||
                    edge.TargetPort != planned.Edge.TargetPort) return false;
            }
        }
        return true;
    }
}

internal sealed class ExecutionWorkflowPlan
{
    internal ExecutionWorkflowPlan(WorkflowDefinition workflow, GraphState state)
    {
        Workflow = workflow;
        Nodes = Array.AsReadOnly(state.Nodes.Select(item => new ExecutionPlanNode(item.Node, item.Enabled)).ToArray());
        Edges = Array.AsReadOnly(state.Edges.Select(item => new ExecutionPlanEdge(item.Edge)).ToArray());
    }

    internal WorkflowDefinition Workflow { get; }
    internal IReadOnlyList<ExecutionPlanNode> Nodes { get; }
    internal IReadOnlyList<ExecutionPlanEdge> Edges { get; }
    internal ExecutionWorkflowSnapshot? CachedSnapshot { get; set; }
}

internal sealed record ExecutionPlanNode(NodeDefinition Node, bool Enabled);
internal sealed record ExecutionPlanEdge(ConnectionDefinition Edge);
