using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
public sealed class ChangeRootWorkspaceSettingsCommand : IUndoableCommand
{
    private readonly SunEyeVision.Workflow.Workflow _workflow;
    private readonly RootWorkspaceSettings _before,_after;
    private readonly Action _changed;
    public ChangeRootWorkspaceSettingsCommand(SunEyeVision.Workflow.Workflow workflow,RootWorkspaceSettings after,Action changed)
    {
        if(workflow.IsSubroutine)throw new InvalidOperationException("根工作区设置不能修改Group。");
        after.Validate();_workflow=workflow;_before=workflow.WorkspaceSettings;_after=after;_changed=changed;
    }
    public event EventHandler? CanExecuteChanged {add{} remove{}}
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter){_workflow.WorkspaceSettings=_after;_changed();}
    public void Undo(){_workflow.WorkspaceSettings=_before;_changed();}
}
