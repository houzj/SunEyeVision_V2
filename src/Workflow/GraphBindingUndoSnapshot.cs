using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.Workflow;
/// <summary>Configuration history excludes live results and global values.</summary>
public sealed class GraphBindingUndoSnapshot
{
    public WorkflowNodeBase[] Nodes {get;private init;}=Array.Empty<WorkflowNodeBase>();
    private Dictionary<WorkflowNodeBase,RuntimeToolConfiguration> _nodes=new();
    private sealed record VariableBindings(ProducerBinding? Producer,List<ConsumerBinding> Consumers,string Reason,string Pending);
    private Dictionary<GlobalVariable,VariableBindings> _variables=new();
    public static GraphBindingUndoSnapshot Capture(IEnumerable<Workflow> flows,Solution? solution)=>Capture(flows.SelectMany(w=>w.Nodes),solution);
    public static GraphBindingUndoSnapshot Capture(IEnumerable<WorkflowNodeBase> nodes,Solution? solution)
    {
        var result=new GraphBindingUndoSnapshot{Nodes=nodes.Distinct().ToArray()};
        foreach(var node in result.Nodes){RuntimeNodeAdmission.RequireShape(node);result._nodes.Add(node,node.RuntimeConfiguration!.Copy());}
        if(solution!=null)foreach(var variable in solution.GlobalVariables)
        {var copy=variable.Clone();result._variables[variable]=new(copy.ProducerBinding,copy.ConsumerBindings,copy.RebindingReason,copy.BindingValidationPending);}
        return result;
    }
    public void Restore()
    {
        var prepared=_nodes.ToDictionary(pair=>pair.Key,pair=>pair.Value.PreserveCurrentValuesFrom(
            pair.Key.RuntimeConfiguration??throw new InvalidOperationException("历史节点缺少运行配置。")));
        foreach(var (node,configuration) in prepared)node.RuntimeConfiguration=configuration;
        foreach(var (variable,state) in _variables)
        {var copy=new GlobalVariable{ProducerBinding=state.Producer,ConsumerBindings=state.Consumers,RebindingReason=state.Reason,BindingValidationPending=state.Pending}.Clone();
         variable.ProducerBinding=copy.ProducerBinding;variable.ConsumerBindings=copy.ConsumerBindings;variable.RebindingReason=copy.RebindingReason;variable.BindingValidationPending=copy.BindingValidationPending;}
    }
}
