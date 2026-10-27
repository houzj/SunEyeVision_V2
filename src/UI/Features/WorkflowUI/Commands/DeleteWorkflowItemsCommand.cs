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
    private readonly GraphBindingUndoSnapshot _before;
    private GraphBindingUndoSnapshot? _after;
    public DeleteWorkflowItemsCommand(Flow target, IEnumerable<WorkflowNodeBase> nodes, IEnumerable<WorkflowConnection> edges,
        WorkflowEngine engine, Solution? solution, Action configure, Action refresh)
    {
        var selected = nodes.ToArray();
        _graph = new(target.Nodes,target.Connections,selected.ToList(),edges.ToList());
        _engine=engine;_solution=solution;_configure=configure;_refresh=refresh;
        var definitions=engine.GetAllWorkflows().Concat(solution?.Workflows ?? Enumerable.Empty<Flow>()).Append(target).Distinct().ToArray();
        _owned=WorkflowOwnership.CollectExclusiveDescendants(definitions,target,selected).ToArray();
        _deleted=selected.Select(n=>new ScopedNodeIdentity(target.Id,n.Id))
            .Concat(_owned.SelectMany(w=>w.Nodes.Select(n=>new ScopedNodeIdentity(w.Id,n.Id)))).ToHashSet();
        _indices=_owned.ToDictionary(w=>w,w=>solution?.Workflows.IndexOf(w)??-1);
        _before=GraphBindingUndoSnapshot.Capture(definitions,solution);
    }
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
                _configure();_after=GraphBindingUndoSnapshot.Capture(_before.Nodes,_solution);
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
}
