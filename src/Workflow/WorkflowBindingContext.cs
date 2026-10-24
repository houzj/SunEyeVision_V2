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
    private sealed record Declaration(BindingSource Source,IReadOnlyDictionary<string,OutputVariable> Outputs);
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
                    // An unsupported declaration is never inferred from a value. Its existing dedicated
                    // adapter remains responsible until its explicit canonical bridge is implemented.
                }
            }
            var schema=new ToolSchema(Array.Empty<Port>(),ports);
            result.Add(node.Id,new(new(new(scope,node.Id),executionScope,schema,0),new ReadOnlyDictionary<string,OutputVariable>(outputs)));
        }
        return result;
    }

    private static BindingCompilation? CompileSetting(WorkflowNodeBase node,ParamSetting setting,
        IReadOnlyDictionary<string,Declaration> declarations,BindingScopeSnapshot scope)
    {
        if(setting.BindingType!=BindingType.Binding)return null;
        var explicitReference=setting.ReferenceExpression is not null;
        var property=node.Parameters.GetType().GetProperty(setting.ParameterName);
        if(property is not {CanWrite:true} || !BindingClrAdapter.TryDescribe(property.PropertyType,out _,out _))
            return explicitReference?Unavailable("target-declaration","This input has no canonical writable-property declaration."):null;
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
            var target=BindingClrAdapter.DescribePort(setting.ParameterName,property.PropertyType);
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

    internal WorkflowBindingContext(WorkflowRunSession session,WorkflowRunScope scope,Workflow workflow,WorkflowExecutionDependencyGraph graph)
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
            foreach(var setting in node.ParamSettings.Settings.Where(s=>BindingInputCatalog.IsInputActive(node.Parameters,s.ParameterName)))
            {
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
                    if(!actual.TryGetValue(output.Name,out var value))throw new InvalidOperationException("The formal output is absent from this invocation.");
                    values.Add(port.Id,BindingClrAdapter.NormalizeValue(port,value.RuntimeValue));
                }
            }
            catch(Exception fault) when(fault is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
            {result.SetError("输出不符合冻结声明："+fault.Message);values.Clear();}
        }
        DeclaredOutputSnapshot.Publish(result,node.AdmittedIdentity!.ToInvocationIdentity(node.Id),result.IsSuccess?values:null);
    }
    public void Dispose()
    {foreach(var node in _nodes.Values)node.AdmittedIdentity?.CompleteCallbacks();}
}
