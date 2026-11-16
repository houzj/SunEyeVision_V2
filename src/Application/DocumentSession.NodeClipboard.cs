using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

/// <summary>Session-issued private definition snapshot. It never holds invocation results or mutable graph collections.</summary>
internal sealed class FrozenNodeClipboard
{
    internal sealed record Dependency(string WorkflowId, NodeDefinition Actual, Guid Generation, CancellationToken Retirement, bool EdgeOnly);
    internal FrozenNodeClipboard(DocumentSession owner, RuntimeDirectorySnapshot directory, string flowId,
        FragmentNodeSnapshot[] nodes, FragmentEdgeSnapshot[] edges, Dependency[] dependencies, GlobalVariable[] variables)
    { Owner=owner; Directory=directory; WorkflowId=flowId; Nodes=nodes; Edges=edges; Dependencies=dependencies; Variables=variables; }
    internal DocumentSession Owner { get; }
    internal RuntimeDirectorySnapshot Directory { get; }
    internal string WorkflowId { get; }
    internal FragmentNodeSnapshot[] Nodes { get; }
    internal FragmentEdgeSnapshot[] Edges { get; }
    internal Dependency[] Dependencies { get; }
    internal GlobalVariable[] Variables { get; }
    public CanvasPoint Anchor => Nodes[0].Position;
}

