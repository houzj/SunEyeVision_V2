using System.Collections.ObjectModel;
using System.Windows;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
/// <summary>恢复InsertExecution：保留端口方向、拒环，位置/边/绑定配置作为一个历史项。</summary>
public sealed class InsertNodeIntoConnectionCommand : IUndoableCommand
{
    private readonly ObservableCollection<WorkflowConnection> _connections;
    private readonly WorkflowConnection[] _before, _after;
    private readonly MoveNodesSnapshotCommand _move;
    private readonly WorkflowNode[] _affected;
    private readonly Action _configure, _refresh;
    private readonly EditConnectionPortsCommand[] _portEdits;
    private readonly GraphBindingUndoSnapshot _beforeConfig;
    private GraphBindingUndoSnapshot? _afterConfig;
    public IReadOnlyList<WorkflowConnection> AddedConnections { get; }
    public InsertNodeIntoConnectionCommand(ObservableCollection<WorkflowConnection> connections, IEnumerable<WorkflowNode> nodes,
        WorkflowConnection original, WorkflowNode inserted, Point position, Action configure, Action refresh)
    {
        if (!connections.Contains(original) || original.SourceNodeId == inserted.Id || original.TargetNodeId == inserted.Id)
            throw new InvalidOperationException("不能把连线端点插入自身连线，或目标连线已失效。");
        _connections = connections; _before = connections.ToArray(); _configure = configure; _refresh = refresh;
        var next = _before.Where(c => !ReferenceEquals(c, original)).ToList();
        var added = new List<WorkflowConnection>();
        var portEdits=new List<EditConnectionPortsCommand>();
        foreach (var pair in new[] { (original.SourceNodeId, inserted.Id), (inserted.Id, original.TargetNodeId) })
        {
            var existing=next.SingleOrDefault(edge=>edge.SourceNodeId==pair.Item1&&edge.TargetNodeId==pair.Item2);
            if(existing is not null)
            {
                if(existing.SourcePort!=original.SourcePort||existing.TargetPort!=original.TargetPort)
                    portEdits.Add(new EditConnectionPortsCommand(existing,original.SourcePort,original.TargetPort));
                continue;
            }
            var edge = new WorkflowConnection(Guid.NewGuid().ToString("N"), pair.Item1, pair.Item2)
            { SourcePort = original.SourcePort, TargetPort = original.TargetPort };
            next.Add(edge);
            added.Add(edge);
        }
        if (HasCycle(next)) throw new InvalidOperationException("插线将形成执行环，已取消。");
        _after = next.ToArray(); AddedConnections = added;
        _portEdits=portEdits.ToArray();
        _move = new MoveNodesSnapshotCommand(new[] { (inserted, inserted.Position.ToWpf(), position) }, _after);
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
    private GraphBindingUndoSnapshot Capture()=>GraphBindingUndoSnapshot.Capture(_affected,null);
    private static void Restore(GraphBindingUndoSnapshot snapshot)=>snapshot.Restore();
    private void ApplyEdges(IEnumerable<WorkflowConnection> edges)
    { _connections.Clear(); foreach (var edge in edges) _connections.Add(edge); }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        try
        {
            ApplyEdges(_after);
            foreach(var edit in _portEdits)edit.Execute(null);
            _move.Execute(null);
            if (_afterConfig == null) { _configure(); _afterConfig = Capture(); } else Restore(_afterConfig);
            _refresh();
        }
        catch(Exception failure){RestoreBefore(failure);throw;}
    }
    private void RestoreBefore(Exception? failure)
    {
        var errors=new List<Exception>();if(failure is not null)errors.Add(failure);
        void Attempt(Action action){try{action();}catch(Exception error){errors.Add(error);}}
        Attempt(()=>ApplyEdges(_before));Attempt(()=>_move.Undo());
        for(var index=_portEdits.Length-1;index>=0;index--)Attempt(_portEdits[index].Undo);
        Attempt(()=>Restore(_beforeConfig));Attempt(_refresh);
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException("插线及配置恢复发生异常。",errors);
    }
    public void Undo()=>RestoreBefore(null);
}
