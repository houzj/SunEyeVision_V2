using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application.Editing;

/// <summary>One private root candidate. Run settings join editing; navigation stays independent.</summary>
public sealed class RootWorkflowConfigurationEditDraft
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly DocumentSession _session;
    private readonly WorkflowScope _scope;
    private readonly RootWorkflowConfiguration _opening;
    private readonly RootWorkspaceSettings _openingWorkspace;
    private RootWorkflowConfiguration _configuration;
    private RootWorkspaceSettings _workspace;
    private bool _consumed;
    private long _revision;

    internal RootWorkflowConfigurationEditDraft(DocumentSession session, WorkflowScope scope,
        RootWorkflowConfiguration? candidate = null, RootWorkspaceSettings? workspace = null)
    {
        _session = session; _scope = scope;
        session.ValidateScope(scope);
        if (scope.Flow.IsSubroutine) throw new InvalidOperationException("根配置只适用于根流程。");
        _opening = scope.Flow.RootConfiguration;
        _openingWorkspace = scope.Flow.WorkspaceSettings;
        _configuration = candidate ?? _opening;
        _workspace = workspace is null ? _openingWorkspace : DocumentSession.MergeWorkspaceRunSettings(workspace, _openingWorkspace);
    }

    public Guid EditSessionId => _session.EditSessionId;
    internal DocumentSession Session => _session;
    internal RootWorkflowConfiguration OpeningConfiguration => _opening;
    internal RootWorkspaceSettings OpeningWorkspace => _openingWorkspace;
    public string WorkflowId => _scope.WorkflowId;
    public WorkflowDefinition Workflow => _scope.Flow;
    public RootWorkflowConfiguration Configuration
    {
        get => _configuration;
        set { EnsureCurrent(); _configuration = value ?? throw new ArgumentNullException(nameof(value)); _revision = checked(_revision + 1); }
    }
    public RootWorkspaceSettings WorkspaceSettings
    {
        get => _workspace;
        set
        {
            EnsureCurrent(); ArgumentNullException.ThrowIfNull(value); value.Validate();
            if (value.OverviewX != _openingWorkspace.OverviewX || value.OverviewY != _openingWorkspace.OverviewY)
                throw new ArgumentException("总览位置属于独立导航，不能作为参数编辑提交。", nameof(value));
            _workspace = value; _revision = checked(_revision + 1);
        }
    }
    internal bool HasPendingChanges => System.Text.Json.JsonSerializer.Serialize(_configuration) != System.Text.Json.JsonSerializer.Serialize(_opening) ||
        !DocumentSession.SameWorkspaceRunSettings(_workspace, _openingWorkspace);
    internal RootWorkflowConfigurationEditProposal Freeze()
    {
        EnsureCurrent(); _configuration.Validate(); _workspace.Validate();
        return new(_session, _scope, _opening, _configuration, _openingWorkspace, _workspace, _revision);
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
        EnsureOwner(); _session.ValidateScope(_scope);
        if (_scope.Flow.IsSubroutine || !ReferenceEquals(_scope.Flow.RootConfiguration, _opening) ||
            !DocumentSession.SameWorkspaceRunSettings(_scope.Flow.WorkspaceSettings, _openingWorkspace))
            throw new InvalidOperationException("根流程配置或运行设置已变化，不能提交过期草稿。");
    }
}
internal sealed record RootWorkflowConfigurationEditProposal(DocumentSession Session, WorkflowScope Scope,
    RootWorkflowConfiguration Opening, RootWorkflowConfiguration Candidate,
    RootWorkspaceSettings OpeningWorkspace, RootWorkspaceSettings Workspace, long DraftRevision);