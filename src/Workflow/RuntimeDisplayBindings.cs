using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>Display references are managed with configuration but compiled in front/self presentation scope.</summary>
internal static class RuntimeDisplayBindings
{
    internal static IEnumerable<(string Key,ReferenceSetting Setting)> References(RuntimeToolConfiguration runtime)
    {
        foreach(var (id,setting) in runtime.Fields.Capture())
        {
            if(setting is ReferenceSetting reference)yield return(id,reference);
            if(setting is DisplayExpressionSetting expression)
                for(var i=0;i<expression.Slots.Count;i++)if(expression.Slots[i].Reference is {} source)
                    yield return(SlotKey(id,i),new(source));
        }
    }
    internal static string SlotKey(string field,int index)=>"display-slot:"+field.Length+":"+field+":"+index;
    internal static void Validate(Workflow workflow,WorkflowNodeBase node,RuntimeToolConfiguration runtime,IReadOnlySet<string>? proposedFields=null)
    {
        if(runtime.DisplaySettingsSchema.Inputs.Count==0)return;
        var graph=WorkflowExecutionDependencyGraph.Compile(workflow);var eligible=new HashSet<string>(StringComparer.Ordinal){node.Id};
        var pending=new Stack<string>(graph.GetPredecessors(node.Id));while(pending.TryPop(out var id))if(eligible.Add(id))foreach(var parent in graph.GetPredecessors(id))pending.Push(parent);
        var declarations=workflow.Nodes.Where(source=>eligible.Contains(source.Id)).Select(source=>new BindingSource(new(workflow.Id,source.Id),Guid.Empty,
            new ToolSchema(Array.Empty<Port>(),(new OutputBindingChangeService().ReadContract(source)??Array.Empty<SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable>())
                .Where(output=>output.Kind==SunEyeVision.Plugin.SDK.Execution.Results.OutputVariableKind.Data).Select(BindingClrAdapter.DescribeOutput)),0)).ToArray();
        var scope=new ResultDisplayBindingScope(new(workflow.Id,node.Id),Guid.Empty,0,declarations);var compiler=new ParameterBindingService();
        foreach(var (id,setting) in runtime.Fields.Capture().Where(pair=>runtime.IsDisplayField(pair.Key)&&(proposedFields is null||proposedFields.Contains(pair.Key))))
        {
            var references=setting switch {ReferenceSetting reference=>new[]{reference.Reference},DisplayExpressionSetting expression=>expression.Slots.Where(slot=>slot.Reference is not null).Select(slot=>slot.Reference!),_=>Array.Empty<OutputReference>()};
            foreach(var reference in references)
            {
                var type=ParameterReferenceRules.SelectedType(reference);while(type.Shape==ValueShape.Sequence)type=type.Element!;
                var target=new Port(id,TypeRef.SequenceOf(type),Nullable:true);
                var result=compiler.CompileBinding(reference,target,scope);
                if(result.Plan is null)throw new ArgumentException(result.Diagnostic?.Message??"显示来源无效。");
            }
        }
    }
}
