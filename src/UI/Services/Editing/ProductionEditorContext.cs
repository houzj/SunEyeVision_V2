using System;
using System.Linq;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Editing.Wpf;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>One actual document-node editor. All publication goes through its session.</summary>
public sealed class ProductionEditorContext : IToolEditorDraftContext, IDocumentEditParticipant, IDisposable
{
    private readonly DocumentSession _session;
    private readonly Func<bool> _isCurrent;
    private readonly Action<NodeConfigurationEditDraft> _bindDraft;
    private readonly Action<DocumentEditReceipt>? _published;
    private NodeConfigurationEditDraft _draft;
    private bool _active = true;
    private bool _preparing;
    internal Func<string?>? PrepareView { get; set; }
    internal Action<NodeConfigurationEditDraft>? PreserveViewAfterRebase { get; set; }
    internal Func<bool>? HasPendingViewEdits { get; set; }
    internal event Action? Deactivated;
    internal NodeConfigurationEditHost Host => _session.Editing.Host;
    internal DocumentSession Session => _session;
    internal NodeConfigurationEditDraft PreparedDraft => _draft;
    public NodeDefinition DraftNode => _draft.Node;
    public object FormalNode { get; }
    public ScopedNodeIdentity Identity => new(_draft.WorkflowId, _draft.Node.Id);
    public long OpenedVersion { get; }
    public Guid ScopeId { get; } = Guid.NewGuid();
    public string ScopeKey => $"node:{Identity.WorkflowId}/{Identity.NodeId}";
    public bool IsActive => _active;
    public string? LastRefreshError { get; private set; }
    public bool IsValid
    {
        get
        {
            if (!_active || !_isCurrent()) return false;
            try { return Host.OwnsNode(_draft.WorkflowId, (NodeDefinition)FormalNode); }
            catch (InvalidOperationException) { return false; }
        }
    }
    public bool HasPendingChanges => IsValid && (Host.HasPendingChanges(_draft) || HasPendingViewEdits?.Invoke() == true);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DocumentSession, SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceExpansionPreference> Preferences = new();
    internal SunEyeVision.Plugin.Wpf.Adapters.References.ReferenceExpansionPreference ReferencePreference => Preferences.GetValue(_session, _ => new());

    public ProductionEditorContext(DocumentSession session, string workflowId, NodeDefinition node,
        Func<bool> isCurrent, Action<NodeConfigurationEditDraft> bindDraft, Action<DocumentEditReceipt>? published = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        FormalNode = node ?? throw new ArgumentNullException(nameof(node));
        _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        _bindDraft = bindDraft ?? throw new ArgumentNullException(nameof(bindDraft));
        _published = published;
        if (!Host.OwnsNode(workflowId, node)) throw new InvalidOperationException("节点不属于当前方案与流程。");
        _draft = Host.BeginEdit(workflowId, node.Id);
        OpenedVersion = session.ChangeRevision;
        session.Editing.Register(this);
    }
    public bool OwnsDraft(object? node) => IsValid && (ReferenceEquals(node, _draft) || ReferenceEquals(node, _draft.Node));
    internal SunEyeVision.Plugin.Contracts.ConstantSetting? ReadCurrentParameter(string fieldId)
    { EnsureCurrent(); return Host.ReadCurrentParameter(_draft, fieldId); }
    internal IReadOnlyDictionary<string, SunEyeVision.Plugin.Contracts.ConstantSetting?> ReadCurrentParameters()
    {
        EnsureCurrent();
        var draft=_draft;var configuration=draft.Configuration;var revision=_session.ChangeRevision;
        var values=configuration.Fields.Declarations.Keys.ToDictionary(id=>id,id=>Host.ReadCurrentParameter(draft,id),StringComparer.Ordinal);
        EnsureCurrent();
        if(!ReferenceEquals(draft,_draft)||!ReferenceEquals(configuration,draft.Configuration)||revision!=_session.ChangeRevision)
            throw new InvalidOperationException("参数当前值批回读期间编辑会话或配置已变化。");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,SunEyeVision.Plugin.Contracts.ConstantSetting?>(values);
    }
    public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
    {
        EnsureCurrent();
        if (!ReferenceEquals(host, Host)) throw new InvalidOperationException("编辑宿主不属于当前方案。");
        if (!TryPrepareEditor(out var error)) throw new InvalidOperationException(error);
        return DocumentEditProposal.Prepare(host, nodes: new[] { _draft }, token: _draft);
    }
    public void Acknowledge(DocumentEditProposal prepared, DocumentEditReceipt receipt)
    {
        EnsureCurrent();
        if (!ReferenceEquals(receipt.Session, _session) || !ReferenceEquals(prepared.Token, _draft))
            throw new InvalidOperationException("编辑回执不属于当前参数页。");
        // A raw, unparsed control edit made during publication must survive the receipt.
        // The draft is rebased in either case; rebuilding WPF would lose that raw text.
        var rebind = prepared.Matches(_draft) && HasPendingViewEdits?.Invoke() != true;
        _draft = Host.RebaseAcceptedDraft(_draft, prepared);
        if (rebind) _bindDraft(_draft); else PreserveViewAfterRebase?.Invoke(_draft);
        try { _published?.Invoke(receipt); } catch (Exception error) { LastRefreshError = error.Message; }
    }
    public bool TryPrepareEditor(out string error)
    {
        try
        {
            EnsureCurrent();
            if (_preparing) throw new InvalidOperationException("参数捕获不能重入。");
            _preparing = true;
            try { if (PrepareView?.Invoke() is { Length: > 0 } viewError) throw new InvalidOperationException(viewError); }
            finally { _preparing = false; }
            error = ""; return true;
        }
        catch (Exception failure) { error = failure.Message; return false; }
    }
    public bool TryValidateEditor(out string error)
    {
        try { EnsureCurrent(); _session.Editing.Validate(ScopeId); error = ""; return true; }
        catch (Exception failure) { error = failure.Message; return false; }
    }
    public bool TryCommitEditor(out string error)
    {
        try { EnsureCurrent(); _session.Editing.Apply(ScopeId); error = ""; return true; }
        catch (Exception failure) { error = failure.Message; return false; }
    }
    private void EnsureCurrent() { if (!IsValid) throw new InvalidOperationException("参数页所属方案、流程或节点实例已失效。"); }
    public void Cancel() => Discard();
    public void Discard()
    {
        if (!_active) return;
        _active = false;
        _session.Editing.Unregister(ScopeId);
        var callbacks = Deactivated; Deactivated = null;
        var errors = new System.Collections.Generic.List<Exception>();
        foreach (Action callback in callbacks?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { callback(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) LastRefreshError = new AggregateException("参数页释放失败。", errors).Message;
    }
    public void Dispose() => Discard();
}
