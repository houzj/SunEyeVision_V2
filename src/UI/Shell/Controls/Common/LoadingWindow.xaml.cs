using System.Windows;

namespace SunEyeVision.UI.Shell.Controls.Common
{
    /// <summary>
    /// LoadingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoadingWindow : Window
    {
        public string Message
        {
            get => MessageText.Text;
            set => MessageText.Text = value;
        }

        public LoadingWindow(string message = "请稍候")
        {
            InitializeComponent();
            Message = message;
            // Startup loading appears before MainWindow exists. Binding Owner to
            // Application.Current.MainWindow here can point back to this window
            // and WPF rejects the self-owner relationship.
            Owner = null;
            ShowInTaskbar = false;
            ShowActivated = true;
            Topmost = true;
        }
    }
}
