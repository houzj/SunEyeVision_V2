using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>UI-owned node draft. Only NodeConfigurationEditHost can publish its configuration.</summary>
public sealed class ProductionEditorContext : IToolEditorDraftContext, IDocumentEditParticipant, IDisposable
{
    private readonly NodeConfigurationEditHost _host;
    private readonly Func<bool> _isCurrent;
    private readonly Action<WorkflowNodeBase> _bindDraft;
    private readonly Action<BindingConfigurationPublication>? _published;
    private NodeConfigurationEditDraft _draft;
    private sealed class RelatedCandidate(WorkflowNodeBase formal, NodeConfigurationEditDraft draft, Action<ToolParameters> validate)
    {
        public WorkflowNodeBase Formal { get; } = formal;
        public NodeConfigurationEditDraft Draft { get; set; } = draft;
        public Action<ToolParameters> Validate { get; } = validate;
    }
    private readonly Dictionary<ScopedNodeIdentity, RelatedCandidate> _related = new();
    private readonly Func<string, WorkflowNodeBase, bool>? _allowRelated;
    private NodeConfigurationEditDraft[] AllDrafts() => new[] { _draft }.Concat(_related.Values.Select(c => c.Draft)).ToArray();
    private bool OwnsAllNodes() => _host.OwnsNode(Identity.WorkflowId, (WorkflowNodeBase)FormalNode) &&
        _related.All(c => _host.OwnsNode(c.Key.WorkflowId, c.Value.Formal) && _allowRelated?.Invoke(c.Key.WorkflowId, c.Value.Formal) == true);

    /// <summary>A Group editor borrows invocation-independent parameter candidates from its own
    /// internal graph. The caller supplies the actual parameter editor validation, never a guessed schema.</summary>
    internal WorkflowNodeBase GetRelatedDraft(string workflowId, WorkflowNodeBase formal, Action<ToolParameters> validate)
    {
        EnsureCurrent(); ArgumentNullException.ThrowIfNull(formal); ArgumentNullException.ThrowIfNull(validate);
        var identity = new ScopedNodeIdentity(workflowId, formal.Id);
        if (identity == Identity || _allowRelated?.Invoke(workflowId, formal) != true || !_host.OwnsNode(workflowId, formal))
            throw new InvalidOperationException("关联参数不属于当前Group内部图或节点身份已经失效。");
        if (_related.TryGetValue(identity, out var existing))
        {
            if (!ReferenceEquals(existing.Formal, formal)) throw new InvalidOperationException("关联节点实例已经替换。");
            return existing.Draft.Node;
        }
        var candidate = new RelatedCandidate(formal, _host.BeginEdit(workflowId, formal.Id), validate);
        _related.Add(identity, candidate);
        return candidate.Draft.Node;
    }
    private void ValidateCandidates()
    {
        EditorDraftAdapter.ForProduction(_draft.Node.Parameters).Validate();
        foreach (var related in _related.Values) related.Validate(related.Draft.Node.Parameters);
    }
    private void RefreshCandidates(bool preserveNewerEdits = false)
    {
        _draft = preserveNewerEdits ? _host.RebaseAcceptedDraft(_draft) : _host.BeginEdit(Identity.WorkflowId, Identity.NodeId);
        foreach (var item in _related)
            item.Value.Draft = preserveNewerEdits ? _host.RebaseAcceptedDraft(item.Value.Draft)
                : _host.BeginEdit(item.Key.WorkflowId, item.Key.NodeId);
    }
    private bool _active = true;
    private bool _committing;
    private readonly DocumentEditSession? _documentEdits;
    private readonly CancellationTokenSource _debugCancellation = new();
    public ToolExecutionOwner DebugOwner { get; } = new();
    public WorkflowContext DebugResults { get; } = new();
    public CancellationToken DebugCancellation { get; }

    public Guid ScopeId { get; } = Guid.NewGuid();
    public bool HasPendingChanges => _active && (AllDrafts().Any(_host.HasPendingChanges) || HasPendingViewEdits?.Invoke() == true);
    public string ScopeKey => $"node:{Identity.WorkflowId}/{Identity.NodeId}";
    internal Func<string?>? PrepareView { get; set; }
    internal Func<bool>? HasPendingViewEdits { get; set; }
    public object FormalNode { get; }
    public WorkflowNodeBase DraftNode => _draft.Node;
    public ScopedNodeIdentity Identity => _draft.Identity;
    public long OpenedVersion { get; }
    public string? LastRefreshError { get; private set; }
    public bool IsActive => _active;
    public bool IsValid => _active && _isCurrent() && OwnsAllNodes();
    internal NodeConfigurationEditHost Host => _host;
    internal NodeConfigurationEditDraft PreparedDraft => _draft;
    internal event Action? Deactivated;

