namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    /// <summary>宿主退出当前画布时复用既有取消路径；不提交移动、重命名或连线编辑。</summary>
    internal void CancelWorkspaceGesture()
    {
        CancelVmCanvasGesture();
        ClearVmWireSelection();
        ClearAllSelections();
    }
}
