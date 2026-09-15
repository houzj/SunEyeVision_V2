using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Constants;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 图形样式配置控件 - 用于图形显示项的可见性和样式配置
    /// </summary>
    public class DispItemColorControl : Control
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

        static DispItemColorControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DispItemColorControl),
                new FrameworkPropertyMetadata(typeof(DispItemColorControl)));
            ForegroundProperty.OverrideMetadata(typeof(DispItemColorControl),
                new FrameworkPropertyMetadata(ColorConstants.TextPrimaryBrush));
        }

        #endregion

        #region DependencyProperty

        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register(
                nameof(Item), typeof(GraphicsStyle), typeof(DispItemColorControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public GraphicsStyle Item
        {
            get => (GraphicsStyle)GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public static readonly DependencyProperty ContentHorizontalAlignmentProperty =
            DependencyProperty.Register(
                nameof(ContentHorizontalAlignment), typeof(HorizontalAlignment),
                typeof(DispItemColorControl),
                new FrameworkPropertyMetadata(HorizontalAlignment.Right, FrameworkPropertyMetadataOptions.AffectsArrange));

        public HorizontalAlignment ContentHorizontalAlignment
        {
            get => (HorizontalAlignment)GetValue(ContentHorizontalAlignmentProperty);
            set => SetValue(ContentHorizontalAlignmentProperty, value);
        }

        #endregion

        #region 构造函数

        public DispItemColorControl()
        {
            SetResourceReference(StyleProperty, typeof(DispItemColorControl));
        }

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
