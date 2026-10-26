using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Themes;

namespace SunEyeVision.Plugin.SDK.UI.Windows
{
    /// <summary>
    /// 无边框窗口壳 - 包装工具调试控件为无边框圆角窗口
    /// </summary>
    /// <remarks>
    /// 架构说明：
    /// - 轻量级窗口壳，仅提供窗口容器功能
    /// - 接受任何 UserControl 作为内容
    /// - 无边框、圆角、可拖拽
    /// - 适用于需要自定义外观的工具
    /// 
    /// 使用场景：
    /// - 需要自定义窗口样式的工具
    /// - 需要无边框圆角窗口的工具
    /// 
    /// 不适用场景：
    /// - 需要标准窗口样式的工具（使用 DefaultDebugWindow）
    /// </remarks>
    public class CustomDebugWindow : Window, SunEyeVision.Plugin.SDK.UI.Controls.IToolControlInitializer
    {
        #region 字段

        private readonly UserControl _control;
        private bool _confirmed;
        public SunEyeVision.Plugin.SDK.UI.Controls.ToolDebugControlBase? EditorControl => _control as SunEyeVision.Plugin.SDK.UI.Controls.ToolDebugControlBase;
        private SunEyeVision.Plugin.SDK.UI.Controls.IToolControlInitializer Initializer =>
            _control as SunEyeVision.Plugin.SDK.UI.Controls.IToolControlInitializer ?? throw new InvalidOperationException("编辑控件未实现初始化合同。");
        public void SetCurrentNode(object node) => Initializer.SetCurrentNode(node);
        public void SetParameters(SunEyeVision.Plugin.SDK.Execution.Parameters.ToolParameters parameters) => Initializer.SetParameters(parameters);
        public void SetDataProvider(object provider) => Initializer.SetDataProvider(provider);
        public void SetMainImageControl(SunEyeVision.Plugin.SDK.UI.Controls.ImageControl? imageControl) => Initializer.SetMainImageControl(imageControl);
        public System.Threading.Tasks.Task InitializeAsync() => Initializer.InitializeAsync();
        private Border? _windowBorder;

        /// <summary>
        /// 自定义无边框窗口 Chrome：CaptionHeight 覆盖标题栏（可拖动），边缘可缩放，圆角。
        /// 用 WindowChrome 而非 AllowsTransparency，保证 Owner 层叠正常（不被主窗口遮挡）。
        /// </summary>
        // WindowChrome拥有Dispatcher；每个窗口取独立实例，不能静态共享跨STA对象。
        public static System.Windows.Shell.WindowChrome DebugWindowChrome => new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 32,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            UseAeroCaptionButtons = false
        };

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建无边框窗口壳
        /// </summary>
        /// <param name="control">工具调试控件实例</param>
        public CustomDebugWindow(UserControl control)
        {
            _control = control;

            PluginLogger.Info("CustomDebugWindow 构造函数开始", "CustomDebugWindow");

            // 设置窗口属性
            Title = "工具调试窗口";
            Width = 450;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            
            // 无边框样式（用 WindowChrome 而非 AllowsTransparency：
            // 保证 Owner 层叠正常，不被主窗口遮挡——分层窗口无法保证 Owner 子窗口层叠）
            WindowStyle = WindowStyle.None;
            WindowChrome.SetWindowChrome(this, DebugWindowChrome);

            // 创建窗口容器
            CreateWindowContainer();

            // 窗口关闭时清理
            Closed += OnWindowClosed;
            Closing += (_, e) => {
                // A later Closing handler can veto. Draft disposal belongs to Closed.
                if (!_confirmed && !e.Cancel && SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.GetDraftContext(_control) is null
                    && !SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorClose.TryClose(_control, this)) e.Cancel = true;
            };

            PluginLogger.Success("CustomDebugWindow 构造函数完成", "CustomDebugWindow");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 创建窗口容器
        /// </summary>
        private void CreateWindowContainer()
        {
            // 创建外层 Border（圆角 + 阴影）
            _windowBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1)
            };
            // 语义键 + 动态绑定（等价 XAML DynamicResource：主题切换自动刷新，键缺失不构造崩溃）
            _windowBorder.SetResourceReference(BackgroundProperty, SdkThemeKeys.PanelBackgroundBrush);
            _windowBorder.SetResourceReference(BorderBrushProperty, SdkThemeKeys.BorderBrush);

            // Publication belongs to the host shell, including controls without their own footer.
            var layout = new DockPanel();
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 8, 12, 8) };
            var confirm = new Button { Content = "确定", Name = "ConfirmButton", MinWidth = 72, Padding = new Thickness(12, 4, 12, 4) };
            System.Windows.Automation.AutomationProperties.SetAutomationId(confirm, "ConfirmButton");
            confirm.SetResourceReference(BackgroundProperty, SdkThemeKeys.ButtonBackgroundBrush);
            confirm.SetResourceReference(ForegroundProperty, SdkThemeKeys.ButtonTextBrush);
            confirm.SetResourceReference(BorderBrushProperty, SdkThemeKeys.ButtonBorderBrush);
            confirm.Click += OnConfirmClicked;
            footer.Children.Add(confirm);
            DockPanel.SetDock(footer, Dock.Bottom);
            layout.Children.Add(footer);
            layout.Children.Add(_control);
            _windowBorder.Child = layout;
            Content = _windowBorder;
        }

        /// <summary>
        /// Commit once through the same host contract as the standard shell.
        /// </summary>
        public bool TryConfirm(out string error)
        {
            if (!SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.TryCommit(_control, out error)) return false;
            _confirmed = true;
            Close();
            return true;
        }

        /// <summary>
        /// 确认按钮点击事件处理
        /// </summary>
        private void OnConfirmClicked(object sender, RoutedEventArgs e)
        {
            if (!TryConfirm(out var error))
            { MessageBox.Show(this, error, "配置未提交", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            PluginLogger.Info("确认按钮已点击，关闭窗口", "CustomDebugWindow");
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.GetDraftContext(_control) is not null)
                SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.DiscardPendingEdits(_control);
            PluginLogger.Info($"[绑定诊断] 自定义调试窗口 Closed | 控件={_control.GetType().Name}", "BindingDiag");
            // Closed retires an optional draft; it must never publish bindings again.

            PluginLogger.Info("窗口已关闭，事件已清理", "CustomDebugWindow");
        }

        #endregion
    }
}
