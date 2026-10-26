using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Flow = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;

/// <summary>删除图、独占子流程及声明绑定配置共用一次历史；不保存运行图像或运行结果。</summary>
public sealed class DeleteWorkflowItemsCommand : IUndoableCommand
{
    private readonly BatchDeleteNodesAndConnectionsCommand _graph;
    private readonly WorkflowEngine _engine;
    private readonly Solution? _solution;
    private readonly Flow[] _owned;
    private readonly HashSet<ScopedNodeIdentity> _deleted;
    private readonly Dictionary<Flow,int> _indices;
    private readonly Action _configure, _refresh;
    private readonly Configuration _before;
    private Configuration? _after;
    public DeleteWorkflowItemsCommand(Flow target, IEnumerable<WorkflowNodeBase> nodes, IEnumerable<WorkflowConnection> edges,
        WorkflowEngine engine, Solution? solution, Action configure, Action refresh)
    {
        var selected = nodes.ToArray();
        _graph = new(target.Nodes,target.Connections,selected.ToList(),edges.ToList());
        _engine=engine;_solution=solution;_configure=configure;_refresh=refresh;
        var definitions=engine.GetAllWorkflows().Concat(solution?.Workflows ?? Enumerable.Empty<Flow>()).Append(target).Distinct().ToArray();
        var byId=definitions.GroupBy(w=>w.Id).ToDictionary(g=>g.Key,g=>g.First());
        var candidates=new HashSet<Flow>();
        void Visit(string? id)
        {
            if(string.IsNullOrEmpty(id)||!byId.TryGetValue(id,out var flow)||ReferenceEquals(flow,target)||!candidates.Add(flow))return;
            foreach(var node in flow.Nodes) if(IsGroup(node))Visit(node.SubroutineId);
        }
        foreach(var node in selected)if(IsGroup(node))Visit(node.SubroutineId);
        // 损坏的历史图可能共享定义；外部仍有拥有者时保留该定义及其后代。
        var retained=new HashSet<Flow>();
        void Retain(string? id)
        {
            if(string.IsNullOrEmpty(id)||!byId.TryGetValue(id,out var flow)||!candidates.Contains(flow)||!retained.Add(flow))return;
            foreach(var node in flow.Nodes)if(IsGroup(node))Retain(node.SubroutineId);
        }
        foreach(var flow in definitions.Where(w=>!candidates.Contains(w)))
            foreach(var node in flow.Nodes.Where(n=>!ReferenceEquals(flow,target)||!selected.Contains(n)))
                if(IsGroup(node))Retain(node.SubroutineId);
        _owned=candidates.Except(retained).ToArray();
        _deleted=selected.Select(n=>new ScopedNodeIdentity(target.Id,n.Id))
            .Concat(_owned.SelectMany(w=>w.Nodes.Select(n=>new ScopedNodeIdentity(w.Id,n.Id)))).ToHashSet();
        _indices=_owned.ToDictionary(w=>w,w=>solution?.Workflows.IndexOf(w)??-1);
        _before=Configuration.Capture(definitions,solution);
    }
    private static bool IsGroup(WorkflowNodeBase node)=>string.Equals(node.ToolType,"Subroutine",StringComparison.OrdinalIgnoreCase);
    public event EventHandler? CanExecuteChanged {add{}remove{}}
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter)
    {
        try
        {
            _graph.Execute(null);
            foreach(var flow in _owned){_solution?.Workflows.Remove(flow);_engine.DeleteWorkflow(flow.Id);}
            if(_after==null)
            {
                if(_solution!=null)foreach(var variable in _solution.GlobalVariables)
                    variable.ConsumerBindings.RemoveAll(c=>_deleted.Contains(new(c.WorkflowId,c.NodeId)));
                _configure();_after=Configuration.Capture(_before.Nodes,_solution);
            }
            else _after.Restore();
            _refresh();
        }
        catch
        {
            RestoreOwned();_graph.Undo();_before.Restore();_refresh();throw;
        }
    }
    public void Undo(){RestoreOwned();_graph.Undo();_before.Restore();_refresh();}
    private void RestoreOwned()
    {
        foreach(var flow in _owned.OrderBy(w=>_indices[w]))
        {
            var registered=_engine.GetWorkflowById(flow.Id);
            if(registered!=null&&!ReferenceEquals(registered,flow))throw new InvalidOperationException("删除撤销时子程序身份被占用。");
            _engine.RegisterWorkflow(flow);
            if(_solution!=null&&!_solution.Workflows.Contains(flow))
            {
                var index=_indices[flow];
                if(index>=0&&index<=_solution.Workflows.Count)_solution.Workflows.Insert(index,flow);
                else _solution.Workflows.Add(flow);
            }
        }
    }
    private sealed class Configuration
    {
        public WorkflowNodeBase[] Nodes {get;private init;}=Array.Empty<WorkflowNodeBase>();
        private Dictionary<WorkflowNodeBase,(ToolParameters? Parameters,ParamSettingContainer? Settings)> _nodes=new();
        private Dictionary<GlobalVariable,GlobalVariable> _variables=new();
        public static Configuration Capture(IEnumerable<Flow> flows,Solution? solution)=>Capture(flows.SelectMany(w=>w.Nodes),solution);
        public static Configuration Capture(IEnumerable<WorkflowNodeBase> nodes,Solution? solution)
        {
            var result=new Configuration{Nodes=nodes.Distinct().ToArray()};
            result._nodes=result.Nodes.ToDictionary(n=>n,n=>(n.Parameters==null?null:ToolParameterSnapshot.Create(n.Parameters),n.ParamSettings?.Clone()));
            result._variables=solution?.GlobalVariables.ToDictionary(v=>v,v=>v.Clone())??new();
            return result;
        }
        public void Restore()
        {
            foreach(var (node,state) in _nodes){node.Parameters=state.Parameters==null?null!:ToolParameterSnapshot.Create(state.Parameters);node.ParamSettings=state.Settings?.Clone()!;}
            foreach(var (variable,saved) in _variables)
            {
                var copy=saved.Clone();variable.ProducerBinding=copy.ProducerBinding;variable.ConsumerBindings=copy.ConsumerBindings;
                variable.RebindingReason=copy.RebindingReason;variable.BindingValidationPending=copy.BindingValidationPending;
            }
        }
    }
}