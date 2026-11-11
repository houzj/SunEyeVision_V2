using SunEyeVision.Application.Editing;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    // Capture actual member scopes, including generation, before creating private roots.
    internal RootWorkflowCollectionEditDraft CreateRootCollectionDraft()
    {
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scopes = Document.Workflows.Select(flow => OpenScope(flow.Id)).ToArray();
            return new RootWorkflowCollectionEditDraft(this, scopes, History.Revision);
        }
    }

    internal FrozenRootCollection FreezeRootCollection(RootCollectionDraftSnapshot snapshot,
        IReadOnlyList<NodeConfigurationEditProposal> nodeEdits,
        IReadOnlyList<RootWorkflowConfigurationEditProposal> rootEdits)
    {
        using var mutation = _owner.BeginDocumentMutation();
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            ValidateRootCollectionOpening(snapshot);
            _preparing = true;
            try
            {
                var used = snapshot.Entries.Where(entry => !entry.Issued).SelectMany(entry => entry.Flow.Nodes)
                    .Select(node => node.GlobalIndex).Where(index => index > 0).ToHashSet();
                static int FirstFree(HashSet<int> values)
                {
                    var index = 1;
                    while (values.Contains(index)) index = checked(index + 1);
                    values.Add(index);
                    return index;
                }
                var entries = new List<RootCollectionDraftEntry>();
                var issued = new Dictionary<SunEyeVision.Domain.Workflows.WorkflowDefinition,
                    SunEyeVision.Domain.Workflows.WorkflowDefinition>(ReferenceEqualityComparer.Instance);
                foreach (var entry in snapshot.Entries)
                {
                    if (!entry.Issued) { entries.Add(entry); continue; }
                    // Never mutate a draft handle: later edits cannot alter an older frozen proposal.
                    var target = new SunEyeVision.Domain.Workflows.WorkflowDefinition(entry.Flow.Id, entry.Name);
                    issued.Add(entry.Flow, target);
                    if (entry.CopySource is { } source)
                    {
                        var scope = snapshot.Opening.SingleOrDefault(item => ReferenceEquals(item.Flow, source))
                            ?? throw new InvalidOperationException("复制源不是当前方案中的实际流程。");
                        ValidateScopeCore(scope);
                        var replacements = nodeEdits.Where(edit => ReferenceEquals(edit.Opening.Scope.Flow, source))
                            .ToDictionary(edit => edit.Opening.Node, edit => edit.Candidate.Configuration,
                                (IEqualityComparer<SunEyeVision.Domain.Workflows.NodeDefinition>)ReferenceEqualityComparer.Instance);
                        var graph = GraphState.Capture(source).WithConfigurations(replacements);
                        var nodes = graph.Nodes.Select(item => new FragmentNodeSnapshot(item.Node.Id, item.Node.ToolType,
                            item.DisplayName, item.Enabled, item.Position, item.Node.Width, item.Node.Height,
                            item.Node.SelectedDisplayReference,item.Node.DisplayOverlays,item.Node.AdditionalData,item.Configuration.WithoutCurrentValues())).ToArray();
                        var edges = graph.Edges.Select(edge => new FragmentEdgeSnapshot(edge.Edge.Id, edge.SourceNodeId,
                            edge.TargetNodeId, edge.SourcePort, edge.TargetPort, edge.ManualPath)).ToArray();
                        var local = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
                        var names = new HashSet<string>(StringComparer.Ordinal);
                        var ids = new Dictionary<string, FragmentIssuedNode>(StringComparer.Ordinal);
                        foreach (var node in nodes)
                        {
                            if (!local.TryGetValue(node.ToolId, out var localIds)) local.Add(node.ToolId, localIds = new HashSet<int>());
                            var globalIndex = FirstFree(used); var localIndex = FirstFree(localIds);
                            var stem = node.DisplayName.TrimEnd('0','1','2','3','4','5','6','7','8','9');
                            if (string.IsNullOrWhiteSpace(stem)) stem = _directory.GetCatalog(node.ToolId)?.DisplayName ?? node.ToolId;
                            var suffix = localIndex; var display = stem + suffix;
                            while (!names.Add(display)) display = stem + checked(++suffix);
                            ids.Add(node.Id, new FragmentIssuedNode(Guid.NewGuid().ToString("N"), globalIndex,
                                localIndex, $"{globalIndex} {display}", display));
                        }
                        var edgeIds = edges.ToDictionary(edge => edge.Id, _ => Guid.NewGuid().ToString("N"), StringComparer.Ordinal);
                        var fragment = FragmentBuilder.Build(source.Id, target.Id, nodes, edges, ids, edgeIds,
                            new SunEyeVision.Domain.Workflows.CanvasPoint(0, 0), false);
                        target.MutableNodes.AddRange(fragment.Nodes);
                        target.MutableConnections.AddRange(fragment.Edges);
                        var editedRoot = rootEdits.SingleOrDefault(edit => ReferenceEquals(edit.Scope.Flow, source));
                        var root = editedRoot?.Candidate ?? source.RootConfiguration;
                        if (root.RunParameters.Any(proxy => !fragment.NodeMap.ContainsKey(proxy.NodeId)))
                            throw new InvalidOperationException("复制根参数的来源节点不存在。");
                        target.RootConfiguration = root.Remap(fragment.NodeMap.ToDictionary(pair => pair.Key, pair => pair.Value.Id, StringComparer.Ordinal));
                        var workspace = editedRoot?.Workspace ?? source.WorkspaceSettings;
                        target.WorkspaceSettings = entry.Overview is { } point
                            ? workspace with { OverviewX = point.X, OverviewY = point.Y } : workspace;
                        target.WorkspaceSettings.Validate();
                        target.AdditionalData = source.AdditionalData;
                    }
                    entries.Add(entry with { Flow = target });
                }
                ValidateRootCollectionOpening(snapshot);
                return new FrozenRootCollection(snapshot, Array.AsReadOnly(entries.ToArray()),
                    new System.Collections.ObjectModel.ReadOnlyDictionary<SunEyeVision.Domain.Workflows.WorkflowDefinition,
                        SunEyeVision.Domain.Workflows.WorkflowDefinition>(issued));
            }
            finally { _preparing = false; }
        }
    }
    internal void ValidateRootCollectionOpening(RootCollectionDraftSnapshot snapshot)
    {
        lock (_gate)
        {
            EnsureOpen();
            if (!ReferenceEquals(snapshot.Session, this) || snapshot.OpeningRevision != History.Revision ||
                snapshot.Opening.Length != Document.Workflows.Count || snapshot.OpeningNames.Length != snapshot.Opening.Length)
                throw new InvalidOperationException("根流程集合草稿已过期。");
            for (var index = 0; index < snapshot.Opening.Length; index++)
            {
                var scope = snapshot.Opening[index];
                ValidateScopeCore(scope);
                if (!ReferenceEquals(scope.Flow, Document.Workflows[index]) || scope.Flow.Name != snapshot.OpeningNames[index])
                    throw new InvalidOperationException("根流程成员、顺序或名称已变化。");
            }
        }
    }
}
internal sealed record FrozenRootCollection(RootCollectionDraftSnapshot Draft,
    IReadOnlyList<RootCollectionDraftEntry> Entries,
    IReadOnlyDictionary<SunEyeVision.Domain.Workflows.WorkflowDefinition,
        SunEyeVision.Domain.Workflows.WorkflowDefinition> Issued);
