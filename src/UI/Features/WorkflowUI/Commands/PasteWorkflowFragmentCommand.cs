using SunEyeVision.Workflow;
using WorkflowModel = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;

/// <summary>复制根片段及所有独占子程序作为同一次编辑；撤销不重新分配身份。</summary>
public sealed class PasteWorkflowFragmentCommand : IUndoableCommand
{
    private readonly WorkflowModel _target;
    private readonly WorkflowFragment _fragment;
    private readonly WorkflowEngine _engine;
    private readonly Solution? _solution;
    private IReadOnlyList<WorkflowModel>? _detachedOwned;
    private Dictionary<WorkflowModel,int>? _definitionIndices;
    private GraphBindingUndoSnapshot? _beforeUndoConfiguration;
    public PasteWorkflowFragmentCommand(WorkflowModel target, WorkflowFragment fragment, WorkflowEngine engine, Solution? solution)
    { _target = target; _fragment = fragment; _engine = engine; _solution = solution; }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        var definitions=_detachedOwned??_fragment.OwnedWorkflows;
        if (_fragment.Nodes.Any(n => _target.Nodes.Any(old => old.Id == n.Id)) ||
            _fragment.Connections.Any(e => _target.Connections.Any(old => old.Id == e.Id)) ||
            definitions.Any(w => _engine.GetWorkflowById(w.Id) != null || _solution?.Workflows.Any(old => old.Id == w.Id) == true))
            throw new InvalidOperationException("粘贴身份已被占用。");
        var rollback=_beforeUndoConfiguration==null?null:GraphBindingUndoSnapshot.Capture(
            _engine.GetAllWorkflows().Concat(_solution?.Workflows??Enumerable.Empty<WorkflowModel>()).Append(_target),_solution);
        var nodes = new List<WorkflowNodeBase>(); var edges = new List<WorkflowConnection>(); var owned = new List<WorkflowModel>();
        try
        {
            foreach (var workflow in definitions.OrderBy(w=>_definitionIndices?.GetValueOrDefault(w)??int.MaxValue))
            {
                owned.Add(workflow); _engine.RegisterWorkflow(workflow);
                if(_solution!=null)
                {
                    var index=_definitionIndices?.GetValueOrDefault(workflow)??-1;
                    if(index>=0&&index<=_solution.Workflows.Count)_solution.Workflows.Insert(index,workflow);
                    else _solution.Workflows.Add(workflow);
                }
            }
            foreach (var node in _fragment.Nodes) { nodes.Add(node); _target.Nodes.Add(node); }
            foreach (var edge in _fragment.Connections)
            { ExecutionConnectionPolicy.EnsureCanAdd(_target.Connections, edge); edges.Add(edge); _target.Connections.Add(edge); }
            _beforeUndoConfiguration?.Restore();
        }
        catch
        {
            foreach (var edge in edges) _target.Connections.Remove(edge);
            foreach (var node in nodes) _target.Nodes.Remove(node);
            foreach (var workflow in owned) { _solution?.Workflows.Remove(workflow); _engine.DeleteWorkflow(workflow.Id); }
            rollback?.Restore();
            throw;
        }
    }
    public void Undo()
    {
        _beforeUndoConfiguration=GraphBindingUndoSnapshot.Capture(
            _engine.GetAllWorkflows().Concat(_solution?.Workflows??Enumerable.Empty<WorkflowModel>()).Append(_target),_solution);
        _detachedOwned=WorkflowOwnership.CollectExclusiveDescendants(
            _engine.GetAllWorkflows().Concat(_solution?.Workflows??Enumerable.Empty<WorkflowModel>()),_target,_fragment.Nodes,_fragment.OwnedWorkflows);
        _definitionIndices=_detachedOwned.ToDictionary(w=>w,w=>_solution?.Workflows.IndexOf(w)??-1);
        foreach (var edge in _fragment.Connections) _target.Connections.Remove(edge);
        foreach (var node in _fragment.Nodes) _target.Nodes.Remove(node);
        foreach (var workflow in _detachedOwned) { _solution?.Workflows.Remove(workflow); _engine.DeleteWorkflow(workflow.Id); }
    }
}
