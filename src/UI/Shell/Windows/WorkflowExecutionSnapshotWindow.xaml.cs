using SunEyeVision.UI.Services.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class WorkflowExecutionSnapshotWindow : ThemeDialogWindow
{
    public WorkflowExecutionSnapshotWindow(WorkflowExecutionSnapshot snapshot)
    {
        InitializeComponent();
        DataContext = snapshot;
    }
}
