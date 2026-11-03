using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application.Editing;

/// <summary>Private root configuration candidate; a flow remains owned by its document session.</summary>
public sealed class RootWorkflowConfigurationEditDraft
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly DocumentSession _session;
    private readonly WorkflowScope _scope;
    private readonly RootWorkflowConfiguration _opening;
    private RootWorkflowConfiguration _configuration;
    private bool _consumed;
    private long _revision;

    internal RootWorkflowConfigurationEditDraft(DocumentSession session, WorkflowScope scope, RootWorkflowConfiguration? candidate = null)
    {
        _session = session;
        _scope = scope;
        session.ValidateScope(scope);
        if (scope.Flow.IsSubroutine) throw new InvalidOperationException("根配置只适用于根流程。");
        _opening = scope.Flow.RootConfiguration;
        _configuration = candidate ?? _opening;
    }

    public Guid EditSessionId => _session.EditSessionId;
    internal DocumentSession Session => _session;
    public string WorkflowId => _scope.WorkflowId;
    public WorkflowDefinition Workflow => _scope.Flow;
    public RootWorkflowConfiguration Configuration
    {
        get => _configuration;
        set { EnsureCurrent(); _configuration = value ?? throw new ArgumentNullException(nameof(value)); _revision = checked(_revision + 1); }
    }

    internal RootWorkflowConfigurationEditProposal Freeze()
    {
        EnsureCurrent();
        _configuration.Validate();
        return new(_session, _scope, _opening, _configuration, _revision);
    }

    internal bool Matches(RootWorkflowConfigurationEditProposal frozen) => ReferenceEquals(frozen.Session, _session) &&
        ReferenceEquals(frozen.Scope.Flow, _scope.Flow) && frozen.DraftRevision == _revision;

    internal void MarkConsumed() { EnsureOwner(); _consumed = true; }
    internal void EnsureCanConsume() => EnsureOwner();
    private void EnsureOwner()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("根配置草稿只能在方案所属线程使用。");
        if (_consumed) throw new InvalidOperationException("根配置草稿已接受。");
    }
    private void EnsureCurrent()
    {
        EnsureOwner();
        _session.ValidateScope(_scope);
        if (_scope.Flow.IsSubroutine || !ReferenceEquals(_scope.Flow.RootConfiguration, _opening))
            throw new InvalidOperationException("根流程或其配置已变化，不能提交过期草稿。");
    }
}

internal sealed record RootWorkflowConfigurationEditProposal(DocumentSession Session, WorkflowScope Scope,
    RootWorkflowConfiguration Opening, RootWorkflowConfiguration Candidate, long DraftRevision);
