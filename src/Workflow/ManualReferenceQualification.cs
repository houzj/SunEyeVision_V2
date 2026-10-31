using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Workflow;

/// <summary>Frozen control predecessors and declared source identities for one consumer.</summary>
internal sealed class ManualReferenceQualification
{
    private readonly string _scope;
    private readonly HashSet<string> _predecessors;
    private readonly Dictionary<string,IReadOnlyList<ManualReferenceSource>> _sources;

    internal ManualReferenceQualification(Workflow workflow,string scope,string consumerId)
    {
        _scope=scope;
        var incoming=workflow.Connections.GroupBy(edge=>edge.TargetNodeId,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.Select(edge=>edge.SourceNodeId).ToArray(),StringComparer.Ordinal);
        _predecessors=new(StringComparer.Ordinal);
        var pending=new Stack<string>();
        pending.Push(consumerId);
        while(pending.Count>0)
        {
            var target=pending.Pop();
            if(!incoming.TryGetValue(target,out var sourceIds))continue;
            foreach(var id in sourceIds)if(_predecessors.Add(id))pending.Push(id);
        }
        _predecessors.Remove(consumerId);
        _sources=workflow.Nodes.ToDictionary(node=>node.Id,
            node=>node.RuntimeConfiguration?.CaptureManualReferenceSources()??Array.Empty<ManualReferenceSource>(),
            StringComparer.Ordinal);
    }

    internal Diagnostic? Validate(OutputReference reference)
    {
        if((reference.ScopeId??_scope)!=_scope||!_predecessors.Contains(reference.NodeId))
            return new("manual-source-scope","普通参数来源必须属于当前流程的控制前序。");
        if(!_sources.TryGetValue(reference.NodeId,out var declarations)||
           !declarations.Any(source=>source.PortId==reference.PortId&&source.MemberPath.SequenceEqual(reference.MemberFieldPath,StringComparer.Ordinal)))
            return new("manual-source-declaration","来源字段未声明为可手动引用。");
        return null;
    }
}