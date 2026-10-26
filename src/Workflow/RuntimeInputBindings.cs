using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>Runtime inputs use declared stable port IDs and semantic references.
/// No SDK writable-property inference or tool-specific parameter conversion.</summary>
internal static class RuntimeInputBindings
{
    internal static object? Constant(Port port,object? value)
    {
        if(value is JsonElement json)
        {
            value=json.ValueKind==JsonValueKind.Null?null:port.Type.Scalar switch
            {
                ScalarKind.Int64=>json.GetInt64(),ScalarKind.Float64=>json.GetDouble(),
                ScalarKind.Boolean=>json.GetBoolean(),ScalarKind.Text=>json.GetString(),
                _=>throw new ArgumentException("This Runtime constant requires an explicit structured-value editor.")
            };
        }
        return ValueRules.Freeze(port,value);
    }
    internal static IReadOnlyList<ExecutionInputIssue> Validate(Workflow flow,WorkflowNodeBase node,string scope)
    {
        var ports=node.RuntimeConfiguration!.Schema.Inputs.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        var issues=new List<ExecutionInputIssue>();
        foreach(var setting in node.ParamSettings.Settings)
        {
            try
            {
                if(!ports.TryGetValue(setting.ParameterName,out var port))throw new ArgumentException("Unknown Runtime input port.");
                if(setting.BindingType==BindingType.Constant)
                {
                    if((port.EditModes&FieldEditModes.Constant)==0)throw new ArgumentException("This input does not allow a persisted constant.");
                    Constant(port,setting.ConstantValue);
                }
                else if(setting.BindingType==BindingType.Binding&&setting.ReferenceExpression is not null)
                {
                    var validation=WorkflowBindingContext.ValidateTyped(flow,node,setting,scope);
                    if(validation.State is BindingCompatibility.Incompatible or BindingCompatibility.Unresolved)
                        throw new ArgumentException(validation.Reason);
                }
                else throw new ArgumentException("Runtime input requires an explicit semantic reference.");
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or JsonException or FormatException or OverflowException)
            {issues.Add(new(setting.ParameterName,setting.ParameterName,error.Message));}
        }
        return issues;
    }
    internal static IReadOnlyList<BindingInputReference> Describe(WorkflowNodeBase node)=>node.ParamSettings.Settings.Select(setting=>
        new BindingInputReference(setting.ParameterName,setting.ParameterName,()=>
        {
            if(setting.ReferenceExpression is null)return "";
            try{var reference=BindingReferenceSerializer.Deserialize(setting.ReferenceExpression);return reference.NodeId+"."+reference.PortId;}
            catch(JsonException){return "";}
        },text=>
        {
            if(text.Length!=0)throw new InvalidOperationException("Use an explicit Runtime semantic reference.");
            node.ParamSettings.RemoveSetting(setting.ParameterName);
        },Array.Empty<Type>(),ReadWorkflowId:()=>setting.SourceWorkflowId,ReferenceOnly:setting.BindingType==BindingType.Binding)).ToArray();
}
