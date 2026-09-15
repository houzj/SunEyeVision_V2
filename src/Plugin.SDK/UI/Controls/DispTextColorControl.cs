using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Constants;

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

        /// <summary>是否已完成 Popup 内 ConfigSetting 的订阅和状态恢复（确保只执行一次）</summary>
        private bool _isPopupSubscribed;

        #endregion

        #region 静态构造函数

        static DispTextColorControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DispTextColorControl),
                new FrameworkPropertyMetadata(typeof(DispTextColorControl)));
            ForegroundProperty.OverrideMetadata(typeof(DispTextColorControl),
                new FrameworkPropertyMetadata(ColorConstants.TextPrimaryBrush));
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

        public DispTextColorControl()
        {
            SetResourceReference(StyleProperty, typeof(DispTextColorControl));
        }

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 清理旧的事件订阅
            UnsubscribeFromConfigSettings();
            UnsubscribeFromPopupOpened();

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

            // Popup 首次打开时才订阅 ConfigSetting 事件和恢复绑定状态
            // （因为 Popup 未打开时 Child 为 null，无法找到 ConfigSetting 控件）
            if (_styleSettingsPopup != null)
                _styleSettingsPopup.Opened += OnStyleSettingsPopupOpened;
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
        /// 取消 Popup.Opened 事件订阅
        /// </summary>
        private void UnsubscribeFromPopupOpened()
        {
            if (_styleSettingsPopup != null)
                _styleSettingsPopup.Opened -= OnStyleSettingsPopupOpened;
            _isPopupSubscribed = false;
        }

        /// <summary>
        /// Popup 首次打开时 - 订阅 ConfigSetting 事件并恢复绑定状态
        /// </summary>
        private void OnStyleSettingsPopupOpened(object? sender, System.EventArgs e)
        {
            // 首次打开 Popup 时执行一次，后续打开不再重复执行
            if (_isPopupSubscribed)
                return;
            _isPopupSubscribed = true;

            // 取消 Opened 事件订阅（只需要第一次）
            _styleSettingsPopup!.Opened -= OnStyleSettingsPopupOpened;

            // 此时 Popup.Child 已创建，ConfigSetting 控件已实例化
            SubscribeToConfigSettings();

            RestoreBindingStateFromTextStyle();
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

                PluginLogger.Info(
                    $"[DispText-Diag] SaveBinding | param={cs.ParameterName} type=Constant | " +
                    $"TextStyleState: name={Item.Name} posXBinding={Item.IsPositionXBinding} posXsrc='{Item.PositionXBindingSource}' posXdisp='{Item.PositionXBindingDisplayText}'",
                    "DispTextColorControl");
            }
            else
            {
                // P0.5-F：路径按**首点**切分（旧 Split('.') 会切丢嵌套路径）；
                // 展示名从**模型**取，不再从显示文本反解（显示名是命名规则的投影，反解＝把显示名当数据存盘）。
                var dotIndex = cs.BindingSource.IndexOf('.');
                var nodeId = dotIndex > 0 ? cs.BindingSource[..dotIndex] : cs.BindingSource;
                var property = dotIndex > 0 ? cs.BindingSource[(dotIndex + 1)..] : cs.BindingSource;

                var setting = ParamSetting.CreateBinding(cs.ParameterName, nodeId, property);

                if (cs.ResolveSelectedDataSource() is { } selected)
                {
                    setting.SourceNodeName = selected.SourceNodeName;
                    setting.SourceDisplayName = selected.EffectiveLeafDisplayName;
                }

                // 保存绑定配置时一并保存当前的常量值，用于切回常量模式时恢复
                var constantValue = Convert.ToDouble(cs.Value ?? 0.0);
                Item.SetBindingConfig(cs.ParameterName, setting, saveConstantValue: true, constantValue: constantValue);

                PluginLogger.Info(
                    $"[DispText-Diag] SaveBinding | param={cs.ParameterName} type=Binding src='{cs.BindingSource}' disp='{cs.BindingDisplayText}' | " +
                    $"TextStyleState: name={Item.Name} posXBinding={Item.IsPositionXBinding} posXsrc='{Item.PositionXBindingSource}' posXdisp='{Item.PositionXBindingDisplayText}'",
                    "DispTextColorControl");
            }
        }

        /// <summary>
        /// 从 TextStyle 恢复 Popup 内所有 ConfigSetting 的绑定状态
        /// </summary>
        private void RestoreBindingStateFromTextStyle()
        {
            if (Item == null) return;

            var configSettings = FindConfigSettingsInTemplate();

            PluginLogger.Info(
                $"[DispText-Diag] RestoreBinding | BEGIN | TextStyles: name={Item.Name} " +
                $"posXBinding={Item.IsPositionXBinding} posXsrc='{Item.PositionXBindingSource}' posXdisp='{Item.PositionXBindingDisplayText}' " +
                $"posYBinding={Item.IsPositionYBinding} posYsrc='{Item.PositionYBindingSource}' posYdisp='{Item.PositionYBindingDisplayText}' " +
                $"configCount={configSettings.Count}",
                "DispTextColorControl");

            foreach (var cs in configSettings)
            {
                var config = Item.GetBindingConfig(cs.ParameterName);

                PluginLogger.Info(
                    $"[DispText-Diag] RestoreBinding | cs={cs.ParameterName} " +
                    $"curType={cs.BindingType} configFound={(config != null)} " +
                    $"configType={(config?.BindingType).ToString()} configSrc='{config?.SourceNodeId}.{config?.SourceProperty}' " +
                    $"configDisplay='{config?.SourceNodeName}.{config?.SourceDisplayName}'",
                    "DispTextColorControl");

                if (config != null && config.BindingType == BindingType.Binding)
                {
                    // 当前为常量模式却需要恢复为绑定模式
                    if (cs.BindingType != BindingType.Binding)
                    {
                        // 先从 TextStyle 获取持久化的常量值，设置到 ConfigSetting
                        // 这样 OnBindingTypeChanged 不会用默认值覆盖原始常量值
                        double persistentConstantValue = cs.ParameterName == "PositionX"
                            ? Item.PositionXBindingConstantValue
                            : Item.PositionYBindingConstantValue;

                        // 使用内部接口在恢复绑定状态前设置好常量值和恢复标志
                        cs.SetConstantValueBeforeBinding(persistentConstantValue);
                        cs.BeginRestoreBindingFromExternal();
                        try
                        {
                            cs.FromParamSetting(config);

                            PluginLogger.Info(
                                $"[DispText-Diag] RestoreBinding | APPLIED | param={cs.ParameterName} " +
                                $"afterRestore: type={cs.BindingType} src='{cs.BindingSource}' disp='{cs.BindingDisplayText}' " +
                                $"restoredConstantValue={persistentConstantValue}",
                                "DispTextColorControl");
                        }
                        finally
                        {
                            cs.EndRestoreBindingFromExternal();
                        }
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