/// <summary>A session-owned ordered selection; definitions are read again when pasted.</summary>
public sealed class NodeClipboardFragment
{
    internal sealed record Source(NodeDefinition Actual, string Id, string Name);
    internal NodeClipboardFragment(DocumentSession owner, RuntimeDirectorySnapshot directory, WorkflowDefinition flow, Source[] sources)
    { Owner=owner; Directory=directory; Flow=flow; FlowName=flow.Name; Sources=sources; }
    internal DocumentSession Owner { get; }
    internal RuntimeDirectorySnapshot Directory { get; }
    internal WorkflowDefinition Flow { get; }
    internal string FlowName { get; }
    internal Source[] Sources { get; }

}
public sealed partial class DocumentSession
{
    public NodeClipboardFragment CaptureNodeClipboard(string workflowId, IReadOnlyList<NodeDefinition> actualNodes)
    {
        ArgumentNullException.ThrowIfNull(actualNodes); EnsureOwnerCanEdit();
        lock (_gate)
        {
            var scope=OpenScope(workflowId); ValidateScopeCore(scope);
            if(scope.Flow.IsSubroutine || actualNodes.Count==0 || actualNodes.Distinct(ReferenceEqualityComparer.Instance).Count()!=actualNodes.Count ||
               actualNodes.Any(node=>!scope.Flow.Nodes.Any(actual=>ReferenceEquals(actual,node))))
                throw new InvalidOperationException("复制范围必须是当前流程实际且不重复的普通节点。");
            if(actualNodes.Any(node=>string.Equals(node.ToolType,"Subroutine",StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.ToolType,"Condition",StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(node.SubroutineId)))
                throw new InvalidOperationException("控制流节点复制尚未接入。");
            return new(this,_directory,scope.Flow,actualNodes.Select(node=>new NodeClipboardFragment.Source(node,node.Id,node.Name)).ToArray());
        }
    }
    private FrozenNodeClipboard FreezeNodeClipboard(string workflowId, IReadOnlyList<NodeDefinition> actualNodes)
    {
        ArgumentNullException.ThrowIfNull(actualNodes);
        EnsureOwnerCanEdit();
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scope=OpenScope(workflowId); ValidateScopeCore(scope);
            if(scope.Flow.IsSubroutine)throw new InvalidOperationException("控制流程复制尚未接入。");
            if (actualNodes.Count == 0 || actualNodes.Distinct(ReferenceEqualityComparer.Instance).Count()!=actualNodes.Count ||
                actualNodes.Any(node=>!scope.Flow.Nodes.Any(actual=>ReferenceEquals(actual,node))))
                throw new InvalidOperationException("复制范围必须是当前流程实际且不重复的节点。");
            if (actualNodes.Any(node=>string.Equals(node.ToolType,"Subroutine",StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.ToolType,"Condition",StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(node.SubroutineId)))
                throw new InvalidOperationException("控制流节点复制尚未接入。");
            var ids=actualNodes.Select(node=>node.Id).ToHashSet(StringComparer.Ordinal);
            var opening=GraphState.Capture(scope.Flow);
            CandidateInputValidation.Validate(_documentId,workflowId,_owner.Id,ChangeRevision,opening,Document.GlobalVariables,ids,false,AdmitConfiguration,false);
            CandidateDisplayValidation.Validate(workflowId,_owner.Id,ChangeRevision,opening,ids,false,AdmitConfiguration);
            var dependencies=new Dictionary<(string,string),FrozenNodeClipboard.Dependency>();
            var variables=new Dictionary<string,GlobalVariable>(StringComparer.Ordinal);
            void CaptureReference(OutputReference reference)
            {
                var flowId=reference.ScopeId ?? workflowId;
                if (!(flowId==workflowId && ids.Contains(reference.NodeId)))
                {
                    var dependencyScope=OpenScope(flowId); ValidateScopeCore(dependencyScope);
                    var actual=dependencyScope.Flow.Nodes.SingleOrDefault(node=>node.Id==reference.NodeId)
                        ?? throw new InvalidOperationException("复制来源引用的节点不存在。" );
                    var lifetime=CaptureNodeLifetime(dependencyScope,actual);
                    dependencies[(flowId,actual.Id)]=new(flowId,actual,lifetime.Generation,lifetime.Retirement,false);
                }
                if (reference.Selection is BindingSelection.AtField index) CaptureReference(index.Reference);
            }
            var nodes=actualNodes.Select(node=>
            {
                var admitted=AdmitConfiguration(node,node.Configuration);
                foreach(var (_,reference) in BindingRelationCatalog.VariableReferences(admitted))
                {
                    if(reference.SolutionId!=_documentId)throw new InvalidOperationException("复制来源的变量不属于当前方案。");
                    var variable=Document.GlobalVariables.SingleOrDefault(item=>item.Id==reference.VariableId)
                        ?? throw new InvalidOperationException("复制来源的变量已不存在。");
                    variables[variable.Id]=variable;
                }
                var fields=node.Configuration.Fields;
                _=fields.MapReferences(reference=>{CaptureReference(reference);return reference;});
                if (node.SelectedDisplayReference is {} selected) CaptureReference(ParameterSettingCanonical.Deserialize(selected));
                if(node.DisplayOverlays is {} overlays)
                    foreach(var reference in overlays.References())CaptureReference(ParameterSettingCanonical.Deserialize(reference));
                return new FragmentNodeSnapshot(node.Id,node.ToolType,node.DispName,node.IsEnabled,node.Position,node.Width,node.Height,
                    node.SelectedDisplayReference,node.DisplayOverlays,node.AdditionalData?.ToDictionary(pair=>pair.Key,pair=>pair.Value.Clone(),StringComparer.Ordinal),node.Configuration.Copy());
            }).ToArray();
            var edges=scope.Flow.Connections.Where(edge=>ids.Contains(edge.TargetNodeId)).Select(edge=>
            {
                if (!ids.Contains(edge.SourceNodeId))
                {
                    var actual=scope.Flow.Nodes.Single(node=>node.Id==edge.SourceNodeId);
                    var lifetime=CaptureNodeLifetime(scope,actual);
                    dependencies.TryAdd((workflowId,actual.Id),new(workflowId,actual,lifetime.Generation,lifetime.Retirement,true));
                }
                return new FragmentEdgeSnapshot(edge.Id,edge.SourceNodeId,edge.TargetNodeId,edge.SourcePort,edge.TargetPort,
                    edge.ManualPathPoints is null?null:Array.AsReadOnly(edge.ManualPathPoints.ToArray()));
            }).ToArray();
            return new FrozenNodeClipboard(this,_directory,workflowId,nodes,edges,dependencies.Values.ToArray(),variables.Values.ToArray());
        }
    }

    public IReadOnlyList<NodeDefinition> ResolveNodeClipboardSources(NodeClipboardFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment); EnsureOwnerCanEdit();
        if(!ReferenceEquals(fragment.Owner,this) || !ReferenceEquals(fragment.Directory,_directory))
            throw new InvalidOperationException("复制来源不属于当前活动方案或目录。");
        lock(_gate)
        {
            var flow=Document.Workflows.FirstOrDefault(item=>ReferenceEquals(item,fragment.Flow) && item.Name==fragment.FlowName);
            return Array.AsReadOnly(flow is null ? Array.Empty<NodeDefinition>() : fragment.Sources
                .Where(source=>flow.Nodes.Any(node=>ReferenceEquals(node,source.Actual) && node.Id==source.Id && node.Name==source.Name))
                .Select(source=>source.Actual).ToArray());
        }
    }
    public sealed record NodePasteReceipt(IReadOnlyList<NodeDefinition> Nodes, IReadOnlyList<ConnectionDefinition> Connections, DocumentChangeSet? Change);

