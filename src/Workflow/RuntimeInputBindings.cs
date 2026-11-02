using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

internal static class RuntimeInputBindings
{
    internal static object? Constant(Port port,object? value)=>ValueRules.Freeze(port,value);
    internal static IReadOnlyList<ExecutionInputIssue> Validate(Workflow flow,WorkflowNodeBase node,string scope,bool validateAdmission=true)
    {
        var configuration=node.RuntimeConfiguration!;var issues=new List<ExecutionInputIssue>();
        if(node.ParamSettings.Settings.Any())issues.Add(new("runtime-configuration","Runtime","Runtime nodes cannot contain SDK ParamSettings."));
        // Persistence permits missing execution inputs in a valid editable draft, but
        // still rejects invalid known settings and malformed stored identities.
        foreach(var diagnostic in validateAdmission?configuration.Validate():configuration.ValidateSettings())
            issues.Add(new(diagnostic.FieldId??"runtime-settings",diagnostic.FieldId??"Runtime",diagnostic.Message));
        foreach(var (id,setting) in configuration.CaptureExecutionFields())
        {
            try
            {
                if(setting is ReferenceSetting reference)
                {
                    if(!string.IsNullOrEmpty(reference.RebindingReason))throw new ArgumentException(reference.RebindingReason);
                    // Variable-index readiness needs the owning solution's frozen declarations.
                    // The document host and real invocation compiler perform that complete check.
                    if(BindingRelationCatalog.IndexVariables(reference.Reference.Selection).Any())continue;
                    var validation=WorkflowBindingContext.ValidateTyped(flow,node,id,reference,scope);
                    if(validation.State is BindingCompatibility.Incompatible or BindingCompatibility.Unresolved)throw new ArgumentException(validation.Reason);
                }
                else if(setting is VariableSetting {RebindingReason:{Length:>0} reason})throw new ArgumentException(reason);
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or JsonException or FormatException or OverflowException)
            {issues.Add(new(id,id,error.Message));}
        }
        return issues;
    }
    internal static IReadOnlyList<BindingInputReference> Describe(WorkflowNodeBase node)
        =>node.RuntimeConfiguration!.Fields.Capture().Where(pair=>pair.Value is not DisplayExpressionSetting).Select(pair=>new BindingInputReference(pair.Key,pair.Key,
            ()=>pair.Value is ReferenceSetting reference?reference.Reference.NodeId+"."+reference.Reference.PortId:"",
            text=>
            {
                if(text.Length!=0)throw new InvalidOperationException("Use an explicit Runtime reference.");
                node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{[pair.Key]=new UnsetSetting()});
            },Array.Empty<Type>(),ReadWorkflowId:()=>pair.Value is ReferenceSetting reference?reference.Reference.ScopeId:null,
            ReferenceOnly:pair.Value is ReferenceSetting)).Concat(RuntimeDisplayBindings.References(node.RuntimeConfiguration!)
                .Where(pair=>!node.RuntimeConfiguration!.Fields.Declarations.ContainsKey(pair.Key)).Select(pair=>new BindingInputReference(pair.Key,pair.Key,
                    ()=>pair.Setting.Reference.NodeId+"."+pair.Setting.Reference.PortId,
                    _=>throw new InvalidOperationException("请通过原文本编辑器修复显示映射；不会静默删除其他映射。"),
                    Array.Empty<Type>(),ReadWorkflowId:()=>pair.Setting.Reference.ScopeId,ReferenceOnly:true))).ToArray();
}
