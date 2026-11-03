using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>Capture one private draft against the current document and retained published readers.</summary>
    internal DocumentDebugInvocation CaptureDebug(DocumentDebugSession debugSession, NodeConfigurationEditDraft draft, IHostInputPreparer? hostInput,
        Action<string> log, CancellationToken cancellation, IReadOnlyCollection<string>? previewInputs, IHostInputFactory? hostFactory, Action onReleased)
    {
        ArgumentNullException.ThrowIfNull(draft); ArgumentNullException.ThrowIfNull(log);
        EnsureOwnerCanEdit();
        var token = draft.CaptureDebugToken();
        var proposal = draft.Freeze();
        if (!ReferenceEquals(draft.Session, this)) throw new InvalidOperationException("草稿不属于当前方案。");
        var readers = new List<DeclaredOutputReader>();
        var displayReaders = new List<DeclaredOutputReader>();
        ExecutionVariableValues? variables = null;
        ToolExecutionOwner? debugOwner = null;
        IHostInputPreparer? createdHost = null;
        IDisposable? activity = AcquireActivityLease("私有工具调试");
        CancellationTokenSource? debugCancellation = null;
        try
        {
            Dictionary<WorkflowDefinition, GraphState> states;
            WorkflowDefinition[] flows;
            GlobalVariable[] definitions;
            GlobalVariable[] actualVariables;
            Dictionary<string, ExecutionWorkflowSnapshot> snapshots;
            List<ExecutionGlobalProducerOperation> producers;
            HashSet<ScopedNodeIdentity> debugSources;
            lock (_gate)
            {
                EnsureOwnerCanEdit(); _preparing = true;
                try
                {
                    _ = RequireCurrent(proposal.Opening);
                    var lifetime = CaptureNodeLifetime(proposal.Opening.Scope, draft.Node);
                    debugCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Retirement, debugSession.Retirement);
                    flows = Document.Workflows.ToArray();
                    states = flows.ToDictionary(flow => flow, GraphState.Capture,
                        (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
                    actualVariables = Document.GlobalVariables.ToArray();
                    lock (GlobalVariable.ValueGate) definitions = GlobalVariable.CaptureDefinitions(actualVariables).ToArray();
                    var targetFlow = proposal.Opening.Scope.Flow;
                    var originalTargetState = states[targetFlow];
                    states[targetFlow] = originalTargetState.WithConfigurations(new Dictionary<NodeDefinition, ToolConfiguration>(ReferenceEqualityComparer.Instance)
                        { [draft.Node] = proposal.Candidate.Configuration });
                    var dependencies = GraphDependencySnapshot.Create(Document, states, definitions, AdmitConfiguration);
                    debugSources = new HashSet<ScopedNodeIdentity>();
                    var waiting = new Stack<ScopedNodeIdentity>(dependencies.GetPredecessors(new(draft.WorkflowId, draft.Node.Id)));
                    while (waiting.TryPop(out var source))
                        if (debugSources.Add(source))
                            foreach (var prior in dependencies.GetPredecessors(source)) waiting.Push(prior);
                    snapshots = new Dictionary<string, ExecutionWorkflowSnapshot>(StringComparer.Ordinal);
                    foreach (var flow in flows)
                    {
                        var scope = OpenScope(flow.Id);
                        var nodes = new List<ExecutionWorkflowNode>();
                        foreach (var item in states[flow].Nodes)
                        {
                            var actual = RequireNode(scope, item.Node.Id);
                            if (!ReferenceEquals(actual.Node, item.Node)) throw new InvalidOperationException("调试捕获期间节点身份已变化。");
                            var admitted = ReferenceEquals(item.Node, draft.Node) ? proposal.Candidate : actual.Configuration;
                            if (!ReferenceEquals(item.Node, draft.Node) && !ReferenceEquals(item.Configuration, actual.Node.Configuration))
                                throw new InvalidOperationException("调试捕获期间节点配置已变化。");
                            var node = new ExecutionNodeSnapshot(flow.Id, item.Node.Id, admitted.Configuration, admitted.Definition,
                                admitted.Directory, admitted.Schema, admitted.SettingsSchema, admitted.DisplaySchema,
                                admitted.ReferenceBehaviors, admitted.BindingNames, admitted.ActiveInputs, admitted.HostDeclarations,
                                admitted.ManualReferenceSources, _owner);
                            nodes.Add(new(node, item.Enabled));
                        }
                        var predecessors = states[flow].Nodes.ToDictionary(item => item.Node.Id,
                            item => (IReadOnlyCollection<string>)dependencies.GetPredecessors(new(flow.Id, item.Node.Id))
                                .Where(source => source.WorkflowId == flow.Id).Select(source => source.NodeId).ToArray(), StringComparer.Ordinal);
                        var controls = states[flow].Nodes.ToDictionary(item => item.Node.Id,
                            item => (IReadOnlyCollection<string>)states[flow].Edges.Where(edge => edge.TargetNodeId == item.Node.Id)
                                .Select(edge => edge.SourceNodeId).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
                        var edges = states[flow].Edges.Select(edge => new ExecutionControlEdge(edge.Edge.Id, edge.SourceNodeId, edge.TargetNodeId)).ToArray();
                        snapshots.Add(flow.Id, new ExecutionWorkflowSnapshot(flow.Id, ChangeRevision, _directory, _owner, nodes, predecessors, controls, edges));
                    }
                    states[targetFlow] = originalTargetState;
                    var capturedVariables = CaptureRuntimeVariables(flows, definitions);
                    variables = capturedVariables.Values;
                    producers = capturedVariables.Producers;
                    if (hostInput is null && previewInputs is null && hostFactory is not null && proposal.Candidate.HostDeclarations.HostInputs is { } declaration)
                    {
                        var formal = CaptureHostInputContextCore(proposal.Opening.Scope, proposal.Opening)
                            ?? throw new InvalidOperationException("调试宿主声明缺失。");
                        var configuration = proposal.Candidate.Configuration;
                        var context = new HostInputFactoryContext(formal.Node, formal.EditSessionId, formal.NodeGeneration,
                            formal.NodeRetirement, configuration.ToolId, proposal.Candidate.Schema, proposal.Candidate.SettingsSchema,
                            new FieldSettings(configuration.Fields.Declarations.Values, configuration.Fields.Capture()),
                            configuration.CaptureCurrentValues(), declaration, formal.ResourcePaths, () =>
                            {
                                formal.EnsureCurrent();
                                if (debugSession.Retirement.IsCancellationRequested || !draft.IsCapturedCurrent(token))
                                    throw new InvalidOperationException("私有调试草稿或编辑器已退役。");
                            }, debugSession.Id, debugSession.Retirement);
                        createdHost = hostFactory.Create(context);
                        hostInput = createdHost;
                    }
                    if (!draft.IsCapturedCurrent(token) || !states.All(pair => pair.Value.MatchesCurrent(pair.Key)))
                        throw new InvalidOperationException("调试捕获期间草稿或流程已变化。");
                }
                finally { _preparing = false; }
            }
            foreach (var flow in flows)
                foreach (var node in states[flow].Nodes)
                {
                    if (!debugSources.Contains(new ScopedNodeIdentity(flow.Id, node.Node.Id))) continue;
                    var reader = AcquirePublishedResult(flow.Id, node.Node.Id);
                    if (reader is null) continue;
                    if (!reader.Snapshot.IsSuccess || !reader.Snapshot.IsComplete) { reader.Dispose(); continue; }
                    readers.Add(reader);
                    var display = AcquirePublishedResult(flow.Id, node.Node.Id);
                    if (display is not null) displayReaders.Add(display);
                }
            bool Current()
            {
                if (!draft.IsCapturedCurrent(token) || _owner.IsRetired || !_directory.IsActive ||
                    readers.Concat(displayReaders).Any(reader => reader.Snapshot.ExecutionIdentity is not { IsDomainCurrent: true })) return false;
                lock (_gate)
                {
                    if (_closed || _closing || Document.Id != _documentId || !draft.IsCapturedCurrent(token) ||
                        flows.Length != Document.Workflows.Count || flows.Where((flow, index) => !ReferenceEquals(flow, Document.Workflows[index])).Any() ||
                        !states.All(pair => pair.Value.MatchesCurrent(pair.Key))) return false;
                    foreach (var reader in readers.Concat(displayReaders))
                    {
                        var snapshot = reader.Snapshot;
                        if (snapshot.SourceWorkflowId is not { } flowId ||
                            !_publishedRuntimeResults.TryGetValue(new ScopedNodeIdentity(flowId, snapshot.Identity.NodeId), out var current) ||
                            !ReferenceEquals(snapshot, current.Snapshot)) return false;
                    }
                    lock (GlobalVariable.ValueGate)
                    {
                        var actual = Document.GlobalVariables.ToArray();
                        if (actual.Length != actualVariables.Length || actual.Where((value, index) => !ReferenceEquals(value, actualVariables[index])).Any()) return false;
                        var current = GlobalVariable.CaptureDefinitions(actual).ToArray();
                        if (current.Length != definitions.Length || current.Where((value, index) => value.Id != definitions[index].Id ||
                            value.ValueRevision != definitions[index].ValueRevision || VariableConfigurationSignature(value) != VariableConfigurationSignature(definitions[index])).Any()) return false;
                    }
                    return true;
                }
            }
            if (!Current()) throw new InvalidOperationException("调试来源、草稿或结果在捕获期间已变化。");
            var workflow = snapshots[draft.WorkflowId];
            var target = workflow.Nodes.Single(item => item.Node.NodeId == draft.Node.Id).Node;
            debugOwner = new ToolExecutionOwner();
            var invocation = ExecutionDebugInvocation.Capture(workflow, target, debugOwner, readers, displayReaders,
                snapshots.Values.ToArray(), producers, variables, hostInput, null, null, Current, log, (debugCancellation ?? throw new InvalidOperationException("调试取消域缺失。")).Token, previewInputs);
            variables.Dispose(); variables = null;
            var wrapper = new DocumentDebugInvocation(draft, token, invocation, debugOwner, Current, activity, debugCancellation, onReleased);
            debugCancellation = null;
            createdHost = null;
            activity = null;
            return wrapper;
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            foreach (var reader in readers.Concat(displayReaders)) try { reader.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { variables?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { createdHost?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { debugOwner?.Retire(); } catch (Exception error) { errors.Add(error); }
            try { activity?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { debugCancellation?.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 1) throw new AggregateException("调试捕获与资源释放失败。", errors);
            throw;
        }
    }
}

/// <summary>Owns one private debug execution and applies values only on the original draft owner thread.</summary>
public sealed class DocumentDebugInvocation : IDisposable
{
    private readonly NodeConfigurationEditDraft _draft;
    private readonly NodeDraftDebugToken _token;
    private readonly ExecutionDebugInvocation _invocation;
    private readonly ToolExecutionOwner _owner;
    private readonly Func<bool> _isCurrent;
    private int _completed, _accepted, _disposed;
    private IDisposable? _activity;
    private CancellationTokenSource? _cancellation;
    private Action? _onReleased;
    internal DocumentDebugInvocation(NodeConfigurationEditDraft draft, NodeDraftDebugToken token,
        ExecutionDebugInvocation invocation, ToolExecutionOwner owner, Func<bool> isCurrent, IDisposable activity, CancellationTokenSource cancellation, Action onReleased)
    { _draft = draft; _token = token; _invocation = invocation; _owner = owner; _isCurrent = isCurrent; _activity = activity; _cancellation = cancellation; _onReleased = onReleased; }
    public async Task<DeclaredOutputSnapshot> ExecuteAsync()
    {
        var result = await _invocation.ExecuteAsync().ConfigureAwait(false);
        if (result.IsSuccess) Volatile.Write(ref _completed, 1);
        return result;
    }
    public void Cancel() { try { Volatile.Read(ref _cancellation)?.Cancel(); } catch (ObjectDisposedException) { } }
    public ExecutionInputPreviewLease CaptureInputs() => _invocation.CaptureInputs();
    public void AcceptParameters()
    {
        if (Volatile.Read(ref _disposed) != 0 || _owner.IsRetired || Volatile.Read(ref _completed) == 0 || Volatile.Read(ref _accepted) != 0 || !_isCurrent())
            throw new InvalidOperationException("调试结果未成功完成、已接纳或来源已失效。");
        _draft.AcceptPrivateParameterValues(_invocation.AcceptedParameters, _token);
        Interlocked.Exchange(ref _accepted, 1);
    }
    public void Dispose()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        _invocation.Dispose(); // Reject while execution or preview still owns readers; keep the session lease.
        Interlocked.Exchange(ref _disposed, 1);
        try { _owner.Retire(); }
        finally
        {
            Interlocked.Exchange(ref _cancellation, null)?.Dispose();
            Interlocked.Exchange(ref _activity, null)?.Dispose();
            Interlocked.Exchange(ref _onReleased, null)?.Invoke();
        }
    }
}
