using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Workflow;

/// <summary>One workflow plan's frozen declarations and admitted attempts. No latest-result identity inference.</summary>
internal sealed class WorkflowBindingContext : IDisposable
{
    private sealed record Declaration(BindingSource Source,IReadOnlyDictionary<string,OutputVariable> Outputs,bool Complete);
    private readonly Dictionary<string,Declaration> _declarations;
    private readonly Dictionary<string,AlgorithmNode> _nodes;
    private readonly IReadOnlyDictionary<BindingNodeId,RunIdentity> _expected;
    private readonly string _scope;
    private readonly long _documentRevision;

    private static Dictionary<string,Declaration> CaptureDeclarations(Workflow workflow,string scope,Guid executionScope)
    {
        var reader=new OutputBindingChangeService();
        var result=new Dictionary<string,Declaration>(StringComparer.Ordinal);
        foreach(var node in workflow.Nodes)
        {
            var outputs=new Dictionary<string,OutputVariable>(StringComparer.Ordinal);
            var ports=new List<Port>();
            var complete=true;
            foreach(var output in reader.ReadContract(node)??Array.Empty<OutputVariable>())
            {
                if(output.Kind!=OutputVariableKind.Data)continue;
                try
                {
                    var port=BindingClrAdapter.DescribeOutput(output);
                    ports.Add(port);outputs.Add(port.Id,output);
                }
                catch(ArgumentException)
                {
                    complete=false;
                    // An unsupported declaration is never inferred from a value. Its existing dedicated
                    // adapter remains responsible until its explicit canonical bridge is implemented.
                }
            }
            var schema=new ToolSchema(Array.Empty<Port>(),ports);
            result.Add(node.Id,new(new(new(scope,node.Id),executionScope,schema,0),new ReadOnlyDictionary<string,OutputVariable>(outputs),complete));
        }
        return result;
    }

    private static BindingCompilation? CompileSetting(WorkflowNodeBase node,ParamSetting setting,
        IReadOnlyDictionary<string,Declaration> declarations,BindingScopeSnapshot scope)
    {
        if(setting.BindingType!=BindingType.Binding)return null;
        var explicitReference=setting.ReferenceExpression is not null;
        var property=node.Parameters.GetType().GetProperty(setting.ParameterName);
        var declared=(node.Parameters as IConfigurationFieldProvider)?.GetConfigurationFields().SingleOrDefault(field=>field.StoragePath==setting.ParameterName);
        var roi=RoiBindingInputs.Describe(node.Parameters).SingleOrDefault(slot=>slot.Key==setting.ParameterName);
        if(declared is null&&roi is null&&(property is not {CanWrite:true} || !BindingClrAdapter.TryDescribe(property.PropertyType,out _,out _)))
            return explicitReference?Unavailable("target-declaration","This input has no canonical writable-property declaration."):null;
        if(declared is not null&&(declared.Port.EditModes&FieldEditModes.Reference)==0)
            return Unavailable("target-mode","This declared field does not allow a reference.");
        OutputReference reference;
        try
        {
            if(explicitReference)reference=BindingReferenceSerializer.Deserialize(setting.ReferenceExpression!);
            else
            {
                if(setting.SourceNodeId is null||setting.SourceNodeId.StartsWith("__",StringComparison.Ordinal))return null;
                if(setting.SourceWorkflowId is {Length:>0} declaredScope && declaredScope!=scope.Consumer.ScopeId)
                    return Unavailable("scope","Source is outside the consumer workflow.");
                if(!declarations.TryGetValue(setting.SourceNodeId,out var source))return Unavailable("source","Source declaration is unavailable.");
                var path=PropertyPathParser.ParsePropertyPath(setting.SourceProperty??"");
                if(path.Count==0)return Unavailable("path","Source field is missing.");
                var output=source.Outputs.Values.FirstOrDefault(o=>setting.SourceFieldId.HasValue
                    ?o.FieldId==setting.SourceFieldId:string.Equals(o.Name,path[0].propertyName,StringComparison.OrdinalIgnoreCase));
                if(output is null)return null; // Explicit geometry/resource adapters are not silently reinterpreted here.
                if(path.Any(part=>part.typeFilter!=null)||path.Skip(1).Any(part=>part.arrayIndex.HasValue))return null;
                var port=source.Source.Schema.Outputs.Single(p=>p.Id==BindingClrAdapter.OutputPortId(output));
                BindingSelection selection=path[0].arrayIndex is >=0
                    ?port.Type.Shape!=ValueShape.Sequence&&path[0].arrayIndex==0?BindingSelection.All:new BindingSelection.AtConstant(path[0].arrayIndex!.Value)
                    :BindingSelection.All;
                reference=new(setting.SourceNodeId,port.Id,port.Type,path.Skip(1).Select(part=>part.propertyName),selection,scope.Consumer.ScopeId,port.Storage);
            }
            var target=roi?.Target(reference)??declared?.Port??BindingClrAdapter.DescribePort(setting.ParameterName,property!.PropertyType);
            return new ParameterBindingService().CompileBinding(reference,target,scope,setting.Conversion);
        }
        catch(Exception fault) when(fault is System.Text.Json.JsonException or ArgumentException or InvalidOperationException or FormatException)
        {return Unavailable("reference",fault.Message);}
        static BindingCompilation Unavailable(string code,string message)=>new(BindingCompatibility.Unresolved,null,new(code,message));
    }

