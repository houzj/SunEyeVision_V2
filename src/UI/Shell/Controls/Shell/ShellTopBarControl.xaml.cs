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
        private Window? _stateWindow;
        /// <summary>
        /// 请求全屏图像显示（跨区域：ImageDisplayContent 在图像面板，故用事件转发）
        /// </summary>
        public event EventHandler? FullscreenRequested;

        public ShellTopBarControl()
        {
            InitializeComponent();
            BuildMenuAndToolbar();

            // 加载完成后订阅窗口状态变化，切换最大化/还原图标（VS 风格）。
            // 用代码监听而非 XAML DataTrigger——UserControl 内 RelativeSource AncestorType=Window
            // 绑定 Window.State 在初始化时可能求值失败，导致图标不切换。
            Loaded += OnHostLoaded;
            Unloaded += OnHostUnloaded;
        }

        private void OnHostLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (!ReferenceEquals(window, _stateWindow))
            {
                if (_stateWindow is not null) _stateWindow.StateChanged -= OnWindowStateChanged;
                _stateWindow = window;
                if (_stateWindow is not null) _stateWindow.StateChanged += OnWindowStateChanged;
            }
            if (_stateWindow is not null) UpdateWindowIcon(_stateWindow.WindowState);
        }

        private void OnHostUnloaded(object sender, RoutedEventArgs e)
        {
            if (_stateWindow is not null) _stateWindow.StateChanged -= OnWindowStateChanged;
            _stateWindow = null;
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (sender is Window window)
                UpdateWindowIcon(window.WindowState);
        }

        /// <summary>
        /// 按窗口状态切换字形与 ToolTip（VS 同款）：最大化显示还原图标(\uE923/两个窗口) + "向下还原"，
        /// 正常显示最大化图标(\uE739/方框) + "最大化"。
        /// 只设置 MaximizeButton 自身的 ToolTip（设置 UserControl 的 ToolTip 会污染文件/视图/运行菜单）。
        /// </summary>
        /// <param name="state">当前窗口状态</param>
        private void UpdateWindowIcon(WindowState state)
        {
            bool isMaximized = state == WindowState.Maximized;
            if (MaximizeGlyph != null)
                MaximizeGlyph.Text = isMaximized ? "\uE923" : "\uE739";
            if (MaximizeButton != null)
                MaximizeButton.ToolTip = isMaximized ? "向下还原" : "最大化";
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
        /// 退出菜单点击 / 关闭按钮 - 关闭应用。
        /// 不能直接用 System.Windows.Application.Current.Shutdown()——它会绕过主窗口 OnClosing 的拦截（强制关闭、跳过保存/确认弹窗）。
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
                System.Windows.Application.Current.Shutdown();
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

    }
}
