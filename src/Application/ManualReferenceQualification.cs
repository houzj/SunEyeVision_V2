using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Frozen control predecessors and admitted manual sources for one candidate node.</summary>
internal sealed class ManualReferenceQualification
{
    private readonly string _scope;
    private readonly HashSet<string> _predecessors;
    private readonly Dictionary<string, IReadOnlyList<ManualReferenceSource>> _sources;

    internal ManualReferenceQualification(string scope, GraphState graph, string consumerId,
        Func<NodeDefinition, ToolConfiguration, AdmittedToolConfiguration> admit)
    {
        _scope = scope;
        var incoming = graph.Edges.GroupBy(edge => edge.TargetNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.SourceNodeId).ToArray(), StringComparer.Ordinal);
        _predecessors = new(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(consumerId);
        while (pending.Count > 0)
        {
            var target = pending.Pop();
            if (!incoming.TryGetValue(target, out var sources)) continue;
            foreach (var source in sources) if (_predecessors.Add(source)) pending.Push(source);
        }
        _predecessors.Remove(consumerId);
        _sources = graph.Nodes.ToDictionary(item => item.Node.Id,
            item => admit(item.Node, item.Configuration).ManualReferenceSources, StringComparer.Ordinal);
    }

    internal Diagnostic? Validate(OutputReference reference)
    {
        if ((reference.ScopeId ?? _scope) != _scope || !_predecessors.Contains(reference.NodeId))
            return new("manual-source-scope", "普通参数来源必须属于当前流程的控制前序。");
        if (!_sources.TryGetValue(reference.NodeId, out var declarations) ||
            !declarations.Any(source => source.PortId == reference.PortId &&
                source.MemberPath.SequenceEqual(reference.MemberFieldPath, StringComparer.Ordinal)))
            return new("manual-source-declaration", "来源字段未声明为可手动引用。");
        return null;
    }
}
