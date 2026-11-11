using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed record PublishedNodeResult(ScopedNodeIdentity Node, RunIdentity Identity);

public sealed partial class DocumentSession
{
    private readonly Dictionary<ScopedNodeIdentity, DeclaredOutputReader> _publishedRuntimeResults = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<NodeDefinition, ResultHistoryBuffer> _resultHistory = new();

    public IReadOnlyList<ResultHistoryRow> ReadResultHistory(string workflowId, NodeDefinition node)
    {
        lock (_gate)
            return ReferenceEquals(FindNode(workflowId, node.Id), node) && _resultHistory.TryGetValue(node, out var history)
                ? history.Read() : Array.Empty<ResultHistoryRow>();
    }

    public void ClearResultHistory(string workflowId, NodeDefinition node)
    {
        lock (_gate)
            if (ReferenceEquals(FindNode(workflowId, node.Id), node) && _resultHistory.TryGetValue(node, out var history))
                history.Clear();
    }
    public Exception? LastRuntimeReleaseError { get; private set; }
    public event EventHandler<PublishedNodeResult>? RuntimeResultAvailable;

    private void PostResultAvailable(ScopedNodeIdentity key, RunIdentity identity)
    {
        var receipt = new PublishedNodeResult(key, identity);
        bool post;
        lock (_gate)
        {
            if (_closed || _closing || !_publishedRuntimeResults.TryGetValue(key, out var current) || current.Snapshot.Identity != identity) return;
            _runtimeResultNotifications.Enqueue(receipt);
            post = _runtimeOwnerContext is not null && !_runtimeNotificationScheduled;
            if (post) _runtimeNotificationScheduled = true;
        }
        if (post) PostRuntimeChanges();
    }

    private void NotifyRuntimeResult(PublishedNodeResult receipt)
    {
        lock (_gate)
            if (_closed || _closing || !_publishedRuntimeResults.TryGetValue(receipt.Node, out var current) || current.Snapshot.Identity != receipt.Identity) return;
        var errors = new List<Exception>();
        foreach (EventHandler<PublishedNodeResult> handler in RuntimeResultAvailable?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { handler(this, receipt); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) LastObserverError = new AggregateException("运行结果观察者失败。", errors);
    }

    /// <summary>Borrow the actual published result; caller releases the returned reader.</summary>
    public DeclaredOutputReader? AcquirePublishedResult(string workflowId, string nodeId)
    {
        var key = new ScopedNodeIdentity(workflowId, nodeId);
        DeclaredOutputReader? entry;
        lock (_gate) { EnsureOpen(); if (!_publishedRuntimeResults.TryGetValue(key, out entry)) return null; }
        var identity = entry.Snapshot.ExecutionIdentity;
        if (identity is null || !identity.IsDomainCurrent) return null;
        DeclaredOutputReader acquired;
        try { acquired = entry.Snapshot.AcquireReader(); }
        catch (ObjectDisposedException) { return null; }
        bool stale;
        lock (_gate) stale = _closed || _closing || !_publishedRuntimeResults.TryGetValue(key, out var current) || !ReferenceEquals(entry, current);
        if (stale) { acquired.Dispose(); return null; }
        if (!identity.IsDomainCurrent) { acquired.Dispose(); return null; }
        return acquired;
    }

    /// <summary>Check publication ownership without borrowing its values or resources.</summary>
    public bool IsPublishedResultCurrent(string workflowId, string nodeId, DeclaredOutputSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var key = new ScopedNodeIdentity(workflowId, nodeId);
        if (snapshot.ExecutionIdentity is not { IsDomainCurrent: true } identity)
            return false;
        lock (_gate)
            if (_closed || _closing || !_publishedRuntimeResults.TryGetValue(key, out var entry)
                || !ReferenceEquals(entry.Snapshot, snapshot))
                return false;
        return identity.IsDomainCurrent;
    }

    // Session.Dispose calls this only after run and editing activities have drained.
    private IReadOnlyList<IDisposable> TakeInvalidatedRuntimePublications(IEnumerable<ScopedNodeIdentity> invalidated)
    {
        ArgumentNullException.ThrowIfNull(invalidated);
        var removed = new List<IDisposable>();
        lock (_gate)
            foreach (var key in invalidated.Distinct())
                if (_publishedRuntimeResults.Remove(key, out var reader)) removed.Add(reader);
        return removed.AsReadOnly();
    }

    private void ReleaseRuntimePublications()
    {
        DeclaredOutputReader[] readers;
        lock (_gate) { readers = _publishedRuntimeResults.Values.ToArray(); _publishedRuntimeResults.Clear(); _resultHistory.Clear(); }
        var failures = new List<Exception>();
        foreach (var reader in readers) try { reader.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("运行结果 reader 释放失败。", failures);
    }
}
