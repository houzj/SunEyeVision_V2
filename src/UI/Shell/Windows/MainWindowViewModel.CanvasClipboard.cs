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
    private DocumentCanvasProjection? _canvasClipboardCanvas;
    private HashSet<NodeDefinition>? _canvasClipboardNodes;
    private Point _canvasClipboardPosition;
    public bool CanPasteCanvasSelection => _canvasClipboard is not null && ReferenceEquals(_canvasClipboardOwner,DocumentManager.CurrentSession) && CanEditWorkflowCanvas;
    private void ClearCanvasClipboard()
    {
        if(_canvasClipboardOwner is not null)DocumentManager.SessionChanged-=CanvasClipboardSessionChanged;
        if(_canvasClipboardCanvas is not null)_canvasClipboardCanvas.NodesDeleted-=CanvasClipboardNodesDeleted;
        _canvasClipboard=null;_canvasClipboardOwner=null;_canvasClipboardCanvas=null;_canvasClipboardNodes=null;_canvasClipboardPosition=default;
    }
    private void CanvasClipboardSessionChanged(object? sender,DocumentSessionReplacement change)=>ClearCanvasClipboard();
    private void CanvasClipboardNodesDeleted(IReadOnlyList<NodeDefinition> removed)
    { if(_canvasClipboardNodes is not null && removed.Any(_canvasClipboardNodes.Contains))ClearCanvasClipboard(); }
    public bool CopyCanvasSelection()
    {
        var tab=WorkflowTabViewModel.SelectedTab;var session=DocumentManager.CurrentSession;
        if(!CanEditWorkflowCanvas || tab?.DocumentCanvas is not {} canvas || session is null)return false;
        try
        {
            var selected=canvas.Nodes.Where(node=>node.IsSelected).ToArray();
            var fragment=session.CaptureNodeClipboard(tab.CurrentWorkflowId,selected.Select(node=>node.Definition).ToArray());
            var topLeft=selected[0].NodeRect.TopLeft;
            ClearCanvasClipboard();_canvasClipboard=fragment;_canvasClipboardOwner=session;_canvasClipboardCanvas=canvas;
            _canvasClipboardNodes=selected.Select(node=>node.Definition).ToHashSet();_canvasClipboardPosition=topLeft;
            canvas.NodesDeleted+=CanvasClipboardNodesDeleted;DocumentManager.SessionChanged+=CanvasClipboardSessionChanged;return true;
        }
        catch(Exception error){StatusText="复制节点失败："+error.Message;return false;}
    }
    private static double PasteCoordinate(double value,bool limit)
    { var rounded=Math.Round((limit?Math.Min(value,4850):value)/5)*5;return rounded<0?5:rounded; }
    private static Dictionary<NodeDefinition,CanvasPoint> PastePositions(IReadOnlyList<CanvasNodeProjection> sources,Point point,bool useDefault)
    {
        if(useDefault)point=new(PasteCoordinate(point.X+100,true),PasteCoordinate(point.Y+100,true));
        point=new(PasteCoordinate(point.X,false),PasteCoordinate(point.Y,false));
        var origin=sources[0].NodeRect.TopLeft;var result=new Dictionary<NodeDefinition,CanvasPoint>();
        for(var i=0;i<sources.Count;i++)
        {
            var source=sources[i];var rect=source.NodeRect;
            var left=i==0?point.X:PasteCoordinate(point.X+rect.Left-origin.X,true);
            var top=i==0?point.Y:PasteCoordinate(point.Y+rect.Top-origin.Y,true);
            result.Add(source.Definition,new CanvasPoint(left+(source.PositionX-rect.Left),top+(source.PositionY-rect.Top)));
        }
        return result;
    }
    public bool PasteCanvasSelection(Point? topLeft=null)
    {
        var tab=WorkflowTabViewModel.SelectedTab;var session=DocumentManager.CurrentSession;
        if(!CanPasteCanvasSelection || tab?.DocumentCanvas is not {} canvas || session is null || _canvasClipboardCanvas is null)return false;
        DocumentSession.NodePasteReceipt receipt;
        try
        {
            var actual=session.ResolveNodeClipboardSources(_canvasClipboard!);
            if(actual.Count==0)return false;
            var sources=actual.Select(node=>_canvasClipboardCanvas.Nodes.Single(projection=>ReferenceEquals(projection.Definition,node))).ToArray();
            var positions=PastePositions(sources,topLeft ?? _canvasClipboardPosition,topLeft is null || topLeft==default(Point));
            receipt=session.PasteNodeClipboard(tab.CurrentWorkflowId,_canvasClipboard!,positions[sources[0].Definition],positions);
        }
        catch(Exception error){StatusText="粘贴节点失败："+error.Message;return false;}
        if(receipt.Nodes.Count==0)return false;SelectPastedNodes(canvas,receipt);return true;
    }
    public bool DuplicateCanvasNodes(IReadOnlyDictionary<CanvasNodeProjection,Point> positions)
    {
        var tab=WorkflowTabViewModel.SelectedTab;var session=DocumentManager.CurrentSession;
        if(!CanEditWorkflowCanvas || tab?.DocumentCanvas is not {} canvas || session is null || positions.Count==0)return false;
        DocumentSession.NodePasteReceipt receipt;
        try
        {
            var sources=positions.Keys.ToArray();var fragment=session.CaptureNodeClipboard(tab.CurrentWorkflowId,sources.Select(node=>node.Definition).ToArray());
            var first=sources[0];var moved=positions[first];var corner=new Point(moved.X-(first.PositionX-first.NodeRect.Left),moved.Y-(first.PositionY-first.NodeRect.Top));
            var targets=PastePositions(sources,corner,false);
            receipt=session.PasteNodeClipboard(tab.CurrentWorkflowId,fragment,targets[first.Definition],targets);
        }
        catch(Exception error){StatusText="复制节点失败："+error.Message;return false;}
        if(receipt.Nodes.Count==0)return false;SelectPastedNodes(canvas,receipt);return true;
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
