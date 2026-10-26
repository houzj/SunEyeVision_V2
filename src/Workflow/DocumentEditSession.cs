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
    public object? Token { get; }
    public bool Matches(NodeConfigurationEditDraft draft) => Matches(new[] { draft });
    public bool Matches(IReadOnlyList<NodeConfigurationEditDraft> drafts) => drafts.Count == Nodes.Count && drafts.Select(d => d.Identity).Distinct().Count() == drafts.Count &&
        drafts.All(draft => Nodes.Any(frozen => frozen.Identity == draft.Identity && NodeConfigurationEditHost.SamePendingEdits(frozen, draft)));
    public bool Matches(GlobalVariableConfigurationEditDraft draft) => Variables is not null && NodeConfigurationEditHost.SamePendingEdits(Variables, draft);
    public GlobalVariableConfigurationEditDraft PreserveNewerVariableEdits(NodeConfigurationEditHost host, GlobalVariableConfigurationEditDraft draft)
        => host.RebaseAcceptedDraft(draft, Variables ?? throw new InvalidOperationException("准备结果不是变量范围。"));
    private DocumentEditProposal(IReadOnlyList<NodeConfigurationEditDraft> nodes, GlobalVariableConfigurationEditDraft? variables, object? token)
    { Nodes = nodes; Variables = variables; Token = token; }
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
    public static DocumentEditProposal ForVariables(NodeConfigurationEditHost host, GlobalVariableConfigurationEditDraft variables, object? token = null)
        => new(Array.Empty<NodeConfigurationEditDraft>(), host.Freeze(variables), token);
}

/// <summary>Single owner of registered editing scopes for one SolutionSession. No WPF dependency.
/// Prepare/accept run on the document owner thread; callbacks never execute under the host value lock.</summary>
public sealed class DocumentEditSession
{
    private readonly Dictionary<Guid, IDocumentEditParticipant> _participants = new();
    private readonly Lazy<NodeConfigurationEditHost> _host;
    private bool _busy;
    private bool _closed;
    public Guid SessionId { get; } = Guid.NewGuid();
    public NodeConfigurationEditHost Host => _host.Value;
    public int RegisteredScopeCount => _participants.Count;
    public bool HasPendingChanges => _participants.Values.Any(p => p.HasPendingChanges);
    public IReadOnlyList<string> RegisteredScopeKeys => _participants.Values.Select(p => p.ScopeKey).ToArray();
    public string? LastRefreshError { get; private set; }
    internal DocumentEditSession(Solution document) => _host = new(() => new NodeConfigurationEditHost(document));

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
            Host.ValidateBatch(nodes, variables.SingleOrDefault());
            if (!accept) return null;
            var publication = Host.CommitBatch(nodes, variables.SingleOrDefault());
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
        _closed = true;
        var participants = _participants.Values.ToArray();
        _participants.Clear();
        foreach (var participant in participants)
            try { participant.Cancel(); } catch (Exception ex) { LastRefreshError ??= ex.Message; }
    }
    private void EnsureAvailable()
    {
        if (_closed) throw new InvalidOperationException("方案编辑会话已经关闭。");
        if (_busy) throw new InvalidOperationException("方案编辑事务不能重入。");
    }
}
