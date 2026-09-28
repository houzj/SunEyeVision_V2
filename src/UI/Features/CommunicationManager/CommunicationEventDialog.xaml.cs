using System.Windows;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationEventDialog : ThemeDialogWindow
{
    public CommunicationReceiveEventViewModel? Result { get; private set; }

    public CommunicationEventDialog()
    {
        InitializeComponent();
        DataContext = new CommunicationReceiveEventViewModel
        {
            ProcessingMethod = CommunicationEventProcessingMethod.Text,
            EventType = CommunicationEventType.ProtocolParse
        };
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        Result = (CommunicationReceiveEventViewModel)DataContext;
        DialogResult = true;
    }
}
