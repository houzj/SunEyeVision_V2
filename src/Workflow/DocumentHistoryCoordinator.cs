using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SunEyeVision.Workflow;

public enum DocumentHistoryDirection { Forward, Backward }
public sealed record DocumentHistoryScope(string RootId, string ContainerId);
public sealed record DocumentHistoryEntry(Guid Id, long Sequence, string Description,
    ImmutableArray<DocumentHistoryScope> Scopes);
public sealed record PreparedDocumentHistoryChange(Action Apply, Action Rollback);

/// <summary>Prepare validates identity and captures rollback state without mutating the model.
/// Payloads contain editing state, never execution resources. Disposal releases only owned payloads.</summary>
public interface IDocumentHistoryOperation : IDisposable
{
    string Description { get; }
    IReadOnlyList<DocumentHistoryScope> Scopes { get; }
    PreparedDocumentHistoryChange Prepare(DocumentHistoryDirection direction);
}

public interface IDocumentHistoryReplayGuard
{
    bool CanReplay(DocumentHistoryDirection direction);
}

/// <summary>Effects of one successfully replayed configuration operation, never runtime values.</summary>
internal interface IDocumentHistoryReplayEffects
{
    IReadOnlyList<ScopedNodeIdentity> InvalidatedNodes { get; }
}

/// <summary>One owner-thread cursor for one document session. No WPF or execution dependency.</summary>
public sealed class DocumentHistoryCoordinator : IDisposable
{
    private sealed record Item(DocumentHistoryEntry Entry, IDocumentHistoryOperation Operation);
    private readonly List<Item> _items = new();
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly int _capacity;
    private int _position;
    private long _sequence;
    private bool _busy, _notifying, _closed;
    private Func<IDisposable>? _mutationLeaseProvider;
    public Guid SessionId { get; }
    public long Revision { get; private set; }
    public DocumentHistoryEntry? LastAppliedEntry { get; private set; }
    public IReadOnlyList<ScopedNodeIdentity> LastInvalidatedNodes { get; private set; } = Array.Empty<ScopedNodeIdentity>();
    public int Position => _position;
    public bool IsClosed => _closed;
    public bool IsApplying => _busy;
    public Exception? Fault { get; private set; }
    public Exception? LastObserverError { get; private set; }
    public Exception? LastReleaseError { get; private set; }
    public bool CanUndo => !_closed && !_busy && Fault is null && _position > 0 && Allowed(_items[_position-1], DocumentHistoryDirection.Backward);
    public bool CanRedo => !_closed && !_busy && Fault is null && _position < _items.Count && Allowed(_items[_position], DocumentHistoryDirection.Forward);
    public ImmutableArray<DocumentHistoryEntry> Entries => _items.Select(x => x.Entry).ToImmutableArray();
    public event EventHandler? Changed;

