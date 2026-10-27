using System.Windows;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
/// <summary>VMAddConnection已有节点对分支：只调整原连线端口，不替换连线身份或输入绑定。</summary>
public sealed class EditConnectionPortsCommand : IUndoableCommand
{
    private readonly WorkflowConnection _edge;
    private readonly string _beforeSource, _beforeTarget, _afterSource, _afterTarget;
    private readonly Point[]? _beforePath;
    public EditConnectionPortsCommand(WorkflowConnection edge, string sourcePort, string targetPort)
    {
        _edge = edge; _beforeSource = edge.SourcePort; _beforeTarget = edge.TargetPort;
        _afterSource = sourcePort; _afterTarget = targetPort; _beforePath = edge.ManualPathPoints?.ToArray();
    }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        _edge.PreviewPathPoints = null; _edge.ManualPathPoints = null;
        _edge.SourcePort = _afterSource; _edge.TargetPort = _afterTarget;
    }
    public void Undo()
    {
        _edge.SourcePort = _beforeSource; _edge.TargetPort = _beforeTarget;
        _edge.ManualPathPoints = _beforePath?.ToArray(); _edge.PreviewPathPoints = null;
    }
}