    public NodePasteReceipt PasteNodeClipboard(string targetWorkflowId, NodeClipboardFragment fragment, CanvasPoint anchor, IReadOnlyDictionary<NodeDefinition,CanvasPoint>? positions = null)
    {
        ArgumentNullException.ThrowIfNull(fragment); EnsureOwnerCanEdit();
        if (!ReferenceEquals(fragment.Owner,this) || !ReferenceEquals(fragment.Directory,_directory))
            throw new InvalidOperationException("复制片段不属于当前活动方案或目录。");
        var available=ResolveNodeClipboardSources(fragment).ToArray();
        var sourceFlow=fragment.Flow;
        if(available.Length==0) return new(Array.Empty<NodeDefinition>(),Array.Empty<ConnectionDefinition>(),null);
        var frozen=FreezeNodeClipboard(sourceFlow!.Id,available);
        if(frozen.Variables.Any(variable=>!Document.GlobalVariables.Any(actual=>ReferenceEquals(actual,variable))))
            throw new InvalidOperationException("复制片段的变量来源已退出当前方案。");
        var target=OpenScope(targetWorkflowId); ValidateScopeCore(target);
        if (target.Flow.IsSubroutine) throw new InvalidOperationException("控制流程复制尚未接入。");
        foreach(var dependency in frozen.Dependencies)
        {
            if(dependency.EdgeOnly && targetWorkflowId!=frozen.WorkflowId) continue;
            var scope=OpenScope(dependency.WorkflowId);ValidateScopeCore(scope);
            if(dependency.Retirement.IsCancellationRequested || !scope.Flow.Nodes.Any(node=>ReferenceEquals(node,dependency.Actual)))
                throw new InvalidOperationException("复制片段的外部来源已退役。");
            var lifetime=CaptureNodeLifetime(scope,dependency.Actual);
            if(lifetime.Generation!=dependency.Generation)throw new InvalidOperationException("复制片段的外部来源代次已变化。");
        }
        var draft=PrepareGraphEdit(targetWorkflowId);
        var dx=anchor.X-frozen.Anchor.X;var dy=anchor.Y-frozen.Anchor.Y;
        static int FirstFree(ISet<int> occupied)
        {
            var value=1;while(occupied.Contains(value))value=checked(value+1);return value;
        }
        var global=Document.Workflows.SelectMany(flow=>flow.Nodes).Select(node=>node.GlobalIndex).Where(index=>index>0).ToHashSet();
        var local=target.Flow.Nodes.Where(node=>node.LocalIndex>0).GroupBy(node=>node.ToolType,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.Select(node=>node.LocalIndex).ToHashSet(),StringComparer.Ordinal);
        var usedNames=target.Flow.Nodes.Select(node=>node.DispName).ToHashSet(StringComparer.Ordinal);
        var issued=new Dictionary<string,FragmentIssuedNode>(StringComparer.Ordinal);
        foreach(var snapshot in frozen.Nodes)
        {
            var globalIndex=FirstFree(global);global.Add(globalIndex);
            if(!local.TryGetValue(snapshot.ToolId,out var allocated)){allocated=new HashSet<int>();local.Add(snapshot.ToolId,allocated);}
            var localIndex=FirstFree(allocated);allocated.Add(localIndex);
            var stem=snapshot.DisplayName.TrimEnd('0','1','2','3','4','5','6','7','8','9');
            if(string.IsNullOrWhiteSpace(stem))stem=_directory.GetCatalog(snapshot.ToolId)?.DisplayName??snapshot.ToolId;
            var suffix=localIndex;var display=stem+suffix;while(usedNames.Contains(display))display=stem+(++suffix);
            usedNames.Add(display);issued.Add(snapshot.Id,new(Guid.NewGuid().ToString("N"),globalIndex,localIndex,$"{globalIndex} {display}",display));
        }
        var selected=frozen.Nodes.Select(node=>node.Id).ToHashSet(StringComparer.Ordinal);
        var edgeIds=frozen.Edges.Where(edge=>selected.Contains(edge.TargetId) &&
            (selected.Contains(edge.SourceId)||targetWorkflowId==frozen.WorkflowId))
            .ToDictionary(edge=>edge.Id,_=>Guid.NewGuid().ToString("N"),StringComparer.Ordinal);
        if(positions is not null && (positions.Count!=available.Length || available.Any(node=>!positions.ContainsKey(node))))
            throw new InvalidOperationException("复制定位与当前有效来源不一致。");
        var fragmentResult=FragmentBuilder.Build(frozen.WorkflowId,targetWorkflowId,frozen.Nodes,frozen.Edges,issued,edgeIds,
            new CanvasPoint(dx,dy),targetWorkflowId==frozen.WorkflowId);
        var mapped=fragmentResult.NodeMap;var edges=fragmentResult.EdgeMap;
        foreach(var node in fragmentResult.Nodes){_=AdmitConfiguration(node,node.Configuration);draft.AddNode(node);}
        foreach(var edge in fragmentResult.Edges)draft.AddConnection(edge);
        if(positions is not null) foreach(var source in available) draft.MoveNode(mapped[source.Id],positions[source]);
        var candidate=GraphState.FromDraft(draft,PrepareConnectionBindings(draft));
        var participants=mapped.Values.Select(node=>node.Id).ToHashSet(StringComparer.Ordinal);
        CandidateInputValidation.Validate(_documentId,targetWorkflowId,_owner.Id,ChangeRevision,candidate,Document.GlobalVariables,participants,false,AdmitConfiguration,false);
        CandidateDisplayValidation.Validate(targetWorkflowId,_owner.Id,ChangeRevision,candidate,participants,false,AdmitConfiguration);
        // The general execution validator intentionally skips inactive input references. Copying
        // may make one active later, so verify every retained non-display reference now.
        var configurations=candidate.Nodes.ToDictionary(item=>item.Node.Id,item=>AdmitConfiguration(item.Node,item.Configuration),StringComparer.Ordinal);
        var bindingSources=configurations.Select(pair=>new BindingSource(new(targetWorkflowId,pair.Key),Guid.Empty,pair.Value.Schema,0)).ToArray();
        foreach(var item in candidate.Nodes.Where(item=>participants.Contains(item.Node.Id)))
        {
            var admitted=configurations[item.Node.Id];
            var scope=new BindingScopeSnapshot(new(targetWorkflowId,item.Node.Id),_owner.Id,ChangeRevision,bindingSources,
                bindingSources.Select(source=>source.Node),Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>(),Array.Empty<BindingVariableSource>());
            var qualification=new ManualReferenceQualification(targetWorkflowId,candidate,item.Node.Id,AdmitConfiguration);
            foreach(var (fieldId,setting) in admitted.Configuration.Fields.Capture())
            {
                if(setting is not ReferenceSetting reference || admitted.IsDisplayField(fieldId) || admitted.IsExecutionFieldActive(fieldId) ||
                    BindingRelationCatalog.IndexVariables(reference.Reference.Selection).Any())continue;
                if(!string.IsNullOrEmpty(reference.RebindingReason))throw new InvalidOperationException($"字段 {fieldId}：{reference.RebindingReason}");
                if(reference.OriginId is {} origin)
                {
                    if(!origin.StartsWith("connection:",StringComparison.Ordinal) || admitted.Schema.WorkflowImageInputId!=fieldId ||
                        ParameterReferenceRules.SelectedType(reference.Reference)!=TypeRef.ResourceOf(ResourceKind.Image) ||
                        reference.Reference.Selection is not BindingSelection.AllItems || reference.Conversion!=BindingConversion.Automatic ||
                        (reference.Reference.ScopeId ?? targetWorkflowId)!=targetWorkflowId || !candidate.Edges.Any(edge=>edge.Edge.Id==origin[11..] &&
                        edge.TargetNodeId==item.Node.Id && edge.SourceNodeId==reference.Reference.NodeId))
                        throw new InvalidOperationException($"字段 {fieldId}：复制的自动来源归属无效。");
                }
                var targetPort=admitted.Schema.Inputs.Concat(admitted.SettingsSchema.Inputs).Single(port=>port.Id==fieldId);
                var numeric=admitted.SettingsSchema.Inputs.Any(port=>port.Id==fieldId && port.Type.Shape==ValueShape.Scalar &&
                    port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64);
                var qualified=numeric?scope.WithReferenceQualification(qualification.Validate):scope;
                var result=new ParameterBindingService().CompileBinding(reference.Reference,targetPort,qualified,reference.Conversion,
                    admitted.ReferenceBehaviors.GetValueOrDefault(fieldId,ParameterReferenceBehavior.StrictExactlyOne));
                if(result.Compatibility is BindingCompatibility.Incompatible or BindingCompatibility.Unresolved)
                    throw new InvalidOperationException($"字段 {fieldId}：{result.Diagnostic?.Message ?? "非活动字段引用无效。"}");
            }
        }
        var incoming=candidate.Edges.GroupBy(edge=>edge.TargetNodeId,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.Select(edge=>edge.SourceNodeId).ToArray(),StringComparer.Ordinal);
        foreach(var node in mapped.Values.Where(node=>node.SelectedDisplayReference is not null||node.DisplayOverlays is not null))
        {
            var eligible=new HashSet<string>(StringComparer.Ordinal){node.Id};var pending=new Stack<string>();pending.Push(node.Id);
            while(pending.TryPop(out var current))if(incoming.TryGetValue(current,out var sources))
                foreach(var source in sources)if(eligible.Add(source))pending.Push(source);
            var declarations=candidate.Nodes.Where(item=>eligible.Contains(item.Node.Id)).Select(item=>
                new BindingSource(new(targetWorkflowId,item.Node.Id),Guid.Empty,AdmitConfiguration(item.Node,item.Configuration).Schema,0)).ToArray();
            var bindingScope=new ResultDisplayBindingScope(new(targetWorkflowId,node.Id),_owner.Id,ChangeRevision,declarations);
            var references=(node.DisplayOverlays?.References()??Enumerable.Empty<string>()).Append(node.SelectedDisplayReference).OfType<string>();
            foreach(var text in references.Distinct(StringComparer.Ordinal))
            {
                var reference=ParameterSettingCanonical.Deserialize(text);
                if(reference.Selection is not (BindingSelection.AllItems or BindingSelection.AtConstant) ||
                    ParameterReferenceRules.SelectedType(reference)!=TypeRef.ResourceOf(ResourceKind.Image)||
                    new ParameterBindingService().CompileBinding(reference,new Port("display-image",TypeRef.ResourceOf(ResourceKind.Image)),bindingScope).Plan is null)
                    throw new InvalidOperationException("复制图像导航来源不属于目标前序或自身。");
            }
        }
        var change=CommitGraphEdit(draft,"粘贴节点") ?? throw new InvalidOperationException("复制没有发布任何节点。");
        return new(Array.AsReadOnly(mapped.Values.ToArray()),Array.AsReadOnly(edges.Values.ToArray()),change);
    }
}
