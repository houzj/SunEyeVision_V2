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
    private sealed record Declaration(BindingSource Source,IReadOnlyDictionary<string,Port> Outputs,IReadOnlyDictionary<string,DeclaredOutputField> Fields,bool Complete);
    private readonly Dictionary<string,Declaration> _declarations;
    private readonly Dictionary<string,AlgorithmNode> _nodes;
    private readonly IReadOnlyDictionary<BindingNodeId,RunIdentity> _expected;
    private readonly string _scope;
    private readonly IReadOnlyDictionary<string,IReadOnlyCollection<string>> _displayPredecessors;
    private readonly RuntimeParameterValueAcceptance? _parameterValues;
    private readonly long _documentRevision;
    private readonly WorkflowRunSession _session;
    private readonly WorkflowRunScope _executionScope;
    private readonly IReadOnlyList<WorkflowGlobalProducerOperation> _globalProducers;
    private readonly WorkflowGlobalRunValues? _producedGlobals;
    private readonly Dictionary<string,IReadOnlyDictionary<string,RuntimeVariableBindingPlan>> _variablePlans=new(StringComparer.Ordinal);


    private static Dictionary<string,Declaration> CaptureDeclarations(Workflow workflow,string scope,Guid executionScope)
    {
        var result=new Dictionary<string,Declaration>(StringComparer.Ordinal);
        foreach(var node in workflow.Nodes)
        {
            var declaration=new OutputBindingChangeService().ReadContract(node)??throw new InvalidOperationException("节点缺少已准入输出声明："+node.Id);
            var schema=new ToolSchema(Array.Empty<Port>(),declaration.Outputs,outputDisplay:declaration.OutputDisplay);
            var outputs=new ReadOnlyDictionary<string,Port>(schema.Outputs.ToDictionary(port=>port.Id,StringComparer.Ordinal));
            var projection=node.RuntimeConfiguration!.ResultProjection;
            var shown=projection?.Kind==SunEyeVision.Plugin.Contracts.Results.Presentation.ResultProjectionKind.PropertyList
                ?projection.Columns.Where(column=>column.Value is not null).Select(column=>column.Value!.PortId).ToHashSet(StringComparer.Ordinal):null;
            var fields=new ReadOnlyDictionary<string,DeclaredOutputField>(schema.Outputs.ToDictionary(port=>port.Id,
                port=>new DeclaredOutputField(port.Id,schema.FindOutputDisplay(port.Id)?.DisplayName??port.Id,"",null,null,null,shown is null||shown.Contains(port.Id)),StringComparer.Ordinal));
            result.Add(node.Id,new(new(new(scope,node.Id),executionScope,schema,0),outputs,fields,true));
        }
        return result;
    }
    internal static BindingValidation ValidateTyped(Workflow workflow,WorkflowNodeBase node,string fieldId,ReferenceSetting setting,string scopeId,Solution? solution=null)
    {
        if(ReferenceOwnershipError(workflow,node,fieldId,setting,scopeId) is {} ownershipError)
            return new(BindingCompatibility.Incompatible,ownershipError);
        var declarations=CaptureDeclarations(workflow,scopeId,Guid.Empty);
        using var variables=solution is null?null:new WorkflowGlobalRunValues(solution.Id,solution.GlobalVariables,Array.Empty<KeyValuePair<string,object?>>());
        var scope=new BindingScopeSnapshot(new(scopeId,node.Id),workflow.ExecutionOwner.Id,0,
            declarations.Values.Select(value=>value.Source),declarations.Values.Select(value=>value.Source.Node),
            Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>(),variables?.CaptureBindingSources());
        if(node.RuntimeConfiguration!.SettingsSchema.Inputs.Any(port=>port.Id==fieldId && OutputBindingChangeService.IsOrdinaryNumericParameter(port)))
            scope=scope.WithReferenceQualification(new ManualReferenceQualification(workflow,scopeId,node.Id).Validate);
        var target=node.RuntimeConfiguration!.BindingTargets.Single(port=>port.Id==fieldId);
        var compiled=new ParameterBindingService().CompileBinding(setting.Reference,target,scope,setting.Conversion,node.RuntimeConfiguration!.GetParameterReferenceBehavior(fieldId));
        return new(compiled.Compatibility,compiled.Diagnostic?.Message??"适配");
    }

    internal static void ValidateVariable(Solution solution,Workflow workflow,WorkflowNodeBase node,string fieldId,VariableSetting setting)
    {
        using var globals=new WorkflowGlobalRunValues(solution.Id,solution.GlobalVariables,Array.Empty<KeyValuePair<string,object?>>());
        var declarations=CaptureDeclarations(workflow,workflow.Id,Guid.Empty);
        var scope=new BindingScopeSnapshot(new(workflow.Id,node.Id),workflow.ExecutionOwner.Id,0,
            declarations.Values.Select(value=>value.Source),declarations.Values.Select(value=>value.Source.Node),
            Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>(),globals.CaptureBindingSources());
        if(node.RuntimeConfiguration!.SettingsSchema.Inputs.Any(port=>port.Id==fieldId && OutputBindingChangeService.IsOrdinaryNumericParameter(port)))
            scope=scope.WithReferenceQualification(new ManualReferenceQualification(workflow,workflow.Id,node.Id).Validate);
        _=RuntimeVariableBindings.Compile(setting,node.RuntimeConfiguration!.BindingTargets.Single(port=>port.Id==fieldId),globals,scope,node.RuntimeConfiguration.GetParameterReferenceBehavior(fieldId));
    }
    internal static string? ReferenceOwnershipError(Workflow workflow,WorkflowNodeBase node,string fieldId,ReferenceSetting setting,string scopeId)
    {
        if(!string.IsNullOrEmpty(setting.RebindingReason))return setting.RebindingReason;
        if(setting.OriginId is null)return null;
        const string prefix="connection:";
        if(!setting.OriginId.StartsWith(prefix,StringComparison.Ordinal)||setting.OriginId.Length==prefix.Length)
            return "自动来源归属不是有效的连线标识。";
        var reference=setting.Reference;
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

    internal void AttachDisplaySources(string nodeId,DeclaredOutputSnapshot result,IReadOnlyList<DeclaredOutputSnapshot> outputs)
    {
        if(!_nodes.TryGetValue(nodeId,out var node)||node.RuntimeConfiguration?.ResultDisplay is null||!result.IsSuccess)return;
        var eligible=_displayPredecessors[nodeId];
        var sources=outputs.Where(source=>source.SourceWorkflowId==_scope&&eligible.Contains(source.Identity.NodeId)
            &&_expected.TryGetValue(new(_scope,source.Identity.NodeId),out var expected)&&source.Identity==expected).ToArray();
        using var set=new ResultDisplaySourceSet(sources);
        result.AttachDisplaySources(set);
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
        var displayPredecessors=new Dictionary<string,IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach(var target in workflow.Nodes)
        {
            var found=new HashSet<string>(StringComparer.Ordinal);var pending=new Stack<string>(graph.GetPredecessors(target.Id));
            while(pending.TryPop(out var id))if(found.Add(id))foreach(var parent in graph.GetPredecessors(id))pending.Push(parent);
            if(found.Contains(target.Id))throw new InvalidOperationException("Display source graph contains a cycle.");
            displayPredecessors.Add(target.Id,Array.AsReadOnly(found.ToArray()));
        }
        _displayPredecessors=displayPredecessors;
        _parameterValues=request?.ParameterValues;
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
                _declarations.Values.Select(d=>d.Source),_declarations.Values.Select(d=>d.Source.Node),predecessors,_producedGlobals?.CaptureBindingSources());
            var plans=new Dictionary<string,BindingPlan>(StringComparer.Ordinal);
            var errors=new List<ExecutionInputIssue>();
            if(node.RuntimeConfiguration is {} runtime)
            {
                var manualScope=compilationScope.WithReferenceQualification(new ManualReferenceQualification(workflow,_scope,node.Id).Validate);
                var settingsIds=runtime.SettingsSchema.Inputs.Where(OutputBindingChangeService.IsOrdinaryNumericParameter).Select(port=>port.Id).ToHashSet(StringComparer.Ordinal);
                var variablePlans=new Dictionary<string,RuntimeVariableBindingPlan>(StringComparer.Ordinal);
                foreach(var (id,setting) in runtime.CaptureExecutionFields().Where(pair=>pair.Value is VariableSetting))
                {
                    try
                    {
                        var variable=RuntimeVariableBindings.Compile((VariableSetting)setting,runtime.BindingTargets.Single(port=>port.Id==id),_producedGlobals,settingsIds.Contains(id)?manualScope:compilationScope,runtime.GetParameterReferenceBehavior(id));
                        variablePlans.Add(id,variable);
                        if(variable.Index is {} index)plans.Add(id,index);
                    }
                    catch(Exception failure)when(failure is ArgumentException or InvalidOperationException)
                    {errors.Add(new(id,id,failure.Message));}
                }
                _variablePlans.Add(node.Id,new ReadOnlyDictionary<string,RuntimeVariableBindingPlan>(variablePlans));
                foreach(var (id,setting) in BindingRelationCatalog.References(node,executionOnly:true))
                {
                    if(ReferenceOwnershipError(workflow,node,id,setting,_scope) is {} ownershipError)
                    {errors.Add(new(id,id,ownershipError));continue;}
                    var target=runtime.BindingTargets.Single(port=>port.Id==id);
                    var compiled=new ParameterBindingService().CompileBinding(setting.Reference,target,settingsIds.Contains(id)?manualScope:compilationScope,setting.Conversion,runtime.GetParameterReferenceBehavior(id));
                    if(compiled.Plan is {} plan)plans.Add(id,plan);
                    else errors.Add(new(id,id,compiled.Diagnostic?.Message??"Runtime reference is not ready."));
                }
                if(errors.Count>0)additional.Add(node.Id,errors);
                node.CompiledBindings=new ReadOnlyDictionary<string,BindingPlan>(plans);
                continue;
            }
            throw new InvalidOperationException("节点缺少已准入 Runtime 配置。");
        }
        graph.AddInputIssues(additional);
        // Freeze demand from this scope's compiled consumers before any producer starts.
        // Include dynamic-index sources; never infer demand from mutable last results or UI visibility.
        var demands=_nodes.Keys.ToDictionary(id=>id,_=>new List<OutputValueRequest>(),StringComparer.Ordinal);
        foreach(var consumer in _nodes.Values.Where(n=>n.IsEnabled))
        {
            foreach(var plan in consumer.CompiledBindings.Values)CollectDemand(plan.Source);
        }
        // Published variables and subroutine returns are consumers too, even when no local
        // algorithm node reads this port. Only the admitted configuration/catalog supplies demand.
        foreach(var variable in variables??Array.Empty<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource>())
            if(variable.HasProducer&&(variable.ProducerWorkflowId==_scope||string.IsNullOrEmpty(variable.ProducerWorkflowId)))
                if(demands.TryGetValue(variable.ProducerNodeId,out var variableDemand)
                    &&_declarations.TryGetValue(variable.ProducerNodeId,out var variableSource)
                    &&variableSource.Outputs.ContainsKey(variable.ProducerField))
                    variableDemand.Add(new(variable.ProducerField));
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
        var expected=new Dictionary<BindingNodeId,RunIdentity>();
        foreach(var node in _nodes.Values)
        {
            node.AdmittedIdentity=owner.Begin(_scope+"/"+node.Id,scope.RunId,FoundationContract.Major+"."+FoundationContract.Minor,session.CancellationToken,
                rootRunId:session.RunId);
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
        var identity=consumer.AdmittedIdentity!.ToInvocationIdentity(consumer.Id);
        return new BindingRunSnapshot(new(_scope,consumer.Id),identity,_documentRevision,candidates,_expected,_producedGlobals?.CaptureBindingValues(identity));
    }
    internal BindingResolution ResolveVariable(AlgorithmNode consumer,string fieldId,IBindingRunSnapshot snapshot)
    {
        if(_producedGlobals is null||!_variablePlans.TryGetValue(consumer.Id,out var fields)||!fields.TryGetValue(fieldId,out var plan))
            return new(false,null,new("variable-unplanned","Runtime变量字段没有本批已编译计划。",fieldId));
        return RuntimeVariableBindings.Resolve(plan,_producedGlobals,snapshot);
    }
    internal Func<ToolOutcome,IDisposable,double,DeclaredOutputSnapshot> PrepareResult(AlgorithmNode node)
    {
        var declaration=_declarations[node.Id];
        var fields=declaration.Fields;
        var identity=node.AdmittedIdentity!;var nodeId=node.Id;
        return (outcome,resources,elapsed)=>DeclaredOutputSnapshot.Capture(declaration.Source.Schema,declaration.Source.SchemaRevision,
            identity,nodeId,outcome,resources,elapsed,fields,_scope,declaration.Complete);
    }
    internal DeclaredOutputSnapshot WithoutResult(AlgorithmNode node,ExecutionStatus status,IEnumerable<Diagnostic> diagnostics)
    {
        var declaration=_declarations[node.Id];
        var fields=declaration.Fields;
        return DeclaredOutputSnapshot.WithoutValues(declaration.Source.Schema,declaration.Source.SchemaRevision,
            node.AdmittedIdentity!.ToInvocationIdentity(node.Id),status,diagnostics,fields,_scope,declaration.Complete,node.AdmittedIdentity);
    }
    internal void AcceptParameterValue(AlgorithmNode node,RuntimeToolConfiguration configuration,string fieldId,object? value)
    {
        var identity=node.AdmittedIdentity??throw new InvalidOperationException("参数接纳缺少原始执行身份。");
        (_parameterValues??throw new InvalidOperationException("引用参数缺少正式宿主的当前值接纳入口。"))
            .Accept(_scope,node.Id,identity,configuration,fieldId,value);
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
