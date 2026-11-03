using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application.Editing;

public enum RootDeletionPolicy { RejectReferenced, MarkDependentsForRebinding }

/// <summary>Private root collection candidate. Issued roots do not become session members until commit.</summary>
public sealed class RootWorkflowCollectionEditDraft : IDisposable
{
    private readonly DocumentSession _session;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly WorkflowScope[] _opening;
    private readonly string[] _openingNames;
    private readonly long _openingRevision;
    private readonly List<RootCollectionDraftEntry> _entries;
    private readonly Dictionary<WorkflowDefinition, RootDeletionPolicy> _deletions = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;
    private long _revision;

    internal RootWorkflowCollectionEditDraft(DocumentSession session, IReadOnlyList<WorkflowScope> scopes, long historyRevision)
    {
        _session = session;
        _opening = scopes.ToArray();
        _openingNames = scopes.Select(scope => scope.Flow.Name).ToArray();
        _openingRevision = historyRevision;
        _entries = scopes.Select(scope => new RootCollectionDraftEntry(scope.Flow, scope.Flow.Name, null, false, null)).ToList();
    }

    public IReadOnlyList<WorkflowDefinition> Workflows
    {
        get { EnsureOwner(); return Array.AsReadOnly(_entries.Select(item => item.Flow).ToArray()); }
    }
    internal DocumentSession Session => _session;
    internal bool HasPendingChanges
    {
        get { EnsureOwner(); return _entries.Count != _opening.Length ||
            _entries.Where((entry, index) => !ReferenceEquals(entry.Flow, _opening[index].Flow) || entry.Name != _openingNames[index]).Any(); }
    }
    public string GetName(WorkflowDefinition flow) { EnsureOwner(); return Find(flow).Name; }

