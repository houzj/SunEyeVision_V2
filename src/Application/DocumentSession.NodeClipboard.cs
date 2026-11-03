using System.Text.Json;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

/// <summary>Session-issued private definition snapshot. It never holds invocation results or mutable graph collections.</summary>
public sealed class NodeClipboardFragment
{
    internal sealed record Node(string Id, string ToolId, string DisplayName, bool Enabled, CanvasPoint Position,
        double Width, double Height, string? SelectedDisplayReference, IReadOnlyDictionary<string,JsonElement>? AdditionalData,
        ToolConfiguration Configuration);
    internal sealed record Edge(string Id, string SourceId, string TargetId, string SourcePort, string TargetPort,
        IReadOnlyList<CanvasPoint>? ManualPath);
    internal sealed record Dependency(string WorkflowId, NodeDefinition Actual, Guid Generation, CancellationToken Retirement, bool EdgeOnly);
    internal NodeClipboardFragment(DocumentSession owner, RuntimeDirectorySnapshot directory, string flowId,
        Node[] nodes, Edge[] edges, Dependency[] dependencies, GlobalVariable[] variables)
    { Owner=owner; Directory=directory; WorkflowId=flowId; Nodes=nodes; Edges=edges; Dependencies=dependencies; Variables=variables; }
    internal DocumentSession Owner { get; }
    internal RuntimeDirectorySnapshot Directory { get; }
    internal string WorkflowId { get; }
    internal Node[] Nodes { get; }
    internal Edge[] Edges { get; }
    internal Dependency[] Dependencies { get; }
    internal GlobalVariable[] Variables { get; }
    public CanvasPoint Anchor => Nodes[0].Position;
}

