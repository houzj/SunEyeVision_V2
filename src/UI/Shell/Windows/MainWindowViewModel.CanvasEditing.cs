using SunEyeVision.Workflow;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Plugin.SDK.UI.Controls;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    /// <summary>实际编辑入口共用：运行、参数窗口和方案切换/关闭准备均阻止图修改。</summary>
    public bool CanEditWorkflowCanvas
    {
        get
        {
            var tab=WorkflowTabViewModel?.SelectedTab;
            return tab!=null&&!IsRunning&&!IsDebugWindowOpen&&!tab.IsRunning
                &&_solutionTransitionLease==null&&!_transitionPreparationInProgress
                &&!_executionManager.IsRunning(tab.RootWorkflowId)&&!_executionManager.IsRunning(tab.CurrentWorkflowId);
        }
    }
    public bool CanDeleteCanvasConnection(WorkflowConnection connection)
    {
        var tab=WorkflowTabViewModel?.SelectedTab;
        return CanEditWorkflowCanvas&&tab!=null&&tab.WorkflowConnections.Contains(connection);
    }
    /// <summary>连线自身的Delete/右键删除，不消费遗留的节点选择。</summary>
    public bool DeleteCanvasConnection(WorkflowConnection connection)
    {
        if(!CanDeleteCanvasConnection(connection))return false;
        var tab=WorkflowTabViewModel.SelectedTab!;var definition=_workflowEngine.GetWorkflowById(tab.CurrentWorkflowId);
        if(definition==null)return false;
        if(!ReferenceEquals(definition.Connections,tab.WorkflowConnections)||!ReferenceEquals(definition.Nodes,tab.WorkflowNodes))return false;
        var solution=ServiceInitializer.SolutionManager?.CurrentSolution;
        var bindings=ServiceInitializer.CommunicationService.OutputBindings;
        var command=new DeleteWorkflowItemsCommand(definition,Array.Empty<WorkflowNodeBase>(),new[]{connection},_workflowEngine,solution,
            ()=>{AutoBindImageSourceOnConnectionRemoved(connection.SourceNodeId,definition.Nodes.FirstOrDefault(n=>n.Id==connection.TargetNodeId),connection.Id);bindings.ReconcileAll();},
            ()=>{bindings.AcceptHistoryConfiguration(solution);ToolEditorCommit.RefreshCommittedBindings();BuildFlatRows();});
        tab.CommandManager.Execute(command);
        if(ReferenceEquals(SelectedConnection,connection))SelectedConnection=null;
        RefreshImageDisplayIfAffected(connection);AddLog("删除连接："+connection.SourceNodeId+" → "+connection.TargetNodeId);
        return true;
    }

    public bool RenameCanvasNode(WorkflowNodeBase node,string name)
    {
        var tab=WorkflowTabViewModel?.SelectedTab;
        if(!CanEditWorkflowCanvas||tab==null||!tab.WorkflowNodes.Contains(node))return false;
        var trimmed=name.Trim();if(trimmed.Length==0||trimmed.Length>64)return false;
        if(node.DispName==trimmed)return true;
        var child=string.Equals(node.ToolType,"Subroutine",StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrEmpty(node.SubroutineId)?_workflowEngine.GetWorkflowById(node.SubroutineId):null;
        tab.CommandManager.Execute(new RenameCanvasNodeCommand(node,trimmed,child,()=>{BuildFlatRows();ToolEditorCommit.RefreshCommittedBindings();}));
        return true;
    }

}
