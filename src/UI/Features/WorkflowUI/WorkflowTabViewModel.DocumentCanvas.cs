using System;
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
        var next = new DocumentCanvasProjection(session, workflowId);
        UnbindDocumentCanvas();
        _documentCanvas = next;
        next.Changed += DocumentCanvasContentChanged;
        OnPropertyChanged(nameof(DocumentCanvas));
        WorkflowContentChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void UnbindDocumentCanvas()
    {
        var current = _documentCanvas;
        if (current is null) return;
        _documentCanvas = null;
        current.Changed -= DocumentCanvasContentChanged;
        current.Dispose();
        OnPropertyChanged(nameof(DocumentCanvas));
    }

    private void DocumentCanvasContentChanged(object? sender, EventArgs e) =>
        WorkflowContentChanged?.Invoke(this, EventArgs.Empty);
}
