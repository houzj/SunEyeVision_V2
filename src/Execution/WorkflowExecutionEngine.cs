using System.Collections.Concurrent;
using System.Diagnostics;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

public enum WorkflowExecutionState { Idle, Running, Paused, Stopped, Completed, Error, Stopping }

/// <summary>Dispatches a frozen workflow using the established dependency-ready queue and resource leases.</summary>
public sealed class WorkflowExecutionEngine
{
    private readonly object _gate = new();
    private bool _running;
    private ExecutionRunControl? _control;
    private WorkflowExecutionState _state;
    public event Action<string, DeclaredOutputSnapshot>? NodeCompleted;
    public event Action<string, ExecutionStatus>? NodeStatusChanged;
    public Exception? LastObserverError { get; private set; }
    public WorkflowExecutionState CurrentState { get { lock (_gate) return _state; } }
    public bool PauseExecution() { lock (_gate) { if (_control?.Pause() != true) return false; _state = WorkflowExecutionState.Paused; return true; } }
    public bool ResumeExecution() { lock (_gate) { if (_control?.Resume() != true) return false; _state = WorkflowExecutionState.Running; return true; } }
    public void StopExecution() { ExecutionRunControl? control; lock (_gate) { control = _control; if (control is not null) _state = WorkflowExecutionState.Stopping; } control?.Stop(); }

