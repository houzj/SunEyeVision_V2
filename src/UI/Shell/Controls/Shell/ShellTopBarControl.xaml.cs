using System;
using System.Diagnostics;
using System.IO;
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
        /// 顶栏工作流下拉框选中项变化 — 通过 ViewModel 的唯一写入口 SetSelectedTab 更新，
        /// 避免 WPF 双向绑定回写 SelectedTab setter 造成重入（单向数据流）。
        /// 仅当选中项与当前 VM 状态不同才写入，防止初始化绑定触发重复回写。
        /// </summary>
        private void WorkflowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ShellTopBarViewModel vm
                && e.AddedItems.Count > 0
                && !ReferenceEquals(vm.SelectedTab, e.AddedItems[0]))
            {
                vm.SetSelectedTab(e.AddedItems[0]);
            }
        }

        /// <summary>
        /// 退出菜单点击 / 关闭按钮 - 关闭应用。
        /// 不能直接用 Application.Current.Shutdown()——它会绕过主窗口 OnClosing 的拦截（强制关闭、跳过保存/确认弹窗）。
        /// 改为调用主窗口 Close()，走正常的 OnClosing 确认流程（用户确认后才真正关闭）。
        /// </summary>
        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.Close();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// 打开开发计划看板（plans/plan.html）。
        /// 从当前执行目录向上查找解决方案根的 plans/ 目录（适配 Debug/Release 输出路径），
        /// 用系统默认浏览器打开。
        /// </summary>
        private void OpenPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 从运行目录逐级向上查找 plans/plan.html（dev/Debug → 解决方案根）
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "plans", "plan.html");
                    if (File.Exists(candidate))
                    {
                        Process.Start(new ProcessStartInfo(candidate) { UseShellExecute = true });
                        return;
                    }
                }

                MessageBox.Show("未找到开发计划文档 plans/plan.html。\n请确认计划文件存在于解决方案根目录的 plans/ 文件夹中。",
                    "开发计划", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开开发计划失败: {ex.Message}",
                    "开发计划", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
