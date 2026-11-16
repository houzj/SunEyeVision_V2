using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace SunEyeVision.UI.Shell.Controls.Common
{
    /// <summary>
    /// LoadingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoadingWindow : Window
    {
        private bool _allowClose;
        private bool _cancelRequested;

        public event EventHandler? CancelRequested;

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
            PreviewKeyDown += LoadingWindow_PreviewKeyDown;
        }

        public void CloseProgrammatically()
        {
            _allowClose = true;
            Close();
        }

        public void ShowCancelling()
        {
            _cancelRequested = true;
            CancelButton.IsEnabled = false;
            Message = "正在取消启动";
        }

        public void ShowError(string message)
        {
            Message = message;
            CancelButton.Content = "×";
            CancelButton.ToolTip = "关闭";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => RequestCancel();

        private void LoadingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                RequestCancel();
                e.Handled = true;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                RequestCancel();
            }
            base.OnClosing(e);
        }

        private void RequestCancel()
        {
            if (_cancelRequested)
                return;
            _cancelRequested = true;
            CancelButton.IsEnabled = false;
            Message = "正在取消启动";
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
