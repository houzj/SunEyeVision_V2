using System.Windows;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private CanvasNodeProjection? _vmRenameNode;
    private WorkflowTabViewModel? _vmRenameTab;
    private object? _vmRenameOwner;
    private string? _vmRenameGraph,_vmRenameConfiguration;
    private bool BeginVmNodeRename(CanvasNodeProjection node)
    {
        CancelVmCanvasGesture();
        var tab=CurrentWorkflowTab;
        if(!CanEditVmCanvas||tab?.DocumentCanvas==null||!tab.DocumentCanvas.Nodes.Contains(node))return false;
        _vmRenameNode=node;_vmRenameTab=tab;_vmRenameOwner=DataContext;
        _vmRenameGraph=VmGraphStamp(tab);_vmRenameConfiguration=VmConfigurationStamp(tab);
        node.BeginRename();return true;
    }
    private bool ValidVmNodeRename(CanvasNodeProjection node)=>CanEditVmCanvas&&ReferenceEquals(_vmRenameNode,node)
        &&ReferenceEquals(_vmRenameOwner,DataContext)&&ReferenceEquals(CurrentWorkflowTab,_vmRenameTab)
        &&_vmRenameTab?.DocumentCanvas?.Nodes.Contains(node)==true&&VmGraphStamp(_vmRenameTab)==_vmRenameGraph
        &&_vmRenameConfiguration!=null&&VmConfigurationStamp(_vmRenameTab)==_vmRenameConfiguration;
    private void CancelVmNodeRename()
    {
        var node=_vmRenameNode;_vmRenameNode=null;_vmRenameTab=null;_vmRenameOwner=null;_vmRenameGraph=null;_vmRenameConfiguration=null;
        node?.CancelRename();
    }
    private bool CommitVmNodeRename(CanvasNodeProjection node,bool cancelInvalid)
    {
        if(!ValidVmNodeRename(node)){CancelVmNodeRename();return false;}
        var name=node.RenameBuffer.Trim();
        if(name.Length==0||name.Length>64){if(cancelInvalid)CancelVmNodeRename();return false;}
        try { _vmRenameTab!.DocumentCanvas!.Rename(node,name); }
        catch(Exception error) { if(_viewModel!=null)_viewModel.StatusText="重命名已取消："+error.Message;CancelVmNodeRename();return false; }
        CancelVmNodeRename();return true;
    }
}
