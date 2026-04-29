using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 文本样式配置控件 - 用于文本显示项的可见性和样式配置
    /// </summary>
    public class DispTextColorControl : Control
    {
        #region 模板部件

        public const string PART_VisibilityButton = "PART_VisibilityButton";
        public const string PART_StyleSettingsButton = "PART_StyleSettingsButton";
        public const string PART_StyleSettingsPopup = "PART_StyleSettingsPopup";

        private Button? _visibilityButton;
        private Button? _styleSettingsButton;
        private Popup? _styleSettingsPopup;

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

        #endregion
    }
}