public sealed partial class DocumentSession
{
    public NodeClipboardFragment CaptureNodeClipboard(string workflowId, IReadOnlyList<NodeDefinition> actualNodes)
    {
        ArgumentNullException.ThrowIfNull(actualNodes);
        EnsureOwnerCanEdit();
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scope=OpenScope(workflowId); ValidateScopeCore(scope);
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
            var dependencies=new Dictionary<(string,string),NodeClipboardFragment.Dependency>();
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
                return new NodeClipboardFragment.Node(node.Id,node.ToolType,node.DispName,node.IsEnabled,node.Position,node.Width,node.Height,
                    node.SelectedDisplayReference,node.AdditionalData?.ToDictionary(pair=>pair.Key,pair=>pair.Value.Clone(),StringComparer.Ordinal),node.Configuration.Copy());
            }).ToArray();
            var edges=scope.Flow.Connections.Where(edge=>ids.Contains(edge.TargetNodeId)).Select(edge=>
            {
                if (!ids.Contains(edge.SourceNodeId))
                {
                    var actual=scope.Flow.Nodes.Single(node=>node.Id==edge.SourceNodeId);
                    var lifetime=CaptureNodeLifetime(scope,actual);
                    dependencies.TryAdd((workflowId,actual.Id),new(workflowId,actual,lifetime.Generation,lifetime.Retirement,true));
                }
                return new NodeClipboardFragment.Edge(edge.Id,edge.SourceNodeId,edge.TargetNodeId,edge.SourcePort,edge.TargetPort,
                    edge.ManualPathPoints is null?null:Array.AsReadOnly(edge.ManualPathPoints.ToArray()));
            }).ToArray();
            return new NodeClipboardFragment(this,_directory,workflowId,nodes,edges,dependencies.Values.ToArray(),variables.Values.ToArray());
        }
    }

    public sealed record NodePasteReceipt(IReadOnlyList<NodeDefinition> Nodes, IReadOnlyList<ConnectionDefinition> Connections, DocumentChangeSet Change);

    public NodePasteReceipt PasteNodeClipboard(string targetWorkflowId, NodeClipboardFragment fragment, CanvasPoint anchor)
    {
        ArgumentNullException.ThrowIfNull(fragment); EnsureOwnerCanEdit();
        if (!ReferenceEquals(fragment.Owner,this) || !ReferenceEquals(fragment.Directory,_directory))
            throw new InvalidOperationException("复制片段不属于当前活动方案或目录。");
        if(fragment.Variables.Any(variable=>!Document.GlobalVariables.Any(actual=>ReferenceEquals(actual,variable))))
            throw new InvalidOperationException("复制片段的变量来源已退出当前方案。");
        var target=OpenScope(targetWorkflowId); ValidateScopeCore(target);
        if (target.Flow.IsSubroutine) throw new InvalidOperationException("控制流程复制尚未接入。");
        foreach(var dependency in fragment.Dependencies)
        {
            if(dependency.EdgeOnly && targetWorkflowId!=fragment.WorkflowId) continue;
            var scope=OpenScope(dependency.WorkflowId);ValidateScopeCore(scope);
            if(dependency.Retirement.IsCancellationRequested || !scope.Flow.Nodes.Any(node=>ReferenceEquals(node,dependency.Actual)))
                throw new InvalidOperationException("复制片段的外部来源已退役。");
            var lifetime=CaptureNodeLifetime(scope,dependency.Actual);
            if(lifetime.Generation!=dependency.Generation)throw new InvalidOperationException("复制片段的外部来源代次已变化。");
        }
        var draft=PrepareGraphEdit(targetWorkflowId);
        var dx=anchor.X-fragment.Anchor.X;var dy=anchor.Y-fragment.Anchor.Y;
        var mapped=new Dictionary<string,NodeDefinition>(StringComparer.Ordinal);
        foreach(var snapshot in fragment.Nodes)
        {
            var node=CreateNode(draft,snapshot.ToolId,new CanvasPoint(snapshot.Position.X+dx,snapshot.Position.Y+dy));
            node.IsEnabled=snapshot.Enabled;node.Width=snapshot.Width;node.Height=snapshot.Height;
            node.AdditionalData=snapshot.AdditionalData;
            var stem=snapshot.DisplayName.TrimEnd('0','1','2','3','4','5','6','7','8','9');
            if(!string.IsNullOrWhiteSpace(stem))
            {
                var suffix=node.LocalIndex;var name=stem+suffix;
                while(draft.Nodes.Any(other=>!ReferenceEquals(other,node)&&other.DispName==name))name=stem+(++suffix);
                node.DispName=name;node.Name=$"{node.GlobalIndex} {name}";
            }
            mapped.Add(snapshot.Id,node);
        }
        var edges=new Dictionary<string,ConnectionDefinition>(StringComparer.Ordinal);
        foreach(var edge in fragment.Edges)
        {
            var internalSource=mapped.TryGetValue(edge.SourceId,out var source);
            if(!internalSource && targetWorkflowId!=fragment.WorkflowId)continue;
            var copy=new ConnectionDefinition(Guid.NewGuid().ToString("N"),internalSource?source!.Id:edge.SourceId,
                mapped[edge.TargetId].Id,edge.SourcePort,edge.TargetPort);
            if(internalSource && edge.ManualPath is not null)
                copy.ManualPathPoints=edge.ManualPath.Select(p=>new CanvasPoint(p.X+dx,p.Y+dy)).ToArray();
            draft.AddConnection(copy);edges.Add(edge.Id,copy);
        }
        OutputReference Remap(OutputReference reference)
        {
            var scopeId=reference.ScopeId ?? fragment.WorkflowId;
            var id=reference.NodeId;
            if(scopeId==fragment.WorkflowId && mapped.TryGetValue(id,out var copy)){id=copy.Id;scopeId=targetWorkflowId;}
            var selection=reference.Selection is BindingSelection.AtField field
                ?new BindingSelection.AtField(Remap(field.Reference),field.ConstructionBehavior):reference.Selection;
            return new OutputReference(id,reference.PortId,reference.Type,reference.MemberFieldPath,selection,scopeId,reference.Storage);
        }
        static string Identity(string id,ParameterSetting setting)
        {
            using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream))
                ParameterSettingCanonical.Write(writer,new Dictionary<string,ParameterSetting>{{id,setting}});
            return Convert.ToBase64String(stream.ToArray());
        }
        foreach(var snapshot in fragment.Nodes)
        {
            var node=mapped[snapshot.Id];var mappedFields=snapshot.Configuration.Fields.MapReferences(Remap);
            var changes=mappedFields.Capture().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            foreach(var id in changes.Keys.ToArray())
                if(changes[id] is ReferenceSetting reference && reference.OriginId is {} origin && origin.StartsWith("connection:",StringComparison.Ordinal))
                {
                    if(!edges.TryGetValue(origin[11..],out var edge))throw new InvalidOperationException("复制的自动来源缺少有效连线。");
                    changes[id]=reference with {OriginId="connection:"+edge.Id};
                }
            var fields=new FieldSettings(snapshot.Configuration.Fields.Declarations.Values,changes);
            var values=snapshot.Configuration.CaptureCurrentValues().Where(pair=>Identity(pair.Key,snapshot.Configuration.Fields.Read(pair.Key))==Identity(pair.Key,fields.Read(pair.Key)))
                .ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            node.Configuration=new ToolConfiguration(node.ToolType,fields,values,parameterBindingOrder:snapshot.Configuration.ParameterBindingOrder);
            _=AdmitConfiguration(node,node.Configuration);
            node.SelectedDisplayReference=snapshot.SelectedDisplayReference is {} selected
                ?ParameterSettingCanonical.Serialize(Remap(ParameterSettingCanonical.Deserialize(selected))):null;
        }
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
                    if(!origin.StartsWith("connection:",StringComparison.Ordinal) || !candidate.Edges.Any(edge=>edge.Edge.Id==origin[11..] &&
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
        foreach(var node in mapped.Values.Where(node=>node.SelectedDisplayReference is not null))
        {
            var reference=ParameterSettingCanonical.Deserialize(node.SelectedDisplayReference!);
            if(reference.Selection is not (BindingSelection.AllItems or BindingSelection.AtConstant) ||
                ParameterReferenceRules.SelectedType(reference)!=TypeRef.ResourceOf(ResourceKind.Image))
                throw new InvalidOperationException("复制图像导航来源无效。");
            var eligible=new HashSet<string>(StringComparer.Ordinal){node.Id};var pending=new Stack<string>();pending.Push(node.Id);
            while(pending.TryPop(out var current))if(incoming.TryGetValue(current,out var sources))
                foreach(var source in sources)if(eligible.Add(source))pending.Push(source);
            var declarations=candidate.Nodes.Where(item=>eligible.Contains(item.Node.Id)).Select(item=>
                new BindingSource(new(targetWorkflowId,item.Node.Id),Guid.Empty,AdmitConfiguration(item.Node,item.Configuration).Schema,0)).ToArray();
            var bindingScope=new ResultDisplayBindingScope(new(targetWorkflowId,node.Id),_owner.Id,ChangeRevision,declarations);
            if(new ParameterBindingService().CompileBinding(reference,new Port("display-image",TypeRef.ResourceOf(ResourceKind.Image)),bindingScope).Plan is null)
                throw new InvalidOperationException("复制图像导航来源不属于目标前序或自身。");
        }
        var change=CommitGraphEdit(draft,"粘贴节点") ?? throw new InvalidOperationException("复制没有发布任何节点。");
        return new(Array.AsReadOnly(mapped.Values.ToArray()),Array.AsReadOnly(edges.Values.ToArray()),change);
    }
}
