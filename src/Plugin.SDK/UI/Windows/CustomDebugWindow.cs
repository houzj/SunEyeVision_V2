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
    public class CustomDebugWindow : Window
    {
        #region 字段

        private readonly UserControl _control;
        private Border? _windowBorder;

        /// <summary>
        /// 自定义无边框窗口 Chrome：CaptionHeight 覆盖标题栏（可拖动），边缘可缩放，圆角。
        /// 用 WindowChrome 而非 AllowsTransparency，保证 Owner 层叠正常（不被主窗口遮挡）。
        /// </summary>
        public static System.Windows.Shell.WindowChrome DebugWindowChrome { get; } = new System.Windows.Shell.WindowChrome
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

            // 尝试订阅确认按钮点击事件（如果控件有）
            SubscribeConfirmEvent(_control);

            // 窗口关闭时清理
            Closed += OnWindowClosed;

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

            // 设置控件为内容（WindowChrome 接管标题栏拖拽，移除手动 DragMove）
            _windowBorder.Child = _control;
            Content = _windowBorder;
        }

        /// <summary>
        /// 订阅确认按钮事件
        /// </summary>
        private void SubscribeConfirmEvent(UserControl control)
        {
            // 使用反射查找 ConfirmClicked 事件
            var eventType = control.GetType().GetEvent("ConfirmClicked");
            if (eventType != null)
            {
                try
                {
                    var handler = Delegate.CreateDelegate(eventType.EventHandlerType, this, nameof(OnConfirmClicked));
                    eventType.AddEventHandler(control, handler);
                    PluginLogger.Info("已订阅 ConfirmClicked 事件", "CustomDebugWindow");
                }
                catch (Exception ex)
                {
                    PluginLogger.Warning($"订阅 ConfirmClicked 事件失败: {ex.Message}", "CustomDebugWindow");
                }
            }
        }

        /// <summary>
        /// 确认按钮点击事件处理
        /// </summary>
        private void OnConfirmClicked(object? sender, EventArgs e)
        {
            PluginLogger.Info("确认按钮已点击，关闭窗口", "CustomDebugWindow");
            Close();
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void OnWindowClosed(object? sender, EventArgs e)
        {
            PluginLogger.Info($"[绑定诊断] 自定义调试窗口 Closed | 控件={_control.GetType().Name}", "BindingDiag");
            // 关闭窗口时显式提交工具调试控件的绑定状态。
            // 不能只依赖 UserControl.Unloaded：窗口复用、关闭动画或宿主切换
            // 都可能让 Unloaded 回调晚于节点清理。
            try
            {
                var persistMethod = _control.GetType().GetMethod("PersistParameterBindings");
                persistMethod?.Invoke(_control, null);
                PluginLogger.Info($"[绑定诊断] 自定义调试窗口提交完成 | 控件={_control.GetType().Name}", "BindingDiag");
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"关闭调试窗口时持久化绑定失败: {ex.Message}", "CustomDebugWindow");
            }

            // 清理事件绑定
            if (_control != null)
            {
                var eventType = _control.GetType().GetEvent("ConfirmClicked");
                if (eventType != null)
                {
                    try
                    {
                        var handler = Delegate.CreateDelegate(eventType.EventHandlerType, this, nameof(OnConfirmClicked));
                        eventType.RemoveEventHandler(_control, handler);
                    }
                    catch
                    {
                        // 忽略清理失败
                    }
                }
            }

            PluginLogger.Info("窗口已关闭，事件已清理", "CustomDebugWindow");
        }

        #endregion
    }
}