    public WorkflowDefinition Add(string name, int rootIndex)
    {
        EnsureOwner();
        var normalized = UniqueName(name, null);
        var flow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), normalized);
        InsertRoot(new(flow, normalized, null, true, null), rootIndex);
        _revision = checked(_revision + 1);
        return flow;
    }

    public WorkflowDefinition Copy(WorkflowDefinition source, string name, int rootIndex, CanvasPoint? overview = null)
    {
        EnsureOwner(); ArgumentNullException.ThrowIfNull(source);
        var entry = Find(source);
        if (entry.Issued || source.IsSubroutine) throw new InvalidOperationException("只能复制当前方案中的实际根流程。");
        var normalized = UniqueName(name, null);
        var flow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), normalized);
        InsertRoot(new(flow, normalized, source, true, overview), rootIndex);
        _revision = checked(_revision + 1);
        return flow;
    }

    public void Rename(WorkflowDefinition flow, string name)
    {
        EnsureOwner(); var entry = Find(flow);
        if (flow.IsSubroutine) throw new InvalidOperationException("暂停的子流程不能通过根流程入口修改。");
        var normalized = UniqueName(name, flow);
        if (entry.Name == normalized) return;
        _entries[_entries.IndexOf(entry)] = entry with { Name = normalized };
        _revision = checked(_revision + 1);
    }

    public void Remove(WorkflowDefinition flow, RootDeletionPolicy policy = RootDeletionPolicy.RejectReferenced)
    {
        EnsureOwner(); var entry = Find(flow);
        if (flow.IsSubroutine) throw new InvalidOperationException("暂停的子流程不能通过根流程入口删除。");
        if (_entries.Count(item => !item.Flow.IsSubroutine) <= 1)
            throw new InvalidOperationException("方案必须保留至少一个根流程。");
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        _deletions[flow] = policy;
        _entries.Remove(entry);
        _revision = checked(_revision + 1);
    }

    private RootCollectionDraftEntry Find(WorkflowDefinition flow) =>
        _entries.SingleOrDefault(item => ReferenceEquals(item.Flow, flow)) ??
        throw new InvalidOperationException("流程不属于此草稿或已移除。");

    private string UniqueName(string name, WorkflowDefinition? except)
    {
        var normalized = RootWorkflowNameRules.Normalize(name);
        if (_entries.Any(item => !item.Flow.IsSubroutine && !ReferenceEquals(item.Flow, except) &&
            string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("根流程名称已存在。");
        return normalized;
    }

    private void InsertRoot(RootCollectionDraftEntry entry, int rootIndex)
    {
        var roots = _entries.Select((item, index) => (item, index)).Where(pair => !pair.item.Flow.IsSubroutine).ToArray();
        if ((uint)rootIndex > (uint)roots.Length) throw new ArgumentOutOfRangeException(nameof(rootIndex));
        var index = rootIndex == roots.Length ? (roots.Length == 0 ? _entries.Count : roots[^1].index + 1) : roots[rootIndex].index;
        _entries.Insert(index, entry);
    }

    internal RootCollectionDraftSnapshot Capture()
    {
        EnsureOwner();
        var snapshot = new RootCollectionDraftSnapshot(_session, _opening, _openingNames, _openingRevision, Array.AsReadOnly(_entries.ToArray()),
            new System.Collections.ObjectModel.ReadOnlyDictionary<WorkflowDefinition, RootDeletionPolicy>(
                new Dictionary<WorkflowDefinition, RootDeletionPolicy>(_deletions, ReferenceEqualityComparer.Instance)), _revision);
        _session.ValidateRootCollectionOpening(snapshot);
        return snapshot;
    }
    internal bool Matches(RootCollectionDraftSnapshot snapshot) => ReferenceEquals(snapshot.Session, _session) &&
        snapshot.Revision == _revision && !_disposed;
    internal RootWorkflowCollectionEditDraft Rebase(FrozenRootCollection frozen, DocumentEditReceipt receipt)
    {
        EnsureOwner();
        if (!ReferenceEquals(receipt.Session, _session) || !ReferenceEquals(frozen.Draft.Session, _session))
            throw new InvalidOperationException("根流程回执不属于当前方案。");
        var current = _session.GetWorkflows();
        if (receipt.AcceptedRootSnapshot is not { } accepted || accepted.Count != current.Count ||
            accepted.Where((item, index) => !ReferenceEquals(item.Flow, current[index]) || item.Name != current[index].Name).Any())
            throw new InvalidOperationException("根流程回执之后集合已变化，不能覆盖后续编辑。");        foreach (var pair in frozen.Issued)
            if (!receipt.AcceptedRoots.TryGetValue(pair.Key, out var actual) || actual.Id != pair.Key.Id ||
                !_session.GetWorkflows().Any(flow => ReferenceEquals(flow, actual)))
                throw new InvalidOperationException("根流程回执的实际身份已改变。");
        var next = _session.CreateRootCollectionDraft();
        var wanted = new List<RootCollectionDraftEntry>();
        foreach (var entry in _entries)
        {
            if (receipt.AcceptedRoots.TryGetValue(entry.Flow, out var actual))
                wanted.Add(entry with { Flow = actual, Issued = false, CopySource = null });
            else if (!entry.Issued && !next._entries.Any(item => ReferenceEquals(item.Flow, entry.Flow)))
                throw new InvalidOperationException("后续草稿引用了已退出方案的实际流程。");
            else wanted.Add(entry);
        }
        var deletions = new Dictionary<WorkflowDefinition, RootDeletionPolicy>(ReferenceEqualityComparer.Instance);
        foreach (var actual in next._entries.Where(item => !wanted.Any(value => ReferenceEquals(value.Flow, item.Flow))))
        {
            var original = receipt.AcceptedRoots.FirstOrDefault(pair => ReferenceEquals(pair.Value, actual.Flow)).Key ?? actual.Flow;
            deletions.Add(actual.Flow, _deletions.GetValueOrDefault(original, RootDeletionPolicy.RejectReferenced));
        }
        next._entries.Clear(); next._entries.AddRange(wanted);
        foreach (var pair in deletions) next._deletions.Add(pair.Key, pair.Value);
        next._revision = _revision == frozen.Draft.Revision ? 0 : 1;
        return next;
    }
    internal void EnsureCanConsume() => EnsureOwner();
    private void EnsureOwner()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("根流程草稿只能在所属线程使用。");
        if (_disposed) throw new ObjectDisposedException(nameof(RootWorkflowCollectionEditDraft));
    }
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("根流程草稿只能在所属线程使用。");
        _disposed = true;
    }
}

internal sealed record RootCollectionDraftEntry(WorkflowDefinition Flow, string Name, WorkflowDefinition? CopySource, bool Issued, CanvasPoint? Overview);
internal sealed record RootCollectionDraftSnapshot(DocumentSession Session, WorkflowScope[] Opening, string[] OpeningNames,
    long OpeningRevision, IReadOnlyList<RootCollectionDraftEntry> Entries,
    IReadOnlyDictionary<WorkflowDefinition, RootDeletionPolicy> Deletions, long Revision);
