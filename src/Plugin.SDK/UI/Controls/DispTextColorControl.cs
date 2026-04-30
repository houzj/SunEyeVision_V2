using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 文本样式配置控件 - 用于文本显示项的可见性和样式配置
    /// </summary>
    /// <remarks>
    /// 管理 ConfigSetting 的绑定状态持久化：当 PositionX/PositionY 切换到绑定模式时，
    /// 绑定配置保存到 TextStyle 中（调试窗口关闭再打开后通过 TextStyle 恢复）。
    /// </remarks>
    public class DispTextColorControl : Control
    {
        #region 模板部件

        public const string PART_VisibilityButton = "PART_VisibilityButton";
        public const string PART_StyleSettingsButton = "PART_StyleSettingsButton";
        public const string PART_StyleSettingsPopup = "PART_StyleSettingsPopup";

        private Button? _visibilityButton;
        private Button? _styleSettingsButton;
        private Popup? _styleSettingsPopup;

        /// <summary>已订阅事件的 ConfigSetting 列表，用于反订阅</summary>
        private readonly List<ConfigSetting> _subscribedConfigSettings = new List<ConfigSetting>();

        #endregion

        #region 静态构造函数

        static DispTextColorControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DispTextColorControl),
                new FrameworkPropertyMetadata(typeof(DispTextColorControl)));
        }

        #endregion

        #region DependencyProperty

        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register(
                nameof(Item), typeof(TextStyle), typeof(DispTextColorControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public TextStyle Item
        {
            get => (TextStyle)GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(
                nameof(AvailableDataSources),
                typeof(System.Collections.IList),
                typeof(DispTextColorControl),
                new FrameworkPropertyMetadata(null));

        public System.Collections.IList? AvailableDataSources
        {
            get => (System.Collections.IList?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        #endregion

        #region 构造函数

        public DispTextColorControl() { }

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 清理旧的事件订阅
            UnsubscribeFromConfigSettings();

            if (_visibilityButton != null)
                _visibilityButton.Click -= OnVisibilityButtonClick;
            if (_styleSettingsButton != null)
                _styleSettingsButton.Click -= OnStyleSettingsButtonClick;

            _visibilityButton = GetTemplateChild(PART_VisibilityButton) as Button;
            _styleSettingsButton = GetTemplateChild(PART_StyleSettingsButton) as Button;
            _styleSettingsPopup = GetTemplateChild(PART_StyleSettingsPopup) as Popup;

            if (_visibilityButton != null)
                _visibilityButton.Click += OnVisibilityButtonClick;

            if (_styleSettingsButton != null)
                _styleSettingsButton.Click += OnStyleSettingsButtonClick;

            // 延迟执行：等待模板完全加载和 ConfigSetting 初始化完成后再恢复绑定状态
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                SubscribeToConfigSettings();
                RestoreBindingStateFromTextStyle();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #endregion

        #region 事件处理

        private void OnVisibilityButtonClick(object sender, RoutedEventArgs e)
        {
            if (Item != null) Item.IsVisible = !Item.IsVisible;
        }

        private void OnStyleSettingsButtonClick(object sender, RoutedEventArgs e)
        {
            _styleSettingsPopup?.SetCurrentValue(Popup.IsOpenProperty, !_styleSettingsPopup.IsOpen);
        }

        /// <summary>
        /// ConfigSetting 绑定配置变更时调用 - 保存到 TextStyle 的持久化字段
        /// </summary>
        private void OnConfigSettingBindingConfigChanged(object? sender, System.EventArgs e)
        {
            var configSetting = sender as ConfigSetting;
            if (configSetting == null || Item == null)
                return;

            SaveConfigSettingState(configSetting);
        }

        #endregion

        #region ConfigSetting 绑定状态管理

        /// <summary>
        /// 订阅 Popup 内所有 ConfigSetting 的 BindingConfigChanged 事件
        /// </summary>
        private void SubscribeToConfigSettings()
        {
            var configSettings = FindConfigSettingsInTemplate();
            foreach (var cs in configSettings)
            {
                cs.BindingConfigChanged += OnConfigSettingBindingConfigChanged;
                _subscribedConfigSettings.Add(cs);
            }
        }

        /// <summary>
        /// 取消所有 ConfigSetting 的事件订阅
        /// </summary>
        private void UnsubscribeFromConfigSettings()
        {
            foreach (var cs in _subscribedConfigSettings)
            {
                cs.BindingConfigChanged -= OnConfigSettingBindingConfigChanged;
            }
            _subscribedConfigSettings.Clear();
        }

        /// <summary>
        /// 将 ConfigSetting 的当前绑定状态保存到 TextStyle
        /// </summary>
        private void SaveConfigSettingState(ConfigSetting cs)
        {
            if (Item == null) return;

            if (cs.BindingType == BindingType.Constant)
            {
                Item.SetBindingConfig(cs.ParameterName, null);
            }
            else if (!string.IsNullOrEmpty(cs.BindingSource))
            {
                var parts = cs.BindingSource.Split('.');
                var nodeId = parts.Length > 0 ? parts[0] : string.Empty;
                var property = parts.Length > 1 ? parts[1] : cs.BindingSource;

                var setting = ParamSetting.CreateBinding(cs.ParameterName, nodeId, property);

                // 从绑定显示文本中提取展示名称
                if (!string.IsNullOrEmpty(cs.BindingDisplayText))
                {
                    var displayParts = cs.BindingDisplayText.Split('.');
                    if (displayParts.Length == 2)
                    {
                        setting.SourceNodeName = displayParts[0];
                        setting.SourceDisplayName = displayParts[1];
                    }
                }

                Item.SetBindingConfig(cs.ParameterName, setting);
            }
        }

        /// <summary>
        /// 从 TextStyle 恢复 Popup 内所有 ConfigSetting 的绑定状态
        /// </summary>
        private void RestoreBindingStateFromTextStyle()
        {
            if (Item == null) return;

            var configSettings = FindConfigSettingsInTemplate();
            foreach (var cs in configSettings)
            {
                var config = Item.GetBindingConfig(cs.ParameterName);
                if (config != null && config.BindingType == BindingType.Binding)
                {
                    // 当前为常量模式却需要恢复为绑定模式
                    if (cs.BindingType != BindingType.Binding)
                    {
                        cs.FromParamSetting(config);
                    }
                }
            }
        }

        /// <summary>
        /// 在 Popup 模板中查找所有 ConfigSetting 控件
        /// </summary>
        private List<ConfigSetting> FindConfigSettingsInTemplate()
        {
            var result = new List<ConfigSetting>();
            if (_styleSettingsPopup?.Child == null)
                return result;

            CollectConfigSettings(_styleSettingsPopup.Child, result);
            return result;
        }

        /// <summary>
        /// 递归收集视觉树中的 ConfigSetting 控件
        /// </summary>
        private static void CollectConfigSettings(DependencyObject element, List<ConfigSetting> result)
        {
            if (element is ConfigSetting configSetting)
            {
                result.Add(configSetting);
                // ConfigSetting 内部不需要继续递归
                return;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                CollectConfigSettings(child, result);
            }
        }

        #endregion
    }
}
