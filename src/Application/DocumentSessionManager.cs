using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed record DocumentSessionReplacement(DocumentSession? Previous, DocumentSession? Current);

/// <summary>Owner-thread authority for the one active document. UI observes replacements;
/// execution and persistence continue to use that exact document session.</summary>
public sealed partial class DocumentSessionManager : IDisposable
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly RuntimeDirectorySnapshot _directory;
    private bool _busy, _closed;
    public DocumentSession? CurrentSession { get; private set; }
    public Exception? LastObserverError { get; private set; }
    public Exception? LastCloseError { get; private set; }
    public event EventHandler<DocumentSessionReplacement>? SessionChanged;

    public DocumentSessionManager(RuntimeDirectorySnapshot directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        if (!directory.IsActive) throw new InvalidOperationException("运行目录已退役。");
    }

    public DocumentSession CreateNew(string name = "未命名方案", bool discardPendingEdits = false)
    {
        EnsureAvailable();
        _busy = true;
        try
        {
            var candidate = DocumentSession.CreateNew(_directory, name);
            Install(candidate, discardPendingEdits);
            return candidate;
        }
        finally { _busy = false; }
    }

    // Only this manager's own creation/opening paths may publish a candidate. There is no
    // public "adopt arbitrary document" route or second registry keyed by display names.
    private void Install(DocumentSession candidate, bool discardPendingEdits)
    {
        if (!ReferenceEquals(candidate.Directory, _directory))
            throw new InvalidOperationException("候选方案不属于管理器的实际运行目录。");
        var previous = CurrentSession;
        try
        {
            if (previous is not null && previous.Editing.HasPendingChanges && !discardPendingEdits)
                throw new InvalidOperationException("方案仍有未提交编辑，请先处理后再切换。");
            if (previous is not null)
            {
                try { previous.Dispose(); LastCloseError = null; }
                catch (Exception error)
                {
                    // A close that could not retire its active execution keeps the old
                    // document authoritative. A retired document must never be resurrected.
                    if (!previous.ExecutionOwner.IsRetired) throw;
                    LastCloseError = error;
                }
            }
        }
        catch (Exception failure)
        {
            try { candidate.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("方案切换失败，候选释放也失败。", failure, cleanup); }
            throw;
        }
        CurrentSession = candidate;
        Notify(previous, candidate);
    }

    public void CloseCurrent(bool discardPendingEdits = false)
    {
        EnsureAvailable(allowRetiredDirectory: true);
        _busy = true;
        try
        {
            var previous = CurrentSession;
            if (previous is null) return;
            if (previous.Editing.HasPendingChanges && !discardPendingEdits)
                throw new InvalidOperationException("方案仍有未提交编辑，请先处理后再关闭。");
            try { previous.Dispose(); LastCloseError = null; }
            catch (Exception error)
            {
                if (!previous.ExecutionOwner.IsRetired) throw;
                LastCloseError = error;
            }
            CurrentSession = null;
            Notify(previous, null);
        }
        finally { _busy = false; }
    }

    private void Notify(DocumentSession? previous, DocumentSession? current)
    {
        var receipt = new DocumentSessionReplacement(previous, current);
        var errors = new List<Exception>();
        foreach (EventHandler<DocumentSessionReplacement> handler in SessionChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { handler(this, receipt); } catch (Exception error) { errors.Add(error); }
        LastObserverError = errors.Count == 0 ? null : new AggregateException("方案已切换，部分观察者失败。", errors);
    }

    private void EnsureAvailable(bool allowRetiredDirectory = false)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("方案管理器只能在所属线程使用。");
        if (_closed || _busy || !allowRetiredDirectory && !_directory.IsActive) throw new InvalidOperationException("方案管理器已关闭、正在切换或目录已退役。");
    }

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("方案管理器只能在所属线程释放。");
        if (_closed) return;
        CloseCurrent();
        _closed = true;
        SessionChanged = null;
    }
}
