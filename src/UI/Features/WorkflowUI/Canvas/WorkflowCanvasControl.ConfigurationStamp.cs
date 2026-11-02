namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    // Document session and editing version identify accepted configuration as one unit.
    private string? VmConfigurationStamp(WorkflowTabViewModel tab)
    {
        var session = Adapters.ServiceInitializer.SolutionManager.Session;
        if (session is null || !session.Document.Workflows.Any(flow => flow.Id == tab.CurrentWorkflowId)) return null;
        var snapshot = session.Editing.Host.Capture();
        return $"{session.Document.ExecutionOwner.Id}:{snapshot.Version}";
    }
}
