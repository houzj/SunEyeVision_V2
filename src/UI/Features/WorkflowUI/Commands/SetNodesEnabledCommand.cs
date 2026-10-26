using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
public sealed class SetNodesEnabledCommand : IUndoableCommand
{
    private readonly (WorkflowNode Node, bool Before)[] _nodes;
    private readonly bool _enabled;
    public SetNodesEnabledCommand(IEnumerable<WorkflowNode> nodes, bool enabled)
    { _nodes = nodes.Select(n => (n, n.IsEnabled)).ToArray(); _enabled = enabled; }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) { foreach (var n in _nodes) n.Node.IsEnabled = _enabled; }
    public void Undo() { foreach (var n in _nodes) n.Node.IsEnabled = n.Before; }
}