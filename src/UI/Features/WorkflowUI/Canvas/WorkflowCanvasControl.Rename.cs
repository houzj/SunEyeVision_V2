using System.Windows;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    private CanvasNodeProjection? _canvasRenameNode;
    private WorkflowTabViewModel? _canvasRenameTab;
    private object? _canvasRenameOwner;
    private string? _canvasRenameGraph,_canvasRenameConfiguration;
    private bool BeginCanvasNodeRename(CanvasNodeProjection node)
    {
        CancelCanvasGesture();
        var tab=CurrentWorkflowTab;
        if(!CanEditCanvas||tab?.DocumentCanvas==null||!tab.DocumentCanvas.Nodes.Contains(node))return false;
        _canvasRenameNode=node;_canvasRenameTab=tab;_canvasRenameOwner=DataContext;
        _canvasRenameGraph=CanvasGraphStamp(tab);_canvasRenameConfiguration=CanvasConfigurationStamp(tab);
        node.BeginRename();return true;
    }
    private bool ValidCanvasNodeRename(CanvasNodeProjection node)=>CanEditCanvas&&ReferenceEquals(_canvasRenameNode,node)
        &&ReferenceEquals(_canvasRenameOwner,DataContext)&&ReferenceEquals(CurrentWorkflowTab,_canvasRenameTab)
        &&_canvasRenameTab?.DocumentCanvas?.Nodes.Contains(node)==true&&CanvasGraphStamp(_canvasRenameTab)==_canvasRenameGraph
        &&_canvasRenameConfiguration!=null&&CanvasConfigurationStamp(_canvasRenameTab)==_canvasRenameConfiguration;
    private void CancelCanvasNodeRename()
    {
        var node=_canvasRenameNode;_canvasRenameNode=null;_canvasRenameTab=null;_canvasRenameOwner=null;_canvasRenameGraph=null;_canvasRenameConfiguration=null;
        node?.CancelRename();
    }
    private bool CommitCanvasNodeRename(CanvasNodeProjection node,bool cancelInvalid)
    {
        if(!ValidCanvasNodeRename(node)){CancelCanvasNodeRename();return false;}
        var name=node.RenameBuffer.Trim();
        if(name.Length==0||name.Length>64){if(cancelInvalid)CancelCanvasNodeRename();return false;}
        try { _canvasRenameTab!.DocumentCanvas!.Rename(node,name); }
        catch(Exception error) { if(_viewModel!=null)_viewModel.StatusText="重命名已取消："+error.Message;CancelCanvasNodeRename();return false; }
        CancelCanvasNodeRename();return true;
    }
}
