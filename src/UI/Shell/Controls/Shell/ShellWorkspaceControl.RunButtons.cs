using System.Windows;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
    public static readonly DependencyProperty WorkflowEditingAllowedProperty = DependencyProperty.Register(
        nameof(WorkflowEditingAllowed), typeof(bool), typeof(ShellWorkspaceControl), new PropertyMetadata(true));
    public bool WorkflowEditingAllowed
    {
        get => (bool)GetValue(WorkflowEditingAllowedProperty);
        set => SetValue(WorkflowEditingAllowedProperty, value);
    }
    public static readonly DependencyProperty SingleExecutionBusyProperty = DependencyProperty.Register(
        nameof(SingleExecutionBusy), typeof(bool), typeof(ShellWorkspaceControl), new PropertyMetadata(false));
    public bool SingleExecutionBusy
    {
        get => (bool)GetValue(SingleExecutionBusyProperty);
        set => SetValue(SingleExecutionBusyProperty, value);
    }
}
