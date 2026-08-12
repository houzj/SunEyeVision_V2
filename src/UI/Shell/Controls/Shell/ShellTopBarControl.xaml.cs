using System;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Shell.Controls.Shell
{
    /// <summary>
    /// 顶部标题栏：Logo + 菜单 + 工具栏 + 窗口控制（VisionMaster 风格一体化紧凑布局）。
    /// </summary>
    public partial class ShellTopBarControl : UserControl
    {
        /// <summary>
        /// 请求全屏图像显示（跨区域：ImageDisplayContent 在图像面板，故用事件转发）
        /// </summary>
        public event EventHandler? FullscreenRequested;

        public ShellTopBarControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 更新缩放百分比（保留接口兼容 - 后续如需在标题栏显示缩放可重新接入）
        /// </summary>
        public void SetZoomPercentage(double percentage)
        {
            // 当前标题栏未显示缩放百分比（缩放已迁移到画布内部）；
            // 保留空实现以避免破坏 MainWindow.xaml.cs:47 的订阅调用。
        }

        /// <summary>
        /// 全屏菜单点击 - 转发到主窗口（图像面板全屏）
        /// </summary>
        private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            FullscreenRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 退出菜单点击 / 关闭按钮 - 关闭应用
        /// </summary>
        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 最小化窗口
        /// </summary>
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        /// <summary>
        /// 最大化/还原窗口
        /// </summary>
        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
        }

        /// <summary>
        /// 连续运行开关：开启时图标切换为红色停止符号
        /// </summary>
        private void ContinuousRunToggle_Checked(object sender, RoutedEventArgs e)
        {
            ContinuousRunIcon.Text = "⏹️";
            ContinuousRunIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0x52, 0x52));
        }

        /// <summary>
        /// 连续运行开关：关闭时图标恢复为灰色连续符号
        /// </summary>
        private void ContinuousRunToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ContinuousRunIcon.Text = "🔁";
            ContinuousRunIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
        }
    }
}
