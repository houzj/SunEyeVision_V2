using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Workflow;

/// <summary>
/// 一次根工作流运行的独立状态与取消边界。
/// </summary>
/// <remarks>
/// 子程序属于发起它的根运行，因此复用同一会话和取消信号；它们自己的节点结果仍保存在各自的
/// <see cref="WorkflowContext"/> 中。此对象避免取消源和根上下文被下一次运行覆盖。
/// </remarks>
public sealed class WorkflowRunSession : IDisposable
{
    private readonly CancellationTokenSource _cancellationSource;
    private readonly object _pauseGate = new();
    private readonly object _scopeGate = new();
    private readonly Dictionary<Guid, WorkflowRunScope> _scopes = new();
    private TaskCompletionSource _resumeSignal = CreateCompletedSignal();

    public WorkflowRunSession(string rootWorkflowId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootWorkflowId))
            throw new ArgumentException("根工作流 ID 不能为空。", nameof(rootWorkflowId));

        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        RootWorkflowId = rootWorkflowId;
        RunId = Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
        State = WorkflowExecutionState.Idle;
        RootScope = new WorkflowRunScope(rootWorkflowId, runId: RunId);
        RootScope.ConsumerScope = rootWorkflowId;
        _scopes.Add(RootScope.RunId, RootScope);
    }

    public Guid RunId { get; }
    internal SunEyeVision.Plugin.SDK.Core.ToolExecutionOwner? ExecutionOwner { get; set; }

    public string RootWorkflowId { get; }
    internal WorkflowRunConfiguration? Configuration { get; set; }

    public DateTimeOffset StartedAt { get; }

    public WorkflowRunScope RootScope { get; }

    public IReadOnlyList<WorkflowRunScope> Scopes
    {
        get
        {
            lock (_scopeGate)
                return _scopes.Values.ToList();
        }
    }

    public CancellationToken CancellationToken => _cancellationSource.Token;

    public WorkflowContext? RootContext { get; internal set; }

    public WorkflowExecutionState State { get; internal set; }

    public bool IsActive => State is WorkflowExecutionState.Running or WorkflowExecutionState.Paused or WorkflowExecutionState.Stopping;

    public WorkflowRunScope CreateChildScope(string workflowId, Guid parentRunId, string? callerNodeId = null)
    {
        lock (_scopeGate)
        {
            if (!_scopes.ContainsKey(parentRunId))
                throw new ArgumentException("父运行作用域不属于当前会话。", nameof(parentRunId));

            var scope = new WorkflowRunScope(workflowId, parentRunId);
            scope.ConsumerScope = _scopes[parentRunId].ConsumerScope + "/" + (callerNodeId ?? workflowId) + "/" + workflowId;
            _scopes.Add(scope.RunId, scope);
            return scope;
        }
    }

    public bool TryStart()
    {
        lock (_pauseGate)
        {
            if (State != WorkflowExecutionState.Idle)
                return false;

            State = WorkflowExecutionState.Running;
            RootScope.State = WorkflowExecutionState.Running;
            return true;
        }
    }

    public bool RequestPause()
    {
        lock (_pauseGate)
        {
            if (State != WorkflowExecutionState.Running)
                return false;

            _resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            State = WorkflowExecutionState.Paused;
            return true;
        }
    }

    public bool RequestResume()
    {
        lock (_pauseGate)
        {
            if (State != WorkflowExecutionState.Paused)
                return false;

            State = WorkflowExecutionState.Running;
            _resumeSignal.TrySetResult();
            return true;
        }
    }

    public async ValueTask WaitIfPausedAsync()
    {
        CancellationToken.ThrowIfCancellationRequested();
        Task resumeTask;
        lock (_pauseGate)
        {
            resumeTask = _resumeSignal.Task;
        }

        await resumeTask.WaitAsync(CancellationToken).ConfigureAwait(false);
        CancellationToken.ThrowIfCancellationRequested();
    }

    public void RequestStop()
    {
        lock (_pauseGate)
        {
            if (IsActive) State = WorkflowExecutionState.Stopping;
            _resumeSignal.TrySetResult();
        }
        _cancellationSource.Cancel();
    }

    private static TaskCompletionSource CreateCompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }

    public void Dispose() => _cancellationSource.Dispose();
}
