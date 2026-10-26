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
    public PasteWorkflowFragmentCommand(WorkflowModel target, WorkflowFragment fragment, WorkflowEngine engine, Solution? solution)
    { _target = target; _fragment = fragment; _engine = engine; _solution = solution; }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        if (_fragment.Nodes.Any(n => _target.Nodes.Any(old => old.Id == n.Id)) ||
            _fragment.Connections.Any(e => _target.Connections.Any(old => old.Id == e.Id)) ||
            _fragment.OwnedWorkflows.Any(w => _engine.GetWorkflowById(w.Id) != null || _solution?.Workflows.Any(old => old.Id == w.Id) == true))
            throw new InvalidOperationException("粘贴身份已被占用。");
        var nodes = new List<WorkflowNodeBase>(); var edges = new List<WorkflowConnection>(); var owned = new List<WorkflowModel>();
        try
        {
            foreach (var workflow in _fragment.OwnedWorkflows)
            { owned.Add(workflow); _engine.RegisterWorkflow(workflow); _solution?.Workflows.Add(workflow); }
            foreach (var node in _fragment.Nodes) { nodes.Add(node); _target.Nodes.Add(node); }
            foreach (var edge in _fragment.Connections)
            { ExecutionConnectionPolicy.EnsureCanAdd(_target.Connections, edge); edges.Add(edge); _target.Connections.Add(edge); }
        }
        catch
        {
            foreach (var edge in edges) _target.Connections.Remove(edge);
            foreach (var node in nodes) _target.Nodes.Remove(node);
            foreach (var workflow in owned) { _solution?.Workflows.Remove(workflow); _engine.DeleteWorkflow(workflow.Id); }
            throw;
        }
    }
    public void Undo()
    {
        foreach (var edge in _fragment.Connections) _target.Connections.Remove(edge);
        foreach (var node in _fragment.Nodes) _target.Nodes.Remove(node);
        foreach (var workflow in _fragment.OwnedWorkflows) { _solution?.Workflows.Remove(workflow); _engine.DeleteWorkflow(workflow.Id); }
    }
}
