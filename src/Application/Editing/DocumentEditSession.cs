namespace SunEyeVision.Application.Editing;

/// <summary>One private editor scope; it prepares candidates without publishing document state.</summary>
public interface IDocumentEditParticipant
{
    Guid ScopeId { get; }
    string ScopeKey { get; }
    bool HasPendingChanges { get; }
    bool IsValid { get; }
    DocumentEditProposal Prepare(NodeConfigurationEditHost host);
    void Cancel();
}

/// <summary>Owner-thread collection of private editors. DocumentSession remains the sole publication and history owner.</summary>
public sealed class DocumentEditSession : IDisposable
{
    private readonly DocumentSession _session;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly Dictionary<Guid, IDocumentEditParticipant> _participants = new();
    private bool _busy, _closed;

    internal DocumentEditSession(DocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Host = new NodeConfigurationEditHost(session);
    }

    public NodeConfigurationEditHost Host { get; }
    public Guid SessionId => _session.EditSessionId;
    public int RegisteredScopeCount => _participants.Count;
    public bool HasPendingChanges => _participants.Values.ToArray().Any(participant => participant.HasPendingChanges);
    public Exception? LastReleaseError { get; private set; }

    public void Register(IDocumentEditParticipant participant)
    {
        EnsureAvailable(); ArgumentNullException.ThrowIfNull(participant);
        _busy = true;
        try
        {
            PruneInvalidScopes();
            if (!participant.IsValid || participant.ScopeId == Guid.Empty || string.IsNullOrWhiteSpace(participant.ScopeKey))
                throw new ArgumentException("编辑参与者的会话范围无效。", nameof(participant));
            if (_participants.TryGetValue(participant.ScopeId, out var current) && ReferenceEquals(current, participant)) return;
            if (_participants.ContainsKey(participant.ScopeId) || _participants.Values.Any(item => item.ScopeKey == participant.ScopeKey))
                throw new InvalidOperationException("同一编辑范围不能注册多个发布者。");
            _participants.Add(participant.ScopeId, participant);
        }
        finally { _busy = false; }
    }

    public void Unregister(Guid scopeId)
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("编辑参与者只能在方案所属线程注销。");
        // Cancellation on document close is allowed to unregister itself after the session
        // has retired; it must not query the retired document or start a new transaction.
        _participants.Remove(scopeId);
    }

    internal PreparedDocumentEdits Prepare(Guid? scopeId = null)
    {
        EnsureAvailable();
        _busy = true;
        try
        {
            PruneInvalidScopes();
            var selected = scopeId is { } id
                ? new[] { _participants.TryGetValue(id, out var participant) ? participant : throw new InvalidOperationException("编辑范围不存在。") }
                : _participants.Values.ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("没有需要准备的编辑范围。");
            var prepared = selected.Select(participant => (Participant: participant, Proposal: participant.Prepare(Host))).ToArray();
            foreach (var item in prepared)
                if (!item.Participant.IsValid || !_participants.TryGetValue(item.Participant.ScopeId, out var current) ||
                    !ReferenceEquals(current, item.Participant))
                    throw new InvalidOperationException("准备期间编辑范围已退出，草稿未接受。");
            return new(DocumentEditProposal.Combine(_session, prepared.Select(item => item.Proposal).ToArray()), prepared);
        }
        finally { _busy = false; }
    }

    private void EnsureAvailable()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("编辑参与者只能在方案所属线程使用。");
        if (_closed || _busy) throw new InvalidOperationException("编辑会话已关闭或正在准备事务。");
        _ = _session.GetWorkflows();
    }

    private void PruneInvalidScopes()
    {
        var errors = new List<Exception>();
        foreach (var participant in _participants.Values.ToArray().Where(item => !item.IsValid))
        {
            _participants.Remove(participant.ScopeId);
            try { participant.Cancel(); } catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count != 0) throw new AggregateException("失效编辑范围释放失败。", errors);
    }

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("编辑参与者只能在方案所属线程关闭。");
        if (_closed) return;
        if (_busy) throw new InvalidOperationException("准备期间不能关闭编辑参与者。");
        _closed = true;
        var errors = new List<Exception>();
        foreach (var participant in _participants.Values.ToArray())
            try { participant.Cancel(); } catch (Exception error) { errors.Add(error); }
        _participants.Clear();
        try { Host.Close(); } catch (Exception error) { errors.Add(error); }
        LastReleaseError = errors.Count == 0 ? null : new AggregateException("编辑参与者释放失败。", errors);
    }
}

internal sealed record PreparedDocumentEdits(DocumentEditProposal Combined,
    IReadOnlyList<(IDocumentEditParticipant Participant, DocumentEditProposal Proposal)> Parts);
