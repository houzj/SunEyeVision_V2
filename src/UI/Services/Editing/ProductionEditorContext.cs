using System;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>UI-owned node draft. Only NodeConfigurationEditHost can publish its configuration.</summary>
public sealed class ProductionEditorContext : IToolEditorDraftContext, IDisposable
{
    private readonly NodeConfigurationEditHost _host;
    private readonly Func<bool> _isCurrent;
    private readonly Action<WorkflowNodeBase> _bindDraft;
    private readonly Action<BindingConfigurationPublication>? _published;
    private NodeConfigurationEditDraft _draft;
    private bool _active = true;
    private bool _committing;
    public object FormalNode { get; }
    public WorkflowNodeBase DraftNode => _draft.Node;
    public ScopedNodeIdentity Identity => _draft.Identity;
    public long OpenedVersion { get; }
    public string? LastRefreshError { get; private set; }
    public bool IsActive => _active;
    internal event Action? Deactivated;

    public ProductionEditorContext(NodeConfigurationEditHost host, string workflowId, WorkflowNodeBase formalNode,
        Func<bool> isCurrent, Action<WorkflowNodeBase> bindDraft, Action<BindingConfigurationPublication>? published = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        FormalNode = formalNode ?? throw new ArgumentNullException(nameof(formalNode));
        _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        _bindDraft = bindDraft ?? throw new ArgumentNullException(nameof(bindDraft));
        _published = published;
        OpenedVersion = host.Capture().Version;
        if (!host.OwnsNode(workflowId, formalNode)) throw new InvalidOperationException("正式节点不属于当前方案与工作流。");
        _draft = host.BeginEdit(workflowId, formalNode.Id);
    }
    public bool TryValidateEditor(out string error)
    {
        try
        {
            EnsureCurrent();
            EditorDraftAdapter.ForProtocolParse(_draft.Node.Parameters).Validate();
            _host.Validate(_draft);
            error = ""; return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
    public bool TryCommitEditor(out string error)
    {
        if (_committing) { error = "编辑提交不能重入。"; return false; }
        if (!TryValidateEditor(out error)) return false;
        _committing = true;
        BindingConfigurationPublication publication;
        try
        {
            EnsureCurrent();
            publication = _host.Commit(_draft);
        }
        catch (Exception ex) { error = ex.Message; _committing = false; return false; }
        // Publication is final. A view callback cannot change its successful outcome.
        try
        {
            if (_active && _isCurrent())
            {
                _draft = _host.BeginEdit(Identity.WorkflowId, Identity.NodeId);
                _bindDraft(_draft.Node);
            }
        }
        catch (Exception ex) { LastRefreshError = ex.Message; Discard(); }
        try { if (_active && _isCurrent()) _published?.Invoke(publication); }
        catch (Exception ex) { LastRefreshError = ex.Message; }
        finally { _committing = false; }
        error = ""; return true;
    }
    private void EnsureCurrent()
    {
        if (_active && (!_isCurrent() || !_host.OwnsNode(Identity.WorkflowId, (WorkflowNodeBase)FormalNode))) Discard();
        if (!_active) throw new InvalidOperationException("编辑所属方案、工作流或节点实例已失效，草稿不能提交。");
    }
    public void Discard()
    {
        if (!_active) return;
        _active = false;
        var handlers = Deactivated;
        Deactivated = null;
        if (handlers is not null)
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception ex) { LastRefreshError ??= ex.Message; }
    }
    public void Dispose() => Discard();
}
