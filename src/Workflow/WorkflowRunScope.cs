using System;

namespace SunEyeVision.Workflow;

/// <summary>
/// 根工作流或一次子程序调用的运行作用域。
/// </summary>
public sealed class WorkflowRunScope
{
    internal WorkflowRunScope(string workflowId, Guid? parentRunId = null, Guid? runId = null)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("工作流 ID 不能为空。", nameof(workflowId));

        WorkflowId = workflowId;
        ParentRunId = parentRunId;
        RunId = runId ?? Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
    }

    public Guid RunId { get; }

    public Guid? ParentRunId { get; }

    public string WorkflowId { get; }

    public DateTimeOffset StartedAt { get; }

    public WorkflowExecutionState State { get; internal set; } = WorkflowExecutionState.Idle;

    public WorkflowContext? Context { get; internal set; }
}
