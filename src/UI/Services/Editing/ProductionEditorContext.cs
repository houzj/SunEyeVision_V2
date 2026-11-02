using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Runtime;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
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
    private NodeConfigurationEditDraft[] AllDrafts() => new[] { _draft };
    private bool OwnsAllNodes() => _host.OwnsNode(Identity.WorkflowId, (WorkflowNodeBase)FormalNode);
    private void ValidateCandidates(bool prepareView) => ValidateDraftModel(prepareView);
    private void RefreshCandidates(bool preserveNewerEdits = false, DocumentEditProposal? prepared = null)
    {
        _draft = preserveNewerEdits ? _host.RebaseAcceptedDraft(_draft) : _host.BeginEdit(Identity.WorkflowId, Identity.NodeId);
    }
    private bool _active = true;
    private bool _committing;
    private readonly DocumentEditSession? _documentEdits;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DocumentEditSession,SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceExpansionPreference> ReferencePreferences=new();
    private readonly SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceExpansionPreference _localReferencePreference=new();
    internal SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceExpansionPreference ReferencePreference => _documentEdits is null ? _localReferencePreference : ReferencePreferences.GetValue(_documentEdits,_=>new(){ScopeDescription=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="zh"?"仅当前方案编辑会话的临时默认方式；保存、重开、Dirty和撤销未接通。":"Temporary default for this document editing session only. Save/reopen, dirty state and undo are not connected."});
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
    public bool OwnsDraft(object? node) => IsValid && (ReferenceEquals(node, _draft.Node));
    public ScopedNodeIdentity Identity => _draft.Identity;
    public long OpenedVersion { get; }
    public string? LastRefreshError { get; private set; }
    public bool IsActive => _active;
    public bool IsValid => _active && _isCurrent() && OwnsAllNodes();
    internal NodeConfigurationEditHost Host => _host;
    internal NodeConfigurationEditDraft PreparedDraft => _draft;
    internal SunEyeVision.Plugin.Contracts.ConstantSetting? ReadCurrentParameter(string fieldId)
    {
        EnsureCurrent();
        return _host.ReadCurrentParameter(_draft,fieldId);
    }
    internal event Action? Deactivated;

    public ProductionEditorContext(NodeConfigurationEditHost host, string workflowId, WorkflowNodeBase formalNode,
        Func<bool> isCurrent, Action<WorkflowNodeBase> bindDraft, Action<BindingConfigurationPublication>? published = null, DocumentEditSession? documentEdits = null)
    {
        DebugCancellation = _debugCancellation.Token;
        _documentEdits = documentEdits;

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
    public bool TryAcceptDebugResult(DeclaredOutputSnapshot result)
    {
        EnsureCurrent();
        var identity = result.ExecutionIdentity;
        if (identity is null || !ReferenceEquals(identity.Owner, DebugOwner) ||
            identity.NodeId != Identity.WorkflowId + "/" + Identity.NodeId) return false;
        return identity.TryPublish(() => DebugResults.SetNodeResult(Identity.NodeId,
            new NodeExecutionResult { NodeId = Identity.NodeId, Result = result, Success = result.IsSuccess, Status = result.Status }));
    }

    public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
    {
        EnsureCurrent();
        if (!ReferenceEquals(host, _host)) throw new InvalidOperationException("编辑范围属于另一个方案。");
        TryRefreshUnmodifiedPublishedDraft();
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
            RefreshCandidates(preserveNewerEdits: true, prepared);
            try { _published?.Invoke(publication); } catch (Exception ex) { LastRefreshError = ex.Message; }
            return;
        }
        AcceptPublication(publication);
    }
    public void Cancel() => Discard();

    // The same safe rebind used by Prepare; never replaces accepted or raw view edits.
    internal bool TryRefreshUnmodifiedPublishedDraft()
    {
        EnsureCurrent();
        var opening=_draft;
        if(HasPendingChanges||!ReferenceEquals(opening,_draft)||!IsValid)return false;
        if(!AllDrafts().Any(_host.HasPublishedNodeChanged))return true;
        if(HasPendingChanges||!ReferenceEquals(opening,_draft)||!IsValid)return false;
        RefreshCandidates();
        var refreshed=_draft;
        _bindDraft(refreshed.Node);
        var clean=!HasPendingChanges;
        return clean&&IsValid&&ReferenceEquals(refreshed,_draft);
    }

    public bool TryValidateEditor(out string error)
    {
        try
        {
            EnsureCurrent();
            ValidateCandidates(true);

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
            ValidateCandidates(false);
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
    private void ValidateDraftModel(bool prepareView)
    {
        if(_draft.Node.RuntimeConfiguration is not null)
        {
            if(prepareView&&PrepareView?.Invoke() is {Length:>0} viewError)throw new InvalidOperationException(viewError);
            var errors=_draft.Node.RuntimeConfiguration.Validate();
            if(errors.Count!=0)throw new InvalidOperationException(errors[0].Message);
        }
        else throw new InvalidOperationException("节点尚未接入 Runtime 编辑事务。");
    }
    public bool TryCommitEditor(out string error)
    {
        if (_committing) { error = "编辑提交不能重入。"; return false; }
        if (_documentEdits is not null)
        {
            try { EnsureCurrent(); _documentEdits.Apply(ScopeId); error = ""; return true; }
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
