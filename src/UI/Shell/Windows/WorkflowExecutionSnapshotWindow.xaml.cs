using System;
using SunEyeVision.UI.Services.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

/// <summary>只读呈现宿主拥有的根流程快照，不复制结果、不调度执行、不持有图像资源。</summary>
public partial class WorkflowExecutionSnapshotWindow : ThemeDialogWindow
{
    public WorkflowExecutionSnapshotWindow(WorkflowExecutionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        InitializeComponent();
        DataContext = snapshot;
        // Binding owns no execution resources. Release the shared snapshot when this view closes.
        Closed += (_, _) => { TimingList.ItemsSource = null; DataContext = null; };
    }
}
