using System.Windows;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
public sealed class EditConnectionPathCommand : IUndoableCommand
{
    private readonly WorkflowConnection _connection;
    private readonly Point[]? _before;
    private readonly Point[]? _after;
    private readonly Action _refresh;
    public EditConnectionPathCommand(WorkflowConnection connection,Point[]? before,Point[]? after,Action refresh)
    {_connection=connection;_before=before?.ToArray();_after=after?.ToArray();_refresh=refresh;}
    public event EventHandler? CanExecuteChanged {add{} remove{}}
    public bool CanExecute(object? parameter)=>true;
    private void Apply(Point[]? points){_connection.PreviewPathPoints=null;_connection.ManualPathPoints=points?.ToArray();_refresh();}
    public void Execute(object? parameter)=>Apply(_after);
    public void Undo()=>Apply(_before);
}