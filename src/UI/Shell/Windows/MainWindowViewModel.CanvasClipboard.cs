using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private NodeClipboardFragment? _canvasClipboard;
    private DocumentSession? _canvasClipboardOwner;
    private int _canvasPasteCount;
    private Vector _canvasClipboardCenterOffset;
    public bool CanPasteCanvasSelection => _canvasClipboard is not null && ReferenceEquals(_canvasClipboardOwner,DocumentManager.CurrentSession) && CanEditWorkflowCanvas;
    private void ClearCanvasClipboard()
    {
        if(_canvasClipboardOwner is not null)DocumentManager.SessionChanged-=CanvasClipboardSessionChanged;
        _canvasClipboard=null;_canvasClipboardOwner=null;_canvasPasteCount=0;_canvasClipboardCenterOffset=default;
    }
    private void CanvasClipboardSessionChanged(object? sender,DocumentSessionReplacement change)=>ClearCanvasClipboard();
    public bool CopyCanvasSelection()
    {
        var tab=WorkflowTabViewModel.SelectedTab;var session=DocumentManager.CurrentSession;
        if(!CanEditWorkflowCanvas || tab?.DocumentCanvas is not {} canvas || session is null)return false;
        try
        {
            var selected=canvas.Nodes.Where(node=>node.IsSelected).ToArray();
            var fragment=session.CaptureNodeClipboard(tab.CurrentWorkflowId,selected.Select(node=>node.Definition).ToArray());
            var first=selected[0];var offset=new Vector(first.PositionX-first.NodeRect.Left,first.PositionY-first.NodeRect.Top);
            ClearCanvasClipboard();_canvasClipboard=fragment;_canvasClipboardOwner=session;_canvasPasteCount=0;
            _canvasClipboardCenterOffset=offset;
            DocumentManager.SessionChanged+=CanvasClipboardSessionChanged;return true;
        }
        catch(Exception error){StatusText="复制节点失败："+error.Message;return false;}
    }
    public bool PasteCanvasSelection(Point? topLeft=null)
    {
        var tab=WorkflowTabViewModel.SelectedTab;var session=DocumentManager.CurrentSession;
        if(!CanPasteCanvasSelection || tab?.DocumentCanvas is not {} canvas || session is null)return false;
        DocumentSession.NodePasteReceipt receipt;
        try
        {
            var next=checked(_canvasPasteCount+1);
            var point=topLeft is {} corner ? corner+_canvasClipboardCenterOffset : new Point(_canvasClipboard!.Anchor.X+20*next,_canvasClipboard.Anchor.Y+20*next);
            receipt=session.PasteNodeClipboard(tab.CurrentWorkflowId,_canvasClipboard!,new CanvasPoint(point.X,point.Y));
            _canvasPasteCount=next;
        }
        catch(Exception error){StatusText="粘贴节点失败："+error.Message;return false;}
        SelectPastedNodes(canvas,receipt);return true;
    }
    public bool DuplicateCanvasNodes(IReadOnlyDictionary<CanvasNodeProjection,Point> positions)
    {
        var tab=WorkflowTabViewModel.SelectedTab;var session=DocumentManager.CurrentSession;
        if(!CanEditWorkflowCanvas || tab?.DocumentCanvas is not {} canvas || session is null || positions.Count==0)return false;
        DocumentSession.NodePasteReceipt receipt;
        try
        {
            var fragment=session.CaptureNodeClipboard(tab.CurrentWorkflowId,positions.Keys.Select(node=>node.Definition).ToArray());
            var first=positions.First().Value;
            receipt=session.PasteNodeClipboard(tab.CurrentWorkflowId,fragment,new CanvasPoint(first.X,first.Y));
        }
        catch(Exception error){StatusText="复制节点失败："+error.Message;return false;}
        SelectPastedNodes(canvas,receipt);return true;
    }
    private void SelectPastedNodes(DocumentCanvasProjection canvas,DocumentSession.NodePasteReceipt receipt)
    {
        try
        {
            foreach(var node in canvas.Nodes)node.IsSelected=receipt.Nodes.Any(item=>ReferenceEquals(item,node.Definition));
            SelectedNode=receipt.Nodes.Count==1?receipt.Nodes[0]:null;
        }
        catch(Exception error){StatusText="节点已粘贴，但选择视图更新失败："+error.Message;}
    }
}
