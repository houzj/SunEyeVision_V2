using System.Collections.ObjectModel;
using System.Text.Json;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

// Frozen, pure definition snapshots. Session ownership and external dependency lifetimes stay outside this builder.
internal sealed record FragmentNodeSnapshot(string Id, string ToolId, string DisplayName, bool Enabled, CanvasPoint Position,
    double Width, double Height, string? SelectedDisplayReference, IReadOnlyDictionary<string,JsonElement>? AdditionalData,
    ToolConfiguration Configuration);
internal sealed record FragmentEdgeSnapshot(string Id, string SourceId, string TargetId, string SourcePort, string TargetPort,
    IReadOnlyList<CanvasPoint>? ManualPath);
internal sealed record FragmentIssuedNode(string NodeId, int GlobalIndex, int LocalIndex, string Name, string DisplayName);
internal sealed record FragmentBuildResult(IReadOnlyList<NodeDefinition> Nodes, IReadOnlyList<ConnectionDefinition> Edges,
    IReadOnlyDictionary<string,NodeDefinition> NodeMap, IReadOnlyDictionary<string,ConnectionDefinition> EdgeMap);

internal static class FragmentBuilder
{
    internal static FragmentBuildResult Build(string sourceFlowId, string targetFlowId,
        IReadOnlyList<FragmentNodeSnapshot> nodes, IReadOnlyList<FragmentEdgeSnapshot> edges,
        IReadOnlyDictionary<string,FragmentIssuedNode> nodeIds, IReadOnlyDictionary<string,string> edgeIds,
        CanvasPoint translation, bool keepExternalIncoming)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFlowId);ArgumentException.ThrowIfNullOrWhiteSpace(targetFlowId);
        ArgumentNullException.ThrowIfNull(nodes);ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(nodeIds);ArgumentNullException.ThrowIfNull(edgeIds);
        var sourceIds=nodes.Select(node=>node.Id).ToHashSet(StringComparer.Ordinal);
        if(sourceIds.Count!=nodes.Count ||
            nodes.Any(node=>!nodeIds.ContainsKey(node.Id)) || nodeIds.Count!=nodes.Count ||
            nodeIds.Values.Any(id=>id is null || string.IsNullOrWhiteSpace(id.NodeId) || sourceIds.Contains(id.NodeId) ||
                id.GlobalIndex<=0 || id.LocalIndex<=0 || string.IsNullOrWhiteSpace(id.Name) || string.IsNullOrWhiteSpace(id.DisplayName)) ||
            nodeIds.Values.Select(id=>id.NodeId).Distinct(StringComparer.Ordinal).Count()!=nodeIds.Count ||
            nodeIds.Values.Select(id=>id.GlobalIndex).Distinct().Count()!=nodeIds.Count ||
            nodes.Select(node=>(node.ToolId,nodeIds[node.Id].LocalIndex)).Distinct().Count()!=nodes.Count)
            throw new InvalidOperationException("复制节点签发身份缺失或重复。");
        var included=edges.Where(edge=>sourceIds.Contains(edge.TargetId) && (sourceIds.Contains(edge.SourceId)||keepExternalIncoming)).ToArray();
        if(included.Select(edge=>edge.Id).Distinct(StringComparer.Ordinal).Count()!=included.Length ||
            included.Any(edge=>!edgeIds.ContainsKey(edge.Id)) || edgeIds.Count!=included.Length ||
            edgeIds.Values.Any(string.IsNullOrWhiteSpace) || edgeIds.Values.Distinct(StringComparer.Ordinal).Count()!=edgeIds.Count ||
            edgeIds.Any(pair=>pair.Key==pair.Value))
            throw new InvalidOperationException("复制连线签发身份缺失或重复。");
        var map=new Dictionary<string,NodeDefinition>(StringComparer.Ordinal);
        foreach(var snapshot in nodes)
        {
            var id=nodeIds[snapshot.Id];
            var node=new NodeDefinition(id.NodeId,id.Name,snapshot.ToolId,snapshot.Configuration.Copy())
            { GlobalIndex=id.GlobalIndex,LocalIndex=id.LocalIndex,DispName=id.DisplayName,IsEnabled=snapshot.Enabled,
              PositionX=new CanvasPoint(snapshot.Position.X+translation.X,snapshot.Position.Y+translation.Y).X,
              PositionY=new CanvasPoint(snapshot.Position.X+translation.X,snapshot.Position.Y+translation.Y).Y,
              Width=snapshot.Width,Height=snapshot.Height,AdditionalData=snapshot.AdditionalData };
            map.Add(snapshot.Id,node);
        }
        var mappedEdges=new Dictionary<string,ConnectionDefinition>(StringComparer.Ordinal);
        foreach(var edge in included)
        {
            var isInternal=map.TryGetValue(edge.SourceId,out var source);
            var copy=new ConnectionDefinition(edgeIds[edge.Id],isInternal?source!.Id:edge.SourceId,map[edge.TargetId].Id,edge.SourcePort,edge.TargetPort);
            if(isInternal && edge.ManualPath is not null)
                copy.ManualPathPoints=edge.ManualPath.Select(point=>new CanvasPoint(point.X+translation.X,point.Y+translation.Y)).ToArray();
            mappedEdges.Add(edge.Id,copy);
        }
        OutputReference Remap(OutputReference reference)
        {
            var scopeId=reference.ScopeId??sourceFlowId;var id=reference.NodeId;
            if(scopeId==sourceFlowId&&map.TryGetValue(id,out var node)){id=node.Id;scopeId=targetFlowId;}
            var selection=reference.Selection is BindingSelection.AtField index
                ?new BindingSelection.AtField(Remap(index.Reference),index.ConstructionBehavior):reference.Selection;
            return new OutputReference(id,reference.PortId,reference.Type,reference.MemberFieldPath,selection,scopeId,reference.Storage);
        }
        static string Identity(string id,ParameterSetting value)
        {
            using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream))
                ParameterSettingCanonical.Write(writer,new Dictionary<string,ParameterSetting>{{id,value}});
            return Convert.ToBase64String(stream.ToArray());
        }
        foreach(var snapshot in nodes)
        {
            var node=map[snapshot.Id];var mappedFields=snapshot.Configuration.Fields.MapReferences(Remap);
            var changes=mappedFields.Capture().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            foreach(var id in changes.Keys.ToArray())
                if(changes[id] is ReferenceSetting reference && reference.OriginId is {} origin && origin.StartsWith("connection:",StringComparison.Ordinal))
                {
                    if(!mappedEdges.TryGetValue(origin[11..],out var edge))throw new InvalidOperationException("复制的自动来源缺少有效连线。");
                    changes[id]=reference with {OriginId="connection:"+edge.Id};
                }
            var fields=new FieldSettings(snapshot.Configuration.Fields.Declarations.Values,changes);
            var values=snapshot.Configuration.CaptureCurrentValues().Where(pair=>Identity(pair.Key,snapshot.Configuration.Fields.Read(pair.Key))==Identity(pair.Key,fields.Read(pair.Key)))
                .ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            node.Configuration=new ToolConfiguration(node.ToolType,fields,values,parameterBindingOrder:snapshot.Configuration.ParameterBindingOrder);
            node.SelectedDisplayReference=snapshot.SelectedDisplayReference is {} selected
                ?ParameterSettingCanonical.Serialize(Remap(ParameterSettingCanonical.Deserialize(selected))):null;
        }
        return new(Array.AsReadOnly(nodes.Select(node=>map[node.Id]).ToArray()),Array.AsReadOnly(included.Select(edge=>mappedEdges[edge.Id]).ToArray()),
            new ReadOnlyDictionary<string,NodeDefinition>(map),new ReadOnlyDictionary<string,ConnectionDefinition>(mappedEdges));
    }
}
