using System.Windows;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.CommunicationManager
{
    /// <summary>
    /// 通讯管理器对话框
    /// </summary>
    public partial class CommunicationManagerDialog : ThemeDialogWindow
    {
        public CommunicationManagerDialog(CommunicationManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