    internal async Task<ExecutionResult> ExecuteAsync(ExecutionRunBatch batch, ExecutionRunRequest request, Action<string> log, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(log);
        var control = new ExecutionRunControl(cancellation);
        lock (_gate)
        {
            if (_running) { control.Dispose(); throw new InvalidOperationException("执行器已有根运行，不能并发接纳。 "); }
            try { request.Claim(); } catch { control.Dispose(); throw; }
            _control = control;
            _running = true;
            _state = WorkflowExecutionState.Running;
            LastObserverError = null;
        }
        cancellation = control.Token;
        var workflow = request.Workflow;
        var root = batch.RootId; var scope = Guid.NewGuid();
        var timer = Stopwatch.StartNew();
        var identities = new Dictionary<string, ToolExecutionIdentity>(StringComparer.Ordinal);
        var outputs = new ConcurrentDictionary<string, DeclaredOutputSnapshot>(StringComparer.Ordinal);
        var nodeResults = new Dictionary<string, NodeExecutionResult>(StringComparer.Ordinal);
        var running = new List<(string Id, IDisposable Lease, Task<DeclaredOutputSnapshot> Task)>();
        IDisposable? workflowLease = null;
        ExecutionResult? final = null;
        Exception? failure = null;
        try
        {
            workflowLease = batch.Enter(workflow);
            var bindings = new ExecutionBindingPlan(workflow, scope, request.VariableValues,
                request.GlobalProducers.Select(item => item.Source));
            foreach (var item in workflow.Nodes)
                identities.Add(item.Node.NodeId, workflow.Owner.Begin(item.Node.OwnerDomainId, scope,
                    FoundationContract.Major + "." + FoundationContract.Minor, cancellation, rootRunId: root));
            var expected = identities.ToDictionary(pair => new BindingNodeId(workflow.WorkflowId, pair.Key),
                pair => pair.Value.ToInvocationIdentity(pair.Key));
            var pending = workflow.Nodes.Select(item => item.Node.NodeId).ToHashSet(StringComparer.Ordinal);
            var items = workflow.Nodes.ToDictionary(item => item.Node.NodeId, StringComparer.Ordinal);
            var leases = new ExecutionResourceLeaseManager();
            var maxConcurrency = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
            while (pending.Count > 0 || running.Count > 0)
            {
                await control.WaitAsync().ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                bool skipped;
                do
                {
                    skipped = false;
                    foreach (var id in pending.Where(id => !items[id].IsEnabled || workflow.Predecessors[id]
                        .Any(source => nodeResults.TryGetValue(source, out var previous) && !previous.Success)).ToArray())
                    {
                        pending.Remove(id); skipped = true;
                        nodeResults.Add(id, new NodeExecutionResult { NodeId = id, Status = ExecutionStatus.NotExecuted, Success = false });
                        NotifyNodeStatus(id, ExecutionStatus.NotExecuted);
                    }
                } while (skipped);
                foreach (var item in workflow.Nodes)
                {
                    var id = item.Node.NodeId;
                    if (!pending.Contains(id) || running.Count >= maxConcurrency ||
                        !workflow.Predecessors[id].All(source => nodeResults.TryGetValue(source, out var previous) && previous.Success)) continue;
                    if (!leases.TryAcquire(workflow.WorkflowId, id, item.Node.Capabilities, out var lease)) continue;
                    pending.Remove(id);
                    NotifyNodeStatus(id, ExecutionStatus.Running);
                    request.HostInputs.TryGetValue(id, out var host);
                    try
                    {
                        var task = Task.Run(async () => await ExecutionToolInvocation.ExecuteAsync(workflow, item.Node, bindings.ForNode(id), identities[id],
                            expected, outputs, request.VariableValues, request.ParameterValues, host, request.WorkflowImage,
                            log, item.Node.ResultFields).ConfigureAwait(false));
                        running.Add((id, lease!, task));
                    }
                    catch { lease!.Dispose(); throw; }
                }
                if (running.Count == 0)
                {
                    if (pending.Count == 0) break;
                    throw new InvalidOperationException("就绪队列无法派发节点，请检查依赖及资源声明。 ");
                }
                await Task.WhenAny(running.Select(item => item.Task)).ConfigureAwait(false);
                foreach (var finished in running.Where(item => item.Task.IsCompleted).ToArray())
                {
                    running.Remove(finished); finished.Lease.Dispose();
                    try
                    {
                        var output = await finished.Task.ConfigureAwait(false);
                        if (!identities[finished.Id].TryPublish(() => outputs[finished.Id] = output))
                        {
                            output.Dispose();
                            cancellation.ThrowIfCancellationRequested();
                            throw new InvalidOperationException("节点执行身份已失效，拒绝发布结果。 ");
                        }
                        cancellation.ThrowIfCancellationRequested();
                        var previousVariables = request.GlobalProducers.Any(item => item.Source.ScopeId == workflow.WorkflowId
                            && item.Source.NodeId == finished.Id) ? request.VariableValues?.CaptureState() : null;
                        try
                        {
                            ExecutionGlobalProducers.Complete(request, finished.Id, root, scope, cancellation, expected, outputs);
                            request.AcceptResult(items[finished.Id].Node, output);
                        }
                        catch (Exception acceptanceFailure)
                        {
                            if (previousVariables is not null)
                                try { request.VariableValues!.RestoreState(previousVariables); }
                                catch (Exception rollback) { throw new AggregateException("结果接纳失败且运行变量回滚失败。", acceptanceFailure, rollback); }
                            throw;
                        }
                        nodeResults.Add(finished.Id, new NodeExecutionResult { NodeId = finished.Id, Success = output.IsSuccess,
                            Status = output.Status, Result = output, ErrorMessages = output.Diagnostics.Select(issue => issue.Message).ToList() });
                        NotifyNodeCompleted(finished.Id, output);
                        NotifyNodeStatus(finished.Id, output.Status);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        nodeResults[finished.Id] = new NodeExecutionResult { NodeId = finished.Id, Success = false,
                            Status = ExecutionStatus.Failed, ErrorMessages = new List<string> { error.Message } };
                        NotifyNodeStatus(finished.Id, ExecutionStatus.Failed);
                    }
                }
            }
            final = BuildResult(workflow, identities, outputs, nodeResults, timer.Elapsed, false);
            return final;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            final = BuildResult(workflow, identities, outputs, nodeResults, timer.Elapsed, true);
            return final;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            var cleanup = new List<Exception>();
            foreach (var item in running)
            {
                try { var late = await item.Task.ConfigureAwait(false); late.Dispose(); } catch { }
                try { item.Lease.Dispose(); } catch (Exception error) { cleanup.Add(error); }
            }
            foreach (var identity in identities.Values) try { identity.CompleteCallbacks(); } catch (Exception error) { cleanup.Add(error); }
            foreach (var output in outputs.Values) try { output.Dispose(); } catch (Exception error) { cleanup.Add(error); }
            try { request.Complete(); } catch (Exception error) { cleanup.Add(error); }
            try { workflowLease?.Dispose(); } catch (Exception error) { cleanup.Add(error); }
            lock (_gate) { _running = false; _control = null; _state = WorkflowExecutionState.Idle; }
            try { control.Dispose(); } catch (Exception error) { cleanup.Add(error); }
            if (cleanup.Count != 0)
            {
                if (final is not null) try { final.Dispose(); } catch (Exception error) { cleanup.Add(error); }
                if (failure is not null) cleanup.Insert(0, failure);
                throw new AggregateException("运行及资源清理失败。", cleanup);
            }
        }
    }