    internal static BindingValidation ValidateTyped(Workflow workflow,WorkflowNodeBase node,ParamSetting setting,string scopeId)
    {
        var declarations=CaptureDeclarations(workflow,scopeId,Guid.Empty);
        var scope=new BindingScopeSnapshot(new(scopeId,node.Id),workflow.ExecutionOwner.Id,0,
            declarations.Values.Select(d=>d.Source),declarations.Values.Select(d=>d.Source.Node),
            Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
        var compiled=CompileSetting(node,setting,declarations,scope);
        return compiled is null?new(BindingCompatibility.Unresolved,"Binding declaration is unavailable.")
            :new(compiled.Compatibility,compiled.Diagnostic?.Message??"适配");
    }

    internal static IEnumerable<BindingNodeId> ReferenceNodes(OutputReference reference,string defaultScope)
    {
        yield return new(reference.ScopeId??defaultScope,reference.NodeId);
        if(reference.Selection is BindingSelection.AtField index)
            foreach(var dependency in ReferenceNodes(index.Reference,defaultScope))yield return dependency;
    }

    internal WorkflowBindingContext(WorkflowRunSession session,WorkflowRunScope scope,Workflow workflow,WorkflowExecutionDependencyGraph graph,
        IReadOnlyList<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource>? variables=null)
    {
        _scope=scope.WorkflowId;
        // Plans are recreated for every actual scope; no document-level plan cache or invented revision counter.
        // Structural schema fingerprints and original admission identities provide the immutable generation boundary.
        _documentRevision=0;
        _declarations=CaptureDeclarations(workflow,_scope,scope.RunId);
        _nodes=workflow.Nodes.OfType<AlgorithmNode>().ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var owner=session.ExecutionOwner??throw new InvalidOperationException("Root execution owner is not activated.");
        var predecessors=workflow.Nodes.Select(n=>new KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>(
            new(_scope,n.Id),graph.GetPredecessors(n.Id).Select(id=>new BindingNodeId(_scope,id)).ToArray())).ToArray();
        var additional=new Dictionary<string,IReadOnlyList<ExecutionInputIssue>>(StringComparer.Ordinal);
        foreach(var node in _nodes.Values)
        {
            var compilationScope=new BindingScopeSnapshot(new(_scope,node.Id),owner.Id,_documentRevision,
                _declarations.Values.Select(d=>d.Source),_declarations.Values.Select(d=>d.Source.Node),predecessors);
            var plans=new Dictionary<string,BindingPlan>(StringComparer.Ordinal);
            var errors=new List<ExecutionInputIssue>();
            var settings=node.ParamSettings.Settings.Concat(RoiBindingInputs.Settings(node.Parameters)).GroupBy(setting=>setting.ParameterName,StringComparer.Ordinal);
            foreach(var candidates in settings)
            {
                if(candidates.Count()!=1)throw new InvalidOperationException("ROI_BINDING_DUPLICATE: 参数绑定存在重复存储："+candidates.Key);
                var setting=candidates.Single();
                if(!BindingInputCatalog.IsInputActive(node.Parameters,setting.ParameterName))continue;
                if(graph.InputIssues.TryGetValue(node.Id,out var existingIssues)&&existingIssues.Any(i=>i.InputKey==setting.ParameterName))continue;
                var compiled=CompileSetting(node,setting,_declarations,compilationScope);
                if(compiled is null)continue;
                if(compiled.Plan is null)errors.Add(new(setting.ParameterName,setting.ParameterName,compiled.Diagnostic?.Message??"Binding is not ready."));
                else plans.Add(setting.ParameterName,compiled.Plan);
            }
            if(errors.Count>0)additional.Add(node.Id,errors);
            node.CompiledBindings=new ReadOnlyDictionary<string,BindingPlan>(plans);
        }
        graph.AddInputIssues(additional);
        // Freeze demand from this scope's compiled consumers before any producer starts.
        // Include dynamic-index sources; never infer demand from mutable last results or UI visibility.
        var demands=_nodes.Keys.ToDictionary(id=>id,_=>new List<OutputValueRequest>(),StringComparer.Ordinal);
        foreach(var consumer in _nodes.Values.Where(n=>n.IsEnabled))
        {
            foreach(var plan in consumer.CompiledBindings.Values)CollectDemand(plan.Source);
            // Existing image/geometry source slots have dedicated CLR adapters rather than a
            // writable canonical property. Their frozen relation descriptors are still real consumers.
            foreach(var input in BindingRelationCatalog.Describe(consumer).Where(i=>i.Active&&!i.InternalProjection))
            {
                if(BindingRelationCatalog.GetSetting(consumer,input.Key)?.ReferenceExpression is not null)continue;
                if(BindingRelationCatalog.SourceWorkflowId(consumer,input,_scope)!=_scope)continue;
                if(BindingInputCatalog.TryReference(input.Read(),_nodes.Keys,input.ReferenceOnly,out var sourceId,out var path))
                    CollectDeclaredDemand(sourceId,path,input.ReadFieldId?.Invoke());
            }
        }
        // Published variables and subroutine returns are consumers too, even when no local
        // algorithm node reads this port. Only the admitted configuration/catalog supplies demand.
        foreach(var variable in variables??Array.Empty<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource>())
            if(variable.HasProducer&&(variable.ProducerWorkflowId==_scope||string.IsNullOrEmpty(variable.ProducerWorkflowId)))
                CollectDeclaredDemand(variable.ProducerNodeId,variable.ProducerField);
        foreach(var caller in session.Configuration?.Definitions??Array.Empty<Workflow>())
            foreach(var consumer in caller.Nodes.Where(n=>n.IsEnabled))
                foreach(var input in BindingRelationCatalog.Describe(consumer).Where(i=>i.Active&&i.InternalProjection))
                    if(BindingRelationCatalog.SourceWorkflowId(consumer,input,caller.Id)==_scope
                        &&BindingInputCatalog.TryReference(input.Read(),_nodes.Keys,input.ReferenceOnly,out var sourceId,out var path))
                        CollectDeclaredDemand(sourceId,path,input.ReadFieldId?.Invoke());
        foreach(var node in _nodes.Values)
            node.OutputRequests=Array.AsReadOnly(demands[node.Id]
                .DistinctBy(d=>System.Text.Json.JsonSerializer.Serialize(new {d.PortId,d.FieldPath})).ToArray());
        void CollectDemand(BindingReferencePlan reference)
        {
            if(reference.Source.Node.ScopeId==_scope&&demands.TryGetValue(reference.Source.Node.NodeId,out var list))
                list.Add(new(reference.Reference.PortId,reference.Reference.MemberFieldPath));
            if(reference.Index is { } index)CollectDemand(index);
        }
        void CollectDeclaredDemand(string sourceId,string path,Guid? fieldId=null)
        {
            if(!demands.TryGetValue(sourceId,out var list)||!_declarations.TryGetValue(sourceId,out var source))return;
            var root=PropertyPathParser.ExtractPropertyName(path.Split('.')[0]);
            var output=source.Outputs.Values.FirstOrDefault(o=>fieldId is Guid id?o.FieldId==id:
                string.Equals(o.Name,root,StringComparison.OrdinalIgnoreCase));
            if(output is not null)list.Add(new(BindingClrAdapter.OutputPortId(output)));
        }
        var expected=new Dictionary<BindingNodeId,RunIdentity>();
        foreach(var node in _nodes.Values)
        {
            node.AdmittedIdentity=owner.Begin(_scope+"/"+node.Id,scope.RunId,ToolContract.Version,session.CancellationToken,
                (node.Parameters as IExecutionAdmissionProvider)?.ExecutionAdmission,session.RunId);
            node.BindingContext=this;
            node.BindingOutputSchema=_declarations[node.Id].Source.Schema;
            expected.Add(new(_scope,node.Id),node.AdmittedIdentity.ToInvocationIdentity(node.Id));
        }
        _expected=new ReadOnlyDictionary<BindingNodeId,RunIdentity>(expected);
    }

    internal IBindingRunSnapshot Capture(AlgorithmNode consumer,IDictionary<string,ToolResults>? results)
    {
        var candidates=new List<BindingNodeResult>();
        foreach(var dependency in consumer.CompiledBindings.Values.SelectMany(p=>p.Dependencies).Distinct())
        {
            if(dependency.ScopeId!=_scope||results?.TryGetValue(dependency.NodeId,out var result)!=true||
                result.ExecutionIdentity is null||result.BindingOutputSchema is null||result.DeclaredOutput is null)continue;
            try
            {
                using var reader=result.DeclaredOutput.AcquireReader();
                if(reader.Values is not { } values)continue;
                candidates.Add(new(dependency,result.ExecutionIdentity.ToInvocationIdentity(dependency.NodeId),result.BindingSchemaRevision,
                    result.BindingOutputSchema.Fingerprint,result.IsSuccess,values));
            }
            catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
            { /* A malformed source is unavailable; never fall back to old or inferred values. */ }
        }
        return new BindingRunSnapshot(new(_scope,consumer.Id),consumer.AdmittedIdentity!.ToInvocationIdentity(consumer.Id),_documentRevision,candidates,_expected);
    }
    internal void FreezeResult(AlgorithmNode node,ToolResults result)
    {
        var declaration=_declarations[node.Id];
        var values=new Dictionary<string,object?>(StringComparer.Ordinal);
        if(result.IsSuccess&&declaration.Source.Schema.Outputs.Count>0)
        {
            try
            {
                var actual=result.GetOutputVariables().Where(v=>v.Kind==OutputVariableKind.Data).ToDictionary(v=>v.Name,StringComparer.Ordinal);
                foreach(var port in declaration.Source.Schema.Outputs)
                {
                    var output=declaration.Outputs[port.Id];
                    if(!actual.TryGetValue(output.Name,out var value))throw new InvalidOperationException("The formal output is absent from this invocation: "+output.Name+" ("+port.Id+").");
                    // This identity labels the actual immutable output capture, not an inferred camera frame.
                    // Physical source provenance comes from this admitted invocation and its stable output port.
                    var frame = port.Type.Shape == ValueShape.Resource && port.Type.Resource == ResourceKind.Image
                        ? new ImageFrameAddress(Guid.NewGuid(), node.Id, node.AdmittedIdentity!.ToInvocationIdentity(node.Id),
                            port.Id, declaration.Source.SchemaRevision) : null;
                    var actualValue=result.TryGetDeclaredOutputValue(port.Id,out var canonical)?canonical:value.RuntimeValue;
                    values.Add(port.Id,BindingClrAdapter.NormalizeValue(port,actualValue,frame,result.RegisterOutputResource));
                }
            }
            catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
            {result.SetError("输出不符合冻结声明："+fault.Message);values.Clear();}
        }
        var fields=declaration.Outputs.ToDictionary(item=>item.Key,item=>new DeclaredOutputField(item.Value.Name,item.Value.DisplayName,
            item.Value.VariableType,item.Value.DeclaredType,item.Value.FieldId,item.Value.Group,item.Value.DisplayInResultPanel),StringComparer.Ordinal);
        DeclaredOutputSnapshot.Publish(result,node.AdmittedIdentity!.ToInvocationIdentity(node.Id),result.IsSuccess?values:null,fields,_scope,declaration.Complete);
    }
    internal DeclaredOutputSnapshot WithoutResult(AlgorithmNode node,ExecutionStatus status,IEnumerable<Diagnostic> diagnostics)
    {
        var declaration=_declarations[node.Id];
        var fields=declaration.Outputs.ToDictionary(item=>item.Key,item=>new DeclaredOutputField(item.Value.Name,item.Value.DisplayName,
            item.Value.VariableType,item.Value.DeclaredType,item.Value.FieldId,item.Value.Group,item.Value.DisplayInResultPanel),StringComparer.Ordinal);
        return DeclaredOutputSnapshot.WithoutValues(declaration.Source.Schema,declaration.Source.SchemaRevision,
            node.AdmittedIdentity!.ToInvocationIdentity(node.Id),status,diagnostics,fields,_scope,declaration.Complete);
    }
    public void Dispose()
    {foreach(var node in _nodes.Values)node.AdmittedIdentity?.CompleteCallbacks();}
}
