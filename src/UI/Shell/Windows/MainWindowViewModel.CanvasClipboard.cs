using System.Windows;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using NodeFactory = SunEyeVision.UI.Services.Workflow.WorkflowNodeFactory;
using SunEyeVision.Workflow;
using Node = SunEyeVision.UI.Shared.WorkflowNode;
using WorkflowModel = SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private sealed record CanvasCopySource(WorkflowModel Definition, string[] Ids, Point Origin);
    private CanvasCopySource? _canvasCopySource;
    public bool CanPasteCanvasSelection => _canvasCopySource is { } source && CanEditWorkflowCanvas &&
        ReferenceEquals(_workflowEngine.GetWorkflowById(source.Definition.Id), source.Definition) &&
        source.Ids.All(id => source.Definition.Nodes.Any(n => n.Id == id));
    public bool CopyCanvasSelection()
    {
        var tab = WorkflowTabViewModel.SelectedTab;
        if (tab == null || !CanEditWorkflowCanvas) return false;
        var nodes = tab.WorkflowNodes.Where(n => n.IsSelected).ToArray();
        if (nodes.Length == 0) return false;
        if (nodes.Any(n => !ToolRegistry.ToolExists(n.ToolType))) { Status = "所选工具未加载，不能复制。"; return false; }
        _canvasCopySource = new(_workflowDefinitions.GetRequired(tab.CurrentWorkflowId), nodes.Select(n => n.Id).ToArray(), ((Node)nodes[0]).NodeRect.TopLeft);
        return true;
    }
    public bool PasteCanvasSelection(Point? topLeft = null)
    {
        var source = _canvasCopySource;
        if (source == null) return false;
        if (!ReferenceEquals(_workflowEngine.GetWorkflowById(source.Definition.Id), source.Definition))
        { _canvasCopySource = null; return false; }
        return PasteCanvasFragment(source.Definition, source.Ids, topLeft ?? source.Origin + new Vector(100,100), null);
    }
    public bool DuplicateCanvasNodes(IReadOnlyDictionary<Node, Point> positions)
    {
        var tab = WorkflowTabViewModel.SelectedTab;
        if (tab == null || positions.Count == 0) return false;
        var source = _workflowDefinitions.GetRequired(tab.CurrentWorkflowId);
        var first = source.Nodes.OfType<Node>().FirstOrDefault(positions.ContainsKey);
        if (first == null) return false;
        var corner = positions[first] - new Vector(first.NodeRect.Width/2, first.NodeRect.Height/2);
        _canvasCopySource = new(source, positions.Keys.Select(n => n.Id).ToArray(), corner);
        return PasteCanvasFragment(source, _canvasCopySource.Ids, null, positions.ToDictionary(p => p.Key.Id, p => p.Value));
    }
    private bool PasteCanvasFragment(WorkflowModel source, string[] ids, Point? topLeft, IReadOnlyDictionary<string, Point>? positions)
    {
        var tab = WorkflowTabViewModel.SelectedTab;
        if (tab == null || !CanEditWorkflowCanvas || tab.SequenceManager == null) return false;
        var target = _workflowDefinitions.GetRequired(tab.CurrentWorkflowId);
        if (!ReferenceEquals(target.Nodes, tab.WorkflowNodes) || !ReferenceEquals(target.Connections, tab.WorkflowConnections)) return false;
        var allocated = new List<(Node Node, string Scope)>(); bool committed = false;
        try
        {
            var factory = new NodeFactory(tab.SequenceManager);
            var fragment = WorkflowFragmentCopy.Prepare(source, ids, target.Id, _workflowEngine.GetWorkflowById, (original, scope) =>
            {
                if (!ToolRegistry.ToolExists(original.ToolType)) throw new InvalidOperationException($"工具未加载：{original.DispName}");
                var copy = factory.CreateNode(original.ToolType, workflowId: scope); allocated.Add((copy, scope)); return copy;
            });
            var originals = source.Nodes.Where(n => ids.Contains(n.Id)).Cast<Node>().ToArray();
            var first = originals[0];
            // 原VM粘贴使用首模块左上角作为原点；键盘粘贴默认偏移100，逐项按页面及5像素网格限位。
            var origin = topLeft ?? first.NodeRect.TopLeft + new Vector(100,100);
            double Snap(double value) { var snapped = Math.Round(Math.Min(value, 5000 - 150) / 5) * 5; return snapped < 0 ? 5 : snapped; }
            origin = new Point(Snap(origin.X), Snap(origin.Y));
            var delta = origin - first.NodeRect.TopLeft;
            for (var i=0; i<fragment.Nodes.Count; i++)
            {
                var copy = (Node)fragment.Nodes[i]; var original = originals[i];
                if (positions != null) copy.Position = positions[original.Id];
                else
                {
                    var corner = original.NodeRect.TopLeft + delta;
                    copy.Position = new Point(Snap(corner.X) + copy.NodeRect.Width/2, Snap(corner.Y) + copy.NodeRect.Height/2);
                }
            }
            foreach (var edge in fragment.Connections)
                if (edge.ManualPathPoints != null) edge.ManualPathPoints = edge.ManualPathPoints.Select(p => p + (fragment.Nodes[0].Position - first.Position)).ToArray();
            tab.CommandManager.Execute(new PasteWorkflowFragmentCommand(target, fragment, _workflowEngine, SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager?.CurrentSolution));
            committed = true;
            foreach (var node in target.Nodes) node.IsSelected = fragment.Nodes.Contains(node);
            foreach (var edge in target.Connections) edge.IsSelected = false;
            SelectedNode = fragment.Nodes.OfType<Node>().FirstOrDefault();
            foreach (var node in fragment.Nodes) PrecompileNodeIfNeeded(node);
            return true;
        }
        catch (Exception error)
        {
            foreach (var item in committed ? Enumerable.Empty<(Node Node, string Scope)>() : allocated)
            { tab.SequenceManager.ReleaseLocalIndex(item.Scope,item.Node.ToolType,item.Node.LocalIndex); tab.SequenceManager.ReleaseGlobalIndex(item.Node.GlobalIndex); }
            Status = $"粘贴已取消：{error.Message}"; return false;
        }
    }
}
