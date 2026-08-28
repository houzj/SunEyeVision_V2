using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Views;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views
{
    /// <summary>
    /// 测量工具栏 - 通用 ToolBarControl 的薄封装
    /// </summary>
    /// <remarks>
    /// 仅做类型化适配：将通用工具栏的 object? SelectedValue 映射为 MeasureGeometryType?，
    /// 保留宿主既有事件签名（MeasureSelectionChanged）与 API（SetSelectedType/ClearSelection），
    /// 所有 UI/交互逻辑收敛到 ToolBarControl。
    /// </remarks>
    public partial class MeasureToolBar : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 测量工具项集合
        /// </summary>
        public static readonly DependencyProperty MeasureToolsProperty =
            DependencyProperty.Register(nameof(MeasureTools),
                typeof(ObservableCollection<MeasureToolItem>),
                typeof(MeasureToolBar),
                new PropertyMetadata(null, OnMeasureToolsChanged));

        /// <summary>
        /// 测量工具项集合
        /// </summary>
        public ObservableCollection<MeasureToolItem>? MeasureTools
        {
            get => (ObservableCollection<MeasureToolItem>?)GetValue(MeasureToolsProperty);
            set => SetValue(MeasureToolsProperty, value);
        }

        #endregion

        #region 事件

        /// <summary>
        /// 几何类型选择变更事件
        /// 参数为选中的 MeasureGeometryType，null 表示取消选中（回到选择模式）
        /// </summary>
        public event EventHandler<MeasureGeometryType?>? MeasureSelectionChanged;

        #endregion

        public MeasureToolBar()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 通过代码设置选中的几何类型（不触发 MeasureSelectionChanged 事件）
        /// </summary>
        public void SetSelectedType(MeasureGeometryType? geometryType)
        {
            InnerToolBar.SetSelectedValue(geometryType);
        }

        /// <summary>
        /// 清除所有选中状态（不触发 MeasureSelectionChanged 事件）
        /// </summary>
        public void ClearSelection()
        {
            InnerToolBar.ClearSelection();
        }

        #region 内部转发

        private static void OnMeasureToolsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MeasureToolBar)d;
            control.InnerToolBar.ItemsSource = e.NewValue as System.Collections.Generic.IEnumerable<ToolItem>;
        }

        private void OnInnerSelectionChanged(object? sender, object? value)
        {
            MeasureSelectionChanged?.Invoke(this, value as MeasureGeometryType?);
        }

        #endregion
    }
}
