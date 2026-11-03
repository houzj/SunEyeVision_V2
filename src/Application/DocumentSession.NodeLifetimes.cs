using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    // A removed flow never revives old scopes, even when history restores the same object.
    // The collection transaction removes this entry under the session gate before publishing.
    private readonly Dictionary<WorkflowDefinition, Guid> _flowGenerations = new(ReferenceEqualityComparer.Instance);
    private Guid GetFlowGeneration(WorkflowDefinition flow)
    {
        if (!_flowGenerations.TryGetValue(flow, out var generation))
            _flowGenerations.Add(flow, generation = Guid.NewGuid());
        return generation;
    }
    private void RetireFlowGeneration(WorkflowDefinition flow) => _flowGenerations.Remove(flow);
    private sealed record NodeLifetime(Guid Generation, CancellationTokenSource Cancellation);
    private readonly CancellationTokenSource _documentRetirement = new();
    private readonly Dictionary<NodeDefinition, NodeLifetime> _nodeLifetimes = new(ReferenceEqualityComparer.Instance);
    private readonly List<CancellationTokenSource> _retiredNodeLifetimes = new();
    public Exception? LastNodeRetirementError { get; private set; }

    // Host-side capture only. A plugin never supplies the generation or an actual domain entity.
    internal (Guid Generation, CancellationToken Retirement) CaptureNodeLifetime(WorkflowScope scope, NodeDefinition node)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("节点生命周期只能由方案所属线程捕获。");
        lock (_gate)
        {
            ValidateScopeCore(scope);
            if (scope.Flow.Nodes.Count(candidate => ReferenceEquals(candidate, node)) != 1 ||
                scope.Flow.Nodes.Count(candidate => candidate.Id == node.Id) != 1)
                throw new InvalidOperationException("节点不属于唯一实际流程，无法签发生命周期。");
            if (!_nodeLifetimes.TryGetValue(node, out var lifetime))
            {
                lifetime = new NodeLifetime(Guid.NewGuid(), CancellationTokenSource.CreateLinkedTokenSource(_documentRetirement.Token));
                _nodeLifetimes.Add(node, lifetime);
            }
            return (lifetime.Generation, lifetime.Cancellation.Token);
        }
    }

    // Queue under the transaction gate; cancellation callbacks run only after every gate is released.
    private void QueueRemovedNodeLifetimes(DocumentChangeSet change)
    {
        foreach (var removed in change.RemovedNodes)
            if (_nodeLifetimes.Remove(removed.Node, out var lifetime)) _retiredNodeLifetimes.Add(lifetime.Cancellation);
    }

    private void RetireNodeLifetimes()
    {
        CancellationTokenSource[] sources;
        lock (_gate)
        {
            sources = _nodeLifetimes.Values.Select(lifetime => lifetime.Cancellation).Concat(_retiredNodeLifetimes).ToArray();
            _nodeLifetimes.Clear(); _retiredNodeLifetimes.Clear(); _flowGenerations.Clear();
        }
        try { _documentRetirement.Cancel(throwOnFirstException: false); }
        finally
        {
            foreach (var source in sources) source.Dispose();
            _documentRetirement.Dispose();
        }
    }
}