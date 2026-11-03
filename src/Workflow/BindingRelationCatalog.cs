using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;



public static class BindingRelationCatalog
{
    internal static IEnumerable<VariableReference> IndexVariables(BindingSelection selection)
    {
        if(selection is BindingSelection.AtVariable variable)yield return variable.Reference;
        if(selection is BindingSelection.AtField field)
            foreach(var index in IndexVariables(field.Reference.Selection))yield return index;
    }
    internal static IEnumerable<(string FieldId,VariableReference Reference)> VariableReferences(WorkflowNodeBase node,bool executionOnly=false)
    {
        if(node.RuntimeConfiguration is not {} runtime)yield break;
        foreach(var (fieldId,setting) in runtime.Fields.Capture())
        {
            if(executionOnly&&!runtime.IsExecutionFieldActive(fieldId))continue;
            if(setting is VariableSetting variable)yield return (fieldId,variable.Reference);
            var selection=setting switch{VariableSetting value=>value.Reference.Selection,ReferenceSetting value=>value.Reference.Selection,_=>null};
            if(selection is not null)foreach(var index in IndexVariables(selection))yield return (fieldId,index);
        }
    }
    public static ReferenceSetting? GetReference(WorkflowNodeBase node,string key)
        =>RuntimeDisplayBindings.References(node.RuntimeConfiguration??throw new InvalidOperationException("节点缺少 Runtime 配置。"))
            .FirstOrDefault(entry=>entry.Key==key).Setting;
    public static IReadOnlyDictionary<string,ReferenceSetting> References(WorkflowNodeBase node,bool executionOnly=false)
    {
        var runtime=node.RuntimeConfiguration??throw new InvalidOperationException("节点缺少 Runtime 配置。");
        return RuntimeDisplayBindings.References(runtime).Where(pair=>!executionOnly||runtime.IsExecutionFieldActive(pair.Key))
            .ToDictionary(pair=>pair.Key,pair=>pair.Setting,StringComparer.Ordinal);
    }
}

