using System.Collections.ObjectModel;
using System.Windows;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
/// <summary>恢复InsertExecution：保留端口方向、拒环，位置/边/绑定配置作为一个历史项。</summary>
public sealed class InsertNodeIntoConnectionCommand : IUndoableCommand
{
    private readonly ObservableCollection<WorkflowConnection> _connections;
    private readonly WorkflowConnection[] _before, _after;
    private readonly MoveNodesSnapshotCommand _move;
    private readonly WorkflowNode[] _affected;
    private readonly Action _configure, _refresh;
    private readonly Dictionary<WorkflowNode, (ToolParameters? Parameters, ParamSettingContainer? Settings)> _beforeConfig;
    private Dictionary<WorkflowNode, (ToolParameters? Parameters, ParamSettingContainer? Settings)>? _afterConfig;
    public IReadOnlyList<WorkflowConnection> AddedConnections { get; }
    public InsertNodeIntoConnectionCommand(ObservableCollection<WorkflowConnection> connections, IEnumerable<WorkflowNode> nodes,
        WorkflowConnection original, WorkflowNode inserted, Point position, Action configure, Action refresh)
    {
        if (!connections.Contains(original) || original.SourceNodeId == inserted.Id || original.TargetNodeId == inserted.Id)
            throw new InvalidOperationException("不能把连线端点插入自身连线，或目标连线已失效。");
        _connections = connections; _before = connections.ToArray(); _configure = configure; _refresh = refresh;
        var next = _before.Where(c => !ReferenceEquals(c, original)).ToList();
        var added = new List<WorkflowConnection>();
        foreach (var pair in new[] { (original.SourceNodeId, inserted.Id), (inserted.Id, original.TargetNodeId) })
        {
            var edge = new WorkflowConnection(Guid.NewGuid().ToString("N"), pair.Item1, pair.Item2)
            { SourcePort = original.SourcePort, TargetPort = original.TargetPort };
            var existing = next.FindIndex(c => c.SourceNodeId == pair.Item1 && c.TargetNodeId == pair.Item2);
            if (existing >= 0) next[existing] = edge; else next.Add(edge);
            added.Add(edge);
        }
        if (HasCycle(next)) throw new InvalidOperationException("插线将形成执行环，已取消。");
        _after = next.ToArray(); AddedConnections = added;
        _move = new MoveNodesSnapshotCommand(new[] { (inserted, inserted.Position, position) }, _after);
        var affectedIds = new[] { original.SourceNodeId, original.TargetNodeId, inserted.Id }.ToHashSet();
        _affected = nodes.Where(n => affectedIds.Contains(n.Id)).ToArray();
        _beforeConfig = Capture();
    }
    private static bool HasCycle(IReadOnlyList<WorkflowConnection> edges)
    {
        var outgoing = edges.GroupBy(c => c.SourceNodeId).ToDictionary(g => g.Key, g => g.Select(c => c.TargetNodeId).ToArray());
        var active = new HashSet<string>(); var done = new HashSet<string>();
        bool Visit(string id)
        {
            if (active.Contains(id)) return true; if (done.Contains(id)) return false;
            active.Add(id);
            if (outgoing.TryGetValue(id, out var targets) && targets.Any(Visit)) return true;
            active.Remove(id); done.Add(id); return false;
        }
        return outgoing.Keys.Any(Visit);
    }
    private Dictionary<WorkflowNode, (ToolParameters? Parameters, ParamSettingContainer? Settings)> Capture() => _affected.ToDictionary(n => n,
        n => (n.Parameters == null ? null : ToolParameterSnapshot.Create(n.Parameters), ToolParameterSnapshot.CopySettings(n.ParamSettings)));
    private static void Restore(Dictionary<WorkflowNode, (ToolParameters? Parameters, ParamSettingContainer? Settings)> snapshot)
    {
        foreach (var pair in snapshot)
        {
            pair.Key.Parameters = pair.Value.Parameters == null ? null! : ToolParameterSnapshot.Create(pair.Value.Parameters);
            pair.Key.ParamSettings = ToolParameterSnapshot.CopySettings(pair.Value.Settings)!;
        }
    }
    private void ApplyEdges(IEnumerable<WorkflowConnection> edges)
    { _connections.Clear(); foreach (var edge in edges) _connections.Add(edge); }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        try
        {
            ApplyEdges(_after); _move.Execute(null);
            if (_afterConfig == null) { _configure(); _afterConfig = Capture(); } else Restore(_afterConfig);
            _refresh();
        }
        catch { ApplyEdges(_before); _move.Undo(); Restore(_beforeConfig); _refresh(); throw; }
    }
    public void Undo() { ApplyEdges(_before); _move.Undo(); Restore(_beforeConfig); _refresh(); }
}