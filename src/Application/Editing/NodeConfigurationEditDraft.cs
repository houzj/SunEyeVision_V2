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

    /// <summary>Apply a complete group through the admitted declaration before replacing the candidate.</summary>
    public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes)
    {
        EnsureCurrent();
        var next = _candidate.WithFieldSettings(changes);
        EnsureCurrent();
        _candidate = next;
        _revision = checked(_revision + 1);
    }

    /// <summary>Private debug value; merged with the live value using the opening revision at commit.</summary>
    public void AcceptPrivateParameterValue(string fieldId, object? value)
    {
        EnsureCurrent();
        var next = _candidate.WithAcceptedParameterValue(fieldId, value);
        EnsureCurrent();
        _candidate = next;
        _revision = checked(_revision + 1);
    }

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
        _consumed = true;
    }
    internal void EnsureCanConsume() => EnsureOwner();

    private void EnsureOwner()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("参数草稿只能在所属方案线程使用。");
        if (_consumed) throw new InvalidOperationException("参数草稿已接受，不能再次提交。");
    }

    private void EnsureCurrent()
    {
        EnsureOwner();
        _session.RequireCurrent(_opening);
    }
}

/// <summary>Frozen candidate and opening identity for the session's one atomic edit operation.</summary>
internal sealed record NodeConfigurationEditProposal(DocumentSession Session, AdmittedNodeView Opening,
    AdmittedToolConfiguration Candidate, long DraftRevision);
