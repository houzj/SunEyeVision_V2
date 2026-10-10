using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Features.Dialogs;
namespace SunEyeVision.UI.Features.CommunicationManager;
public partial class CommunicationTriggersView : UserControl
{
    public CommunicationTriggersView() => InitializeComponent();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CommunicationTriggerRowViewModel row } && DataContext is CommunicationManagerViewModel manager &&
            ThemeMessageDialog.Show("删除此触发规则？仅删除规则，不删除接收事件或流程。", "触发配置", MessageBoxButton.YesNo,
                ThemeMessageDialog.MessageIcon.Question, Window.GetWindow(this)) == MessageBoxResult.Yes)
            manager.RemoveAutomationTrigger(row);
    }
}
