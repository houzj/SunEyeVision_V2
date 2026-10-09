using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.CommunicationManager;
public partial class CommunicationResponsesView : UserControl
{
    public CommunicationResponsesView() => InitializeComponent();
    private void Targets_Opened(object? sender, System.EventArgs e) => (DataContext as CommunicationManagerViewModel)?.RefreshAutomationChoices();
    private void Target_Selected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is TreeView { DataContext: CommunicationResponseRowViewModel row } && e.NewValue is HeartbeatTargetNode target) row.SelectTarget(target);
    }
    private void Enabled_Changed(object sender, RoutedPropertyChangedEventArgs<bool> e)
    {
        if (sender is not ToggleSwitch toggle || toggle.DataContext is not CommunicationResponseRowViewModel row) return;
        row.Definition.Enabled = e.NewValue;
        BindingOperations.SetBinding(toggle, ToggleSwitch.IsCheckedProperty, new Binding(nameof(CommunicationResponseDefinition.Enabled)) { Source = row.Definition, Mode = BindingMode.OneWay });
    }
}
