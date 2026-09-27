using System.Windows;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class ModbusReceiveRecordsDialog : ThemeDialogWindow
{
    public ModbusReceiveRecordsDialog(ModbusAddressPoint point)
    {
        InitializeComponent();
        DataContext = point;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
