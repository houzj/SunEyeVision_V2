using System.Windows;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
    private void WorkflowLocationsLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not WorkflowLocationControl control || _viewModel == null) return;
        control.GetLocations = _viewModel.GetWorkflowLocations;
        control.Navigate = _viewModel.NavigateWorkflowLocation;
        control.SetBinding(WorkflowLocationControl.DefinitionIdentityProperty,
            new System.Windows.Data.Binding("SelectedTab.WorkflowNodes") { Source = _viewModel.WorkflowTabViewModel });
    }
}
