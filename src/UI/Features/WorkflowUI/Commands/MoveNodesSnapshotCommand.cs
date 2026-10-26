using System.Windows;
using SunEyeVision.UI.Shared;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
/// <summary>恢复MoveNodes语义：一次提交坐标并清除关联手工路由，撤销恢复两者。</summary>
public sealed class MoveNodesSnapshotCommand : IUndoableCommand
{
    private readonly (WorkflowNode Node, Point Before, Point After)[] _positions;
    private readonly (WorkflowConnection Connection, Point[]? Before)[] _paths;
    public MoveNodesSnapshotCommand(IEnumerable<(WorkflowNode Node, Point Before, Point After)> positions,
        IEnumerable<WorkflowConnection>? connections = null)
    {
        _positions = positions.ToArray();
        var moved = _positions.Where(p => p.Before != p.After).Select(p => p.Node.Id).ToHashSet();
        _paths = (connections ?? Enumerable.Empty<WorkflowConnection>())
            .Where(c => moved.Contains(c.SourceNodeId) || moved.Contains(c.TargetNodeId))
            .Select(c => (c, c.ManualPathPoints?.ToArray())).ToArray();
    }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        foreach (var p in _paths) { p.Connection.ManualPathPoints = null; p.Connection.PreviewPathPoints = null; }
        foreach (var item in _positions) item.Node.Position = item.After;
    }
    public void Undo()
    {
        foreach (var p in _paths) p.Connection.ManualPathPoints = p.Before?.ToArray();
        foreach (var item in _positions) item.Node.Position = item.Before;
    }
}
