using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Views;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views
{
    /// <summary>
    /// 形状工具栏 - 通用 ToolBarControl 的薄封装
    /// </summary>
    /// <remarks>
    /// 仅做类型化适配：将通用工具栏的 object? SelectedValue 映射为 ShapeType?，
    /// 保留宿主既有事件签名（ShapeSelectionChanged）与 API（SetSelectedShape/ClearSelection），
    /// 所有 UI/交互逻辑收敛到 ToolBarControl。
    /// </remarks>
    public partial class ShapeToolBar : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 形状工具项集合
        /// </summary>
        public static readonly DependencyProperty ShapeToolsProperty =
            DependencyProperty.Register(nameof(ShapeTools),
                typeof(ObservableCollection<ShapeToolItem>),
                typeof(ShapeToolBar),
                new PropertyMetadata(null, OnShapeToolsChanged));

        /// <summary>
        /// 形状工具项集合
        /// </summary>
        public ObservableCollection<ShapeToolItem>? ShapeTools
        {
            get => (ObservableCollection<ShapeToolItem>?)GetValue(ShapeToolsProperty);
            set => SetValue(ShapeToolsProperty, value);
        }

        #endregion

        #region 事件

        /// <summary>
        /// 形状选择变更事件
        /// 参数为选中的 ShapeType，null 表示取消选中（回到选择模式）
        /// </summary>
        public event EventHandler<ShapeType?>? ShapeSelectionChanged;

        #endregion

        public ShapeToolBar()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 通过代码设置选中的形状（不触发 ShapeSelectionChanged 事件）
        /// 用于键盘快捷键或外部 API 调用
        /// </summary>
        public void SetSelectedShape(ShapeType? shapeType)
        {
            InnerToolBar.SetSelectedValue(shapeType);
        }

        /// <summary>
        /// 清除所有选中状态（不触发 ShapeSelectionChanged 事件）
        /// </summary>
        public void ClearSelection()
        {
            InnerToolBar.ClearSelection();
        }

        #region 内部转发

        private static void OnShapeToolsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ShapeToolBar)d;
            control.InnerToolBar.ItemsSource = e.NewValue as System.Collections.Generic.IEnumerable<ToolItem>;
        }

        private void OnInnerSelectionChanged(object? sender, object? value)
        {
            ShapeSelectionChanged?.Invoke(this, value as ShapeType?);
        }

        #endregion
    }
}
