using System;
using System.Linq;
using SunEyeVision.Application;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;

namespace SunEyeVision.UI.Features.WorkflowUI;

public partial class WorkflowTabViewModel
{
    private DocumentCanvasProjection? _documentCanvas;
    public DocumentCanvasProjection? DocumentCanvas => _documentCanvas;

    // 会话由方案管理器拥有；标签页只拥有对应流程的订阅与展示状态。
    internal void BindDocumentCanvas(DocumentSession session, string workflowId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var root=session.GetWorkflows().Single(flow=>flow.Id==workflowId&&!flow.IsSubroutine);
        if(workflowId!=Id)throw new InvalidOperationException("标签页只能绑定自身实际根流程。");
        var next = new DocumentCanvasProjection(session, workflowId);
        UnbindDocumentCanvas();
        _documentCanvas = next;_canvasSession=session;_rootDefinition=root;
        SynchronizeRootName(root.Name);RefreshRootReadiness();
        next.Changed += DocumentCanvasContentChanged;
        OnPropertyChanged(nameof(DocumentCanvas));
        WorkflowContentChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void UnbindDocumentCanvas()
    {
        var current = _documentCanvas;
        if (current is null) return;
        WorkflowContentChanging?.Invoke(this,EventArgs.Empty);
        _documentCanvas = null;_canvasSession=null;_rootDefinition=null;
        current.Changed -= DocumentCanvasContentChanged;
        current.Dispose();
        OnPropertyChanged(nameof(DocumentCanvas));
    }

    private void DocumentCanvasContentChanged(object? sender, EventArgs e)
    {
        if(!ReferenceEquals(sender,_documentCanvas))return;
        if(_rootDefinition is {} root)SynchronizeRootName(root.Name);RefreshRootReadiness();
        OnPropertyChanged(nameof(WorkflowNodes));OnPropertyChanged(nameof(WorkflowConnections));
        WorkflowContentChanged?.Invoke(this,EventArgs.Empty);
    }
}
