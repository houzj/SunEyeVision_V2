using System.Windows;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationSendEventDialog : ThemeDialogWindow
{
    public CommunicationSendEventViewModel? Result { get; private set; }
    public CommunicationSendEventDialog()
    {
        InitializeComponent(); DataContext = new CommunicationSendEventViewModel();
    }
    private void Create_Click(object sender, RoutedEventArgs e)
    {
        Result = (CommunicationSendEventViewModel)DataContext;
        DialogResult = true;
    }
}
