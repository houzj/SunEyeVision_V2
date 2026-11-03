namespace SunEyeVision.Application.Editing;

/// <summary>Creates private editing candidates against one actual document session.
/// Publication and history remain owned by that session.</summary>
public sealed partial class NodeConfigurationEditHost
{
    private readonly DocumentSession _session;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _closed;
    internal DocumentSession Session => _session;

    public bool OwnsNode(string workflowId, SunEyeVision.Domain.Workflows.NodeDefinition node)
    {
        EnsureAvailable(); ArgumentNullException.ThrowIfNull(node);
        return _session.GetNodes(workflowId).Any(item => ReferenceEquals(item, node));
    }
    public bool OwnsRoot(RootWorkflowConfigurationEditDraft draft)
    {
        EnsureAvailable(); ArgumentNullException.ThrowIfNull(draft);
        return ReferenceEquals(draft.Session, _session) && !draft.Workflow.IsSubroutine &&
            _session.GetWorkflows().Any(item => ReferenceEquals(item, draft.Workflow));
    }
    public bool HasPendingChanges(NodeConfigurationEditDraft draft)
    { EnsureAvailable(); ArgumentNullException.ThrowIfNull(draft); return ReferenceEquals(draft.Session, _session) ? draft.HasPendingChanges : throw new InvalidOperationException("草稿属于另一个方案。"); }
    public bool HasPendingChanges(RootWorkflowConfigurationEditDraft draft)
    { EnsureAvailable(); ArgumentNullException.ThrowIfNull(draft); return ReferenceEquals(draft.Session, _session) ? draft.HasPendingChanges : throw new InvalidOperationException("草稿属于另一个方案。"); }
    public bool HasPendingChanges(GlobalVariableConfigurationEditDraft draft)
    { EnsureAvailable(); ArgumentNullException.ThrowIfNull(draft); return ReferenceEquals(draft.Session, _session) ? draft.HasPendingChanges : throw new InvalidOperationException("草稿属于另一个方案。"); }
    public SunEyeVision.Plugin.Contracts.ConstantSetting? ReadCurrentParameter(NodeConfigurationEditDraft draft, string fieldId)
    { EnsureAvailable(); ArgumentNullException.ThrowIfNull(draft); if (!ReferenceEquals(draft.Session, _session)) throw new InvalidOperationException("草稿属于另一个方案。"); return draft.ReadCurrentParameter(fieldId); }
    internal NodeConfigurationEditHost(DocumentSession session)
        => _session = session ?? throw new ArgumentNullException(nameof(session));

    public NodeConfigurationEditDraft BeginEdit(string workflowId, string nodeId)
    {
        EnsureAvailable();
        var scope = _session.OpenScope(workflowId);
        return new NodeConfigurationEditDraft(_session, _session.RequireNode(scope, nodeId));
    }

    public RootWorkflowConfigurationEditDraft BeginRootEdit(string workflowId)
    {
        EnsureAvailable();
        return new RootWorkflowConfigurationEditDraft(_session, _session.OpenScope(workflowId));
    }

    public RootWorkflowConfigurationEditDraft RebaseAcceptedDraft(RootWorkflowConfigurationEditDraft draft, DocumentEditProposal prepared)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Session, _session) || !prepared.IsConsumed || !prepared.Owns(draft) || draft.EditSessionId != _session.EditSessionId)
            throw new InvalidOperationException("编辑回执不属于当前方案或尚未接受。");
        var frozen = prepared.Roots.SingleOrDefault(item => ReferenceEquals(item.Scope.Flow, draft.Workflow))
            ?? throw new InvalidOperationException("编辑回执不包含此根流程草稿。");
        var current = _session.OpenScope(draft.WorkflowId);
        if (!ReferenceEquals(current.Flow, draft.Workflow)) throw new InvalidOperationException("根流程实例已被替换。");
        var rebased = new RootWorkflowConfigurationEditDraft(_session, current,
            draft.Matches(frozen) ? current.Flow.RootConfiguration : draft.Configuration,
            draft.Matches(frozen) ? current.Flow.WorkspaceSettings : draft.WorkspaceSettings);
        draft.MarkConsumed();
        return rebased;
    }

    public GlobalVariableConfigurationEditDraft BeginVariableEdit()
    {
        EnsureAvailable();
        return new GlobalVariableConfigurationEditDraft(_session);
    }

    public GlobalVariableConfigurationEditDraft RebaseAcceptedDraft(GlobalVariableConfigurationEditDraft draft, DocumentEditProposal prepared,
        IReadOnlyDictionary<SunEyeVision.Domain.Solutions.GlobalVariable, SunEyeVision.Domain.Solutions.GlobalVariable> acceptedInstances)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Session, _session) || !prepared.IsConsumed || !prepared.Owns(draft) || draft.EditSessionId != _session.EditSessionId || prepared.Variables is not { } frozen)
            throw new InvalidOperationException("编辑回执不包含当前方案变量草稿或尚未接受。");
        ArgumentNullException.ThrowIfNull(acceptedInstances);
        return draft.Rebase(frozen, acceptedInstances);
    }

    internal GlobalVariableConfigurationEditProposal Freeze(GlobalVariableConfigurationEditDraft draft)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(draft);
        if (!ReferenceEquals(draft.Session, _session))
            throw new InvalidOperationException("变量草稿属于另一个方案会话。");
        return draft.Freeze();
    }

    internal RootWorkflowConfigurationEditProposal Freeze(RootWorkflowConfigurationEditDraft draft)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(draft);
        if (!ReferenceEquals(draft.Session, _session))
            throw new InvalidOperationException("根配置草稿属于另一个方案会话。");
        return draft.Freeze();
    }

    internal NodeConfigurationEditProposal Freeze(NodeConfigurationEditDraft draft)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(draft);
        if (!ReferenceEquals(draft.Session, _session))
            throw new InvalidOperationException("参数草稿属于另一个方案会话。");
        return draft.Freeze();
    }

    /// <summary>After publication, retain private edits made since preparation against the new live baseline.</summary>
    public NodeConfigurationEditDraft RebaseAcceptedDraft(NodeConfigurationEditDraft draft, DocumentEditProposal prepared)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Session, _session) || !prepared.IsConsumed || !prepared.Owns(draft) || draft.EditSessionId != _session.EditSessionId)
            throw new InvalidOperationException("编辑回执不属于当前方案或尚未接受。");
        var frozen = prepared.Nodes.SingleOrDefault(item => ReferenceEquals(item.Opening.Node, draft.Node) &&
            item.Opening.Scope.WorkflowId == draft.WorkflowId)
            ?? throw new InvalidOperationException("编辑回执不包含此节点草稿。");
        var current = _session.RequireNode(_session.OpenScope(draft.WorkflowId), draft.Node.Id);
        if (!ReferenceEquals(current.Node, draft.Node)) throw new InvalidOperationException("节点实例已被替换。");
        var candidate = draft.Matches(frozen) ? current.Configuration :
            draft.CurrentCandidate.MergeCurrentValues(current.Configuration, frozen.Candidate);
        var rebased = new NodeConfigurationEditDraft(_session, current, candidate);
        draft.MarkConsumed();
        return rebased;
    }

    internal void Close()
    {
        EnsureOwner();
        _closed = true;
    }

    private void EnsureOwner()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("编辑宿主只能在方案所属线程使用。");
    }

    private void EnsureAvailable()
    {
        EnsureOwner();
        if (_closed) throw new InvalidOperationException("方案编辑宿主已经关闭。");
    }
}
