using System;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Views.Controls.Shell
{
    /// <summary>
    /// 顶部标题栏：菜单 + 工具栏。
    /// 从 MainWindow 拆分而来，含死菜单清理（rule-008）。
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
        /// 更新缩放百分比显示（由中间工作区 ZoomScaleChanged 事件驱动）
        /// </summary>
        public void SetZoomPercentage(double percentage)
        {
            if (ZoomTextBlock != null)
                ZoomTextBlock.Text = $"缩放: {(int)percentage}%";
        }

        /// <summary>
        /// 全屏菜单点击 - 转发到主窗口（图像面板全屏）
        /// </summary>
        private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            FullscreenRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 退出菜单点击
        /// </summary>
        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
