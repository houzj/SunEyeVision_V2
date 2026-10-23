using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services;

public enum ImageSourceResolutionKind { Found, None, Ambiguous, InvalidSelection }

public sealed record ImageSourceResolution(ImageSourceResolutionKind Kind, WorkflowNodeBase? Source = null);

/// <summary>Resolves a preview source from the current graph, without retained UI selection state.</summary>
public static class ImageSourceResolver
{
    public static ImageSourceResolution InheritParentInput(ImageSourceResolution resolution, string childId,
        IReadOnlyList<string> ancestors, Func<string, SunEyeVision.Workflow.Workflow?> getWorkflow)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { childId };
        foreach (var parentId in ancestors)
        {
            if (resolution.Kind != ImageSourceResolutionKind.None) break;
            if (!visited.Add(parentId)) return new(ImageSourceResolutionKind.InvalidSelection);
            var parent = getWorkflow(parentId);
            if (parent == null) return new(ImageSourceResolutionKind.None);
            var calls = parent.Nodes.Where(node => node.SubroutineId == childId && node.IsEnabled).ToArray();
            if (calls.Length != 1) return new(ImageSourceResolutionKind.Ambiguous);
            resolution = Resolve(calls[0].Id, parent.Nodes, parent.Connections);
            childId = parentId;
        }
        return resolution;
    }

    public static bool IsImageSource(string? toolType) => toolType is not null &&
        (toolType.Contains("ImageLoad", StringComparison.OrdinalIgnoreCase)
        || toolType.Contains("图片载入", StringComparison.OrdinalIgnoreCase)
        || toolType.Contains("ImageSource", StringComparison.OrdinalIgnoreCase));

    public static ImageSourceResolution Resolve(string nodeId, IEnumerable<WorkflowNodeBase> nodes,
        IEnumerable<WorkflowConnection> connections)
    {
        var byId = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var upstream = connections.GroupBy(edge => edge.TargetNodeId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceNodeId).Distinct().ToArray());
        var resolving = new HashSet<string>(StringComparer.Ordinal);

        ImageSourceResolution ResolveBranch(string startId)
        {
            if (!byId.TryGetValue(startId, out var start)) return new(ImageSourceResolutionKind.None);
            if (!resolving.Add(startId)) return new(ImageSourceResolutionKind.InvalidSelection);
            try
            {
                if (IsImageSource(start.ToolType)) return new(ImageSourceResolutionKind.Found, start);
                var visited = new HashSet<string>(StringComparer.Ordinal) { startId };
                var queue = new Queue<string>();
                var sources = new List<WorkflowNodeBase>();
                queue.Enqueue(startId);
                while (queue.TryDequeue(out var current))
                {
                    if (!upstream.TryGetValue(current, out var parents)) continue;
                    foreach (var id in parents)
                    {
                        if (!visited.Add(id) || !byId.TryGetValue(id, out var parent)) continue;
                        if (IsImageSource(parent.ToolType)) sources.Add(parent);
                        else queue.Enqueue(id);
                    }
                }

                var selectedId = start.SelectedDisplaySource?.SourceNodeId;
                if (!string.IsNullOrEmpty(selectedId) && selectedId != startId)
                {
                    // A stale or unrelated explicit selection must not silently choose another branch.
                    if (!visited.Contains(selectedId)) return new(ImageSourceResolutionKind.InvalidSelection);
                    return ResolveBranch(selectedId);
                }
                return sources.Count switch
                {
                    0 => new(ImageSourceResolutionKind.None),
                    1 => new(ImageSourceResolutionKind.Found, sources[0]),
                    _ => new(ImageSourceResolutionKind.Ambiguous)
                };
            }
            finally { resolving.Remove(startId); }
        }

        return ResolveBranch(nodeId);
    }
}
