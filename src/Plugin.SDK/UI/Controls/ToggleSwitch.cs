using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Constants;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 现代开关按钮控件
    /// </summary>
    /// <remarks>
    /// 特性：
    /// - 支持浅色/深色双主题
    /// - 平滑过渡动画
    /// - 高度可配置（颜色、尺寸、圆角）
    /// - 符合视觉软件行业标准
    /// </remarks>
    public class ToggleSwitch : Control
    {
        #region 静态构造函数

        static ToggleSwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
        }

        #endregion

        #region 构造函数

        public ToggleSwitch()
        {
            // 默认尺寸
            SwitchWidth = 42;
            SwitchHeight = 22;
            CornerRadius = 11;
        }

        #endregion

        #region DependencyProperty

        #region IsChecked - 开关状态

        /// <summary>
        /// 开关状态（支持双向绑定）
        /// </summary>
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(
                nameof(IsChecked),
                typeof(bool),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIsCheckedChanged));

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var toggleSwitch = (ToggleSwitch)d;
            var oldValue = (bool)e.OldValue;
            var newValue = (bool)e.NewValue;
            
            PluginLogger.Info($"OnIsCheckedChanged - 旧值: {oldValue}, 新值: {newValue}", "ToggleSwitch");
            
            toggleSwitch.RaiseCheckedChangedEvent(newValue);
        }

        #endregion

        #region SwitchWidth - 开关宽度

        /// <summary>
        /// 开关宽度
        /// </summary>
        public static readonly DependencyProperty SwitchWidthProperty =
            DependencyProperty.Register(
                nameof(SwitchWidth),
                typeof(double),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(42.0));

        public double SwitchWidth
        {
            get => (double)GetValue(SwitchWidthProperty);
            set => SetValue(SwitchWidthProperty, value);
        }

        #endregion

        #region SwitchHeight - 开关高度

        /// <summary>
        /// 开关高度
        /// </summary>
        public static readonly DependencyProperty SwitchHeightProperty =
            DependencyProperty.Register(
                nameof(SwitchHeight),
                typeof(double),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(22.0));

        public double SwitchHeight
        {
            get => (double)GetValue(SwitchHeightProperty);
            set => SetValue(SwitchHeightProperty, value);
        }

        #endregion

        #region CornerRadius - 圆角半径

        /// <summary>
        /// 圆角半径
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(double),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(11.0));

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        #endregion

        #region OnBackground - 开启状态背景色

        /// <summary>
        /// 开启状态背景色。
        /// 默认值取 ColorConstants 兜底（无 UI 宿主时），有 UI 宿主时由 Generic.xaml 样式
        /// Setter 用主题语义 key 覆盖（AccentBrush），随明/暗主题切换。
        /// </summary>
        public static readonly DependencyProperty OnBackgroundProperty =
            DependencyProperty.Register(
                nameof(OnBackground),
                typeof(Brush),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(
                    ColorConstants.SwitchOnBackgroundBrush));

        public Brush OnBackground
        {
            get => (Brush)GetValue(OnBackgroundProperty);
            set => SetValue(OnBackgroundProperty, value);
        }

        #endregion

        #region OffBackground - 关闭状态背景色

        /// <summary>
        /// 关闭状态背景色。
        /// 默认值取 ColorConstants 兜底（无 UI 宿主时），有 UI 宿主时由 Generic.xaml 样式
        /// Setter 用主题语义 key 覆盖（ToggleOffBackgroundBrush），随明/暗主题切换。
        /// </summary>
        public static readonly DependencyProperty OffBackgroundProperty =
            DependencyProperty.Register(
                nameof(OffBackground),
                typeof(Brush),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(
                    ColorConstants.SwitchOffBackgroundBrush));

        public Brush OffBackground
        {
            get => (Brush)GetValue(OffBackgroundProperty);
            set => SetValue(OffBackgroundProperty, value);
        }

        #endregion

        #region ThumbColor - 滑块颜色

        /// <summary>
        /// 滑块颜色。
        /// 默认值取 ColorConstants 兜底（无 UI 宿主时），有 UI 宿主时由 Generic.xaml 样式
        /// Setter 用主题语义 key 覆盖（TextOnDarkBrush，明暗主题均为白色滑块），随明/暗主题切换。
        /// </summary>
        public static readonly DependencyProperty ThumbColorProperty =
            DependencyProperty.Register(
                nameof(ThumbColor),
                typeof(Brush),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(
                    ColorConstants.BackgroundBrush));

        public Brush ThumbColor
        {
            get => (Brush)GetValue(ThumbColorProperty);
            set => SetValue(ThumbColorProperty, value);
        }

        #endregion

        #region OnText - 开启状态文本

        /// <summary>
        /// 开启状态文本
        /// </summary>
        public static readonly DependencyProperty OnTextProperty =
            DependencyProperty.Register(
                nameof(OnText),
                typeof(string),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(string.Empty));

        public string OnText
        {
            get => (string)GetValue(OnTextProperty);
            set => SetValue(OnTextProperty, value);
        }

        #endregion

        #region OffText - 关闭状态文本

        /// <summary>
        /// 关闭状态文本
        /// </summary>
        public static readonly DependencyProperty OffTextProperty =
            DependencyProperty.Register(
                nameof(OffText),
                typeof(string),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(string.Empty));

        public string OffText
        {
            get => (string)GetValue(OffTextProperty);
            set => SetValue(OffTextProperty, value);
        }

        #endregion

        #region ShowText - 是否显示文本

        /// <summary>
        /// 是否显示文本
        /// </summary>
        public static readonly DependencyProperty ShowTextProperty =
            DependencyProperty.Register(
                nameof(ShowText),
                typeof(bool),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(false));

        public bool ShowText
        {
            get => (bool)GetValue(ShowTextProperty);
            set => SetValue(ShowTextProperty, value);
        }

        #endregion

        #endregion

        #region RoutedEvent

        #region CheckedChangedEvent - 状态改变事件

        /// <summary>
        /// 状态改变事件
        /// </summary>
        public static readonly RoutedEvent CheckedChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(CheckedChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<bool>),
                typeof(ToggleSwitch));

        public event RoutedPropertyChangedEventHandler<bool> CheckedChanged
        {
            add => AddHandler(CheckedChangedEvent, value);
            remove => RemoveHandler(CheckedChangedEvent, value);
        }

        private void RaiseCheckedChangedEvent(bool newValue)
        {
            var args = new RoutedPropertyChangedEventArgs<bool>(
                !newValue, newValue, CheckedChangedEvent);
            RaiseEvent(args);
        }

        #endregion

        #endregion

        #region 重写方法

        /// <summary>
        /// 鼠标点击切换状态
        /// </summary>
        protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            PluginLogger.Info($"OnMouseDown 触发 - IsEnabled: {IsEnabled}, 当前 IsChecked: {IsChecked}", "ToggleSwitch");
            
            if (IsEnabled)
            {
                IsChecked = !IsChecked;
                PluginLogger.Info($"IsChecked 已切换为: {IsChecked}", "ToggleSwitch");
            }
            base.OnMouseDown(e);
        }

        /// <summary>
        /// 键盘空格键切换状态
        /// </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space && IsEnabled)
            {
                IsChecked = !IsChecked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        #endregion
    }
}
