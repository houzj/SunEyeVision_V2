using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Workflow;

/// <summary>A document-owned participant. Views may disappear without removing its scope.</summary>
public interface IDocumentEditParticipant
{
    Guid ScopeId { get; }
    string ScopeKey { get; }
    bool HasPendingChanges { get; }
    bool IsValid => true;
    DocumentEditProposal Prepare(NodeConfigurationEditHost host);
    void Acknowledge(DocumentEditProposal prepared, BindingConfigurationPublication publication);
    void Cancel();
}

/// <summary>Frozen configuration prepared before any participant is accepted.</summary>
public sealed class DocumentEditProposal
{
    internal IReadOnlyList<NodeConfigurationEditDraft> Nodes { get; }
    internal GlobalVariableConfigurationEditDraft? Variables { get; }
    internal IReadOnlyList<RootWorkflowConfigurationEditDraft> RootConfigurations {get;}
    public object? Token { get; }
    public bool Matches(NodeConfigurationEditDraft draft) => Matches(new[] { draft });
    public bool Matches(IReadOnlyList<NodeConfigurationEditDraft> drafts) => drafts.Count == Nodes.Count && drafts.Select(d => d.Identity).Distinct().Count() == drafts.Count &&
        drafts.All(draft => Nodes.Any(frozen => frozen.Identity == draft.Identity && NodeConfigurationEditHost.SamePendingEdits(frozen, draft)));
    public bool Matches(RootWorkflowConfigurationEditDraft draft)=>RootConfigurations.Count==1&&NodeConfigurationEditHost.SamePendingEdits(RootConfigurations[0],draft);
    public bool Matches(GlobalVariableConfigurationEditDraft draft) => Variables is not null && NodeConfigurationEditHost.SamePendingEdits(Variables, draft);
    public GlobalVariableConfigurationEditDraft PreserveNewerVariableEdits(NodeConfigurationEditHost host, GlobalVariableConfigurationEditDraft draft)
        => host.RebaseAcceptedDraft(draft, Variables ?? throw new InvalidOperationException("准备结果不是变量范围。"));
    private DocumentEditProposal(IReadOnlyList<NodeConfigurationEditDraft> nodes, GlobalVariableConfigurationEditDraft? variables, object? token, IReadOnlyList<RootWorkflowConfigurationEditDraft>? roots = null)
    { Nodes = nodes; Variables = variables; Token = token; RootConfigurations=roots??Array.Empty<RootWorkflowConfigurationEditDraft>(); }
    public static DocumentEditProposal ForNode(NodeConfigurationEditHost host, NodeConfigurationEditDraft node, object? token = null)
        => ForNodes(host, new[] { node }, token);
    /// <summary>One editor may own a Group and several related internal module candidates.
    /// Freeze every candidate before validation or publication; never accept them one by one.</summary>
    public static DocumentEditProposal ForNodes(NodeConfigurationEditHost host, IReadOnlyList<NodeConfigurationEditDraft> nodes, object? token = null)
    {
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0 || nodes.Any(n => n is null) || nodes.Select(n => n.Identity).Distinct().Count() != nodes.Count)
            throw new ArgumentException("组合编辑必须包含互不重复的正式节点身份。", nameof(nodes));
        return new(Array.AsReadOnly(nodes.Select(host.Freeze).ToArray()), null, token);
    }
    public static DocumentEditProposal ForNodesAndVariables(NodeConfigurationEditHost host,
        IReadOnlyList<NodeConfigurationEditDraft> nodes, GlobalVariableConfigurationEditDraft variables, object? token = null)
    {
        var nodesOnly = ForNodes(host, nodes, token);
        return new(nodesOnly.Nodes, host.Freeze(variables), token);
    }
    public static DocumentEditProposal ForRootConfiguration(NodeConfigurationEditHost host,RootWorkflowConfigurationEditDraft root,IReadOnlyList<NodeConfigurationEditDraft> nodes,object? token=null)
        =>new(Array.AsReadOnly(nodes.Select(host.Freeze).ToArray()),null,token,new[]{host.Freeze(root)});
    public static DocumentEditProposal ForVariables(NodeConfigurationEditHost host, GlobalVariableConfigurationEditDraft variables, object? token = null)
        => new(Array.Empty<NodeConfigurationEditDraft>(), host.Freeze(variables), token);
}

