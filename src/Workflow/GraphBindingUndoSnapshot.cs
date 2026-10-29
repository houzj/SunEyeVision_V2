using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Workflow;
/// <summary>画布图编辑历史使用的绑定配置快照；不回放全局值或运行结果。</summary>
public sealed class GraphBindingUndoSnapshot
{
    public WorkflowNodeBase[] Nodes {get;private init;}=Array.Empty<WorkflowNodeBase>();
    private Dictionary<WorkflowNodeBase,(RuntimeToolConfiguration? Runtime,ToolParameters? Parameters,ParamSettingContainer? Settings)> _nodes=new();
    private sealed record VariableBindings(ProducerBinding? Producer,List<ConsumerBinding> Consumers,string Reason,string Pending);
    private Dictionary<GlobalVariable,VariableBindings> _variables=new();
    public static GraphBindingUndoSnapshot Capture(IEnumerable<Workflow> flows,Solution? solution)=>Capture(flows.SelectMany(w=>w.Nodes),solution);
    public static GraphBindingUndoSnapshot Capture(IEnumerable<WorkflowNodeBase> nodes,Solution? solution)
    {
        var result=new GraphBindingUndoSnapshot{Nodes=nodes.Distinct().ToArray()};
        result._nodes=result.Nodes.ToDictionary(n=>n,n=>
        {
            if(n.RuntimeConfiguration is {} runtime)
            {
                if(n.ParamSettings.Settings.Any())throw new InvalidOperationException("Runtime历史不能包含SDK参数设置。");
                return (runtime.Copy(),(ToolParameters?)null,(ParamSettingContainer?)null);
            }
            return ((RuntimeToolConfiguration?)null,n.Parameters==null?null:ToolParameterSnapshot.Create(n.Parameters),ToolParameterSnapshot.CopySettings(n.ParamSettings));
        });
        if(solution!=null)foreach(var variable in solution.GlobalVariables)
        {
            var copy=variable.Clone();
            result._variables[variable]=new(copy.ProducerBinding,copy.ConsumerBindings,copy.RebindingReason,copy.BindingValidationPending);
        }
        return result;
    }
    public void Restore()
    {
        var prepared=_nodes.ToDictionary(pair=>pair.Key,pair=>(Runtime:pair.Value.Runtime?.Copy(),
            Parameters:pair.Value.Parameters==null?null:ToolParameterSnapshot.Create(pair.Value.Parameters),Settings:ToolParameterSnapshot.CopySettings(pair.Value.Settings)));
        foreach(var (node,state) in prepared)
        {
            node.RuntimeConfiguration=state.Runtime;
            if(state.Runtime is not null)node.ParamSettings=new ParamSettingContainer();
            else {node.Parameters=state.Parameters!;node.ParamSettings=state.Settings!;}
        }
        foreach(var (variable,state) in _variables)
        {
            var copy=new GlobalVariable{ProducerBinding=state.Producer,ConsumerBindings=state.Consumers,RebindingReason=state.Reason,BindingValidationPending=state.Pending}.Clone();
            variable.ProducerBinding=copy.ProducerBinding;variable.ConsumerBindings=copy.ConsumerBindings;
            variable.RebindingReason=copy.RebindingReason;variable.BindingValidationPending=copy.BindingValidationPending;
        }
    }
}
