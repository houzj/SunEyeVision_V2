using System;
using System.Diagnostics;
using System.Windows;
using SunEyeVision.UI.Features.Dialogs;

namespace SunEyeVision.UI.Shell.Windows
{
    /// <summary>
    /// AboutWindow - 关于对话框
    /// </summary>
    public partial class AboutWindow : ThemeDialogWindow
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现检查更新功能
            ThemeMessageDialog.Show("已是最新版本！", "检查更新",
                MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
        }

        private void HelpManual_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }

        private void Website_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.suneyevision.com",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show($"无法打开网站：{ex.Message}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }
    }
}