/// <summary>Single owner of registered editing scopes for one SolutionSession. No WPF dependency.
/// Prepare/accept run on the document owner thread; callbacks never execute under the host value lock.</summary>
public sealed class DocumentEditSession
{
    private readonly Dictionary<Guid, IDocumentEditParticipant> _participants = new();
    private Lazy<NodeConfigurationEditHost> _host;
    private readonly Solution _document;
    private readonly int _ownerThread=Environment.CurrentManagedThreadId;
    private bool _presentationPreparing,_initialPresentationUsed;
    private bool _busy;
    private bool _closed;
    public Guid SessionId { get; } = Guid.NewGuid();
    private DocumentHistoryCoordinator? _history;
    public DocumentHistoryCoordinator History {get{EnsureOpen();return _history ??= new(SessionId);}}
    public NodeConfigurationEditHost Host {get{EnsureOpen();if(_presentationPreparing)throw new InvalidOperationException("方案节点正在准备显示，暂不能访问配置宿主。");return _host.Value;}}
    public int RegisteredScopeCount => _participants.Count;
    public bool HasPendingChanges => _participants.Values.Any(p => p.HasPendingChanges);
    public IReadOnlyList<string> RegisteredScopeKeys => _participants.Values.Select(p => p.ScopeKey).ToArray();
    public string? LastRefreshError { get; private set; }
    internal DocumentEditSession(Solution document) { _document=document;_host=CreateHost(); }
    private Lazy<NodeConfigurationEditHost> CreateHost()=>new(()=>new NodeConfigurationEditHost(_document,History));

    /// <summary>Retire an unused early host before initial UI node materialization, preserving this session and history.</summary>
    public InitialPresentationScope BeginInitialPresentationMaterialization()
    {
        EnsureAvailable();
        if(Environment.CurrentManagedThreadId!=_ownerThread||_initialPresentationUsed||_participants.Count!=0
            ||_document.ExecutionOwner.HasEverStarted
            ||(_history is {} history&&(history.Revision!=0||history.Position!=0||history.Entries.Length!=0||history.Fault is not null||history.IsApplying)))
            throw new InvalidOperationException("当前编辑会话不能重新准备显示节点。");
        var currentHistory=History;
        _presentationPreparing=true;
        IDisposable? historyGuard=null;
        try
        {
            historyGuard=currentHistory.BlockInitialPresentationMutations();
            if(_document.ExecutionOwner.HasEverStarted||_participants.Count!=0||currentHistory.Revision!=0
                ||currentHistory.Position!=0||currentHistory.Entries.Length!=0||currentHistory.Fault is not null)
                throw new InvalidOperationException("取得执行门禁期间会话已被使用，不能准备显示节点。");
            var nextHost=CreateHost();if(_host.IsValueCreated)_host.Value.CloseUnusedForInitialPresentation();_host=nextHost;
            return new InitialPresentationScope(this,historyGuard);
        }
        catch{try{historyGuard?.Dispose();}finally{_presentationPreparing=false;}throw;}
    }
    public void SealInitialPresentation()
    {
        EnsureOpen();
        if(Environment.CurrentManagedThreadId!=_ownerThread||_presentationPreparing)throw new InvalidOperationException("显示节点准备尚未结束。");
        _initialPresentationUsed=true;
    }
    internal bool IsPreparingInitialPresentation=>_presentationPreparing;
    public sealed class InitialPresentationScope(DocumentEditSession owner,IDisposable historyGuard):IDisposable
    {
        private DocumentEditSession? _owner=owner;
        private bool _completed;
        public void Complete(){if(_owner is null)throw new ObjectDisposedException(nameof(InitialPresentationScope));_completed=true;}
        public void Dispose()
        {
            var session=_owner;if(session is null)return;
            if(Environment.CurrentManagedThreadId!=session._ownerThread)throw new InvalidOperationException("显示节点准备必须在方案所属线程结束。");
            _owner=null;session._initialPresentationUsed=_completed;
            try{historyGuard.Dispose();}finally{session._presentationPreparing=false;}
        }
    }