    private void NotifyNodeCompleted(string nodeId, DeclaredOutputSnapshot output)
    {
        var errors = new List<Exception>();
        if (NodeCompleted is { } handlers)
            foreach (Action<string, DeclaredOutputSnapshot> handler in handlers.GetInvocationList())
                try { handler(nodeId, output); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) return;
        if (LastObserverError is { } prior) errors.Insert(0, prior);
        LastObserverError = new AggregateException("节点结果观察者失败。", errors);
    }

    private void NotifyNodeStatus(string nodeId, ExecutionStatus status)
    {
        var errors = new List<Exception>();
        if (NodeStatusChanged is { } handlers)
            foreach (Action<string, ExecutionStatus> handler in handlers.GetInvocationList())
                try { handler(nodeId, status); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) return;
        if (LastObserverError is { } prior) errors.Insert(0, prior);
        LastObserverError = new AggregateException("节点状态观察者失败。", errors);
    }

    private static ExecutionResult BuildResult(ExecutionWorkflowSnapshot workflow,
        IReadOnlyDictionary<string, ToolExecutionIdentity> identities, IReadOnlyDictionary<string, DeclaredOutputSnapshot> outputs,
        IReadOnlyDictionary<string, NodeExecutionResult> outcomes, TimeSpan elapsed, bool cancelled)
    {
        var owned = new List<DeclaredOutputSnapshot>();
        var nodes = new Dictionary<string, NodeExecutionResult>(outcomes, StringComparer.Ordinal);
        try
        {
            foreach (var item in workflow.Nodes)
            {
                var id = item.Node.NodeId;
                if (!identities.TryGetValue(id, out var identity) || outputs.ContainsKey(id)) continue;
                var schema = new ToolSchema(Array.Empty<Port>(), item.Node.Schema.Outputs, outputDisplay: item.Node.Schema.OutputDisplay);
                var status = cancelled ? ExecutionStatus.Cancelled : nodes.TryGetValue(id, out var existing) && existing.Status == ExecutionStatus.Failed
                    ? ExecutionStatus.Failed : ExecutionStatus.NotExecuted;
                var diagnostics = nodes.TryGetValue(id, out var failed)
                    ? failed.ErrorMessages.Select(message => new Diagnostic("execution", message)) : Array.Empty<Diagnostic>();
                var snapshot = DeclaredOutputSnapshot.WithoutValues(schema, 0, identity.ToInvocationIdentity(id), status,
                    diagnostics, fields: item.Node.ResultFields, sourceWorkflowId: workflow.WorkflowId, executionIdentity: identity);
                owned.Add(snapshot);
                if (!nodes.TryGetValue(id, out var result)) nodes[id] = result = new NodeExecutionResult { NodeId = id, Status = status };
                result.Result = snapshot;
            }
            var success = !cancelled && nodes.Values.All(node => node.Success || node.Status == ExecutionStatus.NotExecuted);
            foreach (var item in workflow.Nodes.Where(item => item.Node.HasResultDisplay))
            {
                if (!outputs.TryGetValue(item.Node.NodeId, out var target) || !target.IsSuccess) continue;
                try
                {
                    var ancestors = new HashSet<string>(StringComparer.Ordinal);
                    var pending = new Stack<string>(workflow.Predecessors[item.Node.NodeId]);
                    while (pending.TryPop(out var id)) if (ancestors.Add(id))
                        foreach (var predecessor in workflow.Predecessors[id]) pending.Push(predecessor);
                    var sources = outputs.Where(pair => ancestors.Contains(pair.Key)
                        && identities.TryGetValue(pair.Key, out var sourceIdentity)
                        && pair.Value.Identity == sourceIdentity.ToInvocationIdentity(pair.Key)).Select(pair => pair.Value).ToArray();
                    using var sourceSet = new ResultDisplaySourceSet(sources);
                    target.AttachDisplaySources(sourceSet);
                }
                catch (Exception error) { target.RejectDisplaySources("显示来源捕获失败：" + error.Message); }
            }
            var aggregate = new ExecutionResult { Success = success, Status = cancelled ? ExecutionStatus.Cancelled : success ? ExecutionStatus.Success : ExecutionStatus.Failed,
                IsStopped = cancelled, ExecutionTime = elapsed, NodeResults = nodes,
                Errors = nodes.Values.SelectMany(node => node.ErrorMessages).ToList() };
            try { aggregate.CaptureDeclaredOutputs(nodes.Values.Select(node => node.Result).Where(result => result is not null)!); }
            catch { aggregate.Dispose(); throw; }
            return aggregate;
        }
        finally { foreach (var snapshot in owned) snapshot.Dispose(); }
    }
}
