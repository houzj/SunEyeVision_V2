using System;

namespace SunEyeVision.Workflow;

/// <summary>
/// 一次运行冻结后的可执行计划。源工作流只用于编辑，执行器只消费此计划中的节点图。
/// </summary>
public sealed class WorkflowExecutionPlan
{
    internal WorkflowExecutionPlan(WorkflowRunScope scope, Workflow executableWorkflow)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        ExecutableWorkflow = executableWorkflow ?? throw new ArgumentNullException(nameof(executableWorkflow));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid RunId => Scope.RunId;

    public Guid? ParentRunId => Scope.ParentRunId;

    public string SourceWorkflowId => Scope.WorkflowId;

    public DateTimeOffset CreatedAt { get; }

    public int NodeCount => ExecutableWorkflow.Nodes.Count;

    public int ConnectionCount => ExecutableWorkflow.Connections.Count;

    internal WorkflowRunScope Scope { get; }

    internal Workflow ExecutableWorkflow { get; }
}
