using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationHeartbeatsView : UserControl
{
    public CommunicationHeartbeatsView() => InitializeComponent();
    private void Enabled_Changed(object sender, RoutedPropertyChangedEventArgs<bool> e)
    {
        if (sender is not ToggleSwitch toggle || toggle.DataContext is not CommunicationHeartbeatViewModel row) return;
        row.Enabled = e.NewValue;
        // Pointer/keyboard input in the SDK control replaces its binding; restore model ownership locally.
        BindingOperations.SetBinding(toggle, ToggleSwitch.IsCheckedProperty,
            new Binding(nameof(CommunicationHeartbeatViewModel.Enabled)) { Source = row, Mode = BindingMode.OneWay });
    }
    private void Targets_Opened(object? sender, System.EventArgs e) =>
        (DataContext as CommunicationManagerViewModel)?.RefreshHeartbeatTargets();
    private void Target_Selected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is TreeView { DataContext: CommunicationHeartbeatViewModel row } &&
            e.NewValue is HeartbeatTargetNode target && DataContext is CommunicationManagerViewModel manager)
            manager.SelectHeartbeatTarget(row, target);
    }
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommunicationHeartbeatViewModel row } &&
            DataContext is CommunicationManagerViewModel manager &&
            MessageBox.Show(Window.GetWindow(this), "删除此心跳配置并停止发送？", "心跳管理",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            manager.RemoveHeartbeat(row);
    }
}