    public ProductionEditorContext(NodeConfigurationEditHost host, string workflowId, WorkflowNodeBase formalNode,
        Func<bool> isCurrent, Action<WorkflowNodeBase> bindDraft, Action<BindingConfigurationPublication>? published = null, DocumentEditSession? documentEdits = null,
        Func<string, WorkflowNodeBase, bool>? allowRelated = null)
    {
        DebugCancellation = _debugCancellation.Token;
        _documentEdits = documentEdits;
        _allowRelated = allowRelated;
        _host = host ?? throw new ArgumentNullException(nameof(host));
        FormalNode = formalNode ?? throw new ArgumentNullException(nameof(formalNode));
        _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        _bindDraft = bindDraft ?? throw new ArgumentNullException(nameof(bindDraft));
        _published = published;
        OpenedVersion = host.Capture().Version;
        if (!host.OwnsNode(workflowId, formalNode)) throw new InvalidOperationException("正式节点不属于当前方案与工作流。");
        _draft = host.BeginEdit(workflowId, formalNode.Id);
        if (documentEdits is not null)
        {
            if (!ReferenceEquals(documentEdits.Host, host)) throw new InvalidOperationException("编辑宿主与方案会话不一致。");
            documentEdits.Register(this);
        }
    }
    public bool TryAcceptDebugResult(ToolResults result)
    {
        EnsureCurrent();
        var identity = result.ExecutionIdentity;
        if (identity is null || !ReferenceEquals(identity.Owner, DebugOwner) ||
            identity.NodeId != Identity.WorkflowId + "/" + Identity.NodeId) return false;
        return identity.TryPublish(() => DebugResults.SetNodeResult(Identity.NodeId,
            new NodeExecutionResult { NodeId = Identity.NodeId, Result = result, Success = result.IsSuccess }));
    }

    public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
    {
        EnsureCurrent();
        if (!ReferenceEquals(host, _host)) throw new InvalidOperationException("编辑范围属于另一个方案。");
        if (!HasPendingChanges && AllDrafts().Any(_host.HasPublishedNodeChanged))
        {
            RefreshCandidates();
            _bindDraft(_draft.Node);
        }
        var viewError = PrepareView?.Invoke();
        if (!string.IsNullOrEmpty(viewError)) throw new InvalidOperationException(viewError);
        if (!TryPrepareEditor(out var error)) throw new InvalidOperationException(error);
        return DocumentEditProposal.ForNodes(host, AllDrafts(), _draft);
    }
    public void Acknowledge(DocumentEditProposal prepared, BindingConfigurationPublication publication)
    {
        EnsureCurrent();
        if (!ReferenceEquals(prepared.Token, _draft)) return;
        if (!prepared.Matches(AllDrafts()) || HasPendingViewEdits?.Invoke() == true)
        {
            RefreshCandidates(preserveNewerEdits: true);
            try { _published?.Invoke(publication); } catch (Exception ex) { LastRefreshError = ex.Message; }
            return;
        }
        AcceptPublication(publication);
    }
    public void Cancel() => Discard();

    public bool TryValidateEditor(out string error)
    {
        try
        {
            EnsureCurrent();
            ValidateCandidates();
            _host.ValidateBatch(AllDrafts(), null);
            error = ""; return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
    public bool TryPrepareEditor(out string error)
    {
        try
        {
            EnsureCurrent();
            ValidateCandidates();
            error = ""; return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
    internal void AcceptPublication(BindingConfigurationPublication publication, bool notify = true)
    {
        // The host has already accepted the entire batch. View failures cannot undo it.
        try
        {
            EnsureCurrent();
            RefreshCandidates();
            _bindDraft(_draft.Node);
        }
        catch (Exception ex) { LastRefreshError = ex.Message; Discard(); }
        try { if (notify && _active && _isCurrent()) _published?.Invoke(publication); }
        catch (Exception ex) { LastRefreshError = ex.Message; }
    }
    public bool TryCommitEditor(out string error)
    {
        if (_committing) { error = "编辑提交不能重入。"; return false; }
        if (_documentEdits is not null)
        {
            try { _documentEdits.Apply(ScopeId); error = ""; return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }
        if (!TryValidateEditor(out error)) return false;
        _committing = true;
        BindingConfigurationPublication publication;
        try
        {
            EnsureCurrent();
            publication = _host.CommitBatch(AllDrafts(), null);
        }
        catch (Exception ex) { error = ex.Message; _committing = false; return false; }
        try { AcceptPublication(publication); }
        finally { _committing = false; }
        error = ""; return true;
    }
    private void EnsureCurrent()
    {
        if (_active && (!_isCurrent() || !OwnsAllNodes())) Discard();
        if (!_active) throw new InvalidOperationException("编辑所属方案、工作流或节点实例已失效，草稿不能提交。");
    }
    public void Discard()
    {
        if (!_active) return;
        _active = false;
        _related.Clear();
        _documentEdits?.Unregister(ScopeId);
        try { _debugCancellation.Cancel(); }
        catch (Exception ex) { LastRefreshError ??= ex.Message; }
        try
        {
            if (DebugOwner.ActiveRuns == 0) DebugOwner.Retire();
            else DebugOwner.RetireNode(Identity.WorkflowId + "/" + Identity.NodeId);
        }
        catch (Exception ex) { LastRefreshError ??= ex.Message; }
        var handlers = Deactivated;
        Deactivated = null;
        if (handlers is not null)
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception ex) { LastRefreshError ??= ex.Message; }
        try { DebugResults.ClearNodeResults(); }
        catch (Exception ex) { LastRefreshError ??= ex.Message; }
        _debugCancellation.Dispose();
    }
    public void Dispose() => Discard();
}
