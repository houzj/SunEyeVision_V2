using System;
using System.Windows;
using SunEyeVision.UI.Features.Dialogs;

namespace SunEyeVision.UI.Shell.Windows
{
    public partial class AboutWindow : ThemeDialogWindow
    {
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = new AboutSoftwareInformation();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CopyInformation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(((AboutSoftwareInformation)DataContext).ToDiagnosticText());
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show($"无法复制软件信息：{ex.Message}", "复制软件信息",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }

        private void HelpManual_Click(object sender, RoutedEventArgs e)
        {
            new HelpWindow { Owner = this }.ShowDialog();
        }
    }
}
