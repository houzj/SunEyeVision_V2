using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application.Editing;

/// <summary>A private candidate for one actual, session-admitted document node.
/// Neither editing nor freezing changes the document, history or published results.</summary>
public sealed class NodeConfigurationEditDraft
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly DocumentSession _session;
    private readonly AdmittedNodeView _opening;
    private AdmittedToolConfiguration _candidate;
    private bool _consumed;
    private long _revision;

    internal NodeConfigurationEditDraft(DocumentSession session, AdmittedNodeView opening,
        AdmittedToolConfiguration? candidate = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _opening = opening ?? throw new ArgumentNullException(nameof(opening));
        if (!ReferenceEquals(opening.Scope.Session, session))
            throw new ArgumentException("草稿节点不属于当前会话。", nameof(opening));
        _candidate = candidate is null ? opening.Configuration : opening.Configuration.WithConfiguration(candidate.Configuration);
    }

    public Guid EditSessionId => _session.EditSessionId;
    internal DocumentSession Session => _session;
    public string WorkflowId => _opening.Scope.WorkflowId;
    public NodeDefinition Node => _opening.Node;
    public ToolConfiguration Configuration => _candidate.Configuration;
    public ToolSchema InputSchema => _candidate.Schema;
    public ToolSchema SettingsSchema => _candidate.SettingsSchema;
    public ToolSchema DisplaySchema => _candidate.DisplaySchema;
    internal AdmittedToolConfiguration CurrentCandidate => _candidate;
    internal ConstantSetting? ReadCurrentParameter(string fieldId) { EnsureCurrent(); return _candidate.Configuration.ReadCurrentParameter(fieldId); }
    internal bool HasPendingChanges => _candidate.ConfigurationIdentity != _opening.Configuration.ConfigurationIdentity ||
        _candidate.SettingsSchema.Inputs.Any(port => _candidate.Configuration.CurrentValueChangedFrom(port.Id, _opening.Configuration.Configuration));

    /// <summary>Apply a complete group through the admitted declaration before replacing the candidate.</summary>
    public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, IReadOnlyList<string>? acceptedFieldOrder = null)
    {
        EnsureCurrent();
        var next = _candidate.WithFieldSettings(changes, acceptedFieldOrder);
        EnsureCurrent();
        Interlocked.Exchange(ref _revision, checked(_revision + 1));
        _candidate = next;
    }

    internal AdmittedToolConfiguration CapturePresentationCandidate()
    {
        EnsureCurrent();
        return _candidate;
    }

    internal NodeConfigurationEditDraft PreviewFields(IReadOnlyDictionary<string, ParameterSetting> changes,
        IReadOnlyList<string>? acceptedFieldOrder)
    {
        EnsureCurrent();
        var next = _candidate.WithFieldSettings(changes, acceptedFieldOrder);
        EnsureCurrent();
        return new NodeConfigurationEditDraft(_session, _opening, next);
    }

    /// <summary>Private debug value; merged with the live value using the opening revision at commit.</summary>
    public void AcceptPrivateParameterValue(string fieldId, object? value)
    {
        EnsureCurrent();
        Interlocked.Exchange(ref _revision, checked(_revision + 1));
        var next = _candidate.WithAcceptedParameterValue(fieldId, value);
        EnsureCurrent();
        _candidate = next;
    }

    // Preparation may call admitted tool validation; it must occur outside the Session gate.
    internal PreparedPrivateValues PreparePrivateParameterValues(IReadOnlyDictionary<string, object?> values, NodeDraftDebugToken token)
    {
        ArgumentNullException.ThrowIfNull(values); ArgumentNullException.ThrowIfNull(token);
        EnsureOwner();
        if (!IsCapturedCurrent(token)) throw new InvalidOperationException("调试回执的草稿版本已失效。");
        var previous = _candidate; var candidate = previous;
        foreach (var pair in values) candidate = candidate.WithAcceptedParameterValue(pair.Key, pair.Value);
        if (!IsCapturedCurrent(token) || !ReferenceEquals(previous, _candidate))
            throw new InvalidOperationException("调试准备期间草稿版本已变化。");
        return new PreparedPrivateValues(this, token, previous, candidate, values.Count != 0);
    }

    // Called only inside the owning Session gate after source identities are revalidated.
    internal void InstallPreparedPrivateValues(PreparedPrivateValues prepared)
    {
        EnsureOwner();
        if (!ReferenceEquals(prepared.Draft, this) || !IsCapturedCurrent(prepared.Token) || !ReferenceEquals(_candidate, prepared.Previous))
            throw new InvalidOperationException("调试安装期间草稿版本已变化。");
        if (!prepared.HasValues) return;
        Interlocked.Exchange(ref _revision, checked(_revision + 1));
        _candidate = prepared.Candidate;
    }
    // Debug workers may only inspect this immutable stamp; they never access the
    // owner-thread draft or document members. The Session separately validates scope.
    internal NodeDraftDebugToken CaptureDebugToken()
    {
        EnsureCurrent();
        return new NodeDraftDebugToken(this, Interlocked.Read(ref _revision));
    }

    internal bool IsCapturedCurrent(NodeDraftDebugToken token) => token is not null &&
        ReferenceEquals(token.Draft, this) && !Volatile.Read(ref _consumed) &&
        Interlocked.Read(ref _revision) == token.Revision;

    internal NodeConfigurationEditProposal Freeze()
    {
        EnsureCurrent();
        return new(_session, _opening, _candidate.WithConfiguration(_candidate.Configuration.Copy()), _revision);
    }

    internal bool Matches(NodeConfigurationEditProposal frozen) => ReferenceEquals(frozen.Session, _session) &&
        ReferenceEquals(frozen.Opening.Node, _opening.Node) && frozen.DraftRevision == _revision;

    internal void MarkConsumed()
    {
        EnsureOwner();
        Volatile.Write(ref _consumed, true);
    }
    internal void EnsureCanConsume() => EnsureOwner();

    private void EnsureOwner()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("参数草稿只能在所属方案线程使用。");
        if (Volatile.Read(ref _consumed)) throw new InvalidOperationException("参数草稿已接受，不能再次提交。");
    }

    private void EnsureCurrent()
    {
        EnsureOwner();
        _session.RequireCurrent(_opening);
    }
}

/// <summary>Frozen candidate and opening identity for the session's one atomic edit operation.</summary>
internal sealed record NodeDraftDebugToken(NodeConfigurationEditDraft Draft, long Revision);

internal sealed record NodeConfigurationEditProposal(DocumentSession Session, AdmittedNodeView Opening,
    AdmittedToolConfiguration Candidate, long DraftRevision);

internal sealed record PreparedPrivateValues(NodeConfigurationEditDraft Draft, NodeDraftDebugToken Token, AdmittedToolConfiguration Previous, AdmittedToolConfiguration Candidate, bool HasValues);
