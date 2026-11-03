namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private readonly Queue<DocumentChangeSet> _runtimeChanges = new();
    private SynchronizationContext? _runtimeOwnerContext;
    private bool _runtimeNotificationScheduled, _runtimeDraining;

    // Called on the document owner when a batch is captured, never on a worker.
    private void InitializeRuntimeNotifications()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("运行捕获必须在方案所属线程执行。 ");
        _runtimeOwnerContext ??= SynchronizationContext.Current;
    }

    // The successful CAS caller already holds _gate. Revision order and queue order coincide.
    private bool EnqueueRuntimeChange(DocumentChangeSet change)
    {
        _runtimeChanges.Enqueue(change);
        if (_runtimeNotificationScheduled || _runtimeOwnerContext is null) return false;
        _runtimeNotificationScheduled = true;
        return true;
    }

    // Always called after releasing both the execution owner and document gates.
    private void PostRuntimeChanges()
    {
        var context = _runtimeOwnerContext;
        if (context is null) return;
        try { context.Post(_ => DrainPostedRuntimeChanges(), null); }
        catch
        {
            lock (_gate) _runtimeNotificationScheduled = false;
            // The owner can still drain explicitly; a notification transport failure must not
            // reverse an already accepted value or silently erase its queued change.
        }
    }

    private void DrainPostedRuntimeChanges()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
        {
            lock (_gate) _runtimeNotificationScheduled = false;
            return;
        }
        DrainRuntimeChanges();
    }

    /// <summary>Deliver accepted runtime changes on the document owner; neutral hosts call this explicitly.</summary>
    public void DrainRuntimeChanges()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("运行值通知必须在方案所属线程执行。 ");
        lock (_gate)
        {
            if (_runtimeDraining) return;
            if (_closed || _closing || _owner.IsRetired || !_directory.IsActive)
            { _runtimeChanges.Clear(); _runtimeNotificationScheduled = false; return; }
            _runtimeDraining = true;
        }
        try
        {
            while (true)
            {
                DocumentChangeSet change;
                lock (_gate)
                {
                    if (_closed || _closing || _owner.IsRetired || !_directory.IsActive)
                    { _runtimeChanges.Clear(); _runtimeNotificationScheduled = false; return; }
                    if (_runtimeChanges.Count == 0) { _runtimeNotificationScheduled = false; return; }
                    change = _runtimeChanges.Dequeue();
                }
                NotifyChanged(change);
            }
        }
        finally { lock (_gate) _runtimeDraining = false; }
    }
}
