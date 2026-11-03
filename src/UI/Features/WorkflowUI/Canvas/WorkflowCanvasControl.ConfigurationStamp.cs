namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    // Document session and editing version identify accepted configuration as one unit.
    private string? CanvasConfigurationStamp(WorkflowTabViewModel tab)
    {
        var session = Adapters.ServiceInitializer.SolutionManager.CurrentSession;
        if (session is null || !session.GetWorkflows().Any(flow => flow.Id == tab.CurrentWorkflowId)) return null;
        return $"{session.EditSessionId}:{session.ChangeRevision}";
    }
}
