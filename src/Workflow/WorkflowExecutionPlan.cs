using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;

namespace SunEyeVision.Workflow;

/// <summary>
/// 一次运行冻结后的可执行计划。源工作流只用于编辑，执行器只消费此计划中的节点图。
/// </summary>
public sealed class WorkflowExecutionPlan : IDisposable
{
    internal WorkflowExecutionPlan(
        WorkflowRunScope scope,
        Workflow executableWorkflow,
        WorkflowExecutionDependencyGraph dependencyGraph,
        IReadOnlyDictionary<string, ToolExecutionProfile> nodeExecutionProfiles,
        WorkflowBindingContext bindingContext)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        ExecutableWorkflow = executableWorkflow ?? throw new ArgumentNullException(nameof(executableWorkflow));
        DependencyGraph = dependencyGraph ?? throw new ArgumentNullException(nameof(dependencyGraph));
        NodeExecutionProfiles = nodeExecutionProfiles ?? throw new ArgumentNullException(nameof(nodeExecutionProfiles));
        _bindingContext=bindingContext;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid RunId => Scope.RunId;

    public Guid? ParentRunId => Scope.ParentRunId;

    public string SourceWorkflowId => Scope.WorkflowId;

    public DateTimeOffset CreatedAt { get; }

    public int NodeCount => ExecutableWorkflow.Nodes.Count;

    public int ConnectionCount => ExecutableWorkflow.Connections.Count;

    public WorkflowExecutionDependencyGraph DependencyGraph { get; }

    /// <summary>节点在本次运行开始时冻结的并发与资源声明。</summary>
    public IReadOnlyDictionary<string, ToolExecutionProfile> NodeExecutionProfiles { get; }

    internal WorkflowRunScope Scope { get; }

    internal Workflow ExecutableWorkflow { get; }
    private readonly WorkflowBindingContext _bindingContext;
    public void Dispose()=>_bindingContext.Dispose();
}
