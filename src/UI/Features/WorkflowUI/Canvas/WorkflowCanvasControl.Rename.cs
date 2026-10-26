using System.Windows;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private WorkflowNodeBase? _vmRenameNode;
    private WorkflowTabViewModel? _vmRenameTab;
    private object? _vmRenameOwner;
    private string? _vmRenameGraph,_vmRenameConfiguration;
    private bool BeginVmNodeRename(WorkflowNodeBase node)
    {
        CancelVmCanvasGesture();
        var tab=CurrentWorkflowTab;
        if(!CanEditVmCanvas||tab==null||!tab.WorkflowNodes.Contains(node))return false;
        _vmRenameNode=node;_vmRenameTab=tab;_vmRenameOwner=DataContext;
        _vmRenameGraph=VmGraphStamp(tab);_vmRenameConfiguration=VmConfigurationStamp(tab);
        node.BeginRename();return true;
    }
    private bool ValidVmNodeRename(WorkflowNodeBase node)=>CanEditVmCanvas&&ReferenceEquals(_vmRenameNode,node)
        &&ReferenceEquals(_vmRenameOwner,DataContext)&&ReferenceEquals(CurrentWorkflowTab,_vmRenameTab)
        &&_vmRenameTab!=null&&_vmRenameTab.WorkflowNodes.Contains(node)&&VmGraphStamp(_vmRenameTab)==_vmRenameGraph
        &&_vmRenameConfiguration!=null&&VmConfigurationStamp(_vmRenameTab)==_vmRenameConfiguration;
    private void CancelVmNodeRename()
    {
        var node=_vmRenameNode;_vmRenameNode=null;_vmRenameTab=null;_vmRenameOwner=null;_vmRenameGraph=null;_vmRenameConfiguration=null;
        node?.CancelRename();
    }
    private bool CommitVmNodeRename(WorkflowNodeBase node,bool cancelInvalid)
    {
        if(!ValidVmNodeRename(node)){CancelVmNodeRename();return false;}
        var name=node.RenameBuffer.Trim();
        if(name.Length==0||name.Length>64){if(cancelInvalid)CancelVmNodeRename();return false;}
        if(_viewModel?.RenameCanvasNode(node,name)!=true){CancelVmNodeRename();return false;}
        CancelVmNodeRename();return true;
    }
}
