using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;

public sealed record ScopedNodeIdentity(string WorkflowId, string NodeId);
public sealed record ScopedBindingRelation(ScopedNodeIdentity Target, WorkflowNodeBase Node, BindingInputReference Input);

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
    /// <summary>Typed settings live in their owning model slot; a name projection must not hide an ROI reference.</summary>
    public static ParamSetting? GetSetting(WorkflowNodeBase node,string key)
        =>node.RuntimeConfiguration is not null?null:node.ParamSettings.GetSetting(key)??RoiBindingInputs.Settings(node.Parameters).SingleOrDefault(setting=>setting.ParameterName==key);
    public static IReadOnlyList<ParamSetting> Settings(WorkflowNodeBase node)
        =>node.RuntimeConfiguration is not null?Array.Empty<ParamSetting>():node.ParamSettings.Settings.Concat(RoiBindingInputs.Settings(node.Parameters)).ToArray();
    public static ReferenceSetting? GetReference(WorkflowNodeBase node,string key)
    {
        if(node.RuntimeConfiguration is {} runtime)return RuntimeDisplayBindings.References(runtime).FirstOrDefault(entry=>entry.Key==key).Setting;
        var setting=GetSetting(node,key);
        return setting?.ReferenceExpression is {} expression?new(BindingReferenceSerializer.Deserialize(expression),setting.Conversion):null;
    }
    public static IReadOnlyDictionary<string,ReferenceSetting> References(WorkflowNodeBase node,bool executionOnly=false)
        =>node.RuntimeConfiguration is {} runtime
            ?RuntimeDisplayBindings.References(runtime).Where(pair=>!executionOnly||runtime.IsExecutionFieldActive(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Setting,StringComparer.Ordinal)
            :Settings(node).Where(setting=>setting.BindingType==BindingType.Binding&&setting.ReferenceExpression is not null&&BindingInputCatalog.IsInputActive(node.Parameters,setting.ParameterName))
                .ToDictionary(setting=>setting.ParameterName,setting=>new ReferenceSetting(BindingReferenceSerializer.Deserialize(setting.ReferenceExpression!),setting.Conversion),StringComparer.Ordinal);
    /// <summary>Execution-only view. The unfiltered Describe remains the document's reference-management inventory.</summary>
    internal static IReadOnlyList<BindingInputReference> DescribeExecution(WorkflowNodeBase node)
        =>Describe(node).Where(input=>input.Active&&(node.RuntimeConfiguration is not {} runtime||runtime.IsExecutionFieldActive(input.Key))).ToArray();
    public static IReadOnlyList<BindingInputReference> Describe(WorkflowNodeBase node)
    {
        if(node.RuntimeConfiguration is not null)return RuntimeInputBindings.Describe(node);
        var inputs = BindingInputCatalog.Describe(node.Parameters).ToList();
        foreach (var setting in node.ParamSettings.Settings.ToArray())
        {
            var dedicated = setting.ParameterName == "位置修正" ? inputs.FirstOrDefault(i => i.Key == "PositionCorrectionBindingSource") : null;
            // Row editors also persist a name-keyed ParamSettings projection. A script/subroutine
            // input is not a top-level CLR property: use its declared row contract, not object.
            if (dedicated == null && node.Parameters.GetType().GetProperty(setting.ParameterName) == null)
            {
                var rows = inputs.Where(i => i.Key.StartsWith("Input:", StringComparison.Ordinal) && i.Name == setting.ParameterName).ToArray();
                if (rows.Length == 1) dedicated = rows[0];
            }
            var type = node.Parameters.GetType().GetProperty(setting.ParameterName)?.PropertyType ?? setting.TargetType;
            var slot = (node.Parameters as IInputSlotContract)?.GetInputSlots().FirstOrDefault(s => s.PropertyName == setting.ParameterName);
            var parameter = (node.Parameters as IRuntimeParameterContract)?.GetRuntimeParameters().FirstOrDefault(s => s.PropertyName == setting.ParameterName);
            type = slot?.ValueType ?? parameter?.ValueType ?? type ?? SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToSystemType(slot?.TypeVocabulary ?? parameter?.TypeVocabulary ?? "");
            if (type?.IsGenericType == true && type.GetGenericTypeDefinition() == typeof(ParamValue<>)) type = type.GetGenericArguments()[0];
            // ParamSettings is the authority when the editor persists the same property there.
            inputs.RemoveAll(i => i.Key == setting.ParameterName);
            inputs.Add(new(setting.ParameterName, setting.ParameterName,
                () => setting.BindingType == BindingType.Binding ? setting.SourceNodeId + "." + setting.SourceProperty : "",
                text => { if (text.Length == 0) node.ParamSettings.RemoveSetting(setting.ParameterName); else { var dot = text.IndexOf('.'); setting.SourceNodeId = text[..dot]; setting.SourceProperty = text[(dot + 1)..]; node.ParamSettings.SetSetting(setting); } },
                dedicated?.AllowedTypes ?? (type == null ? Array.Empty<Type>() : new[] { type }),
                ValidateLiteral: setting.BindingType != BindingType.Constant ? null : _ =>
                {
                    if (type == null) return "参数类型未声明";
                    var resolved = new ParameterResolver().Resolve(setting, new Dictionary<string, SunEyeVision.Plugin.SDK.Execution.Results.ToolResults>(), type);
                    return resolved.IsSuccess && resolved.Value != null ? null : resolved.ErrorMessage ?? "常量类型不适配";
                }, ReadWorkflowId: () => setting.SourceWorkflowId, ReferenceOnly: setting.BindingType == BindingType.Binding,
                Active: (dedicated?.Active ?? true) && BindingInputCatalog.IsInputActive(node.Parameters, setting.ParameterName), ReadFieldId: () => setting.SourceFieldId));
        }
        return inputs;
    }

    public static IReadOnlyList<ScopedBindingRelation> Describe(Solution solution) => solution.Workflows.SelectMany(w =>
        w.Nodes.SelectMany(n => Describe(n).Select(i => new ScopedBindingRelation(new(w.Id, n.Id), n, i)))).ToArray();

    public static string SourceWorkflowId(WorkflowNodeBase node, BindingInputReference input, string consumerWorkflowId)
    {
        var explicitScope = input.ReadWorkflowId?.Invoke();
        if (!string.IsNullOrEmpty(explicitScope)) return explicitScope;
        return node.Parameters.BindingSourceIdentities.TryGetValue(input.Key, out var identity) && identity.Expression == input.Read() && !identity.SourceNodeId.StartsWith("__", StringComparison.Ordinal)
            ? identity.WorkflowId : consumerWorkflowId;
    }
}
