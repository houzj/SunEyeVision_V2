using System.Collections.Immutable;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

[Flags]
public enum NodeChangedAspect { Identity = 1, Configuration = 2, Geometry = 4, Presentation = 8, CurrentValues = 16 }
public enum DocumentChangeOrigin { Edit, Undo, Redo, RuntimeCurrentValues }
public enum ConnectionChangeKind { Added, Updated, Removed }
public enum VariableChangeKind { Added, Updated, Removed }
public sealed record NodeChange(ScopedNodeIdentity Identity, NodeDefinition Node, NodeChangedAspect Aspects);
public sealed record RemovedNode(ScopedNodeIdentity Identity, NodeDefinition Node, int OriginalIndex);
public sealed record ConnectionChange(string WorkflowId, ConnectionDefinition Connection, ConnectionChangeKind Kind,
    string? OldSourceNodeId, string? OldTargetNodeId);
public sealed record VariableChange(GlobalVariable Variable, VariableChangeKind Kind, string? OldId, int? OldIndex);
public sealed record DocumentChangeSet(Guid EditSessionId, long Revision, DocumentChangeOrigin Origin,
    ImmutableArray<NodeChange> ChangedNodes, ImmutableArray<RemovedNode> RemovedNodes,
    ImmutableArray<ConnectionChange> ChangedConnections, ImmutableArray<VariableChange> ChangedVariables,
    ImmutableArray<ScopedNodeIdentity> ExecutionInvalidatedNodes);

public sealed partial class DocumentSession
{
    private bool _notifying, _preparing;
    public long ChangeRevision { get; private set; }
    public Exception? LastObserverError { get; private set; }
    public event EventHandler<DocumentChangeSet>? Changed;

    private void EnsureOwnerCanEdit()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("方案编辑必须在所属线程执行。");
        EnsureOpen();
        if (_notifying || _admitting || _preparing) throw new InvalidOperationException("方案通知、准入或事务准备期间不能重入编辑。");
    }

    // The caller holds the session gate and invokes this only after installing the committed state.
    private DocumentChangeSet CreateCommittedChange(DocumentChangeOrigin origin,
        IEnumerable<NodeChange> changedNodes, IEnumerable<RemovedNode> removedNodes,
        IEnumerable<ConnectionChange> changedConnections, IEnumerable<VariableChange> changedVariables,
        IEnumerable<ScopedNodeIdentity> executionInvalidatedNodes)
    {
        var next = checked(ChangeRevision + 1);
        var change = new DocumentChangeSet(EditSessionId, next, origin, changedNodes.ToImmutableArray(),
            removedNodes.ToImmutableArray(), changedConnections.ToImmutableArray(), changedVariables.ToImmutableArray(),
            executionInvalidatedNodes.Distinct().ToImmutableArray());
        ChangeRevision = next;
        return change;
    }
    // Called only after a successful transaction and after releasing its value lock.
    private void NotifyChanged(DocumentChangeSet change)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("方案通知必须在所属线程执行。");
        var observers = Changed;
        if (observers is null) { LastObserverError = null; return; }
        var failures = new List<Exception>();
        _notifying = true;
        try
        {
            foreach (EventHandler<DocumentChangeSet> handler in observers.GetInvocationList())
                try { handler(this, change); } catch (Exception error) { failures.Add(error); }
        }
        finally { _notifying = false; }
        LastObserverError = failures.Count == 0 ? null : new AggregateException("方案变更观察者失败。", failures);
    }
}