    public DocumentHistoryCoordinator(Guid sessionId, int capacity = 15)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("历史必须属于明确的方案会话。", nameof(sessionId));
        if (capacity is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(capacity));
        SessionId = sessionId;
        _capacity = capacity;
    }

    /// <summary>The document host supplies its shared execution exclusion lease. History itself
    /// stays independent of the UI and does not own the host or a running workflow.</summary>
    public void SetMutationLeaseProvider(Func<IDisposable>? provider)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("历史只能由方案所属线程配置。");
        if (_closed && provider is not null) throw new ObjectDisposedException(nameof(DocumentHistoryCoordinator));
        if (provider is not null && _mutationLeaseProvider is not null && _mutationLeaseProvider != provider)
            throw new InvalidOperationException("历史已有另一个执行门禁宿主。");
        _mutationLeaseProvider = provider;
    }

    private IDisposable? AcquireMutationLease()
    {
        var provider = _mutationLeaseProvider;
        return provider is null ? null : provider() ?? throw new InvalidOperationException("历史执行门禁没有返回租约。");
    }

    public bool ReleaseMutationLeaseProvider(Func<IDisposable> expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if(Environment.CurrentManagedThreadId!=_ownerThread)throw new InvalidOperationException("历史只能由方案所属线程配置。");
        if(_mutationLeaseProvider!=expected)return false;
        _mutationLeaseProvider=null;return true;
    }

    /// <summary>Hold the host execution gate while an owner-thread composite edit
    /// applies its constituent changes before registering one history operation.</summary>
    public IDisposable? BeginCompositeMutation()
    {
        EnsureMutationAllowed();
        return AcquireMutationLease();
    }
    /// <summary>Ownership transfers only after successful application. A failed preparation/application
    /// leaves ownership with the caller and does not clear redo or advance the sequence.</summary>
    public DocumentHistoryEntry Execute(IDocumentHistoryOperation operation)
    {
        EnsureMutationAllowed();
        using var mutationLease = AcquireMutationLease();
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(operation.Scopes);
        if (_items.Any(x => ReferenceEquals(x.Operation, operation))) throw new ArgumentException("同一历史载荷不能重复登记。");
        if (string.IsNullOrWhiteSpace(operation.Description)) throw new ArgumentException("历史操作必须有说明。");
        var scopes = operation.Scopes.ToImmutableArray();
        if (scopes.Any(x => string.IsNullOrWhiteSpace(x.RootId) || string.IsNullOrWhiteSpace(x.ContainerId)))
            throw new ArgumentException("历史操作范围必须有正式根与容器身份。");
        var entry = new DocumentHistoryEntry(Guid.NewGuid(), checked(_sequence + 1), operation.Description, scopes);
        var item = new Item(entry, operation);
        var discarded = _items.Skip(_position).ToArray();
        _items.EnsureCapacity(Math.Max(_items.Count, _position + 1));
        Apply(operation, DocumentHistoryDirection.Forward);
        // No observer or payload disposal runs until the authoritative cursor has committed.
        if (_position < _items.Count) _items.RemoveRange(_position, _items.Count - _position);
        _items.Add(item);
        _position++;
        _sequence = entry.Sequence;
        LastAppliedEntry = entry;
        LastInvalidatedNodes = operation is IDocumentHistoryReplayEffects effects ? effects.InvalidatedNodes : Array.Empty<ScopedNodeIdentity>();
        Revision++;
        Item? trimmed = null;
        if (_items.Count > _capacity) { trimmed = _items[0]; _items.RemoveAt(0); _position--; }
        Release(discarded.Concat(trimmed is null ? Array.Empty<Item>() : new[] { trimmed }));
        Notify();
        return entry;
    }

    public bool Undo()
    {
        EnsureMutationAllowed();
        if (!CanUndo) return false;
        using var mutationLease = AcquireMutationLease();
        var item=_items[_position-1];
        Apply(item.Operation, DocumentHistoryDirection.Backward);
        LastAppliedEntry=item.Entry;
        LastInvalidatedNodes = item.Operation is IDocumentHistoryReplayEffects effects ? effects.InvalidatedNodes : Array.Empty<ScopedNodeIdentity>();
        _position--; Revision++; Notify(); return true;
    }

    public bool Redo()
    {
        EnsureMutationAllowed();
        if (!CanRedo) return false;
        using var mutationLease = AcquireMutationLease();
        var item=_items[_position];
        Apply(item.Operation, DocumentHistoryDirection.Forward);
        LastAppliedEntry=item.Entry;
        LastInvalidatedNodes = item.Operation is IDocumentHistoryReplayEffects effects ? effects.InvalidatedNodes : Array.Empty<ScopedNodeIdentity>();
        _position++; Revision++; Notify(); return true;
    }

    /// <summary>Navigate to the state after the identified entry. Every step uses the same atomic replay;
    /// failure leaves the cursor at the last successful step, not at the requested destination.</summary>
    public void NavigateTo(Guid sessionId, Guid? entryId)
    {
        EnsureMutationAllowed();
        if (sessionId != SessionId) throw new InvalidOperationException("历史请求属于已退出的方案会话。");
        var target = entryId is null ? 0 : _items.FindIndex(x => x.Entry.Id == entryId) + 1;
        if (entryId is not null && target == 0) throw new InvalidOperationException("历史条目已被清理或替换。");
        while (_position > target) if (!Undo()) throw new InvalidOperationException("后续Group或外部编辑尚未撤销，不能越过该历史边界。");
        while (_position < target) if (!Redo()) throw new InvalidOperationException("历史来源已变化，不能继续跳转。");
    }

    private static bool Allowed(Item item, DocumentHistoryDirection direction)
    {
        try { return item.Operation is not IDocumentHistoryReplayGuard guard || guard.CanReplay(direction); }
        catch { return false; }
    }

    private void Apply(IDocumentHistoryOperation operation, DocumentHistoryDirection direction)
    {
        _busy = true;
        try
        {
            var prepared = operation.Prepare(direction);
            ArgumentNullException.ThrowIfNull(prepared.Apply);
            ArgumentNullException.ThrowIfNull(prepared.Rollback);
            try { prepared.Apply(); }
            catch (Exception failure)
            {
                try { prepared.Rollback(); }
                catch (Exception rollback)
                {
                    Fault = new AggregateException("编辑回放及回滚均失败，已阻止继续修改历史。", failure, rollback);
                    throw Fault;
                }
                throw;
            }
        }
        finally
        {
            _busy = false;
            // A fault notification is not a successfully committed replay. Never
            // expose effects left by an earlier successful history step.
            if (Fault is not null) { LastInvalidatedNodes = Array.Empty<ScopedNodeIdentity>(); Notify(); }
        }
    }

    private void EnsureMutationAllowed(bool allowFault = false)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("历史只能由方案所属线程修改。");
        if (_closed) throw new ObjectDisposedException(nameof(DocumentHistoryCoordinator));
        if (_busy || _notifying) throw new InvalidOperationException("历史提交、回放或通知过程中不能重入。");
        if (!allowFault && Fault is not null) throw new InvalidOperationException("历史回滚失败，当前会话需要恢复后才能继续。", Fault);
    }

    private void Notify()
    {
        _notifying = true;
        var errors = new List<Exception>();
        try
        {
            foreach (var handler in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((EventHandler)handler)(this, EventArgs.Empty); } catch (Exception error) { errors.Add(error); }
        }
        finally { _notifying = false; }
        LastObserverError = errors.Count == 0 ? null : new AggregateException("历史已提交，部分观察者刷新失败。", errors);
    }

    private void Release(IEnumerable<Item> items)
    {
        var errors = new List<Exception>();
        var wasBusy = _busy; _busy = true;
        try
        {
            foreach (var item in items)
                try { item.Operation.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        finally { _busy = wasBusy; }
        LastReleaseError = errors.Count == 0 ? null : new AggregateException("历史载荷释放失败。", errors);
    }

    public void Dispose()
    {
        if (_closed) return;
        EnsureMutationAllowed(allowFault: true);
        _closed = true; LastInvalidatedNodes = Array.Empty<ScopedNodeIdentity>();
        var discarded = _items.ToArray(); _items.Clear(); _position = 0; Revision++;
        Release(discarded); Notify(); Changed = null; _mutationLeaseProvider = null;
    }
}
