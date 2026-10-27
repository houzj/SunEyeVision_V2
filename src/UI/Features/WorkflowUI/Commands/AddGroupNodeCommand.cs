using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Commands;
/// <summary>新Group节点和独占定义作为同一图片段加入；撤销/重做保持双方身份。</summary>
public sealed class AddGroupNodeCommand : IUndoableCommand
{
    private readonly PasteWorkflowFragmentCommand _addition;
    public Flow OwnedWorkflow {get;}
    public AddGroupNodeCommand(Flow target,WorkflowNodeBase node,WorkflowEngine engine,Solution? solution)
    {
        if(!string.Equals(node.ToolType,"Subroutine",StringComparison.OrdinalIgnoreCase)||node.Parameters is not SubroutineParameters source)
            throw new InvalidOperationException("Group节点需要正式Subroutine参数。");
        if(target.Nodes.Any(n=>n.Id==node.Id)||!string.IsNullOrEmpty(node.SubroutineId))
            throw new InvalidOperationException("只能为未加入定义的新Group分配独占流程。");
        var parameters=(SubroutineParameters)ToolParameterSnapshot.Create(source);
        OwnedWorkflow=new Flow(Guid.NewGuid().ToString(),node.DispName){IsSubroutine=true};
        node.SubroutineId=OwnedWorkflow.Id;parameters.SubroutineId=OwnedWorkflow.Id;parameters.SubroutineName=node.DispName;
        if(!parameters.Inputs.Any(i=>string.Equals(i.Name,"in_1",StringComparison.OrdinalIgnoreCase)))
            parameters.Inputs.Add(new SubroutineInputConfig{Name="in_1",BindingKey="in_1",TypeName="Image"});
        node.Parameters=parameters;
        _addition=new(target,new WorkflowFragment(new[]{node},Array.Empty<WorkflowConnection>(),new[]{OwnedWorkflow}),engine,solution);
    }
    public event EventHandler? CanExecuteChanged{add{}remove{}}
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter)=>_addition.Execute(parameter);
    public void Undo()=>_addition.Undo();
}