using System.Windows;
using System.Windows.Controls;
namespace SunEyeVision.UI.Features.CommunicationManager;
public partial class CommunicationTriggersView : UserControl
{
    public CommunicationTriggersView() => InitializeComponent();
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommunicationTriggerRowViewModel row } && DataContext is CommunicationManagerViewModel manager &&
            MessageBox.Show(Window.GetWindow(this), "删除此触发规则？", "触发配置", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            manager.RemoveAutomationTrigger(row);
    }
}
