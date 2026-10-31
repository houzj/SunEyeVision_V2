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
    private readonly WorkflowRunSession _session;
    private readonly WorkflowRunScope _executionScope;
    private readonly IReadOnlyList<WorkflowGlobalProducerOperation> _globalProducers;
    private readonly WorkflowGlobalRunValues? _producedGlobals;


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
            var schema=new ToolSchema(Array.Empty<Port>(),ports,outputDisplay:ports.Select(port=>
            {
                var output=outputs[port.Id];
                var name=!string.IsNullOrWhiteSpace(output.DisplayName)?output.DisplayName:
                    !string.IsNullOrWhiteSpace(output.Name)?output.Name:port.Id;
                return new OutputDisplayNode(port.Id,name,output.DisplayMembers);
            }));
            result.Add(node.Id,new(new(new(scope,node.Id),executionScope,schema,0),new ReadOnlyDictionary<string,OutputVariable>(outputs),complete));
        }
        return result;
    }

    private static BindingCompilation? CompileSetting(WorkflowNodeBase node,ParamSetting setting,
        IReadOnlyDictionary<string,Declaration> declarations,BindingScopeSnapshot scope)
    {
        if(setting.BindingType!=BindingType.Binding)return null;
        if(node.RuntimeConfiguration is { } runtime)
        {
            if(!string.IsNullOrEmpty(setting.RebindingReason))return Unavailable("rebind-required",setting.RebindingReason);
            var target=runtime.BindingTargets.SingleOrDefault(port=>port.Id==setting.ParameterName);
            if(target is null)return Unavailable("target-declaration","Unknown Runtime input port.");
            if((target.EditModes&FieldEditModes.Reference)==0)return Unavailable("target-mode","Runtime input does not allow a reference.");
            if(setting.ReferenceExpression is null)return Unavailable("target-reference","Runtime input requires an explicit semantic reference.");
            try{return new ParameterBindingService().CompileBinding(BindingReferenceSerializer.Deserialize(setting.ReferenceExpression),target,scope,setting.Conversion);}
            catch(System.Text.Json.JsonException error){return Unavailable("reference",error.Message);}
        }
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
    internal static BindingValidation ValidateTyped(Workflow workflow,WorkflowNodeBase node,string fieldId,ReferenceSetting setting,string scopeId)
    {
        if(ReferenceOwnershipError(workflow,node,fieldId,setting,scopeId) is {} ownershipError)
            return new(BindingCompatibility.Incompatible,ownershipError);
        var declarations=CaptureDeclarations(workflow,scopeId,Guid.Empty);
        var scope=new BindingScopeSnapshot(new(scopeId,node.Id),workflow.ExecutionOwner.Id,0,
            declarations.Values.Select(value=>value.Source),declarations.Values.Select(value=>value.Source.Node),
            Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
        var target=node.RuntimeConfiguration!.BindingTargets.Single(port=>port.Id==fieldId);
        var compiled=new ParameterBindingService().CompileBinding(setting.Reference,target,scope,setting.Conversion);
        return new(compiled.Compatibility,compiled.Diagnostic?.Message??"适配");
    }

    internal static string? ReferenceOwnershipError(Workflow workflow,WorkflowNodeBase node,string fieldId,ReferenceSetting setting,string scopeId)
    {
        if(!string.IsNullOrEmpty(setting.RebindingReason))return setting.RebindingReason;
        if(setting.OriginId is null)return null;
        const string prefix="connection:";
        if(!setting.OriginId.StartsWith(prefix,StringComparison.Ordinal)||setting.OriginId.Length==prefix.Length)
            return "自动来源归属不是有效的连线标识。";
        var reference=setting.Reference;
        // Automatic connections may target one image nested in a declared record.
        // Type remains the root port declaration; only its formal member path is projected.
        TypeRef? selectedType=reference.Type;
        foreach(var member in reference.MemberFieldPath)
        {
            if(selectedType?.Shape!=ValueShape.Record){selectedType=null;break;}
            selectedType=selectedType.Fields.SingleOrDefault(field=>field.Id==member)?.Type;
        }
        if(node.RuntimeConfiguration?.Schema.WorkflowImageInputId!=fieldId
            ||selectedType!=TypeRef.ResourceOf(ResourceKind.Image)
            ||reference.Selection is not BindingSelection.AllItems||setting.Conversion!=BindingConversion.Automatic
            ||(reference.ScopeId??scopeId)!=scopeId)
            return "自动图像来源的字段、引用或工作流归属不一致。";
        var edges=workflow.Connections.Where(edge=>edge.Id==setting.OriginId[prefix.Length..]).ToArray();
        if(edges.Length!=1||edges[0].TargetNodeId!=node.Id||edges[0].SourceNodeId!=reference.NodeId
            ||!workflow.Nodes.Any(source=>source.Id==reference.NodeId))
            return "自动图像来源对应的连线不存在或不属于当前来源与目标节点。";
        return null;
    }

    internal static IEnumerable<BindingNodeId> ReferenceNodes(OutputReference reference,string defaultScope)
    {
        yield return new(reference.ScopeId??defaultScope,reference.NodeId);
        if(reference.Selection is BindingSelection.AtField index)
            foreach(var dependency in ReferenceNodes(index.Reference,defaultScope))yield return dependency;
    }

    internal WorkflowBindingContext(WorkflowRunSession session,WorkflowRunScope scope,Workflow workflow,WorkflowExecutionDependencyGraph graph,
        IReadOnlyList<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource>? variables=null,WorkflowRunRequest? request=null)
    {
        _scope=scope.WorkflowId;_session=session;_executionScope=scope;
        _globalProducers=Array.AsReadOnly((request?.GlobalProducers??Array.Empty<WorkflowGlobalProducerOperation>()).Where(item=>item.Source.ScopeId==_scope).ToArray());
        _producedGlobals=request?.ProducedGlobals;
        if(_globalProducers.Count>0&&_producedGlobals is null)
            throw new InvalidOperationException("已声明全局生产操作但缺少本批运行值槽。");
        _documentRevision=session.Configuration?.DocumentRevision??0;
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
            if(node.RuntimeConfiguration is {} runtime)
            {
                foreach(var (id,setting) in BindingRelationCatalog.References(node))
                {
                    if(ReferenceOwnershipError(workflow,node,id,setting,_scope) is {} ownershipError)
                    {errors.Add(new(id,id,ownershipError));continue;}
                    var target=runtime.BindingTargets.Single(port=>port.Id==id);
                    var compiled=new ParameterBindingService().CompileBinding(setting.Reference,target,compilationScope,setting.Conversion);
                    if(compiled.Plan is {} plan)plans.Add(id,plan);
                    else errors.Add(new(id,id,compiled.Diagnostic?.Message??"Runtime reference is not ready."));
                }
                if(errors.Count>0)additional.Add(node.Id,errors);
                node.CompiledBindings=new ReadOnlyDictionary<string,BindingPlan>(plans);
                continue;
            }
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
                if(BindingRelationCatalog.GetReference(consumer,input.Key) is not null)continue;
                if(BindingRelationCatalog.SourceWorkflowId(consumer,input,_scope)!=_scope)continue;
                if(BindingInputCatalog.TryReference(input.Read(),_nodes.Keys,input.ReferenceOnly,out var sourceId,out var path))
                    CollectDeclaredDemand(sourceId,path,input.ReadFieldId?.Invoke());
            }
        }
        // Published variables and subroutine returns are consumers too, even when no local
        // algorithm node reads this port. Only the admitted configuration/catalog supplies demand.
        foreach(var variable in variables??Array.Empty<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource>())
            if(variable.HasProducer&&(variable.ProducerWorkflowId==_scope||string.IsNullOrEmpty(variable.ProducerWorkflowId)))
                if(demands.TryGetValue(variable.ProducerNodeId,out var variableDemand)
                    &&_declarations.TryGetValue(variable.ProducerNodeId,out var variableSource)
                    &&variableSource.Outputs.ContainsKey(variable.ProducerField))
                    variableDemand.Add(new(variable.ProducerField));
        foreach(var caller in session.Configuration?.Definitions??Array.Empty<Workflow>())
            foreach(var consumer in caller.Nodes.Where(n=>n.IsEnabled))
                foreach(var input in BindingRelationCatalog.Describe(consumer).Where(i=>i.Active&&i.InternalProjection))
                    if(BindingRelationCatalog.SourceWorkflowId(consumer,input,caller.Id)==_scope
                        &&BindingInputCatalog.TryReference(input.Read(),_nodes.Keys,input.ReferenceOnly,out var sourceId,out var path))
                        CollectDeclaredDemand(sourceId,path,input.ReadFieldId?.Invoke());
        foreach(var producer in _globalProducers)
            if(demands.TryGetValue(producer.Source.NodeId,out var requested))requested.Add(new(producer.Source.PortId,producer.Source.MemberFieldPath));
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
            node.AdmittedIdentity=owner.Begin(_scope+"/"+node.Id,scope.RunId,FoundationContract.Major+"."+FoundationContract.Minor,session.CancellationToken,
                (node.Parameters as IExecutionAdmissionProvider)?.ExecutionAdmission,session.RunId);
            node.BindingContext=this;
            node.BindingOutputSchema=_declarations[node.Id].Source.Schema;
            expected.Add(new(_scope,node.Id),node.AdmittedIdentity.ToInvocationIdentity(node.Id));
        }
        _expected=new ReadOnlyDictionary<BindingNodeId,RunIdentity>(expected);
    }

    internal IBindingRunSnapshot Capture(AlgorithmNode consumer,IDictionary<string,DeclaredOutputSnapshot>? results)
    {
        var candidates=new List<BindingNodeResult>();
        foreach(var dependency in consumer.CompiledBindings.Values.SelectMany(p=>p.Dependencies).Distinct())
        {
            if(dependency.ScopeId!=_scope||results?.TryGetValue(dependency.NodeId,out var result)!=true||
                result.ExecutionIdentity is null||result.Schema is null||result is null)continue;
            try
            {
                using var reader=result.AcquireReader();
                if(reader.Values is not { } values)continue;
                candidates.Add(new(dependency,result.ExecutionIdentity.ToInvocationIdentity(dependency.NodeId),result.SchemaRevision,
                    result.Schema.Fingerprint,result.IsSuccess,values));
            }
            catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
            { /* A malformed source is unavailable; never fall back to old or inferred values. */ }
        }
        return new BindingRunSnapshot(new(_scope,consumer.Id),consumer.AdmittedIdentity!.ToInvocationIdentity(consumer.Id),_documentRevision,candidates,_expected);
    }
    internal Func<ToolOutcome,IDisposable,double,DeclaredOutputSnapshot> PrepareResult(AlgorithmNode node)
    {
        var declaration=_declarations[node.Id];
        var fields=declaration.Outputs.ToDictionary(item=>item.Key,item=>new DeclaredOutputField(item.Value.Name,item.Value.DisplayName,
            item.Value.VariableType,item.Value.DeclaredType,item.Value.FieldId,item.Value.Group,item.Value.DisplayInResultPanel),StringComparer.Ordinal);
        var identity=node.AdmittedIdentity!;var nodeId=node.Id;
        return (outcome,resources,elapsed)=>DeclaredOutputSnapshot.Capture(declaration.Source.Schema,declaration.Source.SchemaRevision,
            identity,nodeId,outcome,resources,elapsed,fields,_scope,declaration.Complete);
    }
    internal DeclaredOutputSnapshot WithoutResult(AlgorithmNode node,ExecutionStatus status,IEnumerable<Diagnostic> diagnostics)
    {
        var declaration=_declarations[node.Id];
        var fields=declaration.Outputs.ToDictionary(item=>item.Key,item=>new DeclaredOutputField(item.Value.Name,item.Value.DisplayName,
            item.Value.VariableType,item.Value.DeclaredType,item.Value.FieldId,item.Value.Group,item.Value.DisplayInResultPanel),StringComparer.Ordinal);
        return DeclaredOutputSnapshot.WithoutValues(declaration.Source.Schema,declaration.Source.SchemaRevision,
            node.AdmittedIdentity!.ToInvocationIdentity(node.Id),status,diagnostics,fields,_scope,declaration.Complete,node.AdmittedIdentity);
    }
    internal void CompleteGlobalProducers(string completedNode,WorkflowContext context)
    {
        if(_producedGlobals is null)return;
        var operations=_globalProducers.Where(item=>item.Source.NodeId==completedNode).ToArray();
        if(operations.Length==0)return;
        var values=ResolveGlobalProducers(_session,_executionScope,operations);
        _producedGlobals.Publish(values,_session.CancellationToken);
        // Replace a pure-value snapshot. Already running consumers retain their own generation.
        context.GlobalVariables=new Dictionary<string,object?>(_producedGlobals.Capture(),StringComparer.Ordinal);
    }
    internal IReadOnlyList<WorkflowGlobalProducedValue> ResolveGlobalProducers(WorkflowRunSession session,WorkflowRunScope scope,
        IReadOnlyList<WorkflowGlobalProducerOperation> operations)
    {
        var output=new List<WorkflowGlobalProducedValue>();
        var owner=session.ExecutionOwner??throw new InvalidOperationException("Execution owner is not active.");
        foreach(var operation in operations.Where(item=>item.Source.ScopeId==_scope))
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            if(_documentRevision<=0)throw new InvalidOperationException("Global production requires the captured published configuration revision.");
            if(_nodes.ContainsKey(operation.OperationId))throw new InvalidOperationException("Global operation identity conflicts with a workflow node.");
            var consumer=new BindingNodeId(_scope,operation.OperationId);
            var admission=owner.Begin(_scope+"/"+operation.OperationId,scope.RunId,FoundationContract.Major+"."+FoundationContract.Minor,session.CancellationToken,rootRunId:session.RunId);
            try
            {
                using var executing=owner.EnterRun(admission);
                var compilationScope=new BindingScopeSnapshot(consumer,owner.Id,_documentRevision,
                    _declarations.Values.Select(d=>d.Source),_declarations.Values.Select(d=>d.Source.Node),
                    Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
                var service=new ParameterBindingService();
                var compiled=service.CompileBinding(operation.Source,operation.Target,compilationScope);
                if(compiled.Plan is null)
                {output.Add(new(operation.VariableId,operation.VariableName,null,compiled.Diagnostic??new("global-binding","Producer declaration is unavailable.")));continue;}
                var sourceId=new BindingNodeId(_scope,operation.Source.NodeId);
                var candidates=new List<BindingNodeResult>();
                using var reader=_nodes.GetValueOrDefault(operation.Source.NodeId)?.LastToolResult?.AcquireReader();
                if(reader?.Values is {} values)
                {
                    var snapshot=reader.Snapshot;
                    candidates.Add(new(sourceId,snapshot.Identity,snapshot.SchemaRevision,snapshot.Schema.Fingerprint,
                        snapshot.Status is ExecutionStatus.Success or ExecutionStatus.PartialSuccess,values));
                }
                var run=new BindingRunSnapshot(consumer,admission.ToInvocationIdentity(operation.OperationId),_documentRevision,candidates,_expected);
                var resolved=service.ResolveBinding(compiled.Plan,run);
                if(!resolved.Success)
                {output.Add(new(operation.VariableId,operation.VariableName,null,resolved.Diagnostic??new("global-value","Producer value is unavailable.")));continue;}
                var value=BindingClrAdapter.Materialize(operation.Target,operation.ValueType,resolved.Value);
                session.CancellationToken.ThrowIfCancellationRequested();
                output.Add(new(operation.VariableId,operation.VariableName,value,null));
            }
            catch(Exception error) when(error is ArgumentException or InvalidOperationException or OverflowException)
            {output.Add(new(operation.VariableId,operation.VariableName,null,new("global-production",error.Message)));}
            finally{admission.CompleteCallbacks();}
        }
        return output.AsReadOnly();
    }
    public void Dispose()
    {foreach(var node in _nodes.Values)node.AdmittedIdentity?.CompleteCallbacks();}
}