    public void Register(IDocumentEditParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        EnsureAvailable();
        PruneInvalidScopes();
        if (participant.ScopeId == Guid.Empty || string.IsNullOrWhiteSpace(participant.ScopeKey))
            throw new ArgumentException("编辑范围必须有明确身份。");
        if (_participants.TryGetValue(participant.ScopeId, out var existing) && ReferenceEquals(existing, participant)) return;
        if (_participants.ContainsKey(participant.ScopeId) || _participants.Values.Any(p => p.ScopeKey == participant.ScopeKey))
            throw new InvalidOperationException("同一编辑范围已登记，不能创建第二个独立发布者。");
        _participants.Add(participant.ScopeId, participant);
    }
    public void Unregister(Guid scopeId) => _participants.Remove(scopeId);

    public void ValidateAll() => Transact(null, accept: false);
    public BindingConfigurationPublication? Apply(Guid scopeId) => Transact(scopeId, accept: true);
    public BindingConfigurationPublication? AcceptAll() => Transact(null, accept: true);

    private BindingConfigurationPublication? Transact(Guid? scopeId, bool accept)
    {
        EnsureAvailable();
        PruneInvalidScopes();
        var selected = scopeId is { } id
            ? new[] { _participants.TryGetValue(id, out var p) ? p : throw new InvalidOperationException("编辑范围未登记或已经退出。") }
            : _participants.Values.ToArray();
        if (selected.Length == 0) return null;
        _busy = true;
        try
        {
            Host.SynchronizeStructure();
            var prepared = selected.Select(p => (Participant: p, Proposal: p.Prepare(Host))).ToArray();
            foreach (var entry in prepared)
                if (!_participants.TryGetValue(entry.Participant.ScopeId, out var current) || !ReferenceEquals(current, entry.Participant))
                    throw new InvalidOperationException("准备期间编辑范围已退出，草稿未接受。");
            var nodes = prepared.SelectMany(x => x.Proposal.Nodes).ToArray();
            var variables = prepared.Select(x => x.Proposal.Variables).OfType<GlobalVariableConfigurationEditDraft>().ToArray();
            if (variables.Length > 1) throw new InvalidOperationException("同一方案只能有一个变量编辑范围。");
            var roots=prepared.SelectMany(x=>x.Proposal.RootConfigurations).ToArray();
            Host.ValidateBatch(nodes, variables.SingleOrDefault(), roots);
            if (!accept) return null;
            var publication = Host.CommitBatch(nodes, variables.SingleOrDefault(), roots);
            LastRefreshError = null;
            // Acceptance is final. A failed view notification never replays or rolls back writes.
            foreach (var entry in prepared)
                try { entry.Participant.Acknowledge(entry.Proposal, publication); }
                catch (Exception ex) { LastRefreshError ??= ex.Message; }
            return publication;
        }
        finally { _busy = false; }
    }
    private void PruneInvalidScopes()
    {
        foreach (var entry in _participants.Values.Where(p => !p.IsValid).ToArray())
        {
            _participants.Remove(entry.ScopeId);
            entry.Cancel();
        }
    }
    public void Close()
    {
        if (_closed) return;
        if(_presentationPreparing)throw new InvalidOperationException("显示节点准备期间不能关闭编辑会话。");
        _closed = true;
        if(_host.IsValueCreated)_host.Value.Close();
        var participants = _participants.Values.ToArray();
        _participants.Clear();
        foreach (var participant in participants)
            try { participant.Cancel(); } catch (Exception ex) { LastRefreshError ??= ex.Message; }
        try {_history?.Dispose();}
        catch(Exception ex){LastRefreshError ??= ex.Message;}
    }
    private void EnsureOpen()
    {if(_closed)throw new InvalidOperationException("方案编辑会话已经关闭。");}
    private void EnsureAvailable()
    {
        EnsureOpen();
        if (_busy||_presentationPreparing) throw new InvalidOperationException("方案编辑事务不能重入。");
    }
}
