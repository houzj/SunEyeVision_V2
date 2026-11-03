namespace SunEyeVision.Application.Editing;

/// <summary>One prepared edit containing actual session identities and private frozen candidates.
/// It is consumed only after the session atomically publishes the entire batch.</summary>
public sealed class DocumentEditProposal
{
    private readonly NodeConfigurationEditDraft[] _nodeDrafts;
    private readonly RootWorkflowConfigurationEditDraft[] _rootDrafts;
    private readonly GlobalVariableConfigurationEditDraft? _variableDraft;
    private DocumentEditProposal[] _parts = Array.Empty<DocumentEditProposal>();
    private bool _consumed;

    private DocumentEditProposal(DocumentSession session, NodeConfigurationEditDraft[] nodeDrafts,
        RootWorkflowConfigurationEditDraft[] rootDrafts, GlobalVariableConfigurationEditDraft? variableDraft,
        NodeConfigurationEditProposal[] nodes, RootWorkflowConfigurationEditProposal[] roots,
        GlobalVariableConfigurationEditProposal? variables, object? token = null)
    {
        Session = session; _nodeDrafts = nodeDrafts; _rootDrafts = rootDrafts; _variableDraft = variableDraft;
        Nodes = Array.AsReadOnly(nodes); Roots = Array.AsReadOnly(roots); Variables = variables; Token = token;
    }

    internal DocumentSession Session { get; }
    internal IReadOnlyList<NodeConfigurationEditProposal> Nodes { get; }
    internal IReadOnlyList<RootWorkflowConfigurationEditProposal> Roots { get; }
    internal GlobalVariableConfigurationEditProposal? Variables { get; }
    internal bool IsConsumed => _consumed;
    /// <summary>Opaque participant-local view stamp; it is never persisted or used as document identity.</summary>
    public object? Token { get; }

    internal static DocumentEditProposal Combine(DocumentSession session, IReadOnlyList<DocumentEditProposal> parts)
    {
        ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0) throw new ArgumentException("没有可提交的编辑范围。", nameof(parts));
        foreach (var part in parts)
        {
            if (!ReferenceEquals(part.Session, session)) throw new InvalidOperationException("编辑范围不属于同一方案会话。");
            part.EnsureCanConsume();
        }
        if (parts.Distinct(ReferenceEqualityComparer.Instance).Count() != parts.Count)
            throw new InvalidOperationException("同一编辑提案不能重复参与事务。");
        var nodes = parts.SelectMany(part => part.Nodes).ToArray();
        var roots = parts.SelectMany(part => part.Roots).ToArray();
        var variableParts = parts.Where(part => part.Variables is not null).ToArray();
        if (nodes.Select(item => (item.Opening.Scope.WorkflowId, item.Opening.Node.Id)).Distinct().Count() != nodes.Length ||
            roots.Select(item => item.Scope.WorkflowId).Distinct(StringComparer.Ordinal).Count() != roots.Length || variableParts.Length > 1)
            throw new InvalidOperationException("编辑范围存在重复节点、根流程或变量目录。");
        if (parts.Count == 1) return parts[0];
        var combined = new DocumentEditProposal(session, parts.SelectMany(part => part._nodeDrafts).ToArray(),
            parts.SelectMany(part => part._rootDrafts).ToArray(), variableParts.SingleOrDefault()?._variableDraft,
            nodes, roots, variableParts.SingleOrDefault()?.Variables);
        combined._parts = parts.ToArray();
        return combined;
    }

    /// <summary>Whether this participant still has precisely the edits that were frozen.</summary>
    public bool Matches(NodeConfigurationEditDraft draft) =>
        Owns(draft) && Nodes.SingleOrDefault(item => ReferenceEquals(item.Opening.Node, draft.Node) &&
            item.Opening.Scope.WorkflowId == draft.WorkflowId) is { } frozen && draft.Matches(frozen);
    public bool Matches(RootWorkflowConfigurationEditDraft draft) =>
        Owns(draft) && Roots.SingleOrDefault(item => ReferenceEquals(item.Scope.Flow, draft.Workflow)) is { } frozen && draft.Matches(frozen);
    public bool Matches(GlobalVariableConfigurationEditDraft draft) => Owns(draft) && Variables is { } frozen && draft.Matches(frozen);
    internal bool Owns(NodeConfigurationEditDraft draft) => _nodeDrafts.Any(item => ReferenceEquals(item, draft));
    internal bool Owns(RootWorkflowConfigurationEditDraft draft) => _rootDrafts.Any(item => ReferenceEquals(item, draft));
    internal bool Owns(GlobalVariableConfigurationEditDraft draft) => ReferenceEquals(_variableDraft, draft);

    public static DocumentEditProposal Prepare(NodeConfigurationEditHost host,
        IEnumerable<NodeConfigurationEditDraft>? nodes = null,
        IEnumerable<RootWorkflowConfigurationEditDraft>? roots = null,
        GlobalVariableConfigurationEditDraft? variables = null,
        object? token = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        var nodeDrafts = (nodes ?? Array.Empty<NodeConfigurationEditDraft>()).ToArray();
        var rootDrafts = (roots ?? Array.Empty<RootWorkflowConfigurationEditDraft>()).ToArray();
        if (nodeDrafts.Any(item => item is null) || rootDrafts.Any(item => item is null) ||
            nodeDrafts.Select(item => (item.WorkflowId, item.Node.Id)).Distinct().Count() != nodeDrafts.Length ||
            rootDrafts.Select(item => item.WorkflowId).Distinct(StringComparer.Ordinal).Count() != rootDrafts.Length ||
            (nodeDrafts.Length == 0 && rootDrafts.Length == 0 && variables is null))
            throw new ArgumentException("编辑必须包含互不重复的方案范围。");
        var frozenNodes = nodeDrafts.Select(host.Freeze).ToArray();
        var frozenRoots = rootDrafts.Select(host.Freeze).ToArray();
        var frozenVariables = variables is null ? null : host.Freeze(variables);
        return new(host.Session, nodeDrafts, rootDrafts, variables, frozenNodes, frozenRoots, frozenVariables, token);
    }

    internal void MarkConsumed()
    {
        EnsureCanConsume();
        // Only the immutable proposal is consumed here. The owning edit participant
        // acknowledges the receipt later and must preserve edits made after freezing.
        _consumed = true;
        foreach (var part in _parts) part._consumed = true;
    }

    // The owner-thread transaction checks this before publishing; callbacks cannot run
    // between this check and MarkConsumed while the session mutation gate is held.
    internal void EnsureCanConsume()
    {
        if (_consumed) throw new InvalidOperationException("编辑提案已接受。");
        foreach (var part in _parts) part.EnsureCanConsume();
        foreach (var draft in _nodeDrafts) draft.EnsureCanConsume();
        foreach (var draft in _rootDrafts) draft.EnsureCanConsume();
        _variableDraft?.EnsureCanConsume();
    }
}